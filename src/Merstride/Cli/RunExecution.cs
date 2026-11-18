using Merstride.Application;
using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal static class RunExecution
{
    // The caller holds this exact run's writer lock throughout the synchronous chain.
    internal static (CliResponse Response, int Code) Advance(RunState run, RunStore store, CliOutput output,
        Dictionary<string, string> options, CancellationToken interruption)
    {
        try
        {
            var retained = (options.ContainsKey("--save-stdout") ? ProcessLogs.Stdout : ProcessLogs.None) |
                (options.ContainsKey("--save-stderr") ? ProcessLogs.Stderr : ProcessLogs.None);
            run = WorkflowEngine.Advance(run, new ConsoleActivity(store, output, run),
                output.ProcessRunner(store.RunDirectory(run.RunId), interruption), store, new IdentityAllocator(store), retained, interruption);
            return (Response(run, store.Root), interruption.IsCancellationRequested ? 130 : run.Status is "failed" or "interrupted" ? 5 : 0);
        }
        catch (Exception ex) when (interruption.IsCancellationRequested && ex is IOException or UnauthorizedAccessException)
        {
            return (new(RunId: run.RunId, Status: "interrupted", StateRoot: store.Root,
                Error: new("INTERRUPTED", "User interrupted execution; recovery state could not be saved. Inspect external effects before resolution.")), 130);
        }
    }

    internal static CliResponse Response(RunState state, string root) => new(RunId: state.RunId, Status: state.Status,
        Error: state.Error, StateRoot: root, Cursor: state.Cursor, TransitionCount: state.TransitionCount,
        LastActivation: state.Activations.LastOrDefault(), Request: state.Request, SourcePath: state.SourcePath, WorkflowId: state.Definition.Id);
}
