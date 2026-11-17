using System.Security.Cryptography;
using Merstride.Compilation;
using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal static class StartCommand
{
    internal static int Execute(string path, Dictionary<string, string> options, ProjectLocation location,
        RunStore store, string version, CliOutput output)
    {
        if (!options.ContainsKey("--advance"))
            return output.Write(RunExecution.Response(Create(path, options, location, store, version), store.Root));

        RunState? created = null;
        var phase = "creation";
        var advance = "not_started";
        using var interruption = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; interruption.Cancel(); };
        Console.CancelKeyPress += cancel;
        CliResponse response;
        int code;
        try
        {
            var run = Create(path, options, location, store, version, state =>
            {
                created = state;
                phase = "selection";
                output.Activity(state, "Created new ready run; no node has executed. Initial advance requested.");
            }, interruption.Token);
            phase = "advance";
            if (interruption.IsCancellationRequested)
            {
                response = RunExecution.Response(run, store.Root) with
                { Error = new("INTERRUPTED", "Creation succeeded; canceled before initial advance. The run remains ready; advance this exact run explicitly when intended.") };
                code = 130;
            }
            else
            {
                // Release the root context lock before execution, then use only the created identity.
                using var held = store.Lock(run.RunId);
                run = store.Read(run.RunId);
                advance = "error";
                (response, code) = RunExecution.Advance(run, store, output, interruption.Token);
                advance = code == 130 ? "interrupted" : "finished";
            }
        }
        catch (Exception ex)
        {
            var failure = Failure(ex);
            code = interruption.IsCancellationRequested ? 130 : failure.Code;
            response = new(StateRoot: store.Root, Error: failure.Error, Diagnostics: failure.Diagnostics);
            if (created is not null)
            {
                // Never claim an unsaved result. A fresh public status projects a lost executing writer.
                try { response = RunExecution.Response(store.Status(created.RunId), store.Root) with { Error = failure.Error }; }
                catch (Exception readError) when (readError is MerstrideException or IOException or UnauthorizedAccessException)
                { response = response with { RunId = created.RunId, SourcePath = created.SourcePath, WorkflowId = created.Definition.Id }; }
            }
        }
        finally { Console.CancelKeyPress -= cancel; }
        return output.Write(response with { Start = new(created is not null, phase, advance) }, code);
    }

    private static RunState Create(string path, Dictionary<string, string> options, ProjectLocation location,
        RunStore store, string version, Action<RunState>? saved = null, CancellationToken interruption = default)
    {
        using var file = File.OpenRead(path);
        if (file.Length > 1048576) throw new MerstrideException("INVALID_DOCUMENT", "Document exceeds 1 MiB.", 3, new(1, 1));
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        var workflow = WorkflowCompiler.Compile(bytes, options.GetValueOrDefault("--workflow"));
        var workspace = options.TryGetValue("--workspace", out var selected) ? Absolute(selected) : location.Workspace;
        if (!Directory.Exists(workspace)) throw new MerstrideException("USAGE", "Workspace must be an existing directory. Run merstride --help for usage.", 2);
        var input = options.TryGetValue("--input-file", out var inputFile) ? JsonValues.Compact(AgentFiles.Input(inputFile)) : "{}";
        if (interruption.IsCancellationRequested)
            throw new MerstrideException("INTERRUPTED", "Canceled before run creation; no workflow node executed.", 130);
        location.Prepare();
        var catalog = new RunCatalog(store);
        using var selectionLock = catalog.Lock();
        _ = catalog.Read();
        var state = new RunState(new IdentityAllocator(store).Run(), "ready", workflow,
            Convert.ToHexStringLower(SHA256.HashData(bytes)), version, workspace, input, 0, null, workflow.Entry, [], Receipts: [], SourcePath: Path.GetFullPath(path));
        store.Create(state);
        saved?.Invoke(state);
        catalog.Select(state);
        return state;
    }

    private static string Absolute(string path) => Path.IsPathFullyQualified(path) ? Path.GetFullPath(path)
        : throw new MerstrideException("USAGE", "Path must be absolute. Run merstride --help for usage.", 2);

    private static (Problem Error, int Code, Diagnostic[]? Diagnostics) Failure(Exception ex) => ex switch
    {
        MerstrideException error => (error.Problem, error.ExitCode, error.ExitCode == 3
            ? [new("error", error.Problem.Code, error.Problem.Message, error.Problem.Location)] : null),
        IOException or UnauthorizedAccessException => (new("IO_ERROR", "Cannot access the requested workflow or state storage. Check the path, directory type, permissions and original --state-dir; no fallback or reset was performed."), 6, null),
        _ => (new("INTERNAL_ERROR", "An unexpected internal error occurred."), 6, null)
    };
}
