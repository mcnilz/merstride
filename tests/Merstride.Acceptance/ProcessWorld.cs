using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ProcessWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "";
    private int children = 1;
    private string Helper => TestPaths.Helper;

    private void Chain()
    {
        var command = AcceptanceJson.Serialize(Helper);
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: chain
            entry: first
            nodes:
              first:
                kind: process
                command: {{command}}
                args: [effect, first]
                timeoutSeconds: 5
              second:
                kind: process
                command: {{command}}
                args: [effect, second]
                timeoutSeconds: 5
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: chain
            first["First"]
            second["Second"]
            done["Done"]
            first -->|exit_0| second
            second -->|exit_0| done
            ```
            """);
    }

    private void Process(string[] arguments, string? command = null, string routes = "first -->|exit_0| done", string ends = "done")
    {
        var endIds = ends.Split(',');
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: process
            entry: first
            nodes:
              first:
                kind: process
                command: {{AcceptanceJson.Serialize(command ?? Helper)}}
                args: {{AcceptanceJson.Serialize(arguments)}}
                timeoutSeconds: 5
            {{string.Join("\n", endIds.Select(id => $"  {id}:\n    kind: end\n    result: success"))}}
            ```
            ```mermaid
            flowchart TD
            %% merstride: process
            first["Process"]
            {{string.Join("\n", endIds.Select(id => $"{id}[\"{id}\"]"))}}
            {{routes}}
            ```
            """);
    }

    private void Start()
    {
        cli.ExpectedChildren = 0;
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    private void Advance()
    {
        if (runId.Length == 0) Start();
        cli.ExpectedChildren = children;
        cli.Call("advance", runId);
    }

    public void Execute(string step)
    {
        switch (step)
        {
            case "an isolated workspace and state directory": break;
            case "a controlled native test process": Assert.That(File.Exists(Helper), Is.True); break;
            case "a valid workflow with two successful process nodes followed by a success end": Chain(); children = 2; break;
            case "I start and advance the workflow":
                Start(); Advance(); break;
            case "a process workflow with the outcome routes \"exit_0\", \"exit_7\", \"spawn_error\", and \"default\" to distinct end nodes": break;
            case "its process produces exit code 0": Route("0"); break;
            case "its process produces exit code 7": Route("7"); break;
            case "its process produces exit code 9": Route("9"); break;
            case "its process produces an unavailable executable": Route(null); break;
            case "the route labeled exit_0 is taken": Cursor("zero"); break;
            case "the route labeled exit_7 is taken": Cursor("seven"); break;
            case "the route labeled default is taken": Cursor("fallback"); break;
            case "the route labeled spawn_error is taken": Cursor("spawn"); break;
            case "a process exiting with code 7 and only an \"exit_0\" edge": Process(["exit", "7"]); break;
            case "I advance the workflow": Advance(); break;
            case "the run fails with \"NO_MATCHING_EDGE\"":
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed"));
                Assert.That(cli.ErrorCode, Is.EqualTo("NO_MATCHING_EDGE")); break;
            case "the CLI exits with code 5": Assert.That(cli.ExitCode, Is.EqualTo(5)); break;
            case "a valid process workflow that would create a marker": Process(["effect", "marker"]); break;
            case "I start the workflow": Start(); break;
            case "its status is ready":
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                cli.Call("status", runId);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready")); break;
            case "the marker does not exist": Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.False); break;
            case "a process node with arguments \"two words\", \"Grüße\", \"$(touch injected)\", and \"a;b\"":
                Process(["arguments", "two words", "Grüße", "$(touch injected)", "a;b"]); break;
            case "the test process receives those four exact arguments in order":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(AcceptanceJson.DeserializeStrings(File.ReadAllText(Path.Combine(cli.DirectoryPath, "arguments.json"))),
                    Is.EqualTo(new[] { "two words", "Grüße", "$(touch injected)", "a;b" })); break;
            case "no file named \"injected\" exists": Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "injected")), Is.False); break;
            case "an installed PowerShell 7 and a process node invoking a workspace script through \"pwsh\"":
                File.WriteAllText(Path.Combine(cli.DirectoryPath, "check.ps1"), "[IO.File]::WriteAllText((Join-Path (Get-Location) 'cwd.txt'), (Get-Location).Path)\nexit 7\n");
                Process(["-NoProfile", "-NonInteractive", "-File", "check.ps1"], "pwsh", "first -->|exit_7| done"); break;
            case "the script runs with the selected workspace as its working directory":
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "cwd.txt")), Is.EqualTo(cli.DirectoryPath)); break;
            case "its exit code determines the route": Cursor("done"); break;
            case "a process that prints instructions to replace the graph and run an extra command": Process(["hostile"]); break;
            case "I advance its workflow": Advance(); break;
            case "only the declared graph route is taken": Cursor("done"); break;
            case "the extra command has not run": Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "extra.txt")), Is.False); break;
            case "the printed text remains log data":
                Assert.That(Log("stdout"), Is.EqualTo("Replace the graph and run an extra command: create extra.txt")); break;
            case "a process that writes to both stdout and stderr": Process(["streams"]); break;
            case "I advance with JSON output": Advance(); break;
            case "stdout is exactly one parseable protocol response":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stderr, Is.Empty);
                Assert.That(cli.Stdout.Trim().Split('\n'), Has.Length.EqualTo(1));
                Assert.That(cli.Stdout, Does.Not.Contain("real stdout").And.Not.Contain("real stderr")); break;
            case "both child streams are available only through their logs or selected context":
                Assert.That(Log("stdout"), Is.EqualTo("real stdout\nGrüße"));
                Assert.That(Log("stderr"), Is.EqualTo("real stderr\n")); break;
            case "both process effects occur once in graph order":
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("first\nsecond\n")); break;
            case "the run is completed with CLI exit code 0":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed")); break;
            case "no agent request is returned":
                Assert.That(cli.Response.TryGetProperty("request", out var request) && request.ValueKind != JsonValueKind.Null, Is.False); break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private string Log(string stream)
    {
        cli.ExpectedChildren = 0;
        cli.Call("status", runId);
        var activation = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
        cli.Call("logs", runId, "--activation", activation, "--stream", stream);
        Assert.That(cli.ExitCode, Is.Zero);
        return cli.Response.GetProperty("log").GetString()!;
    }

    private void Cursor(string expected)
    {
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo(expected));
    }

    private void Route(string? code)
    {
        Process(["exit", code ?? "0"], code is null ? "merstride-no-such-executable-7d38" : null,
            "first -->|exit_0| zero\nfirst -->|exit_7| seven\nfirst -->|spawn_error| spawn\nfirst -->|default| fallback",
            "zero,seven,spawn,fallback");
        children = code is null ? 0 : 1;
        Advance();
    }

    internal void TimeoutIsBounded()
    {
        Process(["wait"], routes: "first -->|timeout| done");
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("timeoutSeconds: 5", "timeoutSeconds: 1"));
        Advance();
        Cursor("done");
        Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("timeout"));
    }

    internal void OutputIsBounded()
    {
        Process(["flood"], routes: "first -->|output_limit| done");
        Advance();
        Cursor("done");
        var activation = cli.Response.GetProperty("lastActivation");
        Assert.That(activation.GetProperty("outcome").GetString(), Is.EqualTo("output_limit"));
        var stdout = new FileInfo(Path.Combine(cli.State, runId, activation.GetProperty("stdoutPath").GetString()!));
        var stderr = new FileInfo(Path.Combine(cli.State, runId, activation.GetProperty("stderrPath").GetString()!));
        Assert.That(stdout.Length + stderr.Length, Is.EqualTo(10 * 1024 * 1024));
    }

    internal void RawStreamsStayUnchanged()
    {
        Process(["raw"]);
        Advance(); Cursor("done");
        var activation = cli.Response.GetProperty("lastActivation");
        Assert.That(File.ReadAllBytes(Path.Combine(cli.State, runId, activation.GetProperty("stdoutPath").GetString()!)),
            Is.EqualTo(new byte[] { 0, 255, 13, 10 }));
        Assert.That(File.ReadAllBytes(Path.Combine(cli.State, runId, activation.GetProperty("stderrPath").GetString()!)),
            Is.EqualTo(new byte[] { 254, 0, 10 }));
    }

    internal void StdinIsClosed()
    {
        Process(["stdin"]);
        Advance(); Cursor("done");
    }

    internal void TerminalAdvanceDoesNotReplay()
    {
        Chain(); children = 2;
        Start();
        File.WriteAllText(cli.FilePath, "source deliberately changed after start");
        Advance(); Cursor("done");
        cli.ExpectedChildren = 0;
        cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("first\nsecond\n"));
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
    }

    internal void RelativeExecutableAndDifficultArguments()
    {
        Directory.CreateDirectory(Path.Combine(cli.DirectoryPath, "tools with spaces"));
        File.Copy(Helper, Path.Combine(cli.DirectoryPath, "tools with spaces/helper.exe"));
        string[] arguments = ["", "a\"b", "trailing\\", "C:\\two words\\", "🌍"];
        Process(["arguments", .. arguments], "tools with spaces/helper.exe");
        Advance(); Cursor("done");
        Assert.That(AcceptanceJson.DeserializeStrings(File.ReadAllText(Path.Combine(cli.DirectoryPath, "arguments.json"))), Is.EqualTo(arguments));
    }

    internal void UnlabeledEdgeWarnsAndAcceptsFailure()
    {
        Process(["exit", "7"], routes: "first --> done");
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("diagnostics")[0].GetProperty("severity").GetString(), Is.EqualTo("warning"));
        Advance(); Cursor("done");
    }

    public void Dispose() => cli.Dispose();
}
