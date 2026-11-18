using System.Text.RegularExpressions;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class RunNarrationWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string kind = "";
    private string runId = "";
    private const string Workflow = "Inspect a workspace";
    private const string Prompt = "Review the changes and report what you found.";

    internal void Execute(string step)
    {
        switch (step)
        {
            case "a named workflow with a successful process chain": WriteWorkflow("chain"); break;
            case "a named workflow with an unhandled process failure": WriteWorkflow("failure"); break;
            case "a named workflow with a review handoff": WriteWorkflow("agent"); break;
            case "a named workflow that uses a fallback to retry a step": WriteWorkflow("retry"); break;
            case "I start and advance the named workflow": Run(); break;
            case "the workflow and both steps are clear and success is summarized once": CheckChain(); break;
            case "its failed step, cause and next action are clear": CheckFailure(); break;
            case "its paused step, request, prompt, outcomes and next action are clear": CheckHandoff(); break;
            case "its fallback, repeated step and final success are clear": CheckRetry(); break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private void WriteWorkflow(string fixture)
    {
        kind = fixture;
        var executable = AcceptanceJson.Serialize(TestPaths.Helper);
        var arguments = kind switch { "chain" => "streams", "failure" => "exit, '7'", "retry" => "retry_once", _ => "exit, '0'" };
        var additionalNode = kind switch
        {
            "chain" => $"  second:\n    kind: process\n    command: {executable}\n    args: [exit, '0']\n    timeoutSeconds: 30\n",
            "agent" => $"  review:\n    kind: agent\n    prompt: {Prompt}\n",
            _ => ""
        };
        var route = kind switch
        {
            "chain" => "    second[\"Inspect changes\"]\n    first -->|exit_0| second\n    second -->|exit_0| done",
            "agent" => "    review[\"Review changes\"]\n    first -->|exit_0| review\n    review -->|approved| done\n    review -->|recheck| review",
            "retry" => "    first -->|default| first\n    first -->|exit_0| done",
            _ => "    first -->|exit_0| done"
        };
        File.WriteAllText(cli.FilePath, $$"""
              # {{Workflow}} #

            ```yaml merstride
            schemaVersion: 1
            id: inspect
            entry: first
            rtk: false
            nodes:
              first:
                kind: process
                command: {{executable}}
                args: [{{arguments}}]
                timeoutSeconds: 30
            {{additionalNode}}  done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
                %% merstride: inspect
                first["Inspect status"]
                done["Finished"]
            {{route}}
            ```
            """);
    }

    private void Run()
    {
        cli.ExpectedChildren = kind is "chain" or "retry" ? 2 : 1;
        string[] args = ["start", cli.FilePath, "--advance", "--state-dir", cli.State];
        if (UsabilityAuditWorld.Complementary) UsabilityAuditWorld.Structured(() => cli.Raw([.. args, "--json"]));
        else cli.Raw(args);
        Assert.That(cli.ExitCode, Is.EqualTo(kind == "failure" ? 5 : 0));
        cli.ExpectedChildren = 0;
        if (UsabilityAuditWorld.Complementary)
        {
            runId = cli.Response.GetProperty("runId").GetString()!;
            Assert.That(cli.Response.GetProperty("processOutput").GetArrayLength(), Is.EqualTo(kind is "chain" or "retry" ? 2 : 1));
            Assert.That(cli.Stderr, Is.Empty);
        }
        else
        {
            runId = "r_000001";
            Assert.That(cli.Stderr, Does.StartWith($"Starting {Workflow} (run {runId})."));
            Assert.That(cli.Stderr, Does.Contain("Starting Inspect status."));
        }
    }

    private void CheckChain()
    {
        if (UsabilityAuditWorld.Complementary)
        {
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
            Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
            Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("nodeId").GetString(), Is.EqualTo("second"));
            return;
        }
        Assert.That(cli.Stderr.IndexOf("Starting Inspect status.", StringComparison.Ordinal),
            Is.LessThan(cli.Stderr.IndexOf("Starting Inspect changes.", StringComparison.Ordinal)));
        Assert.That(cli.Stdout, Does.Contain("real stdout\n"));
        Assert.That(cli.Stdout.TrimEnd().EndsWith($"Finished successfully (2 steps, run {runId}).", StringComparison.Ordinal), Is.True);
        Assert.That(Regex.Matches(cli.Stdout + cli.Stderr, "Finished successfully").Count, Is.EqualTo(1));
        Assert.That(cli.Stdout + cli.Stderr, Does.Not.Contain("Flow #").And.Not.Contain("Initial advance:")
            .And.Not.Contain("Cursor:").And.Not.Contain("saved exit code").And.Not.Contain("exit_0"));
    }

    private void CheckFailure()
    {
        if (UsabilityAuditWorld.Complementary)
        {
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed"));
            Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("first"));
            Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("exitCode").GetInt32(), Is.EqualTo(7));
            Assert.That(cli.ErrorCode, Is.EqualTo("NO_MATCHING_EDGE"));
            return;
        }
        Assert.That(cli.Stdout, Does.Contain($"Failed at Inspect status (run {runId}).")
            .And.Contain("Next: Fix the command or add a route for its result, then start a new run."));
        Assert.That(cli.Stderr, Does.Contain("Inspect status exited with code 7, and the workflow has no route for that result."));
    }

    private void CheckHandoff()
    {
        string request;
        if (UsabilityAuditWorld.Complementary)
        {
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
            var handoff = cli.Response.GetProperty("request");
            Assert.That(handoff.GetProperty("nodeId").GetString(), Is.EqualTo("review"));
            Assert.That(handoff.GetProperty("prompt").GetString(), Is.EqualTo(Prompt));
            Assert.That(handoff.GetProperty("allowedOutcomes")[0].GetString(), Is.EqualTo("approved"));
            request = handoff.GetProperty("id").GetString()!;
        }
        else
        {
            request = Regex.Match(cli.Stdout, "Request: (q_[0-9a-f]+)").Groups[1].Value;
            Assert.That(request, Is.Not.Empty);
            Assert.That(cli.Stdout, Does.Contain($"Paused at Review changes (run {runId}).")
                .And.Contain(Prompt).And.Contain("Allowed outcomes: approved")
                .And.Contain($"merstride complete {runId} --request {request} --outcome"));
        }
        UsabilityAuditWorld.Structured(() => cli.Raw("complete", runId, "--request", request, "--outcome", "recheck", "--state-dir", cli.State, "--json"));
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        string[] advance = ["advance", runId, "--state-dir", cli.State];
        if (UsabilityAuditWorld.Complementary)
        {
            UsabilityAuditWorld.Structured(() => cli.Raw([.. advance, "--json"]));
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
            Assert.That(cli.Response.GetProperty("request").GetProperty("id").GetString(), Is.Not.EqualTo(request));
        }
        else
        {
            cli.Raw(advance);
            Assert.That(cli.Stdout, Does.Contain($"Paused at Review changes (visit 2, run {runId})."));
            Assert.That(cli.Stdout, Does.Not.Contain(request));
        }
        Assert.That(cli.ExitCode, Is.Zero);
    }

    private void CheckRetry()
    {
        if (UsabilityAuditWorld.Complementary)
        {
            Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
            Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(2));
            Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("exitCode").GetInt32(), Is.Zero);
            return;
        }
        Assert.That(cli.Stderr, Does.Contain("Inspect status exited with code 7. The fallback leads to Inspect status.")
            .And.Contain("Starting Inspect status (visit 2)."));
        Assert.That(cli.Stdout.Trim(), Is.EqualTo($"Finished successfully (2 steps, run {runId})."));
    }

    public void Dispose() => cli.Dispose();
}
