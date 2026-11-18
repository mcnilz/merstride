using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class AgentWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "";
    private string requestId = "";
    private JsonElement originalRequest, originalReceipt;
    private int transitions;
    private byte[] snapshot = [];
    private string sourceActivation = "";
    private string StateFile => Path.Combine(cli.State, runId, "state.json");
    private static string Helper => TestPaths.Helper;

    private void RepairWorkflow()
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: agents
            entry: check
            nodes:
              check:
                kind: process
                command: '{{Helper}}'
                args: [check]
                timeoutSeconds: 5
              repair:
                kind: agent
                prompt: 'Repair literally ${input}; do not interpolate.'
                context: [nodes.check.exitCode]
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: agents
            check["Check"]
            repair["Repair"]
            done["Done"]
            check -->|exit_0| done
            check -->|exit_1| repair
            repair -->|recheck| check
            repair -->|blocked| done
            ```
            """);
    }

    private void Start()
    {
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    private void Complete(string outcome, string data = "{}", string? id = null)
    {
        cli.ExpectedChildren = 0;
        cli.Call("complete", runId, "--request", id ?? requestId, "--outcome", outcome, "--data", data);
    }

    private void Wait()
    {
        RepairWorkflow(); Start(); cli.ExpectedChildren = 1; cli.Call("advance", runId);
        Execute("it waits for the agent");
        originalRequest = cli.Response.GetProperty("request").Clone();
        transitions = cli.Response.GetProperty("transitionCount").GetInt32();
        snapshot = File.ReadAllBytes(StateFile);
    }

    private void Accept()
    {
        Wait(); Complete("blocked", "{\"one\":1,\"two\":2}");
        Assert.That(cli.ExitCode, Is.Zero);
        originalReceipt = cli.Response.GetProperty("receipt").Clone();
        cli.Call("advance", runId);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
        transitions = cli.Response.GetProperty("transitionCount").GetInt32();
        snapshot = File.ReadAllBytes(StateFile);
    }

    private void AgentLoop(string selectors = "nodes.repair.data")
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: loop
            entry: repair
            nodes:
              repair:
                kind: agent
                prompt: 'Literal ${input}'
                context: [{{selectors}}]
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: loop
            repair["Repair"]
            done["Done"]
            repair -->|recheck| repair
            repair -->|blocked| done
            ```
            """);
    }

    private void SelectedContextWorkflow()
    {
        RepairWorkflow();
        var text = File.ReadAllText(cli.FilePath).Replace("entry: check", "entry: seed")
            .Replace("nodes:\n", "nodes:\n  seed:\n    kind: agent\n    prompt: 'SECRET previous prompt'\n")
            .Replace("args: [check]", "args: [context_output]")
            .Replace("check -->|exit_0| done", "check -->|exit_0| repair")
            .Replace("check[\"Check\"]", "seed[\"Seed\"]\nseed -->|next| check\ncheck[\"Check\"]");
        File.WriteAllText(cli.FilePath, text);
    }

    internal void Execute(string step)
    {
        switch (step)
        {
            case "an isolated workspace and state directory": break;
            case "an agent node selecting only \"nodes.check.exitCode\"": SelectedContextWorkflow(); break;
            case "other nodes have completed with prompts, data, and large logs":
                Start(); cli.Call("advance", runId);
                requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                Complete("next", "{\"secret\":\"SECRET previous data\"}"); break;
            case "the run reaches the agent node": cli.ExpectedChildren = 1; cli.Call("advance", runId); break;
            case "its context contains only \"nodes.check.exitCode\"":
                var context = cli.Response.GetProperty("request").GetProperty("context");
                Assert.That(context.GetArrayLength(), Is.EqualTo(1));
                Assert.That(context[0].GetProperty("selector").GetString(), Is.EqualTo("nodes.check.exitCode"));
                Assert.That(context[0].GetProperty("available").GetBoolean(), Is.True);
                Assert.That(context[0].GetProperty("value").GetInt32(), Is.Zero);
                Assert.That(context[0].GetProperty("sourceActivationId").GetString(), Does.StartWith("a_")); break;
            case "the response omits other prompts, full logs, and completion history":
                var handoff = cli.Response.GetProperty("request").GetRawText();
                Assert.That(handoff, Does.Not.Contain("SECRET").And.Not.Contain("loglog"));
                Assert.That(handoff.Length, Is.LessThan(4096));
                Assert.That(cli.Stdout, Does.Not.Contain("SECRET previous").And.Not.Contain("acceptedAt")); break;
            case "a loop whose agent selects data from a node not yet completed on its first visit":
                AgentLoop(); Start(); cli.Call("advance", runId);
                requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                sourceActivation = cli.Response.GetProperty("request").GetProperty("activationId").GetString()!; break;
            case "I inspect the first request": cli.Call("status", runId); break;
            case "that selector has available false":
                Assert.That(cli.Response.GetProperty("request").GetProperty("context")[0].GetProperty("available").GetBoolean(), Is.False); break;
            case "the selected node completes and the agent is visited again":
                Complete("recheck", "{\"answer\":42}"); cli.Call("advance", runId); break;
            case "the selector exposes the latest completed value and its activation ID":
                var selected = cli.Response.GetProperty("request").GetProperty("context")[0];
                Assert.That(selected.GetProperty("available").GetBoolean(), Is.True);
                Assert.That(selected.GetProperty("value").GetProperty("answer").GetInt32(), Is.EqualTo(42));
                Assert.That(selected.GetProperty("sourceActivationId").GetString(), Is.EqualTo(sourceActivation)); break;
            case "a run waiting for an agent": Wait(); break;
            case "a fresh CLI process reads status and calls advance":
                cli.ExpectedChildren = 0;
                cli.Call("status", runId);
                Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("request"), originalRequest), Is.True);
                cli.Call("advance", runId); break;
            case "both responses contain the original request ID and task":
                Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("request"), originalRequest), Is.True); break;
            case "the transition count is unchanged":
            case "no additional transition or process execution occurs":
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(transitions));
                Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "checks.txt")), Has.Length.EqualTo(1)); break;
            case "I submit an unknown request ID": Complete("blocked", id: "q_unknown"); break;
            case "I submit an undeclared outcome": Complete("invented"); break;
            case "I submit data larger than 16384 bytes": Complete("blocked", "{\"large\":\"" + new string('x', 16384) + "\"}"); break;
            case "the command rejects it with INVALID_REQUEST": Error("INVALID_REQUEST"); break;
            case "the command rejects it with INVALID_OUTCOME": Error("INVALID_OUTCOME"); break;
            case "the command rejects it with DATA_LIMIT": Error("DATA_LIMIT"); break;
            case "persisted run state is unchanged": Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot)); break;
            case "an accepted completion with data containing two object keys":
            case "an accepted completion": Accept(); break;
            case "I submit the same request and values with reversed object key order": Complete("blocked", "{\"two\":2,\"one\":1}"); break;
            case "the original receipt and current status are returned":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("receipt"), originalReceipt), Is.True);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed")); break;
            case "I resubmit its request ID with different data": Complete("blocked", "{\"one\":9,\"two\":2}"); break;
            case "the command fails with \"COMPLETION_CONFLICT\"": Error("COMPLETION_CONFLICT"); break;
            case "a completed agent request followed by a loop back to the same agent node":
                Wait(); Complete("recheck", "{\"one\":1}");
                originalReceipt = cli.Response.GetProperty("receipt").Clone(); break;
            case "the node is activated again": cli.ExpectedChildren = 1; cli.Call("advance", runId); break;
            case "its request ID differs from the previous request ID":
                Assert.That(cli.Response.GetProperty("request").GetProperty("id").GetString(), Is.Not.EqualTo(requestId));
                originalRequest = cli.Response.GetProperty("request").Clone(); break;
            case "I resend the previously accepted completion": Complete("recheck", "{\"one\":1}"); break;
            case "its historical receipt is returned":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("receipt"), originalReceipt), Is.True); break;
            case "the new request remains waiting":
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
                Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("request"), originalRequest), Is.True); break;
            case "a check that initially fails and routes to an agent with outcomes \"recheck\" and \"blocked\"": RepairWorkflow(); break;
            case "I advance the workflow":
                Start(); cli.ExpectedChildren = 1; cli.Call("advance", runId); break;
            case "it waits for the agent":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
                var request = cli.Response.GetProperty("request");
                requestId = request.GetProperty("id").GetString()!;
                Assert.That(request.GetProperty("prompt").GetString(), Is.EqualTo("Repair literally ${input}; do not interpolate."));
                Assert.That(request.GetProperty("allowedOutcomes").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "blocked", "recheck" }));
                break;
            case "the agent repairs the workspace and completes with \"recheck\"":
                File.WriteAllText(Path.Combine(cli.DirectoryPath, "repaired.txt"), "fixed"); Complete("recheck"); break;
            case "the run is ready and the check has not run again":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "checks.txt")), Has.Length.EqualTo(1)); break;
            case "I advance the run": cli.ExpectedChildren = 1; cli.Call("advance", runId); break;
            case "the check succeeds and the workflow completes":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
                Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "checks.txt")), Has.Length.EqualTo(2)); break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private void Error(string code)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(4));
        Assert.That(cli.ErrorCode, Is.EqualTo(code));
    }

    internal void RejectPayload(string payload)
    {
        Wait();
        var path = Path.Combine(cli.DirectoryPath, "bad.json");
        File.WriteAllText(path, payload);
        cli.ExpectedChildren = 0;
        cli.Call("complete", runId, "--request", requestId, "--result-file", path);
        Error("INVALID_PAYLOAD");
        Execute("persisted run state is unchanged");
    }

    internal void RejectInvalidUtf8()
    {
        Wait();
        var path = Path.Combine(cli.DirectoryPath, "bad.json");
        File.WriteAllBytes(path, [.. System.Text.Encoding.UTF8.GetBytes("{\"outcome\":\"blocked\",\"data\":{\"x\":\""), 255, .. System.Text.Encoding.UTF8.GetBytes("\"}}")]);
        cli.ExpectedChildren = 0;
        cli.Call("complete", runId, "--request", requestId, "--result-file", path);
        Error("INVALID_PAYLOAD"); Execute("persisted run state is unchanged");
    }

    internal void ReceiptSemantics()
    {
        Wait(); Complete("recheck", "{\"n\":0.1,\"a\":[1,2],\"z\":-0}");
        Assert.That(cli.ExitCode, Is.Zero);
        var receipt = cli.Response.GetProperty("receipt").Clone();
        snapshot = File.ReadAllBytes(StateFile);
        Complete("recheck", "{\"z\":0.0,\"a\":[1e0,2.0],\"n\":0.10000000000000001}");
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("receipt"), receipt), Is.True);
        Execute("persisted run state is unchanged");
        Complete("recheck", "{\"n\":0.1,\"a\":[2,1],\"z\":0}");
        Error("COMPLETION_CONFLICT"); Execute("persisted run state is unchanged");
    }

    internal void FrozenInput()
    {
        AgentLoop("input");
        var path = Path.Combine(cli.DirectoryPath, "input.json");
        File.WriteAllText(path, "{\"message\":\"Grüße 🌍\"}");
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath, "--input-file", path);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
        File.WriteAllText(path, "{}");
        cli.Call("advance", runId);
        var request = cli.Response.GetProperty("request");
        Assert.That(request.GetProperty("prompt").GetString(), Is.EqualTo("Literal ${input}"));
        var input = request.GetProperty("context")[0];
        Assert.That(input.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(input.GetProperty("value").GetProperty("message").GetString(), Is.EqualTo("Grüße 🌍"));
        Assert.That(input.GetProperty("sourceActivationId").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    internal void ProcessContext()
    {
        RepairWorkflow();
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath)
            .Replace("args: [check]", "args: [context_output]")
            .Replace("check -->|exit_0| done", "check -->|exit_0| repair")
            .Replace("context: [nodes.check.exitCode]", "context: [nodes.check.stdoutTail, nodes.check.stderrTail, nodes.check.exitCode]"));
        Start(); cli.ExpectedChildren = 1; cli.Call("advance", runId, "--save-stdout", "--save-stderr");
        Assert.That(cli.ExitCode, Is.Zero);
        var items = cli.Response.GetProperty("request").GetProperty("context");
        Assert.That(items[0].GetProperty("value").GetString()!.Length, Is.EqualTo(4096));
        Assert.That(items[0].GetProperty("truncated").GetBoolean(), Is.True);
        Assert.That(items[0].GetProperty("observedBytes").GetInt64(), Is.EqualTo(12000));
        Assert.That(items[1].GetProperty("value").GetString(), Is.EqualTo("SECRET unselected error"));
        Assert.That(items[1].GetProperty("truncated").GetBoolean(), Is.False);
        Assert.That(items[2].GetProperty("value").GetInt32(), Is.Zero);
        var source = items[0].GetProperty("sourceActivationId").GetString();
        Assert.That(items[1].GetProperty("sourceActivationId").GetString(), Is.EqualTo(source));
        Assert.That(items[2].GetProperty("sourceActivationId").GetString(), Is.EqualTo(source));
        cli.ExpectedChildren = 0;
        var request = cli.Response.GetProperty("request").Clone();
        cli.Call("status", runId);
        Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("request"), request), Is.True);
    }

    internal void SpawnFailureContext()
    {
        RepairWorkflow();
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath)
            .Replace(Helper, "merstride-missing-context-helper-7381")
            .Replace("check -->|exit_1| repair", "check -->|spawn_error| repair"));
        Start(); cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        var item = cli.Response.GetProperty("request").GetProperty("context")[0];
        Assert.That(item.GetProperty("available").GetBoolean(), Is.True);
        Assert.That(item.GetProperty("value").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(item.GetProperty("sourceActivationId").GetString(), Does.StartWith("a_"));
    }

    internal void InvalidSelector(string context)
    {
        AgentLoop();
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("context: [nodes.repair.data]", context));
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        Assert.That(Directory.Exists(cli.State), Is.False);
    }

    internal void DefaultData()
    {
        Wait();
        var path = Path.Combine(cli.DirectoryPath, "result.json");
        File.WriteAllText(path, "{\"outcome\":\"blocked\"}");
        cli.ExpectedChildren = 0;
        cli.Call("complete", runId, "--request", requestId, "--result-file", path);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("done"));
        Assert.That(cli.Response.GetProperty("receipt").GetProperty("data").EnumerateObject(), Is.Empty);
        snapshot = File.ReadAllBytes(StateFile);
        Complete("blocked");
        Assert.That(cli.ExitCode, Is.Zero);
        Execute("persisted run state is unchanged");
        cli.Call("advance", runId);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
    }

    internal void OversizedContext()
    {
        AgentLoop("input");
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("Literal ${input}", new string('p', 16000)));
        var path = Path.Combine(cli.DirectoryPath, "input.json");
        // Valid input's characters expand in the serialized protocol context.
        File.WriteAllText(path, "{\"text\":\"" + new string('ü', 7000) + "\"}");
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath, "--input-file", path);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
        cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.EqualTo(5));
        Assert.That(cli.ErrorCode, Is.EqualTo("CONTEXT_LIMIT"));
        Assert.That(cli.Response.GetProperty("request").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    internal void InvalidInput(string input, string error)
    {
        AgentLoop("input");
        var path = Path.Combine(cli.DirectoryPath, "input.json");
        File.WriteAllText(path, input == "oversize" ? "{\"large\":\"" + new string('x', 17000) + "\"}" : input);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath, "--input-file", path);
        Error(error);
        Assert.That(Directory.Exists(cli.State), Is.False);
    }

    public void Dispose() => cli.Dispose();
}
