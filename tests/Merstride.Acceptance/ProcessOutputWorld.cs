using System.Diagnostics;
using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ProcessOutputWorld : IDisposable
{
    private static readonly string[] Streams = ["stdout", "stderr"];
    private static readonly string[] Markers = ["tool standard output", "tool standard error"];
    private readonly CliWorld cli = new();
    private int exit;
    private string runId = "";
    private string[] retained = [];
    private bool verified;

    internal void Execute(string step)
    {
        const string given = "a controlled process that exits with ";
        const string save = "I advance saving ";
        if (step.StartsWith(given, StringComparison.Ordinal))
        {
            exit = int.Parse(step[given.Length..], System.Globalization.CultureInfo.InvariantCulture);
            WriteWorkflow();
        }
        else if (step == "I start and advance with default process output") Run(combined: true);
        else if (step.StartsWith(save, StringComparison.Ordinal))
        {
            retained = step[save.Length..].Split(" and ");
            cli.Raw("start", cli.FilePath, "--state-dir", cli.State, "--json");
            Assert.That(cli.ExitCode, Is.Zero);
            runId = cli.Response.GetProperty("runId").GetString()!;
            Run(combined: false);
        }
        else if (step == "I execute a process that fills the redirected stdout pipe") StalledPipe();
        else if (step == "both streams are available in this invocation without log files" ||
                 step == "blocked delivery does not prevent the saved timeout route" ||
                 step == $"only {string.Join(" and ", retained)} can be read with logs")
            Assert.That(verified, Is.True);
        else throw new AssertionException($"Undefined required step: {step}");
    }

    private void WriteWorkflow() => File.WriteAllText(cli.FilePath, $$"""
        ```yaml merstride
        schemaVersion: 1
        id: output
        entry: work
        nodes:
          work:
            kind: process
            command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
            args: [output_barrier, output, '{{exit}}']
            rtk: false
            timeoutSeconds: 20
          done:
            kind: end
            result: success
        ```
        ```mermaid
        flowchart TD
            %% merstride: output
            work["Emit output"]
            done["Done"]
            work -->|exit_0| done
        ```
        """);

    private void Run(bool combined)
    {
        var json = UsabilityAuditWorld.Complementary;
        var start = RedirectedStart();
        string[] command = combined ? ["start", cli.FilePath, "--advance"] : ["advance", runId];
        foreach (var arg in command.Concat(["--state-dir", cli.State]).Concat(retained.Select(s => "--save-" + s)))
            start.ArgumentList.Add(arg);
        if (json) start.ArgumentList.Add("--json");
        var count = ProcessObserver.TotalProcesses();
        using var writer = Process.Start(start)!;
        var seen = Streams.Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var stdout = Read(writer.StandardOutput, Markers[0], seen[0]);
        var stderr = Read(writer.StandardError, Markers[1], seen[1]);
        try
        {
            Assert.That(SpinWait.SpinUntil(() => File.Exists(Path.Combine(cli.DirectoryPath, "output.ready")), 5000), Is.True);
            if (!json)
                for (var i = 0; i < Streams.Length; i++)
                    if (!retained.Contains(Streams[i]))
                        Assert.That(seen[i].Task.Wait(2000), Is.True, $"Expected {Streams[i]} before releasing the process.");
            Assert.That(writer.HasExited, Is.False);
        }
        finally
        {
            File.WriteAllText(Path.Combine(cli.DirectoryPath, "output.release"), "release");
            if (!writer.WaitForExit(10000)) writer.Kill(true);
        }
        Assert.That(writer.ExitCode, Is.EqualTo(exit == 0 ? 0 : 5));
        if (count.HasValue) Assert.That(ProcessObserver.TotalProcesses() - count, Is.EqualTo(2));
        FeatureTests.RecordOperation(combined ? "start-advance" : "advance", json, writer.ExitCode);
        if (json)
        {
            Assert.That(stderr.Result, Is.Empty);
            Assert.That(stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
            using var response = JsonDocument.Parse(stdout.Result);
            var root = response.RootElement;
            Assert.That(root.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
            var processOutput = root.GetProperty("processOutput");
            Assert.That(processOutput.GetArrayLength(), Is.EqualTo(1));
            Assert.That(processOutput[0].GetProperty("activationId").GetString(), Is.EqualTo(root.GetProperty("lastActivation").GetProperty("id").GetString()));
            for (var i = 0; i < Streams.Length; i++)
                Assert.That(processOutput[0].GetProperty(Streams[i]).GetProperty("text").GetString()!.TrimEnd(), Is.EqualTo(Markers[i]));
        }
        else
        {
            var results = new[] { stdout.Result, stderr.Result };
            for (var i = 0; i < Streams.Length; i++)
            {
                Assert.That(results[i].Contains(Markers[i], StringComparison.Ordinal), Is.EqualTo(!retained.Contains(Streams[i])));
                Assert.That(results[i], Does.Not.Contain(Markers[1 - i]));
            }
        }

        cli.Raw("status", "--state-dir", cli.State);
        cli.Raw("status", "--state-dir", cli.State, "--json");
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(exit == 0 ? "completed" : "failed"));
        runId = cli.Response.GetProperty("runId").GetString()!;
        var activation = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
        var directory = Path.Combine(cli.State, runId, "logs");
        Assert.That(Directory.Exists(directory) ? Directory.GetFiles(directory).Length : 0, Is.EqualTo(retained.Length));
        for (var i = 0; i < Streams.Length; i++)
        {
            var stream = Streams[i];
            cli.Raw("logs", runId, "--activation", activation, "--stream", stream, "--state-dir", cli.State, "--json");
            if (retained.Contains(stream))
            {
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("log").GetString()!.TrimEnd(), Is.EqualTo(Markers[i]));
            }
            else
            {
                Assert.That(cli.ExitCode, Is.EqualTo(4));
                Assert.That(cli.ErrorCode, Is.EqualTo("LOG_NOT_RETAINED"));
            }
        }
        verified = true;
    }

    private void StalledPipe()
    {
        WriteWorkflow();
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath)
            .Replace("output_barrier, output, '0'", "concurrent_output, '1048576'", StringComparison.Ordinal)
            .Replace("timeoutSeconds: 20", "timeoutSeconds: 2", StringComparison.Ordinal)
            .Replace("work -->|exit_0| done", "work -->|exit_0| done\n    work -->|timeout| done", StringComparison.Ordinal));
        var json = UsabilityAuditWorld.Complementary;
        var start = RedirectedStart();
        foreach (var arg in new[] { "start", cli.FilePath, "--advance", "--state-dir", cli.State })
            start.ArgumentList.Add(arg);
        if (json) start.ArgumentList.Add("--json");
        var count = ProcessObserver.TotalProcesses();
        using var writer = Process.Start(start)!;
        var stderr = writer.StandardError.ReadToEndAsync();
        var stdout = json ? writer.StandardOutput.ReadToEndAsync() : null;
        if (!writer.WaitForExit(9000))
        {
            writer.Kill(true);
            Assert.Fail("An unread stdout pipe prevented the process timeout from routing.");
        }
        Assert.That(writer.ExitCode, Is.Zero);
        if (count.HasValue) Assert.That(ProcessObserver.TotalProcesses() - count, Is.EqualTo(2));
        FeatureTests.RecordOperation("start-advance", json, writer.ExitCode, variant: "stalled-stdout");
        if (json)
        {
            Assert.That(stderr.Result, Is.Empty);
            using var response = JsonDocument.Parse(stdout!.Result);
            var preview = response.RootElement.GetProperty("processOutput")[0].GetProperty("stdout");
            Assert.That(preview.GetProperty("truncated").GetBoolean(), Is.True);
            Assert.That(preview.GetProperty("text").GetString(), Has.Length.EqualTo(4096));
        }
        else Assert.That(stderr.Result, Does.Contain("timeout"));

        cli.Raw("status", "--state-dir", cli.State);
        cli.Raw("status", "--state-dir", cli.State, "--json");
        Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo(json ? "exit_0" : "timeout"));
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
        verified = true;
    }

    private ProcessStartInfo RedirectedStart()
    {
        var binary = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ??
            Path.Combine(FeatureTests.Root, "artifacts/native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
        return new(binary) { WorkingDirectory = cli.DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true };
    }

    private static async Task<string> Read(StreamReader reader, string marker, TaskCompletionSource seen)
    {
        var result = new System.Text.StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line == marker) seen.TrySetResult();
            result.AppendLine(line);
        }
        return result.ToString();
    }

    public void Dispose() => cli.Dispose();
}
