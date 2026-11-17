using System.Diagnostics;
using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ConsoleActivityWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private Process? writer;
    private Task<string>? stdout;
    private readonly List<string> activity = [];
    private Task? stderr;
    private string runId = "";
    private bool verified;

    private void Text(params string[] args) => cli.Raw([.. args, "--state-dir", cli.State]);
    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    private string[] ActivityLines => Lines(cli.Stderr).Where(l => l.StartsWith("[" + runId + "] ", StringComparison.Ordinal)).ToArray();

    internal void Execute(string step)
    {
        const string process = "a console activity process producing ";
        if (step.StartsWith(process, StringComparison.Ordinal)) { ProcessFixture(step[process.Length..]); return; }
        const string result = "activity reports ";
        if (step.StartsWith(result, StringComparison.Ordinal))
        {
            var values = step[result.Length..].Split(" and ");
            Assert.That(cli.ExitCode, Is.EqualTo(values[1] == "routed" ? 0 : 5));
            Assert.That(ActivityLines, Has.Length.EqualTo(4));
            Assert.That(ActivityLines[0], Does.Contain("launch pending"));
            Assert.That(ActivityLines[1], Does.Contain("saved ").And.Contain(values[0]));
            Assert.That(ActivityLines[2], Does.Contain(values[1] == "NO_MATCHING_EDGE" ? "no route taken" : "-> done"));
            Assert.That(ActivityLines[3], Does.Contain(values[1] == "routed" ? "completed" : values[1]));
            Assert.That(cli.Stderr, Does.Not.Contain("Process started").And.Not.Contain("Process running"));
            return;
        }
        switch (step)
        {
            case "a console activity chain ending at an agent": ChainFixture(); break;
            case "I advance the chain as text":
                cli.ExpectedChildren = 2; Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "both outcomes and routes precede the agent waiting message":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(ActivityLines, Has.Length.EqualTo(7));
                Assert.That(ActivityLines[0], Does.Contain("Process first (visit 1)").And.Contain("activating"));
                Assert.That(ActivityLines[1], Does.Contain("exit_0"));
                Assert.That(ActivityLines[2], Does.Contain("first -> second"));
                Assert.That(ActivityLines[3], Does.Contain("Process second (visit 1)").And.Contain("activating"));
                Assert.That(ActivityLines[4], Does.Contain("exit_7"));
                Assert.That(ActivityLines[5], Does.Contain("second -> review"));
                Assert.That(ActivityLines[6], Does.Contain("waiting for agent; request q_"));
                Assert.That(cli.Stdout, Does.Contain("Next: perform").And.Contain("Status: waiting_agent"));
                Assert.That(cli.Stdout, Does.Contain("Grüße\\u001B[31m\\u0000\\u202E").And.Not.Contain('\u001b').And.Not.Contain('\0'));
                break;
            case "inspection and repeated waiting advance announce no new work":
                cli.Call("status", runId);
                var waiting = cli.Stdout;
                foreach (var command in new[] { "status", "advance" })
                {
                    Text(command, runId);
                    Assert.That(cli.Stderr, Is.Empty);
                    Assert.That(cli.Stdout, Does.Contain("Status: waiting_agent").And.Contain("Next: perform"));
                    cli.Call("status", runId);
                    Assert.That(cli.Stdout, Is.EqualTo(waiting));
                }
                break;
            case "rejected completion announces no acceptance":
                cli.Call("status", runId);
                var request = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                var before = cli.Stdout;
                var stateFile = Path.Combine(cli.State, runId, "state.json");
                var saved = File.ReadAllBytes(stateFile);
                Text("complete", runId, "--request", request, "--outcome", "rejected");
                Assert.That(cli.ExitCode, Is.EqualTo(4));
                Assert.That(cli.Stderr, Does.Contain("INVALID_OUTCOME").And.Not.Contain("accepted and saved").And.Not.Contain("[" + runId + "]"));
                Assert.That(File.ReadAllBytes(stateFile), Is.EqualTo(saved));
                cli.Call("status", runId);
                Assert.That(cli.Stdout, Is.EqualTo(before));
                break;
            case "accepted completion is saved once and executes nothing":
                cli.Call("status", runId);
                var requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                Text("complete", runId, "--request", requestId, "--outcome", "approved");
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(ActivityLines, Has.Length.EqualTo(2));
                Assert.That(cli.Stderr, Does.Contain("accepted and saved").And.Contain(requestId).And.Contain("Next: merstride advance"));
                Assert.That(cli.Stdout, Does.Contain("Status: ready").And.Contain("Cursor: done"));
                var accepted = cli.Stdout;
                Text("complete", runId, "--request", requestId, "--outcome", "approved");
                Assert.That(cli.Stderr, Is.Empty);
                Assert.That(cli.Stdout, Is.EqualTo(accepted));
                break;
            case "final advance reports completion only once":
                Text("advance", runId);
                Assert.That(ActivityLines, Has.Length.EqualTo(1));
                Assert.That(cli.Stderr, Does.Contain("Run ready -> completed"));
                Text("advance", runId);
                Assert.That(cli.Stderr, Is.Empty);
                Assert.That(cli.Stdout, Does.Contain("Status: completed"));
                break;
            case "I execute the handoff and completion with explicit JSON":
                cli.ExpectedChildren = 2; cli.Call("advance", runId); cli.ExpectedChildren = 0; CheckJson();
                var jsonRequest = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                cli.Call("advance", runId); CheckJson();
                cli.Call("complete", runId, "--request", jsonRequest, "--outcome", "invalid"); CheckJson(4);
                cli.Call("complete", runId, "--request", jsonRequest, "--outcome", "approved"); CheckJson();
                cli.Call("complete", runId, "--request", jsonRequest, "--outcome", "approved"); CheckJson();
                cli.Call("advance", runId); CheckJson(); verified = true;
                break;
            case "every JSON call has one protocol response and no activity": Assert.That(verified, Is.True); break;
            case "a console activity workflow containing terminal controls": ProcessFixture("raw"); break;
            case "I advance the process as text":
                cli.ExpectedChildren = File.ReadAllText(cli.FilePath).Contains("unavailable-p17", StringComparison.Ordinal) ? 0 : 1;
                Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "text feedback is escaped and bounded while raw logs remain unchanged":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stderr.Length, Is.LessThan(2000));
                Assert.That(cli.Stderr, Does.Not.Contain('\u001b').And.Not.Contain('\0'));
                cli.Call("status", runId);
                var activationId = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
                cli.Call("logs", runId, "--activation", activationId, "--stream", "stdout");
                Assert.That(cli.Response.GetProperty("log").GetString(), Is.EqualTo("\0\uFFFD\r\n"));
                Text("logs", runId, "--activation", activationId, "--stream", "stdout");
                Assert.That(cli.Stdout, Does.Contain("\\u0000"));
                break;
            case "a failed result save announces neither a saved outcome nor completion": FailedSave(); break;
            case "I interrupt a text advance through the native signal fixture":
                using (var recovery = new RecoveryWorld()) recovery.AssertTextInterruption();
                verified = true; break;
            case "interruption feedback requires inspection and explicit resolution": Assert.That(verified, Is.True); break;
            case "a console activity loop with one allowed transition": ProcessFixture("loop"); break;
            case "the activity consumer closes its pipe before process completion":
                writer = LaunchAdvance();
                var firstLine = writer.StandardError.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Assert.That(firstLine, Does.Contain("activating (launch pending)"));
                writer.StandardError.Close();
                File.WriteAllText(Path.Combine(cli.DirectoryPath, "activity.release"), "release");
                Assert.That(writer.WaitForExit(10000), Is.True);
                Assert.That(writer.ExitCode, Is.Zero, stdout!.Result);
                break;
            case "the saved run still completes successfully":
                cli.Call("status", runId);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
                break;
            case "the budget error follows one saved outcome without another activation":
                Assert.That(cli.ExitCode, Is.EqualTo(5));
                Assert.That(ActivityLines, Has.Length.EqualTo(4));
                Assert.That(ActivityLines[2], Does.Contain("work -> work"));
                Assert.That(ActivityLines[3], Does.Contain("TRANSITION_LIMIT"));
                break;
            case "a console activity workflow with a controlled process":
                File.WriteAllText(cli.FilePath, $$"""
                    ```yaml merstride
                    schemaVersion: 1
                    id: activity
                    entry: work
                    nodes:
                      work:
                        kind: process
                        command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                        args: [barrier, activity]
                        timeoutSeconds: 30
                      done:
                        kind: end
                        result: success
                    ```
                    ```mermaid
                    flowchart TD
                        %% merstride: activity
                        work["Work"]
                        done["Done"]
                        work -->|exit_0| done
                    ```
                    """);
                cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
                Assert.That(cli.ExitCode, Is.Zero);
                runId = cli.Response.GetProperty("runId").GetString()!;
                break;
            case "I advance it with redirected text output":
                writer = LaunchAdvance();
                stderr = Task.Run(async () =>
                {
                    while (await writer.StandardError.ReadLineAsync() is { } line)
                        lock (activity) activity.Add(line);
                });
                break;
            case "its activation is visible before the process is released":
                AssertLiveActivation();
                File.WriteAllText(Path.Combine(cli.DirectoryPath, "activity.release"), "release");
                break;
            case "its activation is visible before a rejected save": AssertLiveActivation(); break;
            case "its saved outcome and terminal transition appear in order":
                Assert.That(writer!.WaitForExit(10000), Is.True);
                stderr!.GetAwaiter().GetResult();
                Assert.That(writer.ExitCode, Is.Zero);
                Assert.That(stdout!.Result, Does.Contain("Status: completed"));
                Assert.That(activity.Count, Is.EqualTo(4));
                Assert.That(activity[0], Does.Contain(runId).And.Contain("Process work").And.Contain("launch pending"));
                Assert.That(activity[1], Does.Contain("exit_0"));
                Assert.That(activity[2], Does.Contain("-> done"));
                Assert.That(activity[3], Does.Contain("completed"));
                break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private void AssertLiveActivation()
    {
        Assert.That(SpinWait.SpinUntil(() =>
        {
            lock (activity) return activity.Any(l => l.Contains("activating (launch pending)", StringComparison.Ordinal));
        }, 5000), Is.True, "Activation feedback must arrive while the helper is still blocked.");
        Assert.That(writer!.HasExited, Is.False);
        Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "activity.release")), Is.False);
        Assert.That(SpinWait.SpinUntil(() => File.Exists(Path.Combine(cli.DirectoryPath, "activity.ready")), 5000), Is.True);
        cli.Call("status", runId);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("executing"));
    }

    private Process LaunchAdvance()
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ??
            Path.Combine(FeatureTests.Root, "artifacts/native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride"))
        {
            WorkingDirectory = cli.DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "advance", runId, "--state-dir", cli.State }
        };
        var launched = Process.Start(start)!;
        stdout = launched.StandardOutput.ReadToEndAsync();
        return launched;
    }

    private void Start()
    {
        Text("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        Assert.That(cli.Stderr, Is.Empty, "Start executes nothing and emits no activity.");
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    private void CheckJson(int code = 0)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(code), cli.Stdout + cli.Stderr);
        Assert.That(cli.Stderr, Is.Empty);
        Assert.That(Lines(cli.Stdout), Has.Length.EqualTo(1));
        Assert.That(cli.Response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
    }

    private void ChainFixture()
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: chain
            entry: first
            nodes:
              first:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [exit, '0']
                timeoutSeconds: 5
              second:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [exit, '7']
                timeoutSeconds: 5
              review:
                kind: agent
                prompt: "Review Grüße\e[31m\u0000\u202E."
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
                %% merstride: chain
                first["First"]
                second["Second"]
                review["Review"]
                done["Done"]
                first -->|exit_0| second
                second -->|default| review
                review -->|approved| done
            ```
            """);
        Start();
    }

    private void ProcessFixture(string outcome)
    {
        var args = outcome switch
        {
            "timeout" => new[] { "wait" }, "output_limit" => ["flood"], "raw" => ["raw"],
            "exit_7" => ["exit", "7"], _ => ["exit", "0"]
        };
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: outcome
            entry: work
            limits:
              maxTransitions: {{(outcome == "loop" ? 1 : 10)}}
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(outcome == "spawn_error" ? "unavailable-p17" : TestPaths.Helper)}}
                args: {{AcceptanceJson.Serialize(args)}}
                timeoutSeconds: 1
              done:
                kind: end
                result: {{(outcome == "exit_0" ? "failure" : "success")}}
            ```
            ```mermaid
            flowchart TD
                %% merstride: outcome
                work["Work"]
                done["Done"]
                {{(outcome == "loop" ? "work -->|exit_0| work\n    work -->|default| done" : "work -->|" + (outcome == "exit_7" ? "exit_0" : "default") + "| done")}}
            ```
            """);
        Start();
    }

    private void FailedSave()
    {
        Assert.That(SpinWait.SpinUntil(() => File.Exists(Path.Combine(cli.DirectoryPath, "activity.ready")), 5000), Is.True);
        var stateFile = Path.Combine(cli.State, runId, "state.json");
        var snapshot = File.ReadAllBytes(stateFile);
        using (new RejectedStateSave(stateFile))
        {
            File.WriteAllText(Path.Combine(cli.DirectoryPath, "activity.release"), "release");
            Assert.That(writer!.WaitForExit(10000), Is.True);
            stderr!.GetAwaiter().GetResult();
            Assert.That(writer.ExitCode, Is.EqualTo(6));
            Assert.That(string.Join('\n', activity), Does.Contain("activating (launch pending)").And.Contain("IO_ERROR")
                .And.Not.Contain("saved exit").And.Not.Contain("Flow #").And.Not.Contain("overall result: success"));
            Assert.That(File.ReadAllBytes(stateFile), Is.EqualTo(snapshot));
        }
        Text("status", runId);
        Assert.That(cli.Stdout, Does.Contain("Status: interrupted").And.Contain("Next: inspect external effects"));
        Assert.That(ActivityLines, Is.Empty, "Inspection must not claim a saved interruption.");
    }

    public void Dispose()
    {
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "activity.release"), "release");
        if (writer is not null && !writer.HasExited && !writer.WaitForExit(3000))
        { writer.Kill(true); writer.WaitForExit(3000); }
        stderr?.GetAwaiter().GetResult();
        writer?.Dispose();
        cli.Dispose();
    }
}

// Public filesystem failure fixture shared by native save-truthfulness scenarios.
internal sealed class RejectedStateSave : IDisposable
{
    private readonly string path, directory;
    private readonly FileAttributes attributes;
    private readonly UnixFileMode mode;

    internal RejectedStateSave(string path)
    {
        this.path = path;
        directory = Path.GetDirectoryName(path)!;
        attributes = File.GetAttributes(path);
        mode = OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(directory);
        try
        {
            File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        try { if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, mode); }
        finally { File.SetAttributes(path, attributes); }
    }
}
