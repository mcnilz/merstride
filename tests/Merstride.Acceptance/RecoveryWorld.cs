using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace Merstride.Acceptance;

internal sealed class RecoveryWorld(bool inline = false) : IDisposable
{
    private readonly CliWorld cli = new();
    private readonly List<Process> helpers = [];
    private Process? writer;
    private Process? signalHost;
    private Task<string>? output, error;
    private string runId = "", priorActivation = "";
    private byte[] snapshot = [];
    private readonly List<JsonElement> readerResponses = [];
    private uint? processBaseline;
    private int readCalls;
    private string Helper => TestPaths.Helper;
    private string StateFile => Path.Combine(cli.State, runId, "state.json");
    private string PathFor(string name) => Path.Combine(cli.DirectoryPath, name);

    internal void Execute(string step)
    {
        switch (step)
        {
            case "advance is holding a run lock while a test process waits on a barrier": StartBarrier(2); break;
            case "an executing process with an attached child": StartInterrupt(); break;
            case "I send the platform's supported user-interrupt signal":
                File.WriteAllText(PathFor("signal.request"), "signal"); WaitFile("signal.exit");
                cli.ExitCode = int.Parse(File.ReadAllText(PathFor("signal.exit")), System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "the CLI exits with code 130": Assert.That(cli.ExitCode, Is.EqualTo(130), File.ReadAllText(PathFor("signal.stderr"))); break;
            case "the process tree is terminated on the supported test platform":
                foreach (var helper in helpers) Assert.That(helper.WaitForExit(2000), Is.True);
                Assert.That(signalHost!.WaitForExit(2000), Is.True);
                if (processBaseline.HasValue) Assert.That(ProcessObserver.TotalProcesses() - processBaseline, Is.EqualTo(OperatingSystem.IsWindows() ? 5 : 4),
                    "Signal host, private console host, CLI and two attached helpers.");
                cli.Response = JsonDocument.Parse(File.ReadAllText(PathFor("signal.stdout"))).RootElement.Clone();
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("interrupted")); break;
            case "a fresh status reports interrupted":
                cli.Call("status", runId);
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("interrupted")); break;
            case "a persisted run whose authoritative state file has been corrupted":
                CreateWorkflow();
                snapshot = "{\"runId\":\"truncated"u8.ToArray(); File.WriteAllBytes(StateFile, snapshot); break;
            case "I read its status": cli.Call("status", runId); break;
            case "the CLI exits with code 6":
                Assert.That(cli.ExitCode, Is.EqualTo(6));
                Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("CORRUPT_STATE")); break;
            case "the corrupt state is not replaced or reset": Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot)); break;
            case "a writer repeatedly commits valid transitions while holding its run lock": StartBarrier(12); break;
            case "independent readers repeatedly query status": ReadConcurrently(); break;
            case "every successful response is one complete protocol JSON object":
                Assert.That(readerResponses.Count, Is.GreaterThanOrEqualTo(12));
                foreach (var response in readerResponses)
                {
                    Assert.That(response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
                    Assert.That(response.GetProperty("error").ValueKind, Is.EqualTo(JsonValueKind.Null));
                }
                break;
            case "every reported cursor and status is a committed combination":
                foreach (var response in readerResponses)
                {
                    var status = response.GetProperty("status").GetString();
                    var transition = response.GetProperty("transitionCount").GetInt32();
                    var cursor = response.GetProperty("cursor").GetString();
                    if (transition == 12)
                    {
                        Assert.That(cursor, Is.EqualTo("done")); Assert.That(status, Is.AnyOf("ready", "completed"));
                    }
                    else
                    {
                        Assert.That(transition, Is.InRange(0, 11));
                        Assert.That(cursor, Is.EqualTo("work" + transition));
                        Assert.That(status, Is.AnyOf("ready", "executing"));
                    }
                }
                Assert.That(readerResponses.Any(r => r.GetProperty("status").GetString() == "executing"), Is.True);
                Assert.That(readerResponses.Any(r => r.GetProperty("status").GetString() == "completed"), Is.True); break;
            case "a process has written an external marker and is waiting on a barrier before exiting": StartBarrier(); break;
            case "the engine process is forcibly terminated": Crash(); break;
            case "a fresh CLI process reads status": cli.Call("status", runId); break;
            case "the run is reported interrupted without modifying persisted state":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("interrupted"));
                Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot)); break;
            case "I advance that run": cli.Call("advance", runId); break;
            case "no additional process is started": AssertEffects(1); break;
            case "the CLI exits with code 5": Assert.That(cli.ExitCode, Is.EqualTo(5)); break;
            case "an interrupted process activation": StartBarrier(); Crash(); break;
            case "I resolve with retry": cli.Call("resolve", runId, "--retry"); break;
            case "the run is ready and no process has started":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("interrupted"));
                AssertEffects(1); break;
            case "I advance":
                File.WriteAllText(PathFor("effect.release"), "release");
                foreach (var helper in helpers) Assert.That(helper.WaitForExit(2000), Is.True);
                cli.ExpectedChildren = 1; cli.Call("advance", runId); cli.ExpectedChildren = 0; break;
            case "a new activation ID is used":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("id").GetString(), Is.Not.EqualTo(priorActivation));
                AssertEffects(2); break;
            case "the previous activation remains recorded as interrupted":
                using (var state = JsonDocument.Parse(File.ReadAllBytes(StateFile)))
                {
                    var activations = state.RootElement.GetProperty("activations");
                    Assert.That(activations.GetArrayLength(), Is.EqualTo(2));
                    Assert.That(activations[0].GetProperty("id").GetString(), Is.EqualTo(priorActivation));
                    Assert.That(activations[0].GetProperty("outcome").GetString(), Is.EqualTo("interrupted"));
                    Assert.That(activations[0].GetProperty("finishedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
                }
                break;
            case "I resolve with fail": cli.Call("resolve", runId, "--fail"); break;
            case "the run is failed and the CLI exits with code 5":
                Assert.That(cli.ExitCode, Is.EqualTo(5));
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed")); break;
            case "no child process is started": AssertEffects(1); break;
            case "a second CLI process attempts to advance the same run": cli.Call("advance", runId); break;
            case "it exits with code 4 and error \"RUN_LOCKED\"":
                Assert.That(cli.ExitCode, Is.EqualTo(4));
                Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("RUN_LOCKED")); break;
            case "the child process has started only once": AssertEffects(1); break;
            case "another run can advance independently":
                using (var other = new CliWorld())
                {
                    File.WriteAllText(other.FilePath, FeatureTests.Workflow);
                    other.Call("start", other.FilePath, "--workspace", other.DirectoryPath);
                    other.Call("advance", other.Response.GetProperty("runId").GetString()!);
                    Assert.That(other.ExitCode, Is.Zero);
                    Assert.That(other.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
                }
                Assert.That(writer!.HasExited, Is.False);
                cli.Call("status", runId);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("executing"));
                File.WriteAllText(PathFor("effect0.release"), "release");
                WaitFile("effect1.ready"); ObserveHelper("effect1.pid");
                foreach (var command in new[] { "advance", "resolve", "complete" })
                {
                    var args = command switch
                    {
                        "resolve" => new[] { command, runId, "--retry" },
                        "complete" => new[] { command, runId, "--request", "q_unknown", "--outcome", "finish" },
                        _ => new[] { command, runId }
                    };
                    cli.Call(args);
                    Assert.That(cli.ExitCode, Is.EqualTo(4));
                    Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("RUN_LOCKED"));
                }
                AssertEffects(2);
                File.WriteAllText(PathFor("effect1.release"), "release"); FinishWriter(0); break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private void CreateWorkflow(int count = 1, string? mode = null)
    {
        string Execution(string[] args) => inline
            ? "script: |\n      #!\"" + Helper + "\" " + string.Join(' ', args) + "\n      test-only stdin payload"
            : "command: " + JsonSerializer.Serialize(Helper) + "\n    args: " + JsonSerializer.Serialize(args);
        var nodes = string.Join("\n", Enumerable.Range(0, count).Select(i => $$"""
              work{{(count == 1 ? "" : i.ToString(System.Globalization.CultureInfo.InvariantCulture))}}:
                kind: process
                {{Execution(mode is null ? ["barrier", count == 1 ? "effect" : "effect" + i] : [mode])}}
                timeoutSeconds: 30
            """));
        var names = Enumerable.Range(0, count).Select(i => count == 1 ? "work" : "work" + i).ToArray();
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: recovery
            entry: {{names[0]}}
            nodes:
            {{nodes}}
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: recovery
            {{string.Join("\n", names.Select(n => n + "[\"Work\"]"))}}
            done["Done"]
            {{string.Join("\n", names.Select((n, i) => n + " -->|exit_0| " + (i + 1 < count ? names[i + 1] : "done")))}}
            ```
            """);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    private static string Binary => Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ??
        Path.Combine(FeatureTests.Root, "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");

    private Process Launch(string command)
    {
        var start = new ProcessStartInfo(Binary)
        { WorkingDirectory = cli.DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { command, runId, "--state-dir", cli.State, "--json" }) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    private void StartBarrier(int count = 1)
    {
        CreateWorkflow(count);
        processBaseline = ProcessObserver.TotalProcesses();
        writer = Launch("advance");
        output = writer.StandardOutput.ReadToEndAsync(); error = writer.StandardError.ReadToEndAsync();
        var name = count == 1 ? "effect" : "effect0";
        WaitFile(name + ".ready"); ObserveHelper(name + ".pid");
        if (processBaseline.HasValue) Assert.That(ProcessObserver.TotalProcesses() - processBaseline, Is.EqualTo(2));
        Assert.That(writer.HasExited, Is.False);
    }

    private void StartInterrupt()
    {
        CreateWorkflow(mode: "interrupt_parent");
        processBaseline = ProcessObserver.TotalProcesses();
        var existing = ProcessObserver.ActiveProcessIds();
        signalHost = WindowsInterrupt.Start(Helper, Binary, runId, cli.State, cli.DirectoryPath);
        WaitFile("parent.ready"); ObserveHelper("parent.pid"); ObserveHelper("child.pid");
        writer = Process.GetProcessById(int.Parse(File.ReadAllText(PathFor("engine.pid")), System.Globalization.CultureInfo.InvariantCulture));
        _ = writer.Handle;
        Assert.That(writer.HasExited, Is.False);
        var created = ProcessObserver.ActiveProcessIds().Except(existing).ToArray();
        Assert.That(created, Has.Length.EqualTo(OperatingSystem.IsWindows() ? 5 : 4));
        if (!OperatingSystem.IsWindows()) return;
        var consoleId = created.Except(helpers.Select(p => p.Id).Concat([writer.Id, signalHost.Id])).Single();
        using var console = Process.GetProcessById(consoleId);
        Assert.That(console.ProcessName, Is.EqualTo("conhost").IgnoreCase, "Extra OS console host belongs to the isolated harness.");
    }

    private void ReadConcurrently()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                using var reader = Launch("status");
                Interlocked.Increment(ref readCalls);
                var stdout = reader.StandardOutput.ReadToEndAsync(); var stderr = reader.StandardError.ReadToEndAsync();
                if (!reader.WaitForExit(10000)) { reader.Kill(true); Assert.Fail("Reader watchdog."); }
                Assert.That(reader.ExitCode, Is.Zero, stdout.Result + stderr.Result);
                var response = JsonDocument.Parse(stdout.Result).RootElement.Clone();
                lock (readerResponses) readerResponses.Add(response);
                if (response.GetProperty("status").GetString() == "completed") break;
            }
        })).ToArray();
        try
        {
            Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref readCalls) >= 4, 5000), Is.True);
            for (var i = 0; i < 12; i++)
            {
                WaitFile("effect" + i + ".ready");
                if (i > 0) ObserveHelper("effect" + i + ".pid");
                File.WriteAllText(PathFor("effect" + i + ".release"), "release");
            }
            FinishWriter(0);
            Assert.That(Task.WaitAll(readers, 12000), Is.True, "Concurrent readers watchdog.");
            if (processBaseline.HasValue) Assert.That(ProcessObserver.TotalProcesses() - processBaseline, Is.EqualTo(13 + readCalls));
        }
        finally { stop.Cancel(); Task.WaitAll(readers, 12000); }
    }

    private void WaitFile(string name)
    {
        var ready = SpinWait.SpinUntil(() => File.Exists(PathFor(name)) || name != "signal.exit" && writer?.HasExited == true, 10000);
        Assert.That(ready && File.Exists(PathFor(name)), Is.True,
            $"Timed out waiting for {name}. Writer exited: {writer?.HasExited}; " +
            (output?.IsCompleted == true ? output.Result : "") + (error?.IsCompleted == true ? error.Result : ""));
    }

    private void ObserveHelper(string name)
    {
        var process = Process.GetProcessById(int.Parse(File.ReadAllText(PathFor(name)), System.Globalization.CultureInfo.InvariantCulture));
        _ = process.Handle; helpers.Add(process);
        Assert.That(process.HasExited, Is.False);
    }

    private void FinishWriter(int expected)
    {
        Assert.That(writer!.WaitForExit(10000), Is.True, "Writer watchdog.");
        Assert.That(writer.ExitCode, Is.EqualTo(expected), error!.Result);
        cli.Response = JsonDocument.Parse(output!.Result).RootElement.Clone(); cli.ExitCode = writer.ExitCode;
    }

    private void Crash()
    {
        cli.Call("status", runId);
        priorActivation = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
        snapshot = File.ReadAllBytes(StateFile);
        writer!.Kill(); // Deliberately terminate just the engine after the external marker.
        Assert.That(writer.WaitForExit(2000), Is.True);
        Assert.That(helpers[0].HasExited, Is.False, "External effect helper outlives engine crash.");
    }

    private void AssertEffects(int count) => Assert.That(File.ReadAllLines(PathFor("effects.txt")), Has.Length.EqualTo(count));

    internal void AssertReadableResolution(bool retry)
    {
        StartBarrier(); Crash();
        cli.Raw("resolve", runId, retry ? "--retry" : "--fail", "--state-dir", cli.State);
        Assert.That(cli.ExitCode, Is.EqualTo(retry ? 0 : 5));
        Assert.That(cli.Stdout, Does.Contain("Run: " + runId).And.Contain("Status: " + (retry ? "ready" : "failed")));
        AssertEffects(1);
        cli.Call("status", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(retry ? "ready" : "failed"));
        Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("interrupted"));
        AssertEffects(1);
    }

    internal void RejectCorruptSnapshot(string defect)
    {
        CreateWorkflow();
        var state = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(StateFile))!;
        switch (defect)
        {
            case "executing-without-activation": state["status"] = "executing"; break;
            case "null-activation": state["activations"]!.AsArray().Add((System.Text.Json.Nodes.JsonNode?)null); break;
            case "unknown-cursor": state["cursor"] = "missing"; break;
            default: throw new AssertionException("Unknown corrupt fixture.");
        }
        snapshot = System.Text.Encoding.UTF8.GetBytes(state.ToJsonString()); File.WriteAllBytes(StateFile, snapshot);
        foreach (var command in new[] { "status", "advance", "resolve" })
        {
            cli.Call(command == "resolve" ? [command, runId, "--retry"] : [command, runId]);
            Assert.That(cli.ExitCode, Is.EqualTo(6));
            Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("CORRUPT_STATE"));
            Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot));
        }
        Assert.That(File.Exists(PathFor("effects.txt")), Is.False);
    }

    internal void InterruptWithReadOnlyState()
    {
        StartInterrupt();
        snapshot = File.ReadAllBytes(StateFile);
        var attributes = File.GetAttributes(StateFile);
        var stateDirectory = Path.GetDirectoryName(StateFile)!;
        var mode = OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(stateDirectory);
        try
        {
            File.SetAttributes(StateFile, attributes | FileAttributes.ReadOnly);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(stateDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            Execute("I send the platform's supported user-interrupt signal");
            Assert.That(cli.ExitCode, Is.EqualTo(130));
            var response = JsonDocument.Parse(File.ReadAllText(PathFor("signal.stdout"))).RootElement;
            Assert.That(response.GetProperty("status").GetString(), Is.EqualTo("interrupted"));
            Assert.That(response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("INTERRUPTED"));
            foreach (var helper in helpers) Assert.That(helper.WaitForExit(2000), Is.True);
            Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot));
            cli.Call("status", runId);
            Assert.That(cli.ExitCode, Is.Zero);
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("interrupted"));
            Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot));
        }
        finally
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(stateDirectory, mode);
            File.SetAttributes(StateFile, attributes);
        }
    }

    public void Dispose()
    {
        if (writer is not null && !writer.HasExited) { writer.Kill(true); writer.WaitForExit(2000); }
        if (signalHost is not null && !signalHost.HasExited) { signalHost.Kill(true); signalHost.WaitForExit(2000); }
        // A crashed writer cannot clean up its helper. Recover only this world's PID markers,
        // verify the image, then retain handles so PID reuse cannot target another process.
        foreach (var marker in Directory.GetFiles(cli.DirectoryPath, "*.pid"))
        {
            if (!int.TryParse(File.ReadAllText(marker), out var pid) || helpers.Any(p => p.Id == pid) || writer?.Id == pid) continue;
            var expectedImage = Path.GetFileName(marker) == "engine.pid" ? Binary : Helper;
            Process? process = null;
            try
            {
                process = Process.GetProcessById(pid); _ = process.Handle;
                if (!process.HasExited && string.Equals(process.MainModule?.FileName, expectedImage, StringComparison.OrdinalIgnoreCase))
                { helpers.Add(process); process = null; }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            finally { process?.Dispose(); }
        }
        foreach (var process in helpers)
        {
            if (!process.HasExited) { process.Kill(true); process.WaitForExit(2000); }
            process.Dispose();
        }
        writer?.Dispose(); signalHost?.Dispose();
        cli.Dispose();
    }
}
