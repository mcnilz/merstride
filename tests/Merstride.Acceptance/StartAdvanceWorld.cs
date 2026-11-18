using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class StartAdvanceWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string stop = "chain", format = "json", separateId = "", combinedId = "";
    private JsonElement separate, combined;
    private StartAdvanceSafetyWorld? safety;
    private string Workspace => Path.Combine(cli.DirectoryPath, "work space");
    private string Effects => Path.Combine(Workspace, "effects.txt");
    private int Children => stop is "chain" or "handoff" ? 2 : stop is "agent" or "success" or "failure" or "spawn" ? 0 : 1;
    private int Exit => stop is "failure" or "no-route" or "transition-limit" ? 5 : 0;

    internal void Execute(string step)
    {
        if (step.StartsWith("a combined-start workflow ending at ", StringComparison.Ordinal))
        { stop = step[36..]; Fixture(); }
        else if (step.StartsWith("I compare separate start and advance with start --advance as ", StringComparison.Ordinal))
        { format = step[61..]; Compare(); }
        else if (step.StartsWith("both paths have equivalent progress and effects at ", StringComparison.Ordinal)) AssertEquivalent();
        else (safety ??= new StartAdvanceSafetyWorld()).Execute(step);
    }

    private void Fixture()
    {
        Directory.CreateDirectory(Workspace);
        File.WriteAllText(Path.Combine(cli.DirectoryPath, "input.json"), "{\"greeting\":\"Grüße\"}");
        var entry = stop == "agent" ? "review" : stop is "success" or "failure" ? "done" : "first";
        var processNodes = stop is "success" or "failure" or "agent" ? "" : $$"""
              first:
                kind: process
                command: {{AcceptanceJson.Serialize(stop == "spawn" ? "unavailable-p21" : TestPaths.Helper)}}
                args: {{AcceptanceJson.Serialize(stop switch {
                    "no-route" => new[] { "exit", "7" }, "timeout" => ["wait"], "output-limit" => ["flood"],
                    _ => ["effect", "first"] })}}
                timeoutSeconds: 1
            """;
        if (stop is "chain" or "handoff" or "transition-limit") processNodes += $$"""

              second:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [effect, second]
                timeoutSeconds: 5
            """;
        var agentNode = stop is "handoff" or "agent" ? """
              review:
                kind: agent
                prompt: "Review Grüße\e[31m and choose."
                context: [input]
            """ : "";
        var declarations = (processNodes.Length > 0 ? "first[\"First\"]\n" : "") +
            (stop is "chain" or "handoff" or "transition-limit" ? "second[\"Second\"]\n" : "") +
            (agentNode.Length > 0 ? "review[\"Review\"]\n" : "");
        var route = stop switch {
            "chain" or "transition-limit" => "first -->|exit_0| second\nsecond -->|exit_0| done",
            "handoff" => "first -->|exit_0| second\nsecond -->|exit_0| review\nreview -->|approved| done",
            "agent" => "review -->|approved| done", "success" or "failure" => "",
            "spawn" or "timeout" or "output-limit" => "first -->|default| done", _ => "first -->|exit_0| done"
        };
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: combined
            entry: {{entry}}
            limits:
              maxTransitions: {{(stop == "transition-limit" ? 1 : 20)}}
            nodes:
            {{processNodes}}
            {{agentNode}}
              done:
                kind: end
                result: {{(stop == "failure" ? "failure" : "success")}}
            ```
            ```mermaid
            flowchart TD
                %% merstride: combined
            {{declarations}}
                done["Done"]
            {{route}}
            ```
            """);
        // Force workflow selection; the other pair must never run.
        File.AppendAllText(cli.FilePath, "\n" + FeatureTests.Workflow);
    }

    private void Compare()
    {
        var startArgs = new[] { "start", cli.FilePath, "--workspace", Workspace, "--workflow", "combined", "--input-file", Path.Combine(cli.DirectoryPath, "input.json") };
        cli.Call(startArgs);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        separateId = cli.Response.GetProperty("runId").GetString()!;
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        Assert.That(File.Exists(Effects), Is.False);
        Assert.That(cli.Response.GetProperty("start").ValueKind, Is.EqualTo(JsonValueKind.Null));
        var save = stop == "output-limit" ? new[] { "--save-stdout", "--save-stderr" } : [];
        var combinedArgs = startArgs.Concat(["--advance", "--details"]).Concat(save);
        cli.ExpectedChildren = Children;
        cli.Call(["advance", separateId, "--details", .. save]); separate = cli.Response;
        Assert.That(cli.ExitCode, Is.EqualTo(Exit), cli.Stdout + cli.Stderr);
        var structuredComplement = UsabilityAuditWorld.Complementary && format == "text";
        if (format == "json" || structuredComplement)
        {
            if (structuredComplement) UsabilityAuditWorld.Structured(() => cli.Call(combinedArgs.ToArray()));
            else cli.Call(combinedArgs.ToArray());
            combined = cli.Response;
            Assert.That(cli.ExitCode, Is.EqualTo(Exit), cli.Stdout + cli.Stderr);
            combinedId = combined.GetProperty("runId").GetString()!;
            if (UsabilityAuditWorld.Complementary && !structuredComplement)
                Assert.That(UsabilityAuditWorld.LastText, Does.Contain("Creation: saved new run").And.Contain("Initial advance: finished"));
            else
            {
                Assert.That(combined.GetProperty("start").GetProperty("created").GetBoolean(), Is.True);
                Assert.That(combined.GetProperty("start").GetProperty("phase").GetString(), Is.EqualTo("advance"));
                Assert.That(combined.GetProperty("start").GetProperty("advance").GetString(), Is.EqualTo("finished"));
            }
            Assert.That(cli.Stderr, Is.Empty);
            Assert.That(cli.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
        }
        else
        {
            cli.Raw(combinedArgs.Concat(["--state-dir", cli.State]).ToArray());
            Assert.That(cli.ExitCode, Is.EqualTo(Exit), cli.Stdout + cli.Stderr);
            combinedId = cli.Stdout.Split('\n').Single(line => line.StartsWith("Run: ", StringComparison.Ordinal))[5..].Trim();
            Assert.That(cli.Stdout, Does.Contain("Creation: saved new run").And.Contain("Initial advance: finished").And.Contain("Next:"));
            Assert.That(cli.Stderr, Does.StartWith("[" + combinedId + "] Created new ready run"));
            if (Children > 0) Assert.That(cli.Stderr, Does.Contain("Process first (visit 1): activating").And.Contain("Flow"));
            Assert.That(cli.Stderr, Does.Not.Contain('\u001b'));
        }
        cli.ExpectedChildren = 0;
        cli.Call("status", combinedId); combined = cli.Response;
    }

    private void AssertEquivalent()
    {
        Assert.That(combinedId, Is.Not.EqualTo(separateId));
        foreach (var field in new[] { "status", "cursor", "transitionCount", "workflowId", "sourcePath" })
            Assert.That(combined.GetProperty(field).GetRawText(), Is.EqualTo(separate.GetProperty(field).GetRawText()));
        Assert.That(combined.GetProperty("error").GetRawText(), Is.EqualTo(separate.GetProperty("error").GetRawText()));
        var expectedState = stop is "agent" or "handoff" ? "waiting_agent" : Exit == 5 ? "failed" : "completed";
        Assert.That(combined.GetProperty("status").GetString(), Is.EqualTo(expectedState));
        var expectedEffects = stop is "chain" or "handoff" ? new[] { "first", "second", "first", "second" }
            : stop == "transition-limit" ? ["first", "first"] : Array.Empty<string>();
        Assert.That(File.Exists(Effects) ? File.ReadAllLines(Effects) : [], Is.EqualTo(expectedEffects));
        cli.Call("status", separateId);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo(expectedState));
        if (expectedState != "waiting_agent") return;
        Assert.That(combined.GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo("Review Grüße\u001b[31m and choose."));
        Assert.That(combined.GetProperty("request").GetProperty("context")[0].GetProperty("value").GetProperty("greeting").GetString(), Is.EqualTo("Grüße"));
        cli.Call("complete", combinedId, "--request", combined.GetProperty("request").GetProperty("id").GetString()!, "--outcome", "approved");
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
        Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("done"));
        Assert.That(File.Exists(Effects) ? File.ReadAllLines(Effects) : [], Is.EqualTo(expectedEffects));
    }

    public void Dispose() { safety?.Dispose(); cli.Dispose(); }
}
