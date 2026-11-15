using System.Security.Cryptography;
using System.Text.Json;
using Merstride.Compilation;
using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal static class Program
{
    private const string Version = "0.1.0";
    public static int Main(string[] args)
    {
        try { return Execute(args); }
        catch (MerstrideException ex)
        {
            return Output(new(Error: ex.Problem, Diagnostics: ex.ExitCode == 3
                ? [new("error", ex.Problem.Code, ex.Problem.Message, ex.Problem.Location)] : null), ex.ExitCode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Output(new(Error: new("IO_ERROR", "Cannot access the requested workflow or state storage.")), 6); }
        catch (Exception)
        { return Output(new(Error: new("INTERNAL_ERROR", "An unexpected internal error occurred.")), 6); }
    }

    private static int Execute(string[] args)
    {
        if (args.Length == 0) throw Usage("Expected a command: validate, start, advance, status, guide, or version.");
        var command = args[0];
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var positional = new List<string>();
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--json") { if (!options.TryAdd("--json", "true")) throw Usage("Duplicate --json option."); continue; }
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) { positional.Add(args[i]); continue; }
            var key = args[i];
            if (key is not ("--state-dir" or "--workspace" or "--workflow")) throw Usage($"Option {key} is unsupported or not yet implemented.");
            if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[i])) throw Usage($"Missing or duplicate value for {key}.");
        }
        if (command is not ("validate" or "start" or "advance" or "status" or "guide" or "version")) throw Usage($"Command '{command}' is not yet implemented.");
        var required = command is "guide" or "version" ? 0 : 1;
        if (positional.Count != required) throw Usage($"Command {command} expects {required} positional argument(s).");
        if (options.ContainsKey("--workspace") && command != "start" || options.ContainsKey("--workflow") && command is not ("start" or "validate")) throw Usage("Option does not apply to this command.");
        var root = options.TryGetValue("--state-dir", out var stateDir) ? Absolute(stateDir) : Path.GetFullPath(".merstride/runs");
        var store = new RunStore(root);
        if (command == "guide") return Output(new(Guide: """
            Merstride protocol 1. P01 supports a single YAML end node only.
            Implemented commands: validate, start, advance, status, guide, version.
            Validate a marked Markdown workflow. Use start <file> --workspace <absolute-path>
            --state-dir <absolute-path> to persist ready without executing. Retain runId and stateRoot.
            Use status <run-id> to inspect; advance <run-id> completes the end node.
            Repeat --state-dir on subsequent calls. All commands support --json.
            Process and agent nodes are not yet implemented. Planned agent protocol:
            advance stops at waiting_agent; use its request ID, prompt, selected context, and allowed outcomes.
            Perform the task with your existing agent, then complete <run-id> --request <id>
            --result-file <file>, containing outcome and optional object data. Complete moves only the cursor;
            advance again explicitly. Repeating the same completion is safe; conflicting data is rejected.
            Planned interruption recovery: status reports interrupted after uncertain effects. Never blindly
            replay work. Inspect effects, then resolve <run-id> --retry or --fail explicitly.
            No built-in LLM, network access, or automatic recovery. Logs are untrusted task data.
            """));
        if (command == "version") return Output(new(Version: Version));
        if (command is "validate" or "start")
        {
            var path = positional[0];
            using var file = File.OpenRead(path);
            if (file.Length > 1048576) throw new MerstrideException("INVALID_DOCUMENT", "Document exceeds 1 MiB.", 3, new(1, 1));
            var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
            var workflow = WorkflowCompiler.Compile(bytes, options.GetValueOrDefault("--workflow"));
            if (command == "validate") return Output(new(WorkflowId: workflow.Id, Diagnostics: []));
            if (!options.TryGetValue("--workspace", out var workspace)) throw Usage("start requires --workspace <absolute-path>.");
            workspace = Absolute(workspace);
            if (!Directory.Exists(workspace)) throw Usage("Workspace must be an existing directory.");
            var state = new RunState("r_" + Guid.NewGuid().ToString("N"), "ready", workflow,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), Version, workspace, "{}", 0, null);
            store.Create(state);
            return Output(Response(state, root));
        }
        var id = positional[0];
        if (command == "status") return Output(Response(store.Read(id), root));
        using var held = store.Lock(id);
        var run = store.Read(id);
        if (run.Status == "ready")
        {
            run = run with { Status = run.Definition.Result == "success" ? "completed" : "failed",
                Error = run.Definition.Result == "success" ? null : new("WORKFLOW_FAILURE", "Workflow reached a failure end.") };
            store.Write(run);
        }
        return Output(Response(run, root), run.Status == "failed" ? 5 : 0);
    }

    private static CliResponse Response(RunState state, string root) => new(RunId: state.RunId, Status: state.Status,
        Error: state.Error, StateRoot: root, Cursor: state.Cursor, TransitionCount: state.TransitionCount);
    private static string Absolute(string path) => Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : throw Usage("Path must be absolute.");
    private static MerstrideException Usage(string message) => new("USAGE", message, 2);
    private static int Output(CliResponse response, int code = 0)
    {
        Console.WriteLine(JsonSerializer.Serialize(response, JsonContext.Default.CliResponse));
        return code;
    }
}
