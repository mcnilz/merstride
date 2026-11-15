using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Merstride.Acceptance;

internal sealed class AuthoringWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "";
    private (int Line, int Column)? expectedLocation;
    private string Source { get => File.ReadAllText(cli.FilePath); set => File.WriteAllText(cli.FilePath, value); }

    internal static string TaskWorkflow => $$"""
        ```yaml merstride
        schemaVersion: 1
        id: authoring
        entry: work
        nodes:
          work:
            kind: process
            command: {{JsonSerializer.Serialize(Path.Combine(FeatureTests.Root, "artifacts/fixture/test-process.exe"))}}
            args: [arguments, "123", "true", "null"]
            timeoutSeconds: 5
          review:
            kind: agent
            prompt: |
              Review the changes.
              Keep the final newline.
          done:
            kind: end
            result: success
        ```
        ```mermaid
        flowchart TD
        %% merstride: authoring
        work["Work"]
        review["Review"]
        done["Finish"]
        work -->|exit_0| review
        review -->|approved| done
        ```
        """;

    internal static string Defect(string defect)
    {
        var source = TaskWorkflow;
        return defect switch
        {
            "a duplicate node key" => source.Replace("  work:\n", "  done:\n    kind: end\n    result: success\n  work:\n"),
            "an unknown node field" => source.Replace("    kind: process", "    kind: process\n    surprise: x"),
            "a YAML alias" => source.Replace("entry: work", "entry: *missing"),
            "a YAML merge key" => source.Replace("entry: work", "entry: work\n<<: {}"),
            "an explicit YAML tag" => source.Replace("entry: work", "entry: !!str work"),
            "a second YAML document" => source.Replace("```\n```mermaid", "---\nother: value\n```\n```mermaid"),
            "a numeric argument without quotes" => source.Replace("\"123\"", "123"),
            "an unsupported schema version" => source.Replace("schemaVersion: 1", "schemaVersion: 2"),
            "a missing metadata node" => source.Replace("  done:\n    kind: end\n    result: success\n", ""),
            "an unmatched workflow marker" => source.Replace("%% merstride: authoring", "%% merstride: missing"),
            "an invalid context selector" => source.Replace("    kind: agent", "    kind: agent\n    context: [nodes.work.data]"),
            "an undeclared edge target" => source.Replace("work -->|exit_0| review", "work -->|exit_0| missing"),
            "a duplicate node declaration" => source.Replace("work[\"Work\"]", "work[\"Work\"]\nwork[\"Duplicate\"]"),
            "duplicate outgoing outcome labels" => source.Replace("work -->|exit_0| review", "work -->|exit_0| review\nwork -->|exit_0| done"),
            "mixed labeled and unlabeled edges" => source.Replace("work -->|exit_0| review", "work -->|exit_0| review\nwork --> done"),
            "an agent default edge" => source.Replace("review -->|approved| done", "review -->|default| done"),
            "an end node with an outgoing edge" => source.Replace("review -->|approved| done", "review -->|approved| done\ndone --> work"),
            "an unreachable node" => source.Replace("work -->|exit_0| review", "work -->|exit_0| done"),
            "a node with no path to an end" => source.Replace("review -->|approved| done", "review -->|approved| review\nwork -->|default| done"),
            "a subgraph statement" => source.Replace("work[\"Work\"]", "subgraph group\nwork[\"Work\"]\nend"),
            "an implicit node declaration" => source.Replace("review[\"Review\"]\n", ""),
            "a click directive" => source.Replace("work[\"Work\"]", "work[\"Work\"]\nclick work callback"),
            _ => throw new AssertionException($"Undefined fixture defect: {defect}")
        };
    }

    internal static string SizedGraph(int nodes, int edges)
    {
        var yaml = new StringBuilder("```yaml merstride\nschemaVersion: 1\nid: sized\nentry: n0\nnodes:\n");
        var graph = new StringBuilder("```mermaid\nflowchart TD\n%% merstride: sized\n");
        for (var i = 0; i < nodes; i++)
        {
            yaml.Append($"  n{i}:\n    kind: {(i == nodes - 1 ? "end\n    result: success" : "process\n    command: unavailable\n    args: []\n    timeoutSeconds: 1")}\n");
            graph.Append($"n{i}[\"Node {i}\"]\n");
        }
        for (var i = 0; i < edges; i++)
            graph.Append($"n{i % (nodes - 1)} -->|route_{i}| n{Math.Min(i % (nodes - 1) + 1, nodes - 1)}\n");
        return yaml.Append("```\n").Append(graph).Append("```\n").ToString();
    }

    public void Execute(string step)
    {
        switch (step)
        {
            case "a document containing two valid workflow pairs \"first\" and \"second\" and an unmarked Mermaid diagram":
            case "a document containing two valid workflow pairs":
                Source = "Prose\n```mermaid\nflowchart TD\nordinary --> diagram\n```\n```yaml\nignored: true\n```\n" +
                    FeatureTests.Workflow.Replace("smoke", "first") + "\n" + FeatureTests.Workflow.Replace("smoke", "second"); break;
            case "I validate the document selecting \"second\"": cli.Call("validate", cli.FilePath, "--workflow", "second"); break;
            case "validation succeeds for \"second\"":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("workflowId").GetString(), Is.EqualTo("second")); break;
            case "I validate without selecting a workflow":
            case "I validate the document": cli.Call("validate", cli.FilePath); break;
            case "validation fails with a source diagnostic":
            case "validation fails before execution":
                Assert.That(cli.ExitCode, Is.EqualTo(3)); Diagnostic(); NoEffects(); break;
            case "a diagnostic identifies the offending source line and column": Diagnostic(); break;
            case "the CLI exits with code 3": Assert.That(cli.ExitCode, Is.EqualTo(3)); break;
            case "no child process has been launched":
            case "no run has been created":
            case "no run or process side effect has been created": NoEffects(); break;
            case "I start that workflow": cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath); break;
            case "an agent workflow with a literal YAML block prompt and quoted numeric string arguments in a process node": Source = TaskWorkflow; break;
            case "I validate and advance the workflow to its agent node":
                cli.Call("validate", cli.FilePath); Assert.That(cli.ExitCode, Is.Zero);
                Start(); Advance();
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent")); break;
            case "the prompt equals the decoded YAML string including its final newline":
                Assert.That(cli.Response.GetProperty("request").GetProperty("prompt").GetString(), Is.EqualTo("Review the changes.\nKeep the final newline.\n")); break;
            case "validation preserves every argument as a string":
                Assert.That(JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(cli.DirectoryPath, "arguments.json"))),
                    Is.EqualTo(new[] { "123", "true", "null" })); break;
            case "a document with a valid workflow and a four-backtick fenced example containing three-backtick workflow fences":
                Source = FeatureTests.Workflow + "\n````markdown\n" + TaskWorkflow + "\n````\n"; break;
            case "only the outer document's valid workflow is selected":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("workflowId").GetString(), Is.EqualTo("smoke")); break;
            case "a started workflow whose next process writes \"original\" to a marker":
                Source = TaskWorkflow.Replace("args: [arguments, \"123\", \"true\", \"null\"]", "args: [effect, original]"); Start(); break;
            case "I change its source metadata to write \"modified\"": Source = Source.Replace("[effect, original]", "[effect, modified]"); break;
            case "I advance the existing run": Advance(); break;
            case "the marker contains \"original\"":
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("original\n")); break;
            default:
                if (step.StartsWith("an otherwise valid workflow with ", StringComparison.Ordinal))
                {
                    Source = Defect(step[33..]);
                    if (step[33..] == "an invalid context selector")
                    {
                        var lines = Source.Split('\n');
                        var index = Array.FindIndex(lines, line => line.Contains("nodes.work.data", StringComparison.Ordinal));
                        expectedLocation = (index + 1, lines[index].IndexOf("nodes.work.data", StringComparison.Ordinal) + 1);
                    }
                }
                else if (step.StartsWith("a workflow with ", StringComparison.Ordinal))
                    Source = step[16..] switch {
                        "101 nodes" => SizedGraph(101, 100),
                        "251 edges" => SizedGraph(3, 251),
                        "1048577 UTF-8 bytes" => FeatureTests.Workflow + new string(' ', 1048577 - Encoding.UTF8.GetByteCount(FeatureTests.Workflow)),
                        _ => throw new AssertionException($"Undefined size fixture: {step}") };
                else throw new AssertionException($"Undefined required step: {step}");
                break;
        }
    }

    private void Start()
    {
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }
    private void Advance()
    {
        cli.ExpectedChildren = 1;
        cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        cli.ExpectedChildren = 0;
    }
    private void Diagnostic()
    {
        var diagnostic = cli.Response.GetProperty("diagnostics")[0];
        Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo("error"));
        Assert.That(diagnostic.GetProperty("location").GetProperty("line").GetInt32(), Is.Positive);
        Assert.That(diagnostic.GetProperty("location").GetProperty("column").GetInt32(), Is.Positive);
        if (expectedLocation is { } expected)
        {
            Assert.That(diagnostic.GetProperty("location").GetProperty("line").GetInt32(), Is.EqualTo(expected.Line));
            Assert.That(diagnostic.GetProperty("location").GetProperty("column").GetInt32(), Is.EqualTo(expected.Column));
        }
    }
    private void NoEffects() => Assert.That(Directory.GetFileSystemEntries(cli.DirectoryPath), Is.EquivalentTo(new[] { cli.FilePath }));
    public void Dispose() => cli.Dispose();
}
