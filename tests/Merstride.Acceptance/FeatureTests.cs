using System.Diagnostics;
using System.Text.Json;
using Gherkin;
using Gherkin.Ast;
using NUnit.Framework;

namespace Merstride.Acceptance;

public sealed record FeatureCase(string File, string Name, long Line, string[] Tags, string[] Steps, bool Implemented)
{
    public override string ToString() => $"{Path.GetFileName(File)}:{Line} {Name}";
}

public class FeatureTests
{
    private readonly Dictionary<string, object> results = new(StringComparer.Ordinal);
    internal static readonly string Root = FindRoot();
    internal const string Workflow = """
        ```yaml merstride
        schemaVersion: 1
        id: smoke
        entry: done
        nodes:
          done:
            kind: end
            result: success
        ```
        ```mermaid
        flowchart TD
            %% merstride: smoke
            done["Finish"]
        ```
        """;

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "features"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Cannot locate feature files.");
    }

    public static IEnumerable<FeatureCase> Cases()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "features"), "*.feature").Order())
        {
            var feature = new Parser().Parse(file).Feature;
            var background = feature.Children.OfType<Background>().SelectMany(b => b.Steps).Select(s => s.Text);
            foreach (var scenario in feature.Children.OfType<Scenario>())
            {
                var tags = feature.Tags.Concat(scenario.Tags).Select(t => t.Name).ToArray();
                var steps = background.Concat(scenario.Steps.Select(s => s.Text)).ToArray();
                if (!scenario.Examples.Any())
                    yield return new(file, scenario.Name, scenario.Location.Line, tags, steps, IsImplemented(tags));
                foreach (var examples in scenario.Examples)
                foreach (var row in examples.TableBody)
                {
                    var values = examples.TableHeader.Cells.Zip(row.Cells).ToDictionary(x => x.First.Value, x => x.Second.Value);
                    string Expand(string text) => values.Aggregate(text, (s, p) => s.Replace($"<{p.Key}>", p.Value));
                    var rowTags = tags.Concat(examples.Tags.Select(t => t.Name)).ToArray();
                    yield return new(file, Expand(scenario.Name) + " [" + string.Join(", ", values.Values) + "]",
                        row.Location.Line, rowTags, steps.Select(Expand).ToArray(), IsImplemented(rowTags));
                }
            }
        }
    }

    private static bool IsImplemented(string[] tags) => tags.Any(t => t is "@P01" or "@P02" or "@P03" or "@P04" or "@P05" or "@P06" or "@P07");

    public static IEnumerable<TestCaseData> RequiredCases() => Cases().Where(c => c.Implemented)
        .Select(c => new TestCaseData(c).SetName(c.ToString()).SetProperty("Requirements", string.Join(' ', c.Tags))
            .SetCategory(c.Steps.Any(s => s.StartsWith("the published native binary on a clean", StringComparison.Ordinal))
                ? "ReleaseEnvironment" : "LocalGate"));

    [TestCaseSource(nameof(RequiredCases))]
    public void ExecuteFeature(FeatureCase scenario)
    {
        try { Execute(scenario); results[scenario.ToString()] = new { status = "passed" }; }
        catch (Exception ex) { results[scenario.ToString()] = new { status = "failed", error = ex.Message }; throw; }
    }

    [OneTimeTearDown]
    public void ReportScenarios()
    {
        var report = Cases().Select(c => new {
            feature = Path.GetRelativePath(Root, c.File), scenario = c.Name, line = c.Line, tags = c.Tags,
            platform = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            evidenceScope = c.Name == "Accept a contribution without publication privileges" ? "local workflow policy; hosted execution unavailable" : "local execution",
            result = results.GetValueOrDefault(c.ToString(), new { status = c.Implemented ? "required; not run (qualification incomplete)" : "not yet implemented" })
        });
        Directory.CreateDirectory(Path.Combine(Root, "artifacts/tests"));
        File.WriteAllText(Path.Combine(Root, "artifacts/tests/scenarios.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Execute(FeatureCase scenario)
    {
        if (scenario.Tags.Contains("@P07") && !scenario.Tags.Contains("@P01"))
        {
            using var release = new ReleaseWorld();
            foreach (var step in scenario.Steps) release.Execute(step);
            return;
        }
        if (scenario.Tags.Contains("@P06"))
        {
            using var recovery = new RecoveryWorld();
            foreach (var step in scenario.Steps) recovery.Execute(step);
            return;
        }
        if (scenario.Tags.Contains("@P05"))
        {
            using var limits = new LimitWorld();
            foreach (var step in scenario.Steps) limits.Execute(step);
            return;
        }
        if (scenario.Tags.Contains("@P04"))
        {
            using var authoring = new AuthoringWorld();
            foreach (var step in scenario.Steps) authoring.Execute(step);
            return;
        }
        if (scenario.Tags.Contains("@P03"))
        {
            using var agent = new AgentWorld();
            foreach (var step in scenario.Steps) agent.Execute(step);
            return;
        }
        if (scenario.Tags.Contains("@build"))
        {
            using var build = new BuildWorld();
            foreach (var step in scenario.Steps) build.Execute(step);
            return;
        }
        if (scenario.Tags.Contains("@P02"))
        {
            using var processWorld = new ProcessWorld();
            foreach (var step in scenario.Steps) processWorld.Execute(step);
            return;
        }
        using var world = new CliWorld();
        foreach (var step in scenario.Steps) world.Execute(step);
    }
}

internal sealed class CliWorld : IDisposable
{
    internal readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "merstride-tests-" + Guid.NewGuid().ToString("N"));
    internal string State => Path.Combine(DirectoryPath, "state");
    internal string FilePath => Path.Combine(DirectoryPath, "workflow.md");
    internal string? WorkingDirectory;
    private string runId = "";
    internal JsonElement Response;
    internal int ExitCode;
    internal int ExpectedChildren;
    internal string Stdout = "", Stderr = "";

    public CliWorld() => Directory.CreateDirectory(DirectoryPath);

    internal void Call(params string[] args)
    {
        var binary = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY")
            ?? Path.Combine(FeatureTests.Root, "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
        var start = new ProcessStartInfo(binary) { WorkingDirectory = WorkingDirectory ?? DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args.Concat(["--state-dir", State, "--json"])) start.ArgumentList.Add(arg);
        var processCount = ProcessObserver.TotalProcesses();
        using var process = Process.Start(start) ?? throw new InvalidOperationException("CLI did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000)) { process.Kill(true); Assert.Fail("CLI exceeded 15-second watchdog."); }
        ExitCode = process.ExitCode;
        if (processCount.HasValue) Assert.That(ProcessObserver.TotalProcesses() - processCount, Is.EqualTo(1 + ExpectedChildren), "Observed CLI and declared child process count.");
        Stdout = stdout.Result; Stderr = stderr.Result;
        TestContext.Out.WriteLine($"CLI {string.Join(' ', args)} -> {ExitCode}: {stdout.Result} {stderr.Result}");
        Response = JsonDocument.Parse(stdout.Result).RootElement.Clone();
        Assert.That(Response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
    }

    internal void Execute(string step)
    {
        switch (step)
        {
            case "an installed native binary with no workflow or run": break;
            case "I invoke guide with JSON output": Call("guide"); break;
            case "the CLI exits with code 0 and returns a guide string":
                Assert.That(ExitCode, Is.Zero);
                Assert.That(Response.GetProperty("guide").GetString(), Is.Not.Empty); break;
            case "the guide explains start, advance, status, complete, allowed outcomes, and interruption recovery":
                var guide = Response.GetProperty("guide").GetString()!;
                foreach (var term in new[] { "start", "advance", "status", "complete", "allowed outcomes", "interrupted", "resolve", "--retry", "--fail" })
                    Assert.That(guide, Does.Contain(term));
                Assert.That(guide, Does.Contain("P01")); break;
            case "no run, child process, or network request is created":
                Assert.That(Directory.GetFileSystemEntries(DirectoryPath), Is.Empty); break;
            case "an isolated workspace and state directory": break;
            case "a marked YAML and Mermaid workflow containing only a success end node":
                File.WriteAllText(FilePath, FeatureTests.Workflow); break;
            case "I start the workflow with the published native binary":
                Call("start", FilePath, "--workspace", DirectoryPath);
                Assert.That(ExitCode, Is.Zero);
                runId = Response.GetProperty("runId").GetString()!;
                Assert.That(Response.GetProperty("stateRoot").GetString(), Is.EqualTo(State)); break;
            case "the run is ready and no child process is launched":
            case "the same ready run is returned":
                Assert.That(ExitCode, Is.Zero);
                Assert.That(Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(Response.GetProperty("runId").GetString(), Is.EqualTo(runId)); break;
            case "a fresh CLI process queries the returned run ID and state root": Call("status", runId); break;
            case "I advance that run": Call("advance", runId); break;
            case "the run is completed with CLI exit code 0":
                Assert.That(ExitCode, Is.Zero);
                Assert.That(Response.GetProperty("status").GetString(), Is.EqualTo("completed")); break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    public void Dispose() => Directory.Delete(DirectoryPath, true);
}
