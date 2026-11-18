using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed partial class ContextWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string first = "", other = "", current = "";
    private bool verified;
    private string OtherFile => Path.Combine(cli.DirectoryPath, "other.md");
    private string Root => Path.Combine(cli.DirectoryPath, ".merstride", "runs");

    private JsonElement Json(params string[] args)
    {
        cli.Raw([.. args, "--json"]);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        return JsonDocument.Parse(cli.Stdout).RootElement.Clone();
    }

    internal void Execute(string step)
    {
        const string contextCase = "I exercise context ";
        if (step.StartsWith(contextCase, StringComparison.Ordinal)) { ContextCase(step[contextCase.Length..]); verified = true; return; }
        const string discardCase = "I exercise discard ";
        if (step.StartsWith(discardCase, StringComparison.Ordinal)) { DiscardCase(step[discardCase.Length..]); verified = true; return; }
        switch (step)
        {
            case "a project with two workflow files":
                Directory.CreateDirectory(Path.Combine(cli.DirectoryPath, ".git"));
                File.WriteAllText(cli.FilePath, AgentWorkflow);
                File.WriteAllText(OtherFile, AgentWorkflow);
                break;
            case "I start both workflows and start the first again":
                first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                other = Json("start", OtherFile).GetProperty("runId").GetString()!;
                current = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                break;
            case "each start creates a distinct nonexecuting run":
                Assert.That(new[] { first, other, current }.Distinct().Count(), Is.EqualTo(3));
                foreach (var id in new[] { first, other, current })
                {
                    var state = Json("status", id);
                    Assert.That(state.GetProperty("status").GetString(), Is.EqualTo("ready"));
                    Assert.That(state.GetProperty("lastActivation").ValueKind, Is.EqualTo(JsonValueKind.Null));
                }
                break;
            case "separate commands continue the first workflow without run IDs":
                cli.Raw("status");
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.Contain("Run: " + current));
                var waiting = Json("advance");
                Assert.That(waiting.GetProperty("runId").GetString(), Is.EqualTo(current));
                var request = waiting.GetProperty("request").GetProperty("id").GetString()!;
                Json("complete", "--request", request, "--outcome", "approved");
                Assert.That(Json("status").GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(Json("advance").GetProperty("status").GetString(), Is.EqualTo("completed"));
                Assert.That(Json("status", other).GetProperty("status").GetString(), Is.EqualTo("ready"));
                break;
            case "I create runs and looping requests with reserved identity collisions":
                first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                Assert.That(first.Length, Is.EqualTo(8));
                Directory.CreateDirectory(Path.Combine(Root, "r_000002"));
                File.WriteAllText(Path.Combine(Root, "r_000002", "preserve.txt"), "unowned content");
                current = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                Assert.That(current, Is.EqualTo("r_000003"));
                var reserved = Path.Combine(Root, ".identities", "q_000004");
                File.WriteAllText(reserved, "");
                var old = Json("advance", current).GetProperty("request").GetProperty("id").GetString()!;
                Assert.That(old, Is.EqualTo("q_000005"));
                var accepted = Json("complete", current, "--request", old, "--outcome", "again");
                var fresh = Json("advance", current).GetProperty("request").GetProperty("id").GetString()!;
                Assert.That(fresh.Length, Is.EqualTo(8));
                var retried = Json("complete", current, "--request", old, "--outcome", "again");
                Assert.That(retried.GetProperty("receipt").GetRawText(), Is.EqualTo(accepted.GetProperty("receipt").GetRawText()));
                Assert.That(retried.GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(fresh));
                Assert.That(File.ReadAllText(Path.Combine(Root, "r_000002", "preserve.txt")), Is.EqualTo("unowned content"));
                break;
            case "eight-character identities remain distinct and historical retries stay safe":
                Assert.That(first, Is.Not.EqualTo(current));
                cli.Raw("status", current);
                Assert.That(cli.Stdout, Does.Contain("Run: " + current).And.Contain("Request: q_").And.Not.Contain("prefix:"));
                break;
            case "I select workflow paths from a project subdirectory":
                var child = Path.Combine(cli.DirectoryPath, "child with spaces");
                Directory.CreateDirectory(child); cli.WorkingDirectory = child;
                Assert.That(Json("status", "--file", "../other.md").GetProperty("runId").GetString(), Is.EqualTo(other));
                Assert.That(Json("status", "--file", "../workflow.md").GetProperty("runId").GetString(), Is.EqualTo(current));
                Assert.That(Json("status", other).GetProperty("runId").GetString(), Is.EqualTo(other));
                Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(current));
                Json("use", first);
                Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(first));
                Assert.That(Json("status", "--file", cli.FilePath).GetProperty("runId").GetString(), Is.EqualTo(first));
                File.WriteAllText(cli.FilePath, "changed invalid source");
                File.Delete(OtherFile);
                Assert.That(Json("advance", "--file", cli.FilePath).GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo("Review this workflow."));
                Assert.That(Json("status", "--file", OtherFile).GetProperty("runId").GetString(), Is.EqualTo(other));
                break;
            case "frozen runs remain associated with their original file and selected workflow":
                var listed = Json("runs", "--file", cli.FilePath).GetProperty("runs");
                Assert.That(listed.GetArrayLength(), Is.EqualTo(2));
                Assert.That(listed.EnumerateArray().Select(r => r.GetProperty("runId").GetString()), Is.EquivalentTo(new[] { first, current }));
                cli.Raw("runs");
                Assert.That(cli.Stdout, Does.Contain(first).And.Contain(other).And.Contain("handoff").And.Contain("other.md"));
                break;
            case "I complete and retry earlier requests while another run is current":
                first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                var original = Json("advance").GetProperty("request").GetProperty("id").GetString()!;
                other = Json("start", OtherFile).GetProperty("runId").GetString()!;
                var unrelated = Json("advance").GetProperty("request").GetProperty("id").GetString()!;
                var receipt = Json("complete", "--request", original, "--outcome", "again");
                Assert.That(receipt.GetProperty("runId").GetString(), Is.EqualTo(first));
                var newer = Json("advance", first).GetProperty("request").GetProperty("id").GetString()!;
                Assert.That(Json("complete", "--request", original, "--outcome", "again").GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(newer));
                cli.Raw("complete", "--request", original, "--outcome", "approved", "--json");
                Error("COMPLETION_CONFLICT", 4);
                Assert.That(Json("status").GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(unrelated));
                Json("complete", "--request", newer, "--outcome", "approved");
                Assert.That(Json("advance", first).GetProperty("status").GetString(), Is.EqualTo("completed"));
                verified = true;
                break;
            case "newer requests and unrelated runs remain unchanged":
                Assert.That(verified, Is.True); break;
            case "exact short and longer run and request identities share their prefix":
                first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                var snapshot = File.ReadAllText(Path.Combine(Root, first, "state.json"));
                foreach (var id in new[] { "r_100000", "r_1000000", "r_100000" + new string('a', 26) })
                {
                    Directory.CreateDirectory(Path.Combine(Root, id));
                    File.WriteAllText(Path.Combine(Root, id, "state.json"), snapshot.Replace(first, id));
                    Assert.That(Json("status", id).GetProperty("runId").GetString(), Is.EqualTo(id));
                }
                // Recheck after all longer identities exist, not just while creating them.
                Assert.That(Json("status", "r_100000").GetProperty("runId").GetString(), Is.EqualTo("r_100000"));
                var previous = Json("advance", first).GetProperty("request").GetProperty("id").GetString()!;
                Json("complete", first, "--request", previous, "--outcome", "again");
                var active = Json("advance", first).GetProperty("request").GetProperty("id").GetString()!;
                var statePath = Path.Combine(Root, first, "state.json");
                File.WriteAllText(statePath, File.ReadAllText(statePath).Replace(previous, "q_100000").Replace(active, "q_1000000"));
                var retryShort = Json("complete", "--request", "q_100000", "--outcome", "again");
                Assert.That(retryShort.GetProperty("request").GetProperty("id").GetString(), Is.EqualTo("q_1000000"));
                Json("complete", first, "--request", "q_1000000", "--outcome", "again");
                var next = Json("advance", first).GetProperty("request").GetProperty("id").GetString()!;
                File.WriteAllText(statePath, File.ReadAllText(statePath).Replace(next, "q_100000" + new string('a', 26)));
                Assert.That(Json("complete", first, "--request", "q_100000", "--outcome", "again").GetProperty("request").GetProperty("id").GetString(), Is.EqualTo("q_100000" + new string('a', 26)));
                Json("complete", "--request", "q_100000" + new string('a', 26), "--outcome", "approved");
                verified = true;
                break;
            case "every exact identity targets its own resource":
                Assert.That(verified, Is.True); break;
            case "I discard a selected completed run with request history":
                first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                var discardedRequest = Json("advance").GetProperty("request").GetProperty("id").GetString()!;
                Json("complete", "--request", discardedRequest, "--outcome", "approved");
                Json("advance");
                other = Json("start", OtherFile).GetProperty("runId").GetString()!;
                var otherPath = Path.Combine(Root, other, "state.json");
                var otherBytes = File.ReadAllText(otherPath);
                Json("use", first);
                var removed = Json("discard", first);
                if (!UsabilityAuditWorld.Complementary) Assert.That(removed.GetProperty("discarded").GetBoolean(), Is.True);
                Assert.That(removed.GetProperty("sourcePath").GetString(), Is.EqualTo(cli.FilePath));
                Assert.That(Directory.Exists(Path.Combine(Root, first)), Is.False);
                Assert.That(File.ReadAllText(otherPath), Is.EqualTo(otherBytes));
                Assert.That(Json("runs").GetProperty("runs").GetArrayLength(), Is.EqualTo(1));
                cli.Raw("status", "--json"); Error("STALE_CONTEXT", 4);
                cli.Raw("complete", "--request", discardedRequest, "--outcome", "approved", "--json"); Error("INVALID_REQUEST", 4);
                current = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                Assert.That(current, Is.Not.EqualTo(first));
                Assert.That(Json("advance").GetProperty("request").GetProperty("id").GetString(), Is.Not.EqualTo(discardedRequest));
                verified = true;
                break;
            case "only its state logs and receipts are removed and identities are not reused":
            case "context selection fails closed or selects the intended frozen run":
            case "discard preserves unrelated data and reports its actual result":
            case "the readable path completes without run IDs or JSON tools":
                Assert.That(verified, Is.True); break;
            case "I follow the readable contextual handoff":
                if (UsabilityAuditWorld.Complementary)
                {
                    UsabilityAuditWorld.Structured(() =>
                    {
                        Json("start", OtherFile);
                        var created = Json("start", cli.FilePath).GetProperty("runId").GetString();
                        Assert.That(Json("status").GetProperty("runId").GetString(), Is.EqualTo(created));
                        var waiting = Json("advance");
                        Assert.That(waiting.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
                        var request = waiting.GetProperty("request").GetProperty("id").GetString()!;
                        var completed = Json("complete", "--request", request, "--outcome", "approved");
                        Assert.That(completed.GetProperty("status").GetString(), Is.EqualTo("ready"));
                        Assert.That(completed.GetProperty("transitionCount").GetInt32(), Is.EqualTo(1));
                        Assert.That(Json("advance").GetProperty("status").GetString(), Is.EqualTo("completed"));
                    });
                    verified = true; break;
                }
                foreach (var args in new[] { new[] { "start", OtherFile }, new[] { "start", cli.FilePath }, new[] { "status" } })
                {
                    cli.Raw(args); Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                    Assert.That(cli.Stdout, Does.Contain("Run: r_").And.Not.StartWith("{"));
                }
                cli.Raw("advance"); Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.StartWith("Paused at Review (run r_").And.Contain("Review this workflow."));
                var displayedRequest = System.Text.RegularExpressions.Regex.Match(cli.Stdout, "Request: (q_[0-9a-f]{6})\\b").Groups[1].Value;
                Assert.That(displayedRequest, Is.Not.Empty);
                cli.Raw("complete", "--request", displayedRequest, "--outcome", "approved");
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.Contain("Status: ready").And.Contain("Completion: " + displayedRequest));
                cli.Raw("advance"); Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.StartWith("Finished successfully (1 step, run r_"));
                verified = true;
                break;
            default: throw new AssertionException("Undefined required context step: " + step);
        }
    }

    private void Error(string code, int exit)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(exit), cli.Stdout + cli.Stderr);
        Assert.That(cli.ErrorCode, Is.EqualTo(code));
    }

    internal const string AgentWorkflow = """
        ```yaml merstride
        schemaVersion: 1
        id: handoff
        entry: review
        nodes:
          review:
            kind: agent
            prompt: Review this workflow.
          done:
            kind: end
            result: success
        ```
        ```mermaid
        flowchart TD
        %% merstride: handoff
        review["Review"]
        done["Done"]
        review -->|again| review
        review -->|approved| done
        ```
        """;

    public void Dispose() => cli.Dispose();
}
