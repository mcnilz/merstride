using Merstride.Domain;

namespace Merstride.Application;

public interface IRunPersistence
{
    void Write(RunState state);
}

public interface IProcessRunner
{
    Activation Execute(Node node, string workspace, Activation activation);
}

public static class WorkflowEngine
{
    public static RunState Advance(RunState run, IRunPersistence store, IProcessRunner processes)
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
