using Merstride.Application;
using Merstride.Domain;

namespace Merstride.Cli;

// Observe successful writes in this invocation, never replay saved history.
internal sealed class ConsoleActivity(IRunPersistence persistence, CliOutput output, RunState previous) : IRunPersistence
{
    public void Write(RunState state)
    {
        persistence.Write(state);
        var activation = state.Activations.LastOrDefault();
        var prior = previous.Activations.LastOrDefault();
        if (state.Status == "executing" && activation is not null && activation.Id != prior?.Id)
            output.Activity(state, $"{Process(state, activation)}: activating (launch pending).");
        if (activation?.FinishedAt is not null && (activation.Id != prior?.Id || prior.FinishedAt is null))
        {
            output.Activity(state, $"{Process(state, activation)}: saved {CliOutput.Outcome(activation)}.");
            if (state.TransitionCount > previous.TransitionCount)
            {
                var edges = state.Definition.Edges.Where(e => e.Source == activation.NodeId).ToArray();
                var reason = edges.Any(e => e.Outcome == activation.Outcome) ? $"exact outcome {activation.Outcome}"
                    : edges.Any(e => e.Outcome == "default") ? $"default (no exact match for {activation.Outcome})"
                    : $"unlabeled edge (accepts any process outcome, including failures)";
                Movement(output, state, activation.NodeId, reason);
            }
            else output.Activity(state, $"Flow stays at {state.Cursor}; no route taken. " +
                (state.Status == "interrupted" ? "External effects may already have happened."
                    : state.Error?.Message));
        }
        if (state.Status == "waiting_agent" && state.Request?.Id != previous.Request?.Id)
            output.Activity(state, $"Run {previous.Status} -> waiting_agent at {state.Cursor} (visit {Visits(state, state.Cursor)}): " +
                $"waiting for agent; request {state.Request!.Id}. Flow stays here. Next: read the prompt on stdout and complete this request with an allowed outcome.");
        if (state.Status is "completed" or "failed" or "interrupted" && state.Status != previous.Status)
            output.Activity(state, $"Run {previous.Status} -> {state.Status} at {state.Cursor}: " + (state.Status == "completed"
                ? "success end; overall result: success."
                : state.Status == "interrupted" ? "execution interrupted; overall result unresolved. Next: inspect external effects, then explicitly resolve --retry or --fail."
                : $"overall result: failure. {state.Error?.Message} ({state.Error?.Code}). Next: inspect status/logs; start a new run after correcting the cause."));
        previous = state;
    }

    internal static void Completion(CliOutput output, RunState before, RunState state, CompletionReceipt receipt)
    {
        output.Activity(state, $"Agent {before.Cursor} (visit {Visits(before, before.Cursor)}): completion {receipt.RequestId} accepted and saved ({receipt.Outcome}).");
        Movement(output, state, before.Cursor, $"agent outcome {receipt.Outcome}",
            " Run waiting_agent -> ready; next node has not executed. Next: merstride advance " + state.RunId + ".");
    }

    internal static void Resolution(CliOutput output, RunState state, bool retry) => output.Activity(state,
        $"Run interrupted -> {state.Status} at {state.Cursor}: explicit {(retry ? "retry allowed" : "failure chosen")}; " +
        "flow stays here; no process executed. " + (retry
            ? $"External effects may repeat. Next: merstride advance {state.RunId}."
            : "Overall result: failure. Next: inspect status/logs; start a new run if needed."));

    private static void Movement(CliOutput output, RunState state, string source, string reason, string next = "")
    {
        var visits = Visits(state, state.Cursor);
        output.Activity(state, $"Flow #{state.TransitionCount}: {source} -> {state.Cursor} via {reason}." +
            (visits > 0 ? $" Revisit: next visit {visits + 1}." : "") + next);
    }

    private static int Visits(RunState state, string node) => state.Activations.Count(a => a.NodeId == node);
    private static string Process(RunState state, Activation activation) => $"Process {activation.NodeId} (visit {Visits(state, activation.NodeId)})";
}
