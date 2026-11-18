#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ProgressionWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "";
    private string requestId = "", originalRequest = "", originalReceipt = "", kind = "";
    private ConsoleActivityWorld? barrier;
    private bool verified;

    private void Text(params string[] args) => cli.Raw([.. args, .. args[0] == "advance" ? new[] { "--details" } : [], "--state-dir", cli.State]);

    internal bool StructuredCompanion(FeatureCase scenario)
    {
        if (scenario.Steps.Contains("I execute the progression loop with explicit JSON")) return false;
        if (scenario.Steps.Contains("a progression workflow with a looping agent"))
        {
            LoopFixture();
            UsabilityAuditWorld.Structured(() => JsonLoop(rejectBeforeAccept: true));
            cli.Raw("status", runId, "--state-dir", cli.State);
            return true;
        }
        var process = scenario.Steps.FirstOrDefault(s => s.StartsWith("a progression process with ", StringComparison.Ordinal));
        if (process is not null)
        {
            ProcessFixture(process[27..]);
            UsabilityAuditWorld.Structured(() =>
            {
                if (kind == "unlabeled")
                {
                    cli.Call("validate", cli.FilePath);
                    Assert.That(cli.Response.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("IGNORES_PROCESS_FAILURE"));
                }
                cli.ExpectedChildren = kind == "spawn_error" ? 0 : 1;
                cli.Call("advance", runId); cli.ExpectedChildren = 0;
                var failed = kind is "no-match" or "failure-end" or "loop-limit";
                Assert.That(cli.ExitCode, Is.EqualTo(failed ? 5 : 0));
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(failed ? "failed" : "completed"));
                Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo(kind is "no-match" or "loop-limit" ? "work" : "done"));
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(kind == "no-match" ? 0 : 1));
                var outcome = kind switch { "no-match" or "unlabeled" => "exit_7", "failure-end" or "loop-limit" => "exit_0", _ => kind };
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo(outcome));
                if (failed) Assert.That(cli.ErrorCode, Is.EqualTo(kind switch { "no-match" => "NO_MATCHING_EDGE", "failure-end" => "WORKFLOW_FAILURE", _ => "TRANSITION_LIMIT" }));
                var state = File.ReadAllBytes(Path.Combine(cli.State, runId, "state.json"));
                cli.Call("status", runId); Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(failed ? "failed" : "completed"));
                Assert.That(File.ReadAllBytes(Path.Combine(cli.State, runId, "state.json")), Is.EqualTo(state));
            });
            return true;
        }
        if (scenario.Steps.Contains("a progression workflow with two process branches"))
        {
            Execute("a progression workflow with two process branches");
            UsabilityAuditWorld.Structured(() =>
            {
                cli.ExpectedChildren = 2; cli.Call("advance", runId); cli.ExpectedChildren = 0;
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
                Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("done"));
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("exit_7"));
            });
            return true;
        }
        return false;
    }

    internal void Execute(string step)
    {
        const string recovery = "I recover interrupted progression with ";
        if (step.StartsWith(recovery, StringComparison.Ordinal))
        {
            using var world = new RecoveryWorld();
            world.AssertProgressionRecovery(step[recovery.Length..] == "retry");
            verified = true; return;
        }
        const string fixture = "a progression process with ";
        if (step.StartsWith(fixture, StringComparison.Ordinal)) { ProcessFixture(step[fixture.Length..]); return; }
        const string agent = "a progression agent allowing ";
        if (step.StartsWith(agent, StringComparison.Ordinal))
        {
            kind = step[agent.Length..];
            File.WriteAllText(cli.FilePath, $$"""
                ```yaml merstride
                schemaVersion: 1
                id: agent_names
                entry: review
                nodes:
                  review:
                    kind: agent
                    prompt: Choose the allowed outcome.
                  done:
                    kind: end
                    result: success
                ```
                ```mermaid
                flowchart TD
                    %% merstride: agent_names
                    review["Review"]
                    done["Done"]
                    review -->|{{kind}}| done
                ```
                """);
            Start(); return;
        }
        const string fail = "the failure explains ";
        if (step.StartsWith(fail, StringComparison.Ordinal))
        {
            var cause = step[fail.Length..].Replace(" and no route or automatic recovery", "", StringComparison.Ordinal);
            Assert.That(cli.ExitCode, Is.EqualTo(5));
            Assert.That(cli.Stderr, Does.Contain(cause).And.Contain("overall result: failure"));
            if (kind == "no-match")
                Ordered(cli.Stderr, "saved exit code 7 (exit_7)", "Flow stays at work; no route taken", "Run executing -> failed at work");
            if (kind == "failure-end")
                Ordered(cli.Stderr, "saved exit code 0 (exit_0)", "Flow #1: work -> done", "Run ready -> failed at done");
            if (kind == "loop-limit")
            {
                Ordered(cli.Stderr, "Flow #1: work -> work", "Revisit: next visit 2", "Run ready -> failed at work");
                Assert.That(cli.Stderr, Does.Not.Contain("Process work (visit 2)"));
            }
            Assert.That(cli.Stdout, Does.Contain("Result: failure").And.Contain("Only interrupted runs support resolve"));
            return;
        }
        const string explanation = "the saved process explanation is ";
        if (step.StartsWith(explanation, StringComparison.Ordinal))
        {
            var text = step[explanation.Length..].Replace(" and overall success is separate", "", StringComparison.Ordinal);
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            Ordered(cli.Stderr, "saved " + text, "Flow #1: work -> done via default", "overall result: success");
            Assert.That(cli.Stdout, Does.Contain("Last process: work - " + text).And.Contain("Result: success"));
            return;
        }
        const string completion = "I complete the loop with outcome ";
        if (step.StartsWith(completion, StringComparison.Ordinal))
        {
            cli.Call("status", runId); requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
            Text("complete", runId, "--request", requestId, "--outcome", step[completion.Length..]);
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            return;
        }
        switch (step)
        {
            case "a progression workflow with two process branches":
                File.WriteAllText(cli.FilePath, $$"""
                    ```yaml merstride
                    schemaVersion: 1
                    id: progression
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
                      done:
                        kind: end
                        result: success
                    ```
                    ```mermaid
                    flowchart TD
                        %% merstride: progression
                        first["First"]
                        second["Second"]
                        done["Done"]
                        first -->|exit_0| second
                        first -->|default| done
                        second -->|default| done
                    ```
                    """);
                cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
                runId = cli.Response.GetProperty("runId").GetString()!;
                break;
            case "I advance the progression as text":
                cli.ExpectedChildren = 2; Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "process results and numbered flow movements explain both branch choices in order":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
                Ordered(cli.Stderr,
                    "Process first (visit 1): activating (launch pending)",
                    "Process first (visit 1): saved exit code 0 (exit_0)",
                    "Flow #1: first -> second via exact outcome exit_0",
                    "Process second (visit 1): activating (launch pending)",
                    "Process second (visit 1): saved exit code 7 (exit_7)",
                    "Flow #2: second -> done via default (no exact match for exit_7)",
                    "Run ready -> completed at done: success end; overall result: success");
                Assert.That(cli.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(7));
                Assert.That(cli.Stderr, Does.Not.Contain("a_"));
                Assert.That(cli.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).All(l => l.StartsWith("[" + runId + "] ", StringComparison.Ordinal)), Is.True);
                break;
            case "the final summary distinguishes the last process outcome from overall success":
                Assert.That(cli.Stdout, Does.Contain("Status: completed (finished successfully)")
                    .And.Contain("Result: success").And.Contain("Last process: second - exit code 7 (exit_7)")
                    .And.Contain("Next: no further execution"));
                break;
            case "a progression workflow with a looping agent": LoopFixture(); break;
            case "I advance the looping agent as text":
            case "I advance through the repeated process visit":
                cli.ExpectedChildren = 1; Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "waiting explains that the flow stays at review and requires completion":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
                Ordered(cli.Stderr, "Flow #1: work -> review", "Run ready -> waiting_agent at review (visit 1)", "Flow stays here", "complete this request");
                Assert.That(cli.Stdout, Does.Contain("Result: not finished").And.Contain("Next: perform").And.Contain("Allowed outcomes: approved, revise"));
                break;
            case "the accepted completion explains review to work without executing work":
                Ordered(cli.Stderr, "Agent review (visit 1): completion " + requestId + " accepted and saved (revise)",
                    "Flow #2: review -> work via agent outcome revise", "Revisit: next visit 2", "Run waiting_agent -> ready", "next node has not executed", "Next: merstride advance " + runId);
                Assert.That(cli.Stdout, Does.Contain("Cursor: work").And.Contain("Result: not finished"));
                Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "effects.txt")), Has.Length.EqualTo(1));
                originalRequest = requestId;
                cli.Call("complete", runId, "--request", requestId, "--outcome", "revise");
                originalReceipt = cli.Response.GetProperty("receipt").GetRawText();
                break;
            case "the repeated visits are identified before the new request":
                Ordered(cli.Stderr, "Process work (visit 2): activating", "Flow #3: work -> review", "Revisit: next visit 2", "waiting_agent at review (visit 2)");
                cli.Call("status", runId);
                requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                Assert.That(requestId, Is.Not.EqualTo(originalRequest));
                break;
            case "retrying the original completion returns its receipt without progression":
                Text("complete", runId, "--request", originalRequest, "--outcome", "revise");
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stderr, Is.Empty);
                Assert.That(cli.Stdout, Does.Contain("saved receipt; retries do not move the flow").And.Contain("Request: " + requestId));
                cli.Call("complete", runId, "--request", originalRequest, "--outcome", "revise");
                Assert.That(cli.Response.GetProperty("receipt").GetRawText(), Is.EqualTo(originalReceipt));
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(3));
                Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "effects.txt")), Has.Length.EqualTo(2));
                break;
            case "I advance the finished loop": Text("advance", runId); break;
            case "terminal inspection and repeated advance have no new activity":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
                Assert.That(cli.Stderr, Does.Contain("Run ready -> completed at done"));
                foreach (var command in new[] { "status", "advance" })
                {
                    Text(command, runId);
                    Assert.That(cli.ExitCode, Is.Zero);
                    Assert.That(cli.Stderr, Is.Empty);
                    Assert.That(cli.Stdout, Does.Contain("Result: success").And.Contain("no further execution"));
                }
                break;
            case "I advance the failing progression":
                cli.ExpectedChildren = kind == "spawn_error" ? 0 : 1;
                Text("advance", runId); cli.ExpectedChildren = 0; break;
            case "read-only failed status succeeds with the same result and next action":
                Text("status", runId);
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stdout, Does.Contain("Status: failed (finished with failure)").And.Contain("Result: failure").And.Contain("correct the cause and start a new run"));
                Assert.That(cli.Stderr, Does.StartWith("Error ").And.Not.Contain("[" + runId + "]"));
                break;
            case "the unlabeled flow explains that it accepts failures too":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
                Ordered(cli.Stderr, "saved exit code 7 (exit_7)", "Flow #1: work -> done via unlabeled edge (accepts any process outcome, including failures)", "overall result: success");
                break;
            case "rejected outcomes and rejected completion saves explain inspection without acceptance": RejectedCompletions(); break;
            case "redirected streams retain escaped prompt data and compact progress":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stdout, Does.Contain("Grüße\\u001B[31m\\u0000\\u202E"));
                Assert.That(cli.Stdout, Does.Not.Contain('\u001b').And.Not.Contain('\0').And.Not.Contain('\u202e'));
                Assert.That(cli.Stderr, Does.Not.Contain("Grüße").And.Not.Contain("a_").And.Not.Contain('\u001b'));
                Assert.That(cli.Stderr.Length, Is.LessThan(1500));
                Assert.That(cli.Stdout, Does.Contain("Run: " + runId).And.Contain("Result: not finished").And.Contain("Next: perform"));
                break;
            case "I execute the progression loop with explicit JSON": JsonLoop(); verified = true; break;
            case "all responses are single protocol-1 objects without human activity":
            case "interruption describes uncertain effects and explicit resolution without replay":
            case "the decision describes an unchanged cursor and the next explicit action": Assert.That(verified, Is.True); break;
            case "a progression process held at a native barrier":
                barrier = new ConsoleActivityWorld();
                barrier.Execute("a console activity workflow with a controlled process"); break;
            case "I observe its live activation before release":
                barrier!.Execute("I advance it with redirected text output");
                // FailedSave releases the process; the activation must already have been flushed.
                barrier.Execute("its activation is visible before a rejected save"); break;
            case "a rejected result save claims no result route or terminal success":
                barrier!.Execute("a failed result save announces neither a saved outcome nor completion"); break;
            case "I interrupt progression through the native signal fixture":
                using (var world = new RecoveryWorld()) world.AssertTextInterruption();
                verified = true; break;
            case "I accept its process-like outcome as text":
                cli.Call("advance", runId);
                requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                Text("complete", runId, "--request", requestId, "--outcome", kind);
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
                break;
            case "the summary describes an agent choice and JSON retains the literal outcome":
                Assert.That(cli.Stdout, Does.Contain("Last agent visit: review - agent outcome " + kind)
                    .And.Contain("Result: not finished").And.Contain("Cursor: done"));
                Assert.That(cli.Stderr, Does.Contain("Flow #1: review -> done via agent outcome " + kind)
                    .And.Not.Contain("Process review"));
                cli.Call("complete", runId, "--request", requestId, "--outcome", kind);
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo(kind));
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("exitCode").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
                Assert.That(cli.Response.GetProperty("receipt").GetProperty("outcome").GetString(), Is.EqualTo(kind));
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(cli.Stderr, Is.Empty);
                break;
            default: throw new AssertionException($"Undefined required progression step: {step}");
        }
    }

    private void RejectedCompletions()
    {
        cli.Call("status", runId); requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
        var before = cli.Stdout;
        Text("complete", runId, "--request", requestId, "--outcome", "invalid");
        Assert.That(cli.ExitCode, Is.EqualTo(4));
        Assert.That(cli.Stderr, Does.Contain("INVALID_OUTCOME").And.Contain("Next: inspect status").And.Not.Contain("[" + runId + "]"));
        var path = Path.Combine(cli.State, runId, "state.json");
        using (new RejectedStateSave(path))
        {
            Text("complete", runId, "--request", requestId, "--outcome", "revise");
            Assert.That(cli.ExitCode, Is.EqualTo(6));
            Assert.That(cli.Stderr, Does.Contain("IO_ERROR").And.Not.Contain("accepted and saved").And.Not.Contain("Flow #"));
        }
        cli.Call("status", runId);
        Assert.That(cli.Stdout, Is.EqualTo(before));
    }

    private void JsonLoop(bool rejectBeforeAccept = false)
    {
        void Call(int children, params string[] args)
        {
            cli.ExpectedChildren = children; cli.Call(args); cli.ExpectedChildren = 0;
            Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            Assert.That(cli.Stderr, Is.Empty);
            Assert.That(cli.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
            Assert.That(cli.Response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(cli.Response.GetProperty("runId").GetString(), Is.EqualTo(runId));
        }
        Call(1, "advance", runId);
        Assert.That(cli.Response.GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo("Review Grüße\u001b[31m\0\u202e and choose."));
        var first = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
        if (rejectBeforeAccept)
        {
            var stateFile = Path.Combine(cli.State, runId, "state.json");
            var snapshot = File.ReadAllBytes(stateFile);
            cli.Call("complete", runId, "--request", first, "--outcome", "invalid");
            Assert.That(cli.ExitCode, Is.EqualTo(4)); Assert.That(cli.ErrorCode, Is.EqualTo("INVALID_OUTCOME"));
            Assert.That(File.ReadAllBytes(stateFile), Is.EqualTo(snapshot));
            using (new RejectedStateSave(stateFile))
            {
                cli.Call("complete", runId, "--request", first, "--outcome", "revise");
                Assert.That(cli.ExitCode, Is.EqualTo(6)); Assert.That(cli.ErrorCode, Is.EqualTo("IO_ERROR"));
                Assert.That(File.ReadAllBytes(stateFile), Is.EqualTo(snapshot));
            }
            Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "effects.txt")), Has.Length.EqualTo(1));
        }
        Call(0, "complete", runId, "--request", first, "--outcome", "revise");
        Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("work"));
        var receipt = cli.Response.GetProperty("receipt").GetRawText();
        Call(1, "advance", runId);
        var second = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
        Call(0, "complete", runId, "--request", first, "--outcome", "revise");
        Assert.That(cli.Response.GetProperty("receipt").GetRawText(), Is.EqualTo(receipt));
        Assert.That(cli.Response.GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(second));
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(3));
        Call(0, "complete", runId, "--request", second, "--outcome", "approved");
        Call(0, "advance", runId);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
        Call(0, "advance", runId);
        Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(4));
        Assert.That(File.ReadAllLines(Path.Combine(cli.DirectoryPath, "effects.txt")), Has.Length.EqualTo(2));
    }

    private void Start()
    {
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    private void LoopFixture()
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: loop
            entry: work
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [effect, work]
                timeoutSeconds: 5
              review:
                kind: agent
                prompt: "Review Grüße\e[31m\u0000\u202E and choose."
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
                %% merstride: loop
                work["Work"]
                review["Review"]
                done["Done"]
                work -->|exit_0| review
                review -->|revise| work
                review -->|approved| done
            ```
            """);
        Start();
    }

    private void ProcessFixture(string type)
    {
        kind = type;
        var args = type switch
        {
            "timeout" => new[] { "wait" }, "output_limit" => ["flood"],
            "no-match" or "unlabeled" => ["exit", "7"], _ => ["exit", "0"]
        };
        var route = type switch
        {
            "no-match" => "work -->|exit_0| done", "unlabeled" => "work --> done",
            "loop-limit" => "work -->|exit_0| work\n    work -->|default| done", _ => "work -->|default| done"
        };
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: process
            entry: work
            limits:
              maxTransitions: 1
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(type == "spawn_error" ? "unavailable-p20" : TestPaths.Helper)}}
                args: {{AcceptanceJson.Serialize(args)}}
                timeoutSeconds: 1
              done:
                kind: end
                result: {{(type == "failure-end" ? "failure" : "success")}}
            ```
            ```mermaid
            flowchart TD
                %% merstride: process
                work["Work"]
                done["Done"]
                {{route}}
            ```
            """);
        Start();
    }

    private static void Ordered(string text, params string[] expected)
    {
        var position = 0;
        foreach (var item in expected)
        {
            var found = text.IndexOf(item, position, StringComparison.Ordinal);
            Assert.That(found >= position, Is.True, $"Missing or out-of-order progression: {item}\n{text}");
            position = found + item.Length;
        }
    }

    public void Dispose() { barrier?.Dispose(); cli.Dispose(); }
}
