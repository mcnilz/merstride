using NUnit.Framework;

namespace Merstride.Acceptance;

public class AuthoringTests
{
    [Test]
    public void IgnoresUnclosedOrdinaryDiagramAfterExecutablePair()
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow + "\n```mermaid\nflowchart TD\nordinary --> example\n");
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
    }

    [TestCase("end")]
    [TestCase("subgraph")]
    [TestCase("style")]
    [TestCase("class")]
    [TestCase("flowchart")]
    [TestCase("graph")]
    [TestCase("interpolate")]
    public void RejectsNodeIdentifiersReservedByOfficialMermaid(string id)
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace("done", id));
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        Assert.That(cli.Response.GetProperty("error").GetProperty("message").GetString(), Does.Contain("reserved by Mermaid"));
    }

    [Test]
    public void LocatesInvalidSelectorValue()
    {
        using var cli = new CliWorld();
        var source = AuthoringWorld.TaskWorkflow.Replace("    kind: agent", "    kind: agent\n    context: [input, nodes.work.data]");
        File.WriteAllText(cli.FilePath, source);
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        var lines = source.Split('\n');
        var line = Array.FindIndex(lines, x => x.Contains("nodes.work.data", StringComparison.Ordinal));
        var location = cli.Response.GetProperty("error").GetProperty("location");
        Assert.That(location.GetProperty("line").GetInt32(), Is.EqualTo(line + 1));
        Assert.That(location.GetProperty("column").GetInt32(), Is.EqualTo(lines[line].IndexOf("nodes.work.data", StringComparison.Ordinal) + 1));
    }

    [Test]
    public void LocatesYamlValueInOriginalMarkdown()
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, "Prose\n\n" + FeatureTests.Workflow.Replace("schemaVersion: 1", "schemaVersion: 2"));
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        var location = cli.Response.GetProperty("error").GetProperty("location");
        Assert.That(location.GetProperty("line").GetInt32(), Is.EqualTo(4));
        Assert.That(location.GetProperty("column").GetInt32(), Is.EqualTo(16));
    }

    [Test]
    public void LocatesIndentedUnsupportedGraphStatement()
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace("    done[\"Finish\"]", "    click done callback"));
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        var location = cli.Response.GetProperty("error").GetProperty("location");
        Assert.That(location.GetProperty("line").GetInt32(), Is.EqualTo(13));
        Assert.That(location.GetProperty("column").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public void LocatesUnlabeledEdgeWarning()
    {
        using var cli = new CliWorld();
        var source = AuthoringWorld.TaskWorkflow.Replace("work -->|exit_0| review", "  work --> review");
        File.WriteAllText(cli.FilePath, source);
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
        var warning = cli.Response.GetProperty("diagnostics")[0];
        Assert.That(warning.GetProperty("severity").GetString(), Is.EqualTo("warning"));
        Assert.That(warning.GetProperty("location").GetProperty("line").GetInt32(),
            Is.EqualTo(Array.FindIndex(source.Split('\n'), line => line.Contains("work --> review", StringComparison.Ordinal)) + 1));
        Assert.That(warning.GetProperty("location").GetProperty("column").GetInt32(), Is.EqualTo(3));
    }

    [TestCase("prompt: |\n      Review the changes.\n      Keep the final newline.", "prompt: true")]
    [TestCase("args: [arguments, \"123\", \"true\", \"null\"]", "args: [arguments, false]")]
    [TestCase("args: [arguments, \"123\", \"true\", \"null\"]", "args: [arguments, 0x10]")]
    [TestCase("args: [arguments, \"123\", \"true\", \"null\"]", "args: [arguments, 1e3]")]
    [TestCase("args: [arguments, \"123\", \"true\", \"null\"]", "args: [arguments, null]")]
    [TestCase("args: [arguments, \"123\", \"true\", \"null\"]", "args: [arguments, .inf]")]
    [TestCase("args: [arguments, \"123\", \"true\", \"null\"]", "args: [arguments, .nan]")]
    [TestCase("limits: missing", "limits: missing")]
    [TestCase("entry: work", "entry: work\nlimits: {maxTransitions: 1, maxTransitions: 2}")]
    [TestCase("entry: work", "entry: work\nlimits: {unknown: 1}")]
    [TestCase("entry: work", "entry: work\ntrue: value")]
    [TestCase("entry: work", "entry: work\n? [complex, key]\n: value")]
    [TestCase("entry: work", "entry: &anchor work")]
    [TestCase("work[\"Work\"]", "work[\"<b>HTML</b>\"]")]
    [TestCase("work[\"Work\"]", "work[\"Back\\slash\"]")]
    [TestCase("work[\"Work\"]", "work((Circle))")]
    [TestCase("work -->|exit_0| review", "work --> review --> done")]
    [TestCase("work -->|exit_0| review", "work -.-> review")]
    [TestCase("review -->|approved| done", "review --> done")]
    public void RejectsUnsupportedTypesAndStatements(string original, string replacement)
    {
        using var cli = new CliWorld();
        var source = AuthoringWorld.TaskWorkflow;
        source = original == "limits: missing" ? source.Replace("entry: work", "entry: work\nlimits: []") : source.Replace(original, replacement);
        File.WriteAllText(cli.FilePath, source);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        Assert.That(Directory.Exists(cli.State), Is.False);
    }

    [TestCase("~~~")]
    [TestCase("    ```")]
    [TestCase("> ```")]
    public void IgnoresUnsupportedExecutableContainers(string fence)
    {
        using var cli = new CliWorld();
        var example = FeatureTests.Workflow.Replace("smoke", "hidden");
        example = fence switch {
            "~~~" => example.Replace("```", "~~~"),
            "    ```" => string.Join('\n', example.Split('\n').Select(line => "    " + line)),
            _ => string.Join('\n', example.Split('\n').Select(line => "> " + line)) };
        File.WriteAllText(cli.FilePath, example + "\n" + FeatureTests.Workflow);
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("workflowId").GetString(), Is.EqualTo("smoke"));
    }

    [TestCase("duplicate metadata")]
    [TestCase("duplicate diagram")]
    [TestCase("missing diagram")]
    [TestCase("missing metadata")]
    [TestCase("conflicting marker")]
    [TestCase("unclosed fence")]
    [TestCase("unknown selection")]
    public void RejectsMissingOrConflictingPairs(string defect)
    {
        using var cli = new CliWorld();
        var source = FeatureTests.Workflow;
        var boundary = source.IndexOf("```mermaid", StringComparison.Ordinal);
        source = defect switch {
            "duplicate metadata" => source[..boundary] + source,
            "duplicate diagram" => source + "\n" + source[boundary..],
            "missing diagram" => source[..boundary],
            "missing metadata" => source[boundary..],
            "conflicting marker" => source.Replace("%% merstride: smoke", "%% merstride: smoke\n%% merstride: other"),
            "unclosed fence" => source[..^3],
            _ => source };
        File.WriteAllText(cli.FilePath, source);
        cli.Call("validate", cli.FilePath, "--workflow", defect == "unknown selection" ? "absent" : "smoke");
        Assert.That(cli.ExitCode, Is.EqualTo(3));
    }

    [Test]
    public void AcceptsMaximumDocumentAndGraphEnvelope()
    {
        using var cli = new CliWorld();
        var source = AuthoringWorld.SizedGraph(100, 250);
        source += new string(' ', 1048576 - System.Text.Encoding.UTF8.GetByteCount(source));
        File.WriteAllText(cli.FilePath, source);
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
    }

    [Test]
    public void RejectsMalformedUtf8Document()
    {
        using var cli = new CliWorld();
        File.WriteAllBytes(cli.FilePath, [0xff, 0xfe]);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.EqualTo(3));
        Assert.That(Directory.Exists(cli.State), Is.False);
    }

    [TestCase("schemaVersion: 1", "schemaVersion: 0x1")]
    [TestCase("schemaVersion: 1", "schemaVersion: 01")]
    [TestCase("schemaVersion: 1", "schemaVersion: +1")]
    [TestCase("entry: done", "limits: {maxTransitions: 0o12}\nentry: done")]
    public void AcceptsCoreIntegerSpellings(string original, string replacement)
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace(original, replacement));
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
    }

    [TestCase("flowchart TD", "flowchart   LR")]
    [TestCase("done[\"Finish\"]", "  done[\"Finish #quot; &amp; text\"]")]
    public void AllowsWhitespaceOutsideGraphLabels(string original, string replacement)
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace(original, replacement));
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
    }

    [TestCase("2026-11-15")]
    [TestCase("123abc")]
    [TestCase("1.2.3")]
    [TestCase("yes")]
    public void CoreProfilePreservesPlainStrings(string value)
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow.Replace("    kind: end\n    result: success",
            $"    kind: agent\n    prompt: {value}")
            .Replace("done[\"Finish\"]", "done[\"Finish\"]\nfinish[\"End\"]\ndone -->|ok| finish")
            .Replace("```\n```mermaid", "  finish:\n    kind: end\n    result: success\n```\n```mermaid"));
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        cli.Call("advance", cli.Response.GetProperty("runId").GetString()!);
        Assert.That(cli.Response.GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo(value));
    }

    [Test]
    public void SelectsWorkflowRegardlessOfPairOrder()
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow + "\n" + FeatureTests.Workflow.Replace("smoke", "second"));
        cli.Call("validate", cli.FilePath, "--workflow", "second");
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("workflowId").GetString(), Is.EqualTo("second"));
    }
}
