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
        if (output.CompactExecution)
        {
            CompactActivity(state, activation, prior);
            previous = state;
            return;
        }
        if (state.Status == "executing" && activation is not null && activation.Id != prior?.Id)
            output.Activity(state, $"{Process(state, activation)}: activating (launch pending).");
        if (activation?.FinishedAt is not null && (activation.Id != prior?.Id || prior.FinishedAt is null))
        {
            output.Activity(state, $"{Process(state, activation)}: saved {CliOutput.Outcome(activation)}.");
            if (state.TransitionCount > previous.TransitionCount)
            {
                var route = state.Definition.ProcessRoute(activation.NodeId, activation.Outcome);
                var reason = route?.Outcome switch
                {
                    "default" => $"default (no exact match for {activation.Outcome})",
                    null => "unlabeled edge (accepts any process outcome, including failures)",
                    _ => $"exact outcome {activation.Outcome}"
                };
                Movement(output, state, activation.NodeId, reason);
            }
            else output.Activity(state, $"Flow stays at {state.Cursor}; no route taken. " +
                (state.Status == "interrupted" ? "External effects may already have happened."
                    : state.Error?.Message));
        }
        if (state.Status == "waiting_agent" && state.Request?.Id != previous.Request?.Id)
            output.Activity(state, $"Run {previous.Status} -> waiting_agent at {state.Cursor} (visit {ExecutionNarration.Visits(state, state.Cursor)}): " +
                $"waiting for agent; request {state.Request!.Id}. Flow stays here. Next: read the prompt on stdout and complete this request with an allowed outcome.");
        if (state.Status is "completed" or "failed" or "interrupted" && state.Status != previous.Status)
            output.Activity(state, $"Run {previous.Status} -> {state.Status} at {state.Cursor}: " + (state.Status == "completed"
                ? "success end; overall result: success."
                : state.Status == "interrupted" ? "execution interrupted; overall result unresolved. Next: inspect external effects, then explicitly resolve --retry or --fail."
                : $"overall result: failure. {state.Error?.Message} ({state.Error?.Code}). Next: inspect status/logs; start a new run after correcting the cause."));
        previous = state;
    }

    private void CompactActivity(RunState state, Activation? activation, Activation? prior)
    {
        if (activation is null) return;
        var step = ExecutionNarration.Step(state, activation.NodeId);
        if (state.Status == "executing" && activation.Id != prior?.Id)
        {
            var visit = ExecutionNarration.Visits(state, activation.NodeId);
            output.Activity(state, $"Starting {step}{(visit > 1 ? $" (visit {visit})" : "")}.");
        }
        else if (activation.FinishedAt is not null && prior?.FinishedAt is null && state.TransitionCount > previous.TransitionCount)
        {
            var fallback = state.Definition.ProcessRoute(activation.NodeId, activation.Outcome)?.Outcome == "default";
            if (fallback || activation.Outcome != "exit_0")
            {
                var destination = ExecutionNarration.Step(state, state.Cursor);
                var next = fallback ? $"The fallback leads to {destination}." : $"The workflow continues to {destination}.";
                output.Activity(state, $"{step} {ExecutionNarration.ProcessResult(activation)}. {next}");
            }
        }
    }

    internal static void Completion(CliOutput output, RunState before, RunState state, CompletionReceipt receipt)
    {
        output.Activity(state, $"Agent {before.Cursor} (visit {ExecutionNarration.Visits(before, before.Cursor)}): completion {receipt.RequestId} accepted and saved ({receipt.Outcome}).");
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
        var visits = ExecutionNarration.Visits(state, state.Cursor);
        output.Activity(state, $"Flow #{state.TransitionCount}: {source} -> {state.Cursor} via {reason}." +
            (visits > 0 ? $" Revisit: next visit {visits + 1}." : "") + next);
    }

    private static string Process(RunState state, Activation activation) => $"Process {activation.NodeId} (visit {ExecutionNarration.Visits(state, activation.NodeId)})";
}
