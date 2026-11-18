using System.Diagnostics;
using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed partial class ContextWorld
{
    private void ContextCase(string kind)
    {
        if (kind is "positional-workflow" or "positional-multiple-pairs" or "positional-other-commands" or "id-shaped-source" or "literal-delimiter" or "selector-conflicts")
        { PositionalCase(kind); return; }
        if (kind == "simultaneous-writer") { ConcurrentContext(); return; }
        if (kind == "multiple-pairs")
        {
            File.WriteAllText(cli.FilePath, AgentWorkflow + "\n" + AgentWorkflow.Replace("handoff", "second"));
            first = Json("start", cli.FilePath, "--workflow", "handoff").GetProperty("runId").GetString()!;
            other = Json("start", cli.FilePath, "--workflow", "second").GetProperty("runId").GetString()!;
            cli.Raw("status", "--file", cli.FilePath, "--json"); Error("AMBIGUOUS_WORKFLOW", 4);
            Assert.That(Json("status", "--file", cli.FilePath, "--workflow", "handoff").GetProperty("runId").GetString(), Is.EqualTo(first));
            Assert.That(Json("status", "--file", cli.FilePath, "--workflow", "second").GetProperty("runId").GetString(), Is.EqualTo(other));
            cli.Raw("status", first, "--file", cli.FilePath, "--json"); Error("USAGE", 2);
            cli.Raw("status", "--workflow", "handoff", "--json"); Error("USAGE", 2);
            return;
        }
        if (kind == "explicit-roots")
        {
            first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
            var external = Path.Combine(cli.DirectoryPath, "external storage");
            other = Json("start", OtherFile, "--state-dir", external).GetProperty("runId").GetString()!;
            Assert.That(Json("status", "--state-dir", external).GetProperty("sourcePath").GetString(), Is.EqualTo(OtherFile));
            Assert.That(Json("status").GetProperty("sourcePath").GetString(), Is.EqualTo(cli.FilePath));
            Json("discard", other, "--state-dir", external);
            Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(first));
            return;
        }
        first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
        var context = Path.Combine(Root, "context.json");
        var statePath = Path.Combine(Root, first, "state.json");
        if (kind == "renamed-source")
        {
            var renamed = Path.Combine(cli.DirectoryPath, "renamed.md");
            File.Move(cli.FilePath, renamed);
            cli.Raw("status", "--file", renamed, "--json"); Error("STALE_CONTEXT", 4);
            Assert.That(Json("advance", first).GetProperty("request").GetProperty("nodeId").GetString(), Is.EqualTo("review"));
            return;
        }
        if (kind == "legacy-state")
        {
            var oldId = "r_" + new string('a', 32);
            var oldState = File.ReadAllText(statePath).Replace(first, oldId);
            using (var parsed = JsonDocument.Parse(oldState))
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(parsed.RootElement.GetRawText())!;
                node.AsObject().Remove("sourcePath");
                oldState = node.ToJsonString();
            }
            Directory.CreateDirectory(Path.Combine(Root, oldId));
            File.WriteAllText(Path.Combine(Root, oldId, "state.json"), oldState);
            Json("discard", first);
            File.Delete(context); // A pre-P19 root has no selection metadata.
            Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(oldId));
            Assert.That(Json("status", oldId[..10]).GetProperty("runId").GetString(), Is.EqualTo(oldId));
            Json("use", oldId);
            Json("advance");
            Assert.That(Json("runs").GetProperty("runs")[0].GetProperty("sourcePath").ValueKind, Is.EqualTo(JsonValueKind.Null));
            return;
        }
        if (kind == "extended-identities")
        {
            File.WriteAllText(Path.Combine(Root, ".identities", "sequence"), "16777215");
            var extended = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
            Assert.That(extended, Is.EqualTo("r_1000000"));
            Assert.That(Json("status", extended).GetProperty("runId").GetString(), Is.EqualTo(extended));
            return;
        }
        if (kind == "terminal-current")
        {
            File.WriteAllText(OtherFile, FeatureTests.Workflow.Replace("result: success", "result: failure"));
            other = Json("start", OtherFile).GetProperty("runId").GetString()!;
            cli.Raw("advance", "--json");
            Assert.That(cli.ExitCode, Is.EqualTo(5));
            Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(other));
            Assert.That(Json("status").GetProperty("status").GetString(), Is.EqualTo("failed"));
            Assert.That(Json("status", first).GetProperty("status").GetString(), Is.EqualTo("ready"));
            return;
        }
        if (kind == "unreadable-request-search")
        {
            var request = Json("advance").GetProperty("request").GetProperty("id").GetString()!;
            other = Json("start", OtherFile).GetProperty("runId").GetString()!;
            File.WriteAllText(Path.Combine(Root, other, "state.json"), "{corrupt");
            cli.Raw("complete", "--request", request, "--outcome", "approved", "--json"); Error("CORRUPT_STATE", 6);
            Assert.That(Json("status", first).GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
            Json("complete", first, "--request", request, "--outcome", "approved");
            return;
        }
        var bytes = File.ReadAllText(statePath);
        if (kind == "context-lock")
        {
            using var held = new FileStream(Path.Combine(Root, "context.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            cli.Raw("start", OtherFile, "--json"); Error("CONTEXT_LOCKED", 4);
            cli.Raw("use", first, "--json"); Error("CONTEXT_LOCKED", 4);
            Assert.That(Directory.GetDirectories(Root).Count(p => Path.GetFileName(p).StartsWith("r_", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(File.ReadAllText(statePath), Is.EqualTo(bytes));
            // Explicit operations on another run do not need to switch context.
            Json("advance", first);
            return;
        }
        if (kind == "identity-lock")
        {
            using var held = new FileStream(Path.Combine(Root, ".identities", "allocator.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            cli.Raw("advance", first, "--json"); Error("IDENTITY_LOCKED", 4);
            Assert.That(File.ReadAllText(statePath), Is.EqualTo(bytes));
            return;
        }
        other = Json("start", OtherFile).GetProperty("runId").GetString()!;
        switch (kind)
        {
            case "stale-current": File.WriteAllText(context, File.ReadAllText(context).Replace(other, "r_ffffff")); break;
            case "corrupt-context": File.WriteAllText(context, "{corrupt"); break;
            case "null-context-source":
                var malformed = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(context))!;
                malformed["workflows"]![0]!["sourcePath"] = null;
                File.WriteAllText(context, malformed.ToJsonString());
                break;
            case "duplicate-context":
                var duplicate = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(context))!;
                duplicate["workflows"]!.AsArray().Add(duplicate["workflows"]![0]!.DeepClone());
                File.WriteAllText(context, duplicate.ToJsonString());
                break;
            case "missing-selected-snapshot": File.Delete(Path.Combine(Root, other, "state.json")); break;
            case "context-path-conflict": File.Delete(context); Directory.CreateDirectory(context); break;
            case "missing-context": File.Delete(context); break;
            default: throw new AssertionException("Unknown context case: " + kind);
        }
        cli.Raw("advance", "--json");
        Error(kind switch { "corrupt-context" or "null-context-source" or "duplicate-context" => "CORRUPT_CONTEXT", "context-path-conflict" or "missing-selected-snapshot" => "IO_ERROR", "missing-context" => "AMBIGUOUS_RUN", _ => "STALE_CONTEXT" }, kind is "corrupt-context" or "null-context-source" or "duplicate-context" or "context-path-conflict" or "missing-selected-snapshot" ? 6 : 4);
        Assert.That(File.ReadAllText(statePath), Is.EqualTo(bytes));
        Assert.That(Json("status", first).GetProperty("runId").GetString(), Is.EqualTo(first));
        if (kind == "context-path-conflict") Directory.Delete(context);
        if (kind is "null-context-source" or "duplicate-context")
        {
            var inventory = Json("runs");
            Assert.That(inventory.GetProperty("runs").GetArrayLength(), Is.EqualTo(2));
            Assert.That(inventory.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("CORRUPT_CONTEXT"));
        }
        Json("use", first);
        Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(first));
    }

    private void DiscardCase(string kind)
    {
        first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
        other = Json("start", OtherFile).GetProperty("runId").GetString()!;
        var directory = Path.Combine(Root, first);
        var statePath = Path.Combine(directory, "state.json");
        var protectedPath = Path.Combine(Root, other, "state.json");
        var protectedBytes = File.ReadAllBytes(protectedPath);
        if (kind == "writer-lock")
        {
            using var held = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            cli.Raw("discard", first, "--json"); Error("RUN_LOCKED", 4);
        }
        else if (kind == "staged-failure")
        {
            File.WriteAllText(Path.Combine(Root, ".trash"), "preserve conflicting content");
            cli.Raw("discard", first, "--json"); Error("DISCARD_INCOMPLETE", 6);
            Assert.That(File.ReadAllText(Path.Combine(Root, ".trash")), Is.EqualTo("preserve conflicting content"));
            cli.Raw("advance", first, "--json"); Error("DISCARD_PENDING", 4);
            var pendingInventory = Json("runs");
            Assert.That(pendingInventory.GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("runId").GetString() == first).GetProperty("status").GetString(), Is.EqualTo("discarding"));
            Assert.That(pendingInventory.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("DISCARD_STORAGE_CONFLICT"));
            File.Delete(Path.Combine(Root, ".trash"));
            Json("discard", first);
            Assert.That(Directory.Exists(directory), Is.False);
        }
        else if (kind == "staged-retry")
        {
            Directory.CreateDirectory(Path.Combine(Root, ".trash"));
            File.WriteAllText(Path.Combine(directory, "discard.pending"), "");
            Directory.Move(directory, Path.Combine(Root, ".trash", first));
            Assert.That(Json("runs").GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("runId").GetString() == first).GetProperty("discarding").GetBoolean(), Is.True);
            Json("discard", first);
            Assert.That(Directory.Exists(Path.Combine(Root, ".trash", first)), Is.False);
        }
        else if (kind is "empty-staged-retry" or "receipt-only-retry" or "unowned-empty-stage" or "conflicting-staged-file")
        {
            // Public storage fixtures represent interruption immediately after removing
            // the last state file, or the empty directory, before journal cleanup.
            Directory.CreateDirectory(Path.Combine(Root, ".trash"));
            var stagedPath = Path.Combine(Root, ".trash", first);
            File.Delete(statePath); File.Delete(Path.Combine(directory, "writer.lock")); Directory.Delete(directory);
            if (kind is not ("receipt-only-retry" or "conflicting-staged-file")) Directory.CreateDirectory(stagedPath);
            if (kind == "unowned-empty-stage")
            {
                cli.Raw("discard", first, "--json"); Error("IO_ERROR", 6);
                Assert.That(Directory.Exists(stagedPath), Is.True);
            }
            else
            {
                File.WriteAllText(Path.Combine(Root, ".trash", first + ".discard.json"), $$"""{"runId":"{{first}}","sourcePath":{{AcceptanceJson.Serialize(cli.FilePath)}},"workflowId":"handoff"}""");
                if (kind == "conflicting-staged-file")
                {
                    File.WriteAllText(stagedPath, "preserve unknown file");
                    cli.Raw("discard", first, "--json"); Error("DISCARD_REFUSED", 6);
                    Assert.That(File.ReadAllText(stagedPath), Is.EqualTo("preserve unknown file"));
                    Assert.That(File.Exists(Path.Combine(Root, ".trash", first + ".discard.json")), Is.True);
                    Assert.That(File.ReadAllBytes(protectedPath), Is.EqualTo(protectedBytes));
                    return;
                }
                Assert.That(Json("runs").GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("runId").GetString() == first).GetProperty("status").GetString(), Is.EqualTo("discarding"));
                Json("discard", first);
                Assert.That(Directory.Exists(stagedPath), Is.False);
                Assert.That(File.Exists(Path.Combine(Root, ".trash", first + ".discard.json")), Is.False);
            }
        }
        else if (kind == "process-logs")
        {
            File.WriteAllText(cli.FilePath, ProcessWorkflow);
            current = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
            cli.ExpectedChildren = 1; Json("advance", "--save-stdout", "--save-stderr"); cli.ExpectedChildren = 0;
            Assert.That(Directory.GetFiles(Path.Combine(Root, current, "logs")).Length, Is.EqualTo(2));
            Json("discard", current);
            Assert.That(Directory.Exists(Path.Combine(Root, current)), Is.False);
        }
        else
        {
            var link = Path.Combine(directory, "logs");
            var outside = Path.Combine(cli.DirectoryPath, "protected logs");
            if (kind == "corrupt-state") File.WriteAllText(statePath, "{corrupt");
            if (kind == "unknown-file") File.WriteAllText(Path.Combine(directory, "valuable.txt"), "retain me");
            if (kind == "unknown-directory") Directory.CreateDirectory(Path.Combine(directory, "valuable"));
            if (kind == "read-only-state") File.SetAttributes(statePath, File.GetAttributes(statePath) | FileAttributes.ReadOnly);
            if (kind == "linked-logs")
            {
                Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "valuable.txt"), "retain linked content");
                if (OperatingSystem.IsWindows())
                {
                    var start = new ProcessStartInfo("cmd.exe") { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var arg in new[] { "/c", "mklink", "/J", link, outside }) start.ArgumentList.Add(arg);
                    using var process = Process.Start(start)!;
                    Assert.That(process.WaitForExit(5000), Is.True); Assert.That(process.ExitCode, Is.Zero);
                }
                else Directory.CreateSymbolicLink(link, outside);
            }
            var before = File.ReadAllBytes(statePath);
            try
            {
                cli.Raw("discard", first, "--json");
                Error(kind == "corrupt-state" ? "CORRUPT_STATE" : kind == "read-only-state" ? "DISCARD_INCOMPLETE" : "DISCARD_REFUSED", 6);
                Assert.That(File.ReadAllBytes(statePath), Is.EqualTo(before));
                if (kind == "linked-logs") Assert.That(File.ReadAllText(Path.Combine(outside, "valuable.txt")), Is.EqualTo("retain linked content"));
            }
            finally
            {
                if (kind == "read-only-state") File.SetAttributes(statePath, File.GetAttributes(statePath) & ~FileAttributes.ReadOnly);
                if (kind == "linked-logs") Directory.Delete(link);
            }
        }
        Assert.That(File.ReadAllBytes(protectedPath), Is.EqualTo(protectedBytes));
        Assert.That(Json("status", other).GetProperty("status").GetString(), Is.EqualTo("ready"));
    }

    private static string ProcessWorkflow => $$"""
        ```yaml merstride
        schemaVersion: 1
        id: logs
        entry: work
        nodes:
          work:
            kind: process
            command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
            args: [streams]
            timeoutSeconds: 5
          done:
            kind: end
            result: success
        ```
        ```mermaid
        flowchart TD
        %% merstride: logs
        work["Work"]
        done["Done"]
        work -->|exit_0| done
        ```
        """;

    private void ConcurrentContext()
    {
        File.WriteAllText(cli.FilePath, ProcessWorkflow.Replace("args: [streams]", "args: [barrier, context]").Replace("timeoutSeconds: 5", "timeoutSeconds: 30"));
        first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
        var binary = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ?? Path.Combine(FeatureTests.Root, "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
        var baseline = ProcessObserver.TotalProcesses();
        var start = new ProcessStartInfo(binary) { WorkingDirectory = cli.DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("advance");
        if (!UsabilityAuditWorld.Complementary) start.ArgumentList.Add("--json");
        using var writer = Process.Start(start)!;
        var output = writer.StandardOutput.ReadToEndAsync(); var error = writer.StandardError.ReadToEndAsync();
        try
        {
            Assert.That(SpinWait.SpinUntil(() => File.Exists(Path.Combine(cli.DirectoryPath, "context.ready")) || writer.HasExited, 10000), Is.True);
            Assert.That(writer.HasExited, Is.False, output.IsCompleted ? output.Result + error.Result : "Writer stopped before barrier.");
            if (baseline.HasValue) Assert.That(ProcessObserver.TotalProcesses() - baseline, Is.EqualTo(2));
            other = Json("start", OtherFile).GetProperty("runId").GetString()!;
            Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(other));
            cli.Raw("discard", first, "--json"); Error("RUN_LOCKED", 4);
            Assert.That(Json("status", first).GetProperty("status").GetString(), Is.EqualTo("executing"));
            File.WriteAllText(Path.Combine(cli.DirectoryPath, "context.release"), "release");
            Assert.That(writer.WaitForExit(10000), Is.True, "Native writer watchdog.");
            Assert.That(writer.ExitCode, Is.Zero, error.Result);
            FeatureTests.RecordOperation("advance", !UsabilityAuditWorld.Complementary, writer.ExitCode, variant: "selection-race");
            if (UsabilityAuditWorld.Complementary)
                Assert.That(output.Result.Trim(), Is.EqualTo($"Finished successfully (1 step, run {first})."));
            else
            {
                var result = JsonDocument.Parse(output.Result).RootElement;
                Assert.That(result.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
                Assert.That(result.GetProperty("runId").GetString(), Is.EqualTo(first));
                Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("completed"));
                Assert.That(result.GetProperty("transitionCount").GetInt32(), Is.EqualTo(1));
                Assert.That(output.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
                Assert.That(error.Result, Is.Empty);
            }
            Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(other));
            Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("context\n"));
            Json("discard", first);
        }
        finally
        {
            File.WriteAllText(Path.Combine(cli.DirectoryPath, "context.release"), "release");
            if (!writer.WaitForExit(2000)) { writer.Kill(true); writer.WaitForExit(2000); }
        }
    }

    private void PositionalCase(string kind)
    {
        if (kind == "literal-delimiter")
        {
            File.WriteAllText(Path.Combine(cli.DirectoryPath, "--workflow.md"), AgentWorkflow);
            cli.Raw("start", "--json", "--", "--workflow.md");
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            var literalRun = JsonDocument.Parse(cli.Stdout).RootElement.GetProperty("runId").GetString();
            cli.Raw("status", "--json", "--", "--workflow.md");
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            Assert.That(JsonDocument.Parse(cli.Stdout).RootElement.GetProperty("runId").GetString(), Is.EqualTo(literalRun));
            cli.Raw("status", "--", "--json");
            Assert.That(cli.ExitCode, Is.EqualTo(4));
            Assert.That(cli.Stderr, Does.Contain("STALE_CONTEXT"));
            return;
        }
        first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
        other = Json("start", OtherFile).GetProperty("runId").GetString()!;
        if (kind == "selector-conflicts")
        {
            foreach (var args in new[] { new[] { "status", cli.FilePath, "--file", OtherFile }, new[] { "advance", first, "--file", cli.FilePath }, new[] { "status", first, "--workflow", "handoff" }, new[] { "status", cli.FilePath, OtherFile }, new[] { "status", "" } })
            { cli.Raw([.. args, "--json"]); Error("USAGE", 2); }
            cli.Raw("status", cli.FilePath, "--file", OtherFile, "--help", "--json"); Error("USAGE", 2);
            cli.Raw("status", first, "--workflow", "handoff", "--help", "--json"); Error("USAGE", 2);
            cli.Raw("status", "r_malformed", "--json"); Error("INVALID_RUN_ID", 4);
            cli.Raw("status", "missing-workflow", "--json"); Error("STALE_CONTEXT", 4);
            cli.Raw("discard", cli.FilePath, "--json"); Error("INVALID_RUN_ID", 4);
            cli.Raw("use", cli.FilePath, "--json"); Error("INVALID_RUN_ID", 4);
            cli.Raw("status", cli.FilePath, "--workflow", "handoff", "--help");
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            Assert.That(Json("status", first).GetProperty("status").GetString(), Is.EqualTo("ready"));
            Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(other));
            return;
        }
        if (kind == "id-shaped-source")
        {
            var shapedPath = Path.Combine(cli.DirectoryPath, first);
            File.WriteAllText(shapedPath, AgentWorkflow);
            current = Json("start", first).GetProperty("runId").GetString()!;
            Assert.That(Json("status", first).GetProperty("sourcePath").GetString(), Is.EqualTo(cli.FilePath));
            Assert.That(Json("status", "./" + first).GetProperty("runId").GetString(), Is.EqualTo(current));
            Assert.That(Json("status", "--file", first).GetProperty("runId").GetString(), Is.EqualTo(current));
            File.Delete(shapedPath);
            Assert.That(Json("advance", "./" + first).GetProperty("request").GetProperty("nodeId").GetString(), Is.EqualTo("review"));
            return;
        }
        if (kind == "positional-multiple-pairs")
        {
            File.WriteAllText(cli.FilePath, AgentWorkflow + "\n" + AgentWorkflow.Replace("handoff", "second"));
            Json("start", cli.FilePath, "--workflow", "second");
            cli.Raw("status", cli.FilePath, "--json"); Error("AMBIGUOUS_WORKFLOW", 4);
            Assert.That(Json("status", cli.FilePath, "--workflow", "handoff").GetProperty("runId").GetString(), Is.EqualTo(first));
            var waiting = Json("advance", cli.FilePath, "--workflow", "handoff");
            var request = waiting.GetProperty("request").GetProperty("id").GetString()!;
            Json("complete", cli.FilePath, "--workflow", "handoff", "--request", request, "--outcome", "approved");
            Assert.That(Json("advance", cli.FilePath, "--workflow", "handoff").GetProperty("status").GetString(), Is.EqualTo("completed"));
            return;
        }
        if (kind == "positional-other-commands")
        {
            File.WriteAllText(cli.FilePath, ProcessWorkflow);
            current = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
            cli.ExpectedChildren = 1;
            var advanced = Json("advance", cli.FilePath, "--workflow", "logs", "--save-stdout"); cli.ExpectedChildren = 0;
            var activation = advanced.GetProperty("lastActivation").GetProperty("id").GetString()!;
            Assert.That(Json("logs", cli.FilePath, "--workflow", "logs", "--activation", activation, "--stream", "stdout").GetProperty("log").GetString(), Does.Contain("real stdout"));
            var statePath = Path.Combine(Root, current, "state.json");
            File.WriteAllText(statePath, File.ReadAllText(statePath).Replace("\"status\":\"completed\"", "\"status\":\"interrupted\""));
            Assert.That(Json("resolve", cli.FilePath, "--workflow", "logs", "--retry").GetProperty("status").GetString(), Is.EqualTo("ready"));
            return;
        }
        var child = Path.Combine(cli.DirectoryPath, "child directory with spaces");
        Directory.CreateDirectory(child); cli.WorkingDirectory = child;
        Assert.That(Json("runs", "../workflow.md").GetProperty("runs").GetArrayLength(), Is.EqualTo(1));
        Assert.That(Json("status", "../workflow.md").GetProperty("runId").GetString(), Is.EqualTo(first));
        var handoff = Json("advance", cli.FilePath);
        Assert.That(handoff.GetProperty("runId").GetString(), Is.EqualTo(first));
        File.Delete(cli.FilePath);
        Assert.That(Json("status", "../workflow.md").GetProperty("runId").GetString(), Is.EqualTo(first));
        Json("complete", "../workflow.md", "--request", handoff.GetProperty("request").GetProperty("id").GetString()!, "--outcome", "approved");
        Assert.That(Json("advance", "../workflow.md").GetProperty("status").GetString(), Is.EqualTo("completed"));
        Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(other));
        cli.WorkingDirectory = cli.DirectoryPath;
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "plain-workflow"), AgentWorkflow);
        current = Json("start", "plain-workflow").GetProperty("runId").GetString()!;
        Assert.That(Json("status", "plain-workflow").GetProperty("runId").GetString(), Is.EqualTo(current));
    }
}
