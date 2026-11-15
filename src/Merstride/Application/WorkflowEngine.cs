using Merstride.Domain;

namespace Merstride.Application;

public interface IRunPersistence
{
    void Write(RunState state);
}

public interface IContextLogs
{
    string ReadLog(RunState run, string activationId, string stream, int tailBytes, out bool truncated);
}

public interface IProcessRunner
{
    Activation Execute(Node node, string workspace, Activation activation);
}

public static class WorkflowEngine
{
    public static RunState Advance(RunState run, IRunPersistence store, IProcessRunner processes, IContextLogs logs)
    {
        if (run.Status == "executing")
        {
            run = run with { Status = "interrupted", Error = new("INTERRUPTED", "A prior activation did not finish; automatic replay is forbidden.") };
            store.Write(run);
        }
        while (run.Status == "ready")
        {
            var node = run.Definition.Nodes.Single(n => n.Id == run.Cursor);
            if (node.Kind == "end")
            {
                run = run with { Status = node.Result == "success" ? "completed" : "failed",
                    Error = node.Result == "success" ? null : new("WORKFLOW_FAILURE", "Workflow reached a failure end.") };
            }
            else if (run.TransitionCount >= run.Definition.MaxTransitions)
                run = run with { Status = "failed", Error = new("TRANSITION_LIMIT", "Transition budget exhausted.") };
            else if (node.Agent is not null)
            {
                var context = AgentContext.Select(run, node.Agent.Context, logs);
                if (!AgentContext.Fits(node.Agent.Prompt, context))
                {
                    run = run with { Status = "failed", Error = new("CONTEXT_LIMIT", "Prompt plus selected context exceeds 32768 UTF-8 bytes.") };
                    store.Write(run);
                    return run;
                }
                var activation = new Activation("a_" + Guid.NewGuid().ToString("N"), node.Id, DateTimeOffset.UtcNow,
                    null, null, null, "", "");
                run = run with { Status = "waiting_agent", Activations = [.. run.Activations, activation],
                    Request = new("q_" + Guid.NewGuid().ToString("N"), activation.Id, node.Id, node.Agent.Prompt,
                        run.Definition.Edges.Where(e => e.Source == node.Id).Select(e => e.Outcome!).Order(StringComparer.Ordinal).ToArray(), context) };
            }
            else
            {
                var id = "a_" + Guid.NewGuid().ToString("N");
                var activation = new Activation(id, node.Id, DateTimeOffset.UtcNow, null, null, null,
                    $"logs/{id}.stdout", $"logs/{id}.stderr");
                run = run with { Status = "executing", Activations = [.. run.Activations, activation] };
                store.Write(run);
                activation = processes.Execute(node, run.Workspace, activation);
                var edges = run.Definition.Edges.Where(e => e.Source == node.Id).ToArray();
                var route = edges.SingleOrDefault(e => e.Outcome == activation.Outcome)
                    ?? edges.SingleOrDefault(e => e.Outcome == "default") ?? edges.SingleOrDefault(e => e.Outcome is null);
                run = run with { Activations = [.. run.Activations[..^1], activation] };
                run = route is null
                    ? run with { Status = "failed", Error = new("NO_MATCHING_EDGE", $"No route matches {activation.Outcome} at {node.Id}.") }
                    : run with { Status = "ready", Cursor = route.Target, TransitionCount = run.TransitionCount + 1 };
            }
            store.Write(run);
        }
        return run;
    }
}
