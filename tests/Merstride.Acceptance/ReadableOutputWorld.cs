using System.Text.Json;
using System.Text.RegularExpressions;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ReadableOutputWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "", requestId = "", defect = "", logKind = "", outputFormat = "";
    private string[] failing = [];
    private JsonElement before, receipt;
    private bool verified;

    internal bool StructuredCompanion(FeatureCase scenario)
    {
        if (scenario.Steps.Contains("a readable workflow with selected truncated and unavailable context"))
        {
            HandoffFixture();
            UsabilityAuditWorld.Structured(() =>
            {
                cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
                Assert.That(cli.ExitCode, Is.Zero);
                runId = cli.Response.GetProperty("runId").GetString()!;
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(cli.Response.GetProperty("lastActivation").ValueKind, Is.EqualTo(JsonValueKind.Null));
                cli.ExpectedChildren = 1; cli.Call("advance", runId); cli.ExpectedChildren = 0;
                Assert.That(cli.ExitCode, Is.Zero);
                var request = cli.Response.GetProperty("request");
                requestId = request.GetProperty("id").GetString()!;
                Assert.That(request.GetProperty("prompt").GetString(), Is.EqualTo("First line\nSecond Grüße\u001b[31m\U000E0001"));
                Assert.That(request.GetProperty("allowedOutcomes")[0].GetString(), Is.EqualTo("approved"));
                Assert.That(request.GetProperty("context")[1].GetProperty("truncated").GetBoolean(), Is.True);
                Assert.That(request.GetProperty("context")[2].GetProperty("available").GetBoolean(), Is.False);
                Assert.That(cli.Stdout, Does.Not.Contain("SECRET"));
                cli.Call("complete", runId, "--request", requestId, "--outcome", "approved");
                Assert.That(cli.ExitCode, Is.Zero);
                var accepted = cli.Response.GetProperty("receipt").GetRawText();
                Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("later"));
                Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.False);
                cli.Call("complete", runId, "--request", requestId, "--outcome", "approved");
                Assert.That(cli.Response.GetProperty("receipt").GetRawText(), Is.EqualTo(accepted));
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
                cli.ExpectedChildren = 1; cli.Call("advance", runId); cli.ExpectedChildren = 0;
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
                cli.Call("advance", runId);
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("later\n"));
                cli.Raw("status", runId, "--state-dir", cli.State);
            });
            return true;
        }
        if (scenario.Steps.Contains("a failed readable workflow"))
        {
            File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace("result: success", "result: failure"));
            cli.Call("start", cli.FilePath); runId = cli.Response.GetProperty("runId").GetString()!;
            UsabilityAuditWorld.Structured(() =>
            {
                cli.Call("advance", runId); Assert.That(cli.ExitCode, Is.EqualTo(5));
                Assert.That(cli.ErrorCode, Is.EqualTo("WORKFLOW_FAILURE"));
                var state = File.ReadAllBytes(Path.Combine(cli.State, runId, "state.json"));
                cli.Call("status", runId); Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed"));
                Assert.That(cli.ErrorCode, Is.EqualTo("WORKFLOW_FAILURE"));
                Assert.That(File.ReadAllBytes(Path.Combine(cli.State, runId, "state.json")), Is.EqualTo(state));
            });
            return true;
        }
        return false;
    }

    private void Text(params string[] args) => cli.Raw([.. args, "--state-dir", cli.State]);
    private static JsonElement Protocol(CliWorld world)
    {
        Assert.That(world.Stderr, Is.Empty);
        Assert.That(world.Stdout.Trim().Split('\n'), Has.Length.EqualTo(1));
        var result = JsonDocument.Parse(world.Stdout).RootElement.Clone();
        Assert.That(result.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
        return result;
    }

    internal void Execute(string step)
    {
        const string discovery = "I invoke readable discovery with ";
        if (step.StartsWith(discovery, StringComparison.Ordinal))
        {
            cli.Raw(step[discovery.Length..].Split(';'));
            return;
        }
        var discover = Regex.Match(step, "^discovery is (text|json) containing (.+) without filesystem effects$");
        if (discover.Success)
        {
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            var content = discover.Groups[2].Value;
            if (discover.Groups[1].Value == "json")
            {
                var response = Protocol(cli);
                Assert.That(response.GetProperty(content).GetString(), Is.Not.Empty);
                Assert.That(response.GetProperty("status").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(response.GetProperty("error").ValueKind, Is.EqualTo(JsonValueKind.Null));
            }
            else
            {
                Assert.That(cli.Stdout, Does.Contain(content).And.Not.StartWith("{"));
                Assert.That(cli.Stderr, Is.Empty);
                if (content == "Merstride workflow creation")
                    Assert.That(cli.Stdout.Replace("\r\n", "\n"), Does.Contain("\n```yaml merstride\n"));
            }
            Assert.That(Directory.GetFileSystemEntries(cli.DirectoryPath), Is.Empty);
            return;
        }
        const string fixture = "a readable-output fixture with ";
        if (step.StartsWith(fixture, StringComparison.Ordinal)) { ErrorFixture(step[fixture.Length..]); return; }
        const string command = "I invoke its failing command as ";
        if (step.StartsWith(command, StringComparison.Ordinal))
        {
            outputFormat = step[command.Length..];
            cli.Raw([.. failing, .. outputFormat == "json" && !defect.Contains("json", StringComparison.Ordinal) ? new[] { "--json" } : []]);
            return;
        }
        var error = Regex.Match(step, "^the selected output reports ([A-Z_]+) with exit ([0-9]+)$");
        if (error.Success) { AssertError(error.Groups[1].Value, int.Parse(error.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)); return; }
        const string logs = "a readable log workflow producing ";
        if (step.StartsWith(logs, StringComparison.Ordinal)) { LogFixture(step[logs.Length..]); return; }
        const string resolve = "I resolve interrupted work as text with ";
        if (step.StartsWith(resolve, StringComparison.Ordinal))
        {
            using var recovery = new RecoveryWorld();
            recovery.AssertReadableResolution(step[resolve.Length..] == "retry"); verified = true; return;
        }
        switch (step)
        {
            case "an isolated readable-output workspace": break;
            case "a readable workflow with selected truncated and unavailable context": HandoffFixture(); break;
            case "I start the workflow as text":
                Text("start", cli.FilePath, "--workspace", cli.DirectoryPath); break;
            case "start displays the run ID and state root without executing":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                runId = Regex.Match(cli.Stdout, "Run: (r_[0-9a-f]{6,32})").Groups[1].Value;
                Assert.That(runId, Is.Not.Empty);
                Assert.That(cli.Stdout, Does.Contain("State root: " + cli.State).And.Contain("Status: ready").And.Contain("Cursor: work"));
                cli.Call("status", runId);
                Assert.That(cli.Response.GetProperty("lastActivation").ValueKind, Is.EqualTo(JsonValueKind.Null)); break;
            case "I advance to the agent as text":
                cli.ExpectedChildren = 1; Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "the handoff displays literal instructions outcomes and selected context":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.Contain("Status: waiting_agent").And.Contain("Node: review")
                    .And.Contain("First line\nSecond Grüße\\u001B[31m\\uDB40\\uDC01").And.Contain("Allowed outcomes: approved")
                    .And.Contain("nodes.work.stdoutTail").And.Contain("truncated: yes")
                    .And.Contain("nodes.later.stdoutTail: unavailable").And.Not.Contain("SECRET").And.Not.Contain('\u001b'));
                requestId = Regex.Match(cli.Stdout, "Request: (q_[0-9a-f]{6,32})").Groups[1].Value;
                Assert.That(requestId, Is.Not.Empty);
                cli.Call("status", runId); before = cli.Response;
                Assert.That(before.GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo("First line\nSecond Grüße\u001b[31m\U000E0001"));
                Assert.That(before.GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(requestId)); break;
            case "I complete the request as text and repeat it as JSON":
                Text("complete", runId, "--request", requestId, "--outcome", "approved");
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.Contain("Status: ready").And.Contain("Completion: " + requestId).And.Contain("approved"));
                cli.Call("complete", runId, "--request", requestId, "--outcome", "approved"); receipt = cli.Response; break;
            case "the receipt is idempotent and only the cursor moved":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(receipt.GetProperty("transitionCount").GetInt32(), Is.EqualTo(before.GetProperty("transitionCount").GetInt32() + 1));
                Assert.That(receipt.GetProperty("receipt").GetProperty("requestId").GetString(), Is.EqualTo(requestId));
                Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.False); break;
            case "I advance to the terminal state as text":
                cli.ExpectedChildren = 1; Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "readable status agrees with JSON and does not replay work":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stdout, Does.Contain("Status: completed").And.Contain("Cursor: done"));
                cli.Call("status", runId);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
                Text("advance", runId);
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("later\n")); break;
            case "I read its logs as text and JSON": CompareLogs(); break;
            case "log bytes stay unchanged and both modes describe the same bounded tail":
            case "recovery retains its existing status exit and no-replay semantics": Assert.That(verified, Is.True); break;
            case "I validate its unlabeled process edge as text": Text("validate", cli.FilePath); break;
            case "validation success is on stdout and a located warning is on stderr":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stdout, Is.EqualTo("Valid workflow: readable" + Environment.NewLine));
                Assert.That(cli.Stderr, Does.Contain("Warning IGNORES_PROCESS_FAILURE").And.Contain("line ").And.Contain("column ")); break;
            case "a failed readable workflow":
                File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace("result: success", "result: failure"));
                cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath); runId = cli.Response.GetProperty("runId").GetString()!;
                Text("advance", runId);
                Assert.That(cli.ExitCode, Is.EqualTo(5)); Assert.That(cli.Stderr, Does.Contain("WORKFLOW_FAILURE")); break;
            case "I inspect the failed workflow as text and JSON":
                Text("status", runId); Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stdout, Does.Contain("Status: failed")); Assert.That(cli.Stderr, Does.Contain("WORKFLOW_FAILURE"));
                cli.Call("status", runId); break;
            case "both inspections exit zero and preserve the failure reason":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.ErrorCode, Is.EqualTo("WORKFLOW_FAILURE")); break;
            case "the default response is a readable version with no state or children":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stdout, Does.StartWith("Merstride ").And.Contain("protocol 1"));
                Assert.That(cli.Stderr, Is.Empty);
                Assert.That(Directory.GetFileSystemEntries(cli.DirectoryPath), Is.Empty); break;
            default: throw new AssertionException($"Undefined required readable-output step: {step}");
        }
    }

    private void ErrorFixture(string kind)
    {
        defect = kind;
        failing = kind switch
        {
            "usage" => ["unknown"],
            "validation" => ["validate", cli.FilePath],
            "state" => ["status", "r_bad-id"],
            "io" => ["validate", cli.FilePath],
            "duplicate-json" => ["version", "--json", "--json"],
            "malformed-json" => ["version", "--json=false"],
            "json-after-end" => ["version", "--", "--json"],
            "json-global" => ["--json", "unknown"],
            _ => throw new AssertionException("Unknown readable error fixture.")
        };
        if (kind == "validation") File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace("result: success", "result: other"));
    }

    private void AssertError(string code, int exit)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(exit));
        if (outputFormat == "json")
        {
            var problem = Protocol(cli).GetProperty("error");
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(code));
        }
        else
        {
            Assert.That(cli.Stdout, Is.Empty);
            Assert.That(cli.Stderr, Does.Contain("Error " + code).And.Not.Contain(" at Merstride."));
            if (code == "USAGE") Assert.That(cli.Stderr, Does.Contain("--help"));
            if (code == "INVALID_DOCUMENT") Assert.That(cli.Stderr, Does.Contain("line ").And.Contain("column "));
        }
        Assert.That(Directory.Exists(cli.State), Is.False);
    }

    private void HandoffFixture() => File.WriteAllText(cli.FilePath, $$"""
        ```yaml merstride
        schemaVersion: 1
        id: readable
        entry: work
        nodes:
          work:
            kind: process
            command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
            args: [context_output]
            timeoutSeconds: 5
          review:
            kind: agent
            prompt: "First line\nSecond Grüße\u001B[31m\U000E0001"
            context: [input, nodes.work.stdoutTail, nodes.later.stdoutTail]
          later:
            kind: process
            command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
            args: [effect, later]
            timeoutSeconds: 5
          done:
            kind: end
            result: success
        ```
        ```mermaid
        flowchart TD
        %% merstride: readable
        work["Work"]
        review["Review"]
        later["Later"]
        done["Done"]
        work -->|exit_0| review
        review -->|approved| later
        later -->|exit_0| done
        ```
        """);

    private void LogFixture(string content)
    {
        logKind = content;
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: readable
            entry: work
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [{{(content == "raw" ? "raw" : "streams")}}]
                timeoutSeconds: 5
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: readable
            work["Work"]
            done["Done"]
            work --> done
            ```
            """);
    }

    private void CompareLogs()
    {
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath); runId = cli.Response.GetProperty("runId").GetString()!;
        cli.ExpectedChildren = 1; cli.Call("advance", runId); cli.ExpectedChildren = 0;
        var activation = cli.Response.GetProperty("lastActivation");
        var id = activation.GetProperty("id").GetString()!;
        var logPath = Path.Combine(cli.State, runId, activation.GetProperty("stdoutPath").GetString()!);
        var original = File.ReadAllBytes(logPath);
        var statePath = Path.Combine(cli.State, runId, "state.json"); var state = File.ReadAllBytes(statePath);
        var tail = logKind == "tail" ? "5" : "65536";
        cli.Call("logs", runId, "--activation", id, "--stream", "stdout", "--tail-bytes", tail);
        var result = cli.Response;
        Text("logs", runId, "--activation", id, "--stream", "stdout", "--tail-bytes", tail);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Stdout, Does.Contain("Activation: " + id).And.Contain("Stream: stdout")
            .And.Contain("Tail truncated: " + (logKind == "tail" ? "yes" : "no")));
        if (logKind == "raw")
        {
            Assert.That(original, Is.EqualTo(new byte[] { 0, 255, 13, 10 }));
            Assert.That(cli.Stdout, Does.Contain("\\u0000�\n").And.Not.Contain('\0'));
        }
        else Assert.That(cli.Stdout, Does.Contain(result.GetProperty("log").GetString()!));
        Assert.That(File.ReadAllBytes(logPath), Is.EqualTo(original));
        Assert.That(File.ReadAllBytes(statePath), Is.EqualTo(state));
        Assert.That(cli.Stdout.Length, Is.LessThan(400000)); verified = true;
    }

    public void Dispose() => cli.Dispose();
}
