using System.Text.Json;
using System.Text.RegularExpressions;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ProjectWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string Root => Path.Combine(cli.DirectoryPath, ".merstride", "runs");
    private string runId = "";
    private JsonElement response;
    private string original = "";
    private string checkedPath = "";
    private bool verified;

    private JsonElement Json(params string[] args)
    {
        cli.Raw([.. args, "--json"]);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
        return JsonDocument.Parse(cli.Stdout).RootElement.Clone();
    }

    internal void Execute(string step)
    {
        const string locationStep = "I inspect project storage from ";
        if (step.StartsWith(locationStep, StringComparison.Ordinal)) { Discovery(step[locationStep.Length..]); return; }
        const string defectStep = "I select project storage with ";
        if (step.StartsWith(defectStep, StringComparison.Ordinal)) { Defect(step[defectStep.Length..]); return; }
        var error = Regex.Match(step, "^selection reports ([A-Z_]+) with exit ([0-9]+) without changing stored content$");
        if (error.Success)
        {
            Assert.That(cli.ExitCode, Is.EqualTo(int.Parse(error.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)), cli.Stdout + cli.Stderr);
            Assert.That(cli.ErrorCode, Is.EqualTo(error.Groups[1].Value));
            if (checkedPath.Length > 0) Assert.That(File.ReadAllText(checkedPath), Is.EqualTo(original));
            return;
        }
        switch (step)
        {
            case "an isolated project with a workflow":
                Directory.CreateDirectory(Path.Combine(cli.DirectoryPath, ".git"));
                File.WriteAllText(cli.FilePath, FeatureTests.Workflow); break;
            case "I start and inspect it without workspace state or run arguments":
                var start = Json("start", cli.FilePath);
                runId = start.GetProperty("runId").GetString()!;
                cli.Raw("status"); break;
            case "the ready run uses the project workspace and state root":
                Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                Assert.That(cli.Stdout, Does.Contain("Run: " + runId).And.Contain("Status: ready").And.Contain("State root: " + Root));
                Assert.That(Json("status", runId).GetProperty("lastActivation").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, ".merstride", ".gitignore")), Does.Contain("/runs/")); break;
            case "I perform a looping handoff with short run and request prefixes": Loop(); break;
            case "retries return the original receipt without completing a newer request":
            case "ambiguous selections fail and full IDs still select the original resources":
            case "storage discovery preserves the selected root workspace and saved IDs": Assert.That(verified, Is.True); break;
            case "I create two runs and attempt an implicit selection":
                var first = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                Json("advance", first);
                var second = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
                // P19 remembers successful starts. A legacy root without context still
                // fails closed instead of inferring a newest run.
                File.Delete(Path.Combine(Root, "context.json"));
                original = File.ReadAllText(Path.Combine(Root, first, "state.json"));
                checkedPath = Path.Combine(Root, second, "state.json");
                var secondState = File.ReadAllText(checkedPath);
                foreach (var command in new[] { "status", "advance", "resolve", "complete", "logs" })
                {
                    string[] extra = command switch { "resolve" => ["--retry"], "complete" => ["--request", "q_12345678", "--outcome", "approved"], "logs" => ["--activation", "a_12345678", "--stream", "stdout"], _ => [] };
                    cli.Raw([command, .. extra, "--json"]);
                    Error(command == "complete" ? "INVALID_REQUEST" : "AMBIGUOUS_RUN", 4);
                }
                Assert.That(File.ReadAllText(checkedPath), Is.EqualTo(secondState));
                checkedPath = Path.Combine(Root, first, "state.json"); break;
            case "selection fails without changing either run":
                Assert.That(File.ReadAllText(checkedPath), Is.EqualTo(original)); break;
            case "stored runs and requests share short prefixes": Collisions(); break;
            case "I start with existing ignore and configuration content":
                Directory.CreateDirectory(Path.Combine(cli.DirectoryPath, ".merstride"));
                File.WriteAllText(Path.Combine(cli.DirectoryPath, ".gitignore"), "# project rules\nkeep-local/\n");
                File.WriteAllText(Path.Combine(cli.DirectoryPath, ".merstride", "config.json"), "arbitrary existing configuration\n");
                File.WriteAllText(Path.Combine(cli.DirectoryPath, ".merstride", ".gitignore"), "# user rule\r\nprivate/");
                Json("start", cli.FilePath); Json("start", cli.FilePath); break;
            case "only a narrow state ignore rule is appended and no content is replaced":
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, ".gitignore")), Is.EqualTo("# project rules\nkeep-local/\n"));
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, ".merstride", "config.json")), Is.EqualTo("arbitrary existing configuration\n"));
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, ".merstride", ".gitignore")), Is.EqualTo("# user rule\r\nprivate/\n/runs/\n")); break;
            case "I start from a subdirectory and advance a process using defaults": Process(); break;
            case "the process effect and selected logs belong to the project root": Assert.That(verified, Is.True); break;
            case "I read all offline discovery commands with a conflicting state marker":
                checkedPath = Path.Combine(cli.DirectoryPath, ".merstride"); original = "preserve conflicting marker";
                File.WriteAllText(checkedPath, original);
                foreach (var command in new[] { "--help", "version", "guide", "creation-guide" })
                {
                    cli.Raw(command, "--state-dir", "not-absolute");
                    Assert.That(cli.ExitCode, Is.Zero, cli.Stderr);
                    Assert.That(cli.Stdout, Is.Not.Empty);
                }
                cli.Raw("validate", cli.FilePath); Assert.That(cli.ExitCode, Is.Zero, cli.Stderr); break;
            case "discovery succeeds without modifying the marker":
                Assert.That(File.ReadAllText(checkedPath), Is.EqualTo(original)); break;
            case "I start with an existing negated run ignore rule":
                Directory.CreateDirectory(Path.GetDirectoryName(Root)!);
                checkedPath = Path.Combine(Path.GetDirectoryName(Root)!, ".gitignore");
                original = "/runs/\n!/runs/\n# retained comment\n";
                File.WriteAllText(checkedPath, original);
                Json("start", cli.FilePath); break;
            case "existing ignore bytes are preserved and the final rule ignores runs":
                Assert.That(File.ReadAllText(checkedPath), Is.EqualTo(original + "/runs/\n")); break;
            default: throw new AssertionException("Undefined required project step: " + step);
        }
    }

    private void Process()
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: project
            entry: work
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [effect, project]
                timeoutSeconds: 5
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: project
            work["Work"]
            done["Done"]
            work -->|exit_0| done
            ```
            """);
        var child = Path.Combine(cli.DirectoryPath, "sub directory with spaces");
        Directory.CreateDirectory(child); cli.WorkingDirectory = child;
        Json("start", cli.FilePath);
        Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.False);
        cli.ExpectedChildren = 1; var result = Json("advance"); cli.ExpectedChildren = 0;
        Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("completed"));
        Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "effects.txt")), Is.EqualTo("project\n"));
        Assert.That(File.Exists(Path.Combine(child, "effects.txt")), Is.False);
        var activation = result.GetProperty("lastActivation").GetProperty("id").GetString()!;
        var log = Json("logs", "--activation", activation, "--stream", "stdout");
        Assert.That(log.GetProperty("runId").GetString(), Is.EqualTo(result.GetProperty("runId").GetString()));
        Assert.That(log.GetProperty("error").ValueKind, Is.EqualTo(JsonValueKind.Null)); verified = true;
    }

    private void Error(string code, int exit)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(exit), cli.Stdout + cli.Stderr);
        Assert.That(cli.ErrorCode, Is.EqualTo(code));
    }

    private void AgentWorkflow() => File.WriteAllText(cli.FilePath, """
        ```yaml merstride
        schemaVersion: 1
        id: handoff
        entry: review
        nodes:
          review:
            kind: agent
            prompt: Review the project.
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
        """);

    private void Loop()
    {
        AgentWorkflow();
        runId = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
        var first = Json("advance", runId).GetProperty("request").GetProperty("id").GetString()!;
        var accepted = Json("complete", runId, "--request", first, "--outcome", "again");
        Assert.That(accepted.GetProperty("status").GetString(), Is.EqualTo("ready"));
        var second = Json("advance").GetProperty("request").GetProperty("id").GetString()!;
        Assert.That(second, Is.Not.EqualTo(first));
        var retry = Json("complete", "--request", first, "--outcome", "again");
        Assert.That(retry.GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(second));
        Assert.That(retry.GetProperty("receipt").GetRawText(), Is.EqualTo(accepted.GetProperty("receipt").GetRawText()));
        cli.Raw("complete", "--request", first, "--outcome", "approved", "--json"); Error("COMPLETION_CONFLICT", 4);
        Assert.That(Json("status").GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(second));
        Json("complete", "--request", second, "--outcome", "approved");
        Assert.That(Json("status").GetProperty("status").GetString(), Is.EqualTo("ready"));
        Assert.That(Json("advance").GetProperty("status").GetString(), Is.EqualTo("completed"));
        verified = true;
    }

    private void Collisions()
    {
        AgentWorkflow();
        var real = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
        Json("advance", real);
        var source = File.ReadAllText(Path.Combine(Root, real, "state.json"));
        var first = "r_12345678" + new string('a', 24);
        var second = "r_12345678" + new string('b', 24);
        foreach (var id in new[] { first, second })
        {
            Directory.CreateDirectory(Path.Combine(Root, id));
            File.WriteAllText(Path.Combine(Root, id, "state.json"), source.Replace(real, id));
        }
        cli.Raw("advance", "r_12345678", "--json"); Error("AMBIGUOUS_RUN", 4);
        Assert.That(File.ReadAllText(Path.Combine(Root, first, "state.json")), Is.EqualTo(source.Replace(real, first)));
        Assert.That(Json("status", first).GetProperty("runId").GetString(), Is.EqualTo(first));
        // Storage fixtures give two historical/current IDs a controlled shared prefix.
        var request = Json("status", real).GetProperty("request").GetProperty("id").GetString()!;
        Json("complete", real, "--request", request, "--outcome", "again");
        var current = Json("advance", real).GetProperty("request").GetProperty("id").GetString()!;
        var oldId = "q_12345678" + new string('a', 24);
        var newId = "q_12345678" + new string('b', 24);
        var path = Path.Combine(Root, real, "state.json");
        var fixture = File.ReadAllText(path).Replace(request, oldId).Replace(current, newId);
        File.WriteAllText(path, fixture);
        cli.Raw("complete", real, "--request", "q_12345678", "--outcome", "approved", "--json"); Error("AMBIGUOUS_REQUEST", 4);
        Assert.That(File.ReadAllText(path), Is.EqualTo(fixture));
        Assert.That(Json("complete", real, "--request", oldId, "--outcome", "again").GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(newId));
        Json("complete", real, "--request", newId, "--outcome", "approved"); verified = true;
    }

    private void Discovery(string kind)
    {
        var child = Path.Combine(cli.DirectoryPath, "sub directory with spaces");
        Directory.CreateDirectory(child);
        var expected = Root;
        var workspace = cli.DirectoryPath;
        if (kind == "git-file") { Directory.Delete(Path.Combine(cli.DirectoryPath, ".git")); File.WriteAllText(Path.Combine(cli.DirectoryPath, ".git"), "gitdir: elsewhere"); }
        if (kind is "outside-project" or "legacy-directory") Directory.Delete(Path.Combine(cli.DirectoryPath, ".git"));
        if (kind == "legacy-directory") Directory.CreateDirectory(Path.Combine(cli.DirectoryPath, ".merstride", "runs"));
        if (kind == "nested-git")
        {
            Json("start", cli.FilePath);
            Directory.CreateDirectory(Path.Combine(child, ".git"));
            cli.WorkingDirectory = child; workspace = child; expected = Path.Combine(child, ".merstride", "runs");
        }
        if (kind == "explicit-root")
        {
            expected = Path.Combine(child, "explicit state");
            runId = Json("start", cli.FilePath, "--workspace", child, "--state-dir", expected).GetProperty("runId").GetString()!;
            cli.WorkingDirectory = child;
            response = Json("status", runId, "--state-dir", expected);
            Assert.That(Directory.Exists(Path.Combine(cli.DirectoryPath, ".merstride")), Is.False);
        }
        else
        {
            runId = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
            if (kind is "subdirectory" or "legacy-directory" or "git-file") cli.WorkingDirectory = child;
            response = Json("status", runId);
            Assert.That(response.GetProperty("stateRoot").GetString(), Is.EqualTo(expected));
            var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(expected, runId, "state.json"))).RootElement;
            Assert.That(state.GetProperty("workspace").GetString(), Is.EqualTo(workspace));
        }
        Assert.That(response.GetProperty("runId").GetString(), Is.EqualTo(runId));
        Assert.That(response.GetProperty("stateRoot").GetString(), Is.EqualTo(expected)); verified = true;
    }

    private void Defect(string kind)
    {
        string[] args = ["status"];
        if (kind is "corrupt-run" or "missing-snapshot" or "request-path" or "short-request" or "stale-request")
        {
            AgentWorkflow(); runId = Json("start", cli.FilePath).GetProperty("runId").GetString()!;
            Json("advance"); checkedPath = Path.Combine(Root, runId, "state.json");
            if (kind == "corrupt-run") File.WriteAllText(checkedPath, "{corrupt");
            if (kind == "missing-snapshot") { File.Delete(checkedPath); checkedPath = ""; args = ["status", runId]; }
            if (kind.Contains("request", StringComparison.Ordinal))
                args = ["complete", "--request", kind switch { "request-path" => "../outside", "short-request" => "q_123", _ => "q_00000000" }, "--outcome", "approved"];
        }
        if (kind == "marker-file") { checkedPath = Path.Combine(cli.DirectoryPath, ".merstride"); File.WriteAllText(checkedPath, "keep marker bytes"); }
        if (kind == "root-file") { Directory.CreateDirectory(Path.GetDirectoryName(Root)!); File.WriteAllText(Root, "keep root bytes"); checkedPath = Root; }
        if (kind == "missing-run") args = ["advance", "r_" + new string('0', 32)];
        if (kind == "run-path") args = ["advance", "../other-run"];
        if (kind == "short-run") args = ["advance", "r_123"];
        if (kind == "stale-prefix") args = ["advance", "r_00000000"];
        if (checkedPath.Length > 0) original = File.ReadAllText(checkedPath);
        cli.Raw([.. args, "--json"]);
    }

    public void Dispose() => cli.Dispose();
}
