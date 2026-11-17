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
        if (state.Status == "executing" && activation?.Id != prior?.Id)
            output.Activity(state, "Activating process (launch pending).", activation);
        if (activation?.FinishedAt is not null && (activation.Id != prior?.Id || prior.FinishedAt is null))
            output.Activity(state, $"Process outcome saved: {activation.Outcome}." +
                (state.TransitionCount > previous.TransitionCount ? $" Route: {activation.NodeId} -> {state.Cursor}." : " No route taken."), activation);
        if (state.Status == "waiting_agent" && state.Request?.Id != previous.Request?.Id)
            output.Activity(state, $"Waiting for agent; request {state.Request!.Id}. Read the prompt and return an allowed outcome with complete.", activation);
        if (state.Status is "completed" or "failed" or "interrupted" && state.Status != previous.Status)
            output.Activity(state, $"Run {state.Status}." + (state.Status == "interrupted"
                ? " Inspect external effects, then explicitly resolve --retry or --fail."
                : state.Error is { } error ? $" Reason: {error.Code}." : ""));
        previous = state;
    }
}
