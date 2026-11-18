using System.Diagnostics;
using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class StartAdvanceSafetyWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private Process? writer;
    private Task<string>? stdout, stderr;
    private string seed = "", created = "", failure = "", format = "json";
    private JsonElement response;
    private byte[] seedSnapshot = [];
    private bool verified;
    private string Effects => Path.Combine(cli.DirectoryPath, "effects.txt");
    private string State(string id) => Path.Combine(cli.State, id, "state.json");

    internal void Execute(string step)
    {
        const string creation = "combined start fails before creation because of ";
        const string advance = "a combined first advance encounters ";
        const string interrupt = "I interrupt a combined start in ";
        if (step.StartsWith(creation, StringComparison.Ordinal)) CreationFailure(step[creation.Length..]);
        else if (step == "no node executes and the existing ready run is unchanged")
        {
            Assert.That(File.Exists(Effects), Is.False);
            Assert.That(File.ReadAllBytes(State(seed)), Is.EqualTo(seedSnapshot));
            cli.Call("status", seed);
            Assert.That(cli.ExitCode, Is.Zero);
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        }
        else if (step.StartsWith(advance, StringComparison.Ordinal))
        {
            var parts = step[advance.Length..].Split(" in ", StringSplitOptions.None);
            failure = parts[0]; format = UsabilityAuditWorld.Complementary ? parts[1] == "json" ? "text" : "json" : parts[1]; AdvanceFailure();
        }
        else if (step == "the created run is recoverable without rollback or automatic replay") Recover();
        else if (step == "combined start runs behind a barrier while same and different workflows start") Concurrent();
        else if (step == "only the created run advances using its frozen input and workflow") Assert.That(verified, Is.True);
        else if (step.StartsWith(interrupt, StringComparison.Ordinal))
        {
            var parts = step[interrupt.Length..].Split(" and choose ", StringSplitOptions.None);
            using var recovery = new RecoveryWorld();
            recovery.AssertCombinedInterruption(parts[0] == "text", parts[1] == "retry");
            verified = true;
        }
        else if (step == "cancellation retains identity and requires explicit recovery without replay") Assert.That(verified, Is.True);
        else throw new AssertionException($"Undefined required combined-start step: {step}");
    }

    private void Fixture(bool barrier = false)
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: combined_safety
            entry: work
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: {{(barrier ? "[barrier, combined]" : "[effect, combined]")}}
                timeoutSeconds: 30
              review:
                kind: agent
                prompt: Frozen review
                context: [input]
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
                %% merstride: combined_safety
                work["Work"]
                review["Review"]
                done["Done"]
                work -->|exit_0| review
                review -->|approved| done
            ```
            """);
    }

    private void Seed()
    {
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        seed = cli.Response.GetProperty("runId").GetString()!;
        seedSnapshot = File.ReadAllBytes(State(seed));
    }

    private void CreationFailure(string defect)
    {
        Fixture(); Seed();
        var args = new List<string> { "start", cli.FilePath, "--advance", "--workspace", cli.DirectoryPath };
        IDisposable? held = null;
        var expected = 6;
        try
        {
            switch (defect)
            {
                case "malformed workflow": File.WriteAllText(cli.FilePath, "not a workflow"); expected = 3; break;
                case "invalid input":
                    var input = Path.Combine(cli.DirectoryPath, "input.json"); File.WriteAllText(input, "[]");
                    args.AddRange(["--input-file", input]); expected = 4; break;
                case "missing workspace": args[^1] = Path.Combine(cli.DirectoryPath, "absent"); expected = 2; break;
                case "preparation conflict":
                    Directory.CreateDirectory(Path.Combine(cli.DirectoryPath, ".merstride", ".gitignore"));
                    // Default-root preparation must fail before creating a run; the explicit seed remains intact.
                    break;
                case "initial persistence conflict": File.WriteAllText(Path.Combine(cli.State, "r_000002"), "preserve this file"); break;
                case "context lock": held = new FileStream(Path.Combine(cli.State, "context.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); expected = 4; break;
                case "corrupt context": File.WriteAllText(Path.Combine(cli.State, "context.json"), "{"); break;
                case "duplicate flag": args.Add("--advance"); expected = 2; break;
                case "false flag": args[2] = "--advance=false"; expected = 2; break;
                default: throw new AssertionException("Unknown creation failure.");
            }
            if (defect == "preparation conflict") cli.Raw(args.Concat(["--json"]).ToArray());
            else cli.Call(args.ToArray());
            Assert.That(cli.ExitCode, Is.EqualTo(expected), cli.Stdout + cli.Stderr);
            if (UsabilityAuditWorld.Complementary)
            {
                Assert.That(UsabilityAuditWorld.LastText, Does.Contain("Error "));
                Assert.That(Directory.GetDirectories(cli.State, "r_*").Length, Is.EqualTo(1));
                return;
            }
            response = defect == "preparation conflict" ? JsonDocument.Parse(cli.Stdout).RootElement.Clone() : cli.Response;
            Assert.That(response.GetProperty("runId").ValueKind, Is.EqualTo(JsonValueKind.Null));
            if (response.GetProperty("start").ValueKind != JsonValueKind.Null)
            {
                Assert.That(response.GetProperty("start").GetProperty("created").GetBoolean(), Is.False);
                Assert.That(response.GetProperty("start").GetProperty("advance").GetString(), Is.EqualTo("not_started"));
            }
            Assert.That(Directory.GetDirectories(cli.State, "r_*").Length, Is.EqualTo(1));
            Assert.That(cli.Stderr, Is.Empty);
        }
        finally { held?.Dispose(); }
    }

    private void Launch()
    {
        var binary = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ?? Path.Combine(FeatureTests.Root, "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
        var start = new ProcessStartInfo(binary) { WorkingDirectory = cli.DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "start", cli.FilePath, "--advance", "--details", "--workspace", cli.DirectoryPath, "--state-dir", cli.State,
            "--input-file", Path.Combine(cli.DirectoryPath, "input.json") }) start.ArgumentList.Add(arg);
        if (format == "json") start.ArgumentList.Add("--json");
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "input.json"), "{\"frozen\":true}");
        writer = Process.Start(start)!;
        stdout = writer.StandardOutput.ReadToEndAsync(); stderr = writer.StandardError.ReadToEndAsync();
        Assert.That(SpinWait.SpinUntil(() => File.Exists(Path.Combine(cli.DirectoryPath, "combined.ready")) || writer.HasExited, 10000), Is.True);
        Assert.That(writer.HasExited, Is.False, stdout.IsCompleted ? stdout.Result + stderr.Result : "Barrier did not start.");
        cli.Call("runs");
        created = cli.Response.GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("runId").GetString() != seed).GetProperty("runId").GetString()!;
        cli.Call("status", created);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("executing"));
        cli.Call("advance", created);
        Assert.That(cli.ExitCode, Is.EqualTo(4));
        Assert.That(cli.ErrorCode, Is.EqualTo("RUN_LOCKED"));
    }

    private void Finish(int expected)
    {
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "combined.release"), "release");
        Assert.That(writer!.WaitForExit(10000), Is.True, "Combined writer watchdog.");
        Assert.That(writer.ExitCode, Is.EqualTo(expected), stdout!.Result + stderr!.Result);
        FeatureTests.RecordOperation("start-advance", format == "json", writer.ExitCode, variant: "controlled-writer");
        if (format == "json")
        {
            response = JsonDocument.Parse(stdout.Result).RootElement.Clone();
            Assert.That(response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
            Assert.That(response.GetProperty("runId").GetString(), Is.EqualTo(created));
            Assert.That(response.GetProperty("start").GetProperty("created").GetBoolean(), Is.True);
            Assert.That(response.GetProperty("start").GetProperty("phase").GetString(), Is.EqualTo("advance"));
            Assert.That(stderr.Result, Is.Empty);
        }
        else Assert.That(stdout!.Result, Does.Contain("Run: " + created).And.Contain("Creation: saved new run")
            .And.Contain(expected == 0 ? "Initial advance: finished" : "Initial advance: stopped by an error"));
        Assert.That(File.ReadAllLines(Effects), Is.EqualTo(new[] { "combined" }));
        Assert.That(File.ReadAllBytes(State(seed)), Is.EqualTo(seedSnapshot));
    }

    private void AdvanceFailure()
    {
        Fixture(true); Seed(); Launch();
        using IDisposable held = failure == "request lock"
            ? new FileStream(Path.Combine(cli.State, ".identities", "allocator.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            : new RejectedStateSave(State(created));
        Finish(failure == "request lock" ? 4 : 6);
        if (format == "json")
        {
            Assert.That(response.GetProperty("start").GetProperty("advance").GetString(), Is.EqualTo("error"));
            Assert.That(response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(failure == "request lock" ? "IDENTITY_LOCKED" : "IO_ERROR"));
            Assert.That(response.GetProperty("status").GetString(), Is.EqualTo(failure == "request lock" ? "ready" : "interrupted"));
        }
        else if (failure == "request lock")
            Assert.That(stderr!.Result, Does.Contain("IDENTITY_LOCKED").And.Contain("Flow #1: work -> review").And.Not.Contain("overall result: success"));
        else Assert.That(stderr!.Result, Does.Contain("activating").And.Contain("IO_ERROR").And.Not.Contain("saved exit").And.Not.Contain("Flow #").And.Not.Contain("overall result: success"));
    }

    private void Recover()
    {
        cli.Call("status", created);
        if (failure == "request lock")
        {
            Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("review"));
            Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(1));
            cli.Call("advance", created);
            Assert.That(cli.ExitCode, Is.Zero);
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
        }
        else
        {
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("interrupted"));
            cli.Call("advance", created);
            Assert.That(cli.ExitCode, Is.EqualTo(5));
            cli.Call("resolve", created, "--fail");
            Assert.That(cli.ExitCode, Is.EqualTo(5));
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed"));
        }
        Assert.That(File.ReadAllLines(Effects), Is.EqualTo(new[] { "combined" }));
    }

    private void Concurrent()
    {
        if (UsabilityAuditWorld.Complementary) format = "text";
        Fixture(true); Seed(); Launch();
        SeedNew(out var same);
        var other = Path.Combine(cli.DirectoryPath, "other.md"); File.WriteAllText(other, FeatureTests.Workflow);
        cli.Call("start", other, "--advance");
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        var otherId = cli.Response.GetProperty("runId").GetString()!;
        File.WriteAllText(cli.FilePath, "source replaced while new run executes");
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "input.json"), "{\"frozen\":false}");
        Finish(0);
        if (UsabilityAuditWorld.Complementary) { cli.Call("status", created); response = cli.Response; }
        Assert.That(response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
        Assert.That(response.GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo("Frozen review"));
        Assert.That(response.GetProperty("request").GetProperty("context")[0].GetProperty("value").GetProperty("frozen").GetBoolean(), Is.True);
        cli.Call("status"); Assert.That(cli.Response.GetProperty("runId").GetString(), Is.EqualTo(otherId));
        cli.Call("status", same); Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        cli.Call("runs"); Assert.That(cli.Response.GetProperty("runs").GetArrayLength(), Is.EqualTo(4));
        verified = true;
    }

    private void SeedNew(out string id)
    {
        cli.Call("start", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        id = cli.Response.GetProperty("runId").GetString()!;
    }

    public void Dispose()
    {
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "combined.release"), "release");
        if (writer is not null && !writer.HasExited && !writer.WaitForExit(2000)) { writer.Kill(true); writer.WaitForExit(2000); }
        writer?.Dispose(); cli.Dispose();
    }
}
