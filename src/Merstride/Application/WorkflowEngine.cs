using Merstride.Domain;

namespace Merstride.Application;

public interface IRunPersistence
{
    void Write(RunState state);
}

public interface IContextLogs
{
    string? ReadLog(RunState run, string activationId, string stream, int tailBytes, out bool truncated);
}

public interface IProcessRunner
{
    Activation Execute(Node node, string workspace, Activation activation);
}

public interface IRequestIds
{
    string Request();
}

[Flags]
public enum ProcessLogs { None = 0, Stdout = 1, Stderr = 2 }

public static class WorkflowEngine
{
    public static RunState Interrupted(RunState run) => run with
    {
        Status = "interrupted",
        Error = new("INTERRUPTED", "An activation did not finish; inspect external effects before explicit resolution."),
        Activations = run.Activations.Select(a => a.FinishedAt is null && a.Id == run.Activations.LastOrDefault()?.Id
            ? a with { FinishedAt = DateTimeOffset.UtcNow, Outcome = "interrupted" } : a).ToArray()
    };

    public static RunState Resolve(RunState run, bool retry)
    {
        if (run.Status == "executing") run = Interrupted(run);
        if (run.Status != "interrupted")
            throw new MerstrideException("INVALID_STATE", "Only an interrupted run can be resolved.", 4);
        return run with { Status = retry ? "ready" : "failed", Error = retry ? null : run.Error };
    }

    public static RunState Advance(RunState run, IRunPersistence store, IProcessRunner processes, IContextLogs logs,
        IRequestIds ids, ProcessLogs retained, CancellationToken interruption = default)
    {
        if (run.Status == "executing")
        {
            run = Interrupted(run);
            store.Write(run);
        }
        while (run.Status == "ready")
        {
            if (interruption.IsCancellationRequested)
            {
                run = Interrupted(run);
                store.Write(run);
                return run;
            }
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
                    Request = new(ids.Request(), activation.Id, node.Id, node.Agent.Prompt,
                        run.Definition.Edges.Where(e => e.Source == node.Id).Select(e => e.Outcome!).Order(StringComparer.Ordinal).ToArray(), context) };
            }
            else
            {
                var id = "a_" + Guid.NewGuid().ToString("N");
                var activation = new Activation(id, node.Id, DateTimeOffset.UtcNow, null, null, null,
                    retained.HasFlag(ProcessLogs.Stdout) ? $"logs/{id}.stdout" : "",
                    retained.HasFlag(ProcessLogs.Stderr) ? $"logs/{id}.stderr" : "", Process: true);
                run = run with { Status = "executing", Activations = [.. run.Activations, activation] };
                store.Write(run);
                activation = processes.Execute(node, run.Workspace, activation);
                run = run with { Activations = [.. run.Activations[..^1], activation] };
                if (interruption.IsCancellationRequested)
                {
                    run = Interrupted(run);
                    store.Write(run);
                    return run;
                }
                var route = run.Definition.ProcessRoute(node.Id, activation.Outcome);
                run = route is null
                    ? run with { Status = "failed", Error = new("NO_MATCHING_EDGE", $"No route matches {activation.Outcome} at {node.Id}.") }
                    : run with { Status = "ready", Cursor = route.Target, TransitionCount = run.TransitionCount + 1 };
            }
            store.Write(run);
        }
        return run;
    }
}
