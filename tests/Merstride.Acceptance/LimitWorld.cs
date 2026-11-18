using System.Diagnostics;
using System.Text;
using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class LimitWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "";
    private readonly List<Process> descendants = [];
    private static string Helper => TestPaths.Helper;
    private readonly Stopwatch elapsed = new();
    private bool timeoutTree;
    private string data = "";
    private string dataError = "";
    private byte[] snapshot = [];
    private string StateFile => Path.Combine(cli.State, runId, "state.json");
    private string firstActivation = "";
    private Dictionary<string, byte[]> logSnapshot = [];

    private void Workflow(string nodes, string declarations, string edges, string entry = "first", string limits = "") =>
        File.WriteAllText(cli.FilePath, $"```yaml merstride\nschemaVersion: 1\nid: limits\nentry: {entry}\n{limits}nodes:\n{nodes}\n```\n```mermaid\nflowchart TD\n%% merstride: limits\n{declarations}\n{edges}\n```\n");

    private static string ProcessNode(string id, string[] args, int timeout = 5) =>
        $"  {id}:\n    kind: process\n    command: {AcceptanceJson.Serialize(Helper)}\n    args: {AcceptanceJson.Serialize(args)}\n    timeoutSeconds: {timeout}\n";
    private const string EndNode = "  done:\n    kind: end\n    result: success\n";

    private void Start()
    {
        cli.ExpectedChildren = 0;
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    private void TimeoutTree()
    {
        Start();
        cli.ExpectedChildren = 2;
        elapsed.Start();
        var advance = Task.Run(() => cli.Call("advance", runId, "--save-stdout", "--save-stderr"));
        try
        {
            var ready = Path.Combine(cli.DirectoryPath, "parent.ready");
            Assert.That(SpinWait.SpinUntil(() => File.Exists(ready) || advance.IsCompleted, 5000), Is.True, "Native readiness barrier.");
            Assert.That(File.Exists(ready), Is.True, "Both helpers must be ready before timeout.");
            foreach (var name in new[] { "parent.pid", "child.pid" })
            {
                var child = Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(cli.DirectoryPath, name))));
                _ = child.Handle; // Retain an OS handle so PID reuse cannot make the exit assertion pass.
                descendants.Add(child);
                Assert.That(child.HasExited, Is.False);
            }
            File.WriteAllText(Path.Combine(cli.DirectoryPath, "release"), "observed");
        }
        finally { advance.GetAwaiter().GetResult(); elapsed.Stop(); }
    }

    internal void Execute(string step)
    {
        switch (step)
        {
            case "a process node with a one-second timeout and an attached child waiting indefinitely":
                timeoutTree = true;
                Workflow(ProcessNode("first", ["attached_parent"], 1) + EndNode.Replace("success", "failure"), "first[\"First\"]\ndone[\"Done\"]", "first -->|timeout| done"); break;
            case "a \"timeout\" edge to a failure end": break;
            case "I advance the workflow":
                if (timeoutTree) TimeoutTree(); else Advance(1);
                break;
            case "a process concurrently writing more than 10 MiB across stdout and stderr":
                OutputWorkflow(24 * 1024 * 1024); break;
            case "an \"output_limit\" edge": break;
            case "the output_limit route is taken within the test watchdog deadline":
                Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("done"));
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("output_limit")); break;
            case "combined retained logs are no larger than 10 MiB":
                AssertOutputBudget(); break;
            case "a workflow with maxTransitions 2 whose cursor reaches a success end on the second transition": TransitionWorkflow("end"); break;
            case "a workflow with maxTransitions 2 whose cursor reaches a process node on the second transition": TransitionWorkflow("process"); break;
            case "a workflow with maxTransitions 2 whose cursor reaches an agent node on the second transition": TransitionWorkflow("agent"); break;
            case "I advance until the next stopping point": Advance(2); break;
            case "the result is completed": TransitionResult(false); break;
            case "the result is failed TRANSITION_LIMIT": TransitionResult(true); break;
            case "a completed process with more than 4096 bytes of multibyte UTF-8 output":
                ContextWorkflow("unicode_output"); break;
            case "an agent requests its stdoutTail": Advance(1); break;
            case "the tail is valid UTF-8 and no larger than 4096 bytes":
                var tail = cli.Response.GetProperty("request").GetProperty("context")[0].GetProperty("value").GetString()!;
                Assert.That(tail, Is.EqualTo(string.Concat(Enumerable.Repeat("界🌍", 585))));
                Assert.That(new UTF8Encoding(false, true).GetByteCount(tail), Is.EqualTo(4095)); break;
            case "truncation and observed output size are reported":
                var item = cli.Response.GetProperty("request").GetProperty("context")[0];
                Assert.That(item.GetProperty("truncated").GetBoolean(), Is.True);
                Assert.That(item.GetProperty("observedBytes").GetInt64(), Is.EqualTo(7000));
                Assert.That(item.GetProperty("available").GetBoolean(), Is.True);
                Assert.That(item.GetProperty("sourceActivationId").GetString(), Does.StartWith("a_")); break;
            case "valid individual prompt and data values whose assembled prompt and context exceed 32768 bytes": ContextBoundary(1); break;
            case "the agent node is activated": cli.Call("advance", runId); break;
            case "the run fails with \"CONTEXT_LIMIT\"":
                Assert.That(cli.ExitCode, Is.EqualTo(5));
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed"));
                Assert.That(cli.ErrorCode, Is.EqualTo("CONTEXT_LIMIT")); break;
            case "no silently truncated structured context is returned":
                Assert.That(cli.Response.GetProperty("request").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(cli.Response.GetProperty("lastActivation").ValueKind, Is.EqualTo(JsonValueKind.Null)); break;
            case "a run waiting for an agent": WaitForAgent(); break;
            case "I complete with data having exactly 16384 serialized bytes": CompleteData("exact"); break;
            case "I complete with data having 16385 serialized bytes": CompleteData("oversize"); break;
            case "I complete with data having nesting depth 16": CompleteData("depth16"); break;
            case "I complete with data having nesting depth 17": CompleteData("depth17"); break;
            case "I complete with data having duplicate object keys": CompleteData("duplicate"); break;
            case "I complete with data having integer 9007199254740992": CompleteData("unsafe"); break;
            case "acceptance is allowed": AssertData(true); break;
            case "acceptance is rejected": AssertData(false); break;
            case "two process activations with different stdout and stderr logs": TwoLogs(); break;
            case "I request 100 tail bytes of the first activation's stderr":
                cli.ExpectedChildren = 0;
                cli.Call("logs", runId, "--activation", firstActivation, "--stream", "stderr", "--tail-bytes", "100"); break;
            case "only that stream's tail is returned with truncation metadata":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("log").GetString(), Is.EqualTo(new string('E', 100)));
                Assert.That(cli.Response.GetProperty("stream").GetString(), Is.EqualTo("stderr"));
                Assert.That(cli.Response.GetProperty("truncated").GetBoolean(), Is.True);
                Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot));
                foreach (var log in logSnapshot) Assert.That(File.ReadAllBytes(log.Key), Is.EqualTo(log.Value)); break;
            case "no child process is launched": Assert.That(cli.ExpectedChildren, Is.Zero); break;
            case "the timeout route is taken within the test watchdog deadline":
                Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
                Assert.That(cli.ExitCode, Is.EqualTo(5));
                Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("done"));
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("timeout"));
                Assert.That(cli.ErrorCode, Is.EqualTo("WORKFLOW_FAILURE")); break;
            case "the parent and attached child have terminated":
                Assert.That(descendants, Has.Count.EqualTo(2));
                foreach (var child in descendants) Assert.That(child.WaitForExit(2000), Is.True, "Observed helper must exit."); break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private void TwoLogs()
    {
        Workflow(ProcessNode("first", ["labelled_streams", "O", "E"]) + ProcessNode("second", ["labelled_streams", "X", "Y"]) +
            AgentNode.Replace("prompt: Review", "prompt: Review\n    context: [nodes.first.stdoutTail]") + EndNode,
            "first[\"First\"]\nagent[\"Agent\"]\nsecond[\"Second\"]\ndone[\"Done\"]",
            "first -->|exit_0| agent\nagent -->|next| second\nsecond -->|exit_0| done");
        Advance(1);
        var request = cli.Response.GetProperty("request");
        firstActivation = request.GetProperty("context")[0].GetProperty("sourceActivationId").GetString()!;
        Assert.That(request.GetProperty("context")[0].GetProperty("value").GetString(), Is.EqualTo(new string('O', 200)));
        cli.ExpectedChildren = 0; CompleteData("depth16");
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
        Advance(1);
        Assert.That(cli.ExitCode, Is.Zero);
        var second = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
        Assert.That(second, Is.Not.EqualTo(firstActivation));
        cli.ExpectedChildren = 0;
        cli.Call("logs", runId, "--activation", second, "--stream", "stdout");
        Assert.That(cli.Response.GetProperty("log").GetString(), Is.EqualTo(new string('X', 200)));
        cli.Call("logs", runId, "--activation", second, "--stream", "stderr");
        Assert.That(cli.Response.GetProperty("log").GetString(), Is.EqualTo(new string('Y', 200)));
        snapshot = File.ReadAllBytes(StateFile);
        logSnapshot = Directory.GetFiles(Path.Combine(cli.State, runId, "logs")).ToDictionary(p => p, File.ReadAllBytes);
    }

    private void AgentWorkflow() => Workflow(AgentNode + EndNode, "agent[\"Agent\"]\ndone[\"Done\"]", "agent -->|next| done", "agent");

    private void WaitForAgent()
    {
        AgentWorkflow(); Advance(0);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
        snapshot = File.ReadAllBytes(StateFile);
    }

    private static (string Json, string Error) DataFixture(string property) => property switch
    {
        // {"x":""} is eight ASCII bytes; a literal ü is two UTF-8 bytes.
        "exact" => ("{\"x\":\"" + new string('x', 16374) + "ü\"}", ""),
        "oversize" => ("{\"x\":\"" + new string('x', 16375) + "ü\"}", "DATA_LIMIT"),
        // Root object = 1, fifteen arrays = depth 16. The completion envelope is excluded.
        "depth16" => ("{\"x\":" + new string('[', 15) + "0" + new string(']', 15) + "}", ""),
        "depth17" => ("{\"x\":" + new string('[', 16) + "0" + new string(']', 16) + "}", "INVALID_PAYLOAD"),
        "duplicate" => ("{\"x\":1,\"x\":2}", "INVALID_PAYLOAD"),
        "unsafe" => ("{\"x\":9007199254740992}", "INVALID_PAYLOAD"),
        "negativeUnsafe" => ("{\"x\":-9007199254740992}", "INVALID_PAYLOAD"),
        "safe" => ("{\"min\":-9007199254740991,\"max\":9007199254740991}", ""),
        "objectDepth16" => (string.Concat(Enumerable.Repeat("{\"x\":", 16)) + "0" + new string('}', 16), ""),
        "objectDepth17" => (string.Concat(Enumerable.Repeat("{\"x\":", 17)) + "0" + new string('}', 17), "INVALID_PAYLOAD"),
        _ => throw new AssertionException($"Unknown data fixture: {property}")
    };

    private void CompleteData(string property)
    {
        var requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
        (data, dataError) = DataFixture(property);
        cli.Call("complete", runId, "--request", requestId, "--outcome", "next", "--data", data);
    }

    private void AssertData(bool allowed)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(allowed ? 0 : 4));
        if (allowed)
        {
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
            Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(1));
            using var expected = JsonDocument.Parse(data);
            Assert.That(JsonElement.DeepEquals(cli.Response.GetProperty("receipt").GetProperty("data"), expected.RootElement), Is.True);
        }
        else
        {
            Assert.That(cli.ErrorCode, Is.EqualTo(dataError));
            Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot));
        }
    }

    internal void InputBoundary(string property)
    {
        AgentWorkflow();
        var (json, error) = DataFixture(property);
        var path = Path.Combine(cli.DirectoryPath, "input.json"); File.WriteAllText(path, json);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath, "--input-file", path);
        if (error.Length == 0)
        {
            Assert.That(cli.ExitCode, Is.Zero);
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        }
        else
        {
            Assert.That(cli.ExitCode, Is.EqualTo(4));
            Assert.That(cli.ErrorCode, Is.EqualTo(error == "DATA_LIMIT" ? "INPUT_LIMIT" : error));
            Assert.That(Directory.Exists(cli.State), Is.False);
        }
    }

    internal void CompletionBoundary(string property)
    {
        WaitForAgent(); CompleteData(property); AssertData(DataFixture(property).Error.Length == 0);
    }

    internal void AgentTransition(string target)
    {
        TransitionWorkflow(target);
        File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath)
            .Replace(ProcessNode("second", ["effect", "second"]), AgentNode.Replace("agent:", "second:"))
            .Replace("second -->|exit_0|", "second -->|next|"));
        Advance(1);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(1));
        cli.ExpectedChildren = 0; CompleteData("depth16");
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
        Advance(0);
        Assert.That(cli.ExitCode, Is.EqualTo(target == "end" ? 0 : 5));
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(target == "end" ? "completed" : "failed"));
        if (target != "end") Assert.That(cli.ErrorCode, Is.EqualTo("TRANSITION_LIMIT"));
        Assert.That(cli.Response.GetProperty("request").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
        Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("first\n"));
    }

    internal void InvalidByteTail()
    {
        ContextWorkflow("invalid_output"); Advance(1);
        Assert.That(cli.ExitCode, Is.Zero);
        var item = cli.Response.GetProperty("request").GetProperty("context")[0];
        var value = item.GetProperty("value").GetString()!;
        Assert.That(value, Is.EqualTo(new string('\uFFFD', 1365)));
        Assert.That(new UTF8Encoding(false, true).GetByteCount(value), Is.EqualTo(4095));
        Assert.That(item.GetProperty("observedBytes").GetInt64(), Is.EqualTo(5000));
        Assert.That(item.GetProperty("truncated").GetBoolean(), Is.True);
    }

    private void ContextBoundary(int extra)
    {
        // ASCII JSON needs no escaping. This literal is the public context envelope,
        // independent of the production serializer and Fits calculation.
        const string envelope = "[{\"selector\":\"input\",\"available\":true,\"value\":{\"x\":\"\"},\"sourceActivationId\":null,\"truncated\":null,\"observedBytes\":null}]";
        var payloadLength = 16384 - envelope.Length + extra;
        data = "{\"x\":\"" + new string('x', payloadLength) + "\"}";
        Assert.That(Encoding.UTF8.GetByteCount(data), Is.LessThanOrEqualTo(16384));
        Workflow(AgentNode.Replace("prompt: Review", "prompt: '" + new string('p', 16384) + "'\n    context: [input]") + EndNode,
            "agent[\"Agent\"]\ndone[\"Done\"]", "agent -->|next| done", "agent");
        var path = Path.Combine(cli.DirectoryPath, "input.json"); File.WriteAllText(path, data);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath, "--input-file", path);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    internal void ExactContext()
    {
        ContextBoundary(0); cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        var request = cli.Response.GetProperty("request");
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
        Assert.That(request.GetProperty("prompt").GetString()!.Length, Is.EqualTo(16384));
        Assert.That(request.GetProperty("context")[0].GetProperty("value").GetRawText(), Is.EqualTo(data));
        Assert.That(Encoding.UTF8.GetByteCount(request.GetProperty("context").GetRawText()), Is.EqualTo(16384));
    }

    private void ContextWorkflow(string mode) => Workflow(ProcessNode("first", [mode]) + AgentNode.Replace("    prompt: Review", "    prompt: Review\n    context: [nodes.first.stdoutTail]") + EndNode,
        "first[\"First\"]\nagent[\"Agent\"]\ndone[\"Done\"]", "first -->|exit_0| agent\nagent -->|next| done");

    private const string AgentNode = "  agent:\n    kind: agent\n    prompt: Review\n";

    private void TransitionWorkflow(string target)
    {
        var tail = target == "end" ? "done" : "target";
        var nodes = ProcessNode("first", ["effect", "first"]) + ProcessNode("second", ["effect", "second"]) + EndNode;
        var declarations = "first[\"First\"]\nsecond[\"Second\"]\ndone[\"Done\"]";
        var edges = $"first -->|exit_0| second\nsecond -->|exit_0| {tail}";
        if (target != "end")
        {
            nodes += target == "process" ? ProcessNode("target", ["effect", "FORBIDDEN"]) : AgentNode.Replace("agent:", "target:");
            declarations += "\ntarget[\"Target\"]";
            edges += "\ntarget -->|next| done";
        }
        Workflow(nodes, declarations, edges, limits: "limits:\n  maxTransitions: 2\n");
    }

    private void TransitionResult(bool failed)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(failed ? 5 : 0));
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(failed ? "failed" : "completed"));
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
        Assert.That(cli.Response.GetProperty("request").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("nodeId").GetString(), Is.EqualTo("second"));
        Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("first\nsecond\n"));
        if (failed) Assert.That(cli.ErrorCode, Is.EqualTo("TRANSITION_LIMIT"));
    }

    private void Advance(int children)
    {
        if (runId.Length == 0) Start();
        cli.ExpectedChildren = children;
        elapsed.Restart(); cli.Call("advance", runId, "--save-stdout", "--save-stderr"); elapsed.Stop();
    }

    private void OutputWorkflow(int bytes) => Workflow(ProcessNode("first", ["concurrent_output", bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)]) + EndNode,
        "first[\"First\"]\ndone[\"Done\"]", "first -->|output_limit| done\nfirst -->|exit_0| done");

    private void AssertOutputBudget()
    {
        var activation = cli.Response.GetProperty("lastActivation");
        var logs = Directory.GetFiles(Path.Combine(cli.State, runId, "logs"));
        Assert.That(logs, Has.Length.EqualTo(2));
        Assert.That(logs.Sum(p => new FileInfo(p).Length), Is.EqualTo(10 * 1024 * 1024));
        Assert.That(activation.GetProperty("stdoutBytes").GetInt64(), Is.Positive);
        Assert.That(activation.GetProperty("stderrBytes").GetInt64(), Is.Positive);
        Assert.That(activation.GetProperty("stdoutBytes").GetInt64() + activation.GetProperty("stderrBytes").GetInt64(), Is.GreaterThan(10 * 1024 * 1024));
        Assert.That(activation.GetProperty("stdoutTruncated").GetBoolean() || activation.GetProperty("stderrTruncated").GetBoolean(), Is.True);
    }

    internal void ExactOutput(int extra)
    {
        OutputWorkflow(10 * 1024 * 1024 + extra); Advance(1);
        Assert.That(cli.ExitCode, Is.Zero);
        var activation = cli.Response.GetProperty("lastActivation");
        Assert.That(activation.GetProperty("outcome").GetString(), Is.EqualTo(extra == 0 ? "exit_0" : "output_limit"));
        if (extra != 0) AssertOutputBudget();
        else
        {
            Assert.That(Directory.GetFiles(Path.Combine(cli.State, runId, "logs")).Sum(p => new FileInfo(p).Length), Is.EqualTo(10485760));
            Assert.That(activation.GetProperty("stdoutTruncated").GetBoolean(), Is.False);
            Assert.That(activation.GetProperty("stderrTruncated").GetBoolean(), Is.False);
        }
    }

    public void Dispose()
    {
        // Even a failed readiness/termination assertion must not orphan a fixture.
        // Only recover PIDs from this world's private markers and verify the image.
        if (timeoutTree)
        foreach (var name in new[] { "parent.pid", "child.pid" })
        {
            var path = Path.Combine(cli.DirectoryPath, name);
            if (!File.Exists(path) || !int.TryParse(File.ReadAllText(path), out var pid) || descendants.Any(p => p.Id == pid)) continue;
            Process? child = null;
            try
            {
                child = Process.GetProcessById(pid);
                _ = child.Handle;
                if (!child.HasExited && string.Equals(child.MainModule?.FileName, Helper, StringComparison.OrdinalIgnoreCase))
                { descendants.Add(child); child = null; }
            }
            catch (ArgumentException) { } // It exited before observation.
            catch (InvalidOperationException) { } // It exited while its image was inspected.
            finally { child?.Dispose(); }
        }
        foreach (var child in descendants)
        {
            if (!child.HasExited) { child.Kill(true); child.WaitForExit(2000); }
            child.Dispose();
        }
        cli.Dispose();
    }
}
