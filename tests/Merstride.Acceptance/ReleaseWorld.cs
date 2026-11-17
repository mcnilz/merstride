#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif
using System.Text.Json;

namespace Merstride.Acceptance;

internal sealed class ReleaseWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "", activation = "";
    private Dictionary<string, byte[]> snapshot = [];
    private JsonElement benchmark;
    private string releaseDefect = "none";
    private bool releaseChecked;
    private JsonElement cleanReport;

    private Dictionary<string, byte[]> Snapshot() => Directory.GetFiles(cli.State, "*", SearchOption.AllDirectories)
        .ToDictionary(p => p, File.ReadAllBytes, StringComparer.Ordinal);

    private void Start(bool logs = false, bool failure = false)
    {
        var workflow = logs ? $$"""
            ```yaml merstride
            schemaVersion: 1
            id: inspect
            entry: work
            nodes:
              work:
                kind: process
                command: {{AcceptanceJson.Serialize(TestPaths.Helper)}}
                args: [streams]
                timeoutSeconds: 5
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: inspect
            work["Work"]
            done["Done"]
            work -->|exit_0| done
            ```
            """ : FeatureTests.Workflow.Replace("result: success", failure ? "result: failure" : "result: success");
        File.WriteAllText(cli.FilePath, workflow);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
        if (logs || failure)
        {
            cli.ExpectedChildren = logs ? 1 : 0;
            cli.Call("advance", runId); cli.ExpectedChildren = 0;
            Assert.That(cli.ExitCode, Is.EqualTo(failure ? 5 : 0));
            if (logs) activation = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
        }
        snapshot = Snapshot();
    }

    internal void Execute(string step)
    {
        switch (step)
        {
            case "the published native binary on a clean Windows 11 x64 with no .NET SDK or runtime":
            case "the published native binary on a clean Linux x64 with no .NET SDK or runtime":
            case "the published native binary on a clean macOS 15 arm64 with no .NET SDK or runtime":
                var windows = step.Contains("Windows 11", StringComparison.Ordinal);
                var macos = step.Contains("macOS", StringComparison.Ordinal);
                var reportPath = Environment.GetEnvironmentVariable(windows ? "MERSTRIDE_CLEAN_WINDOWS_REPORT" : macos ? "MERSTRIDE_CLEAN_MACOS_REPORT" : "MERSTRIDE_CLEAN_LINUX_REPORT");
                Assert.That(reportPath is not null && File.Exists(reportPath), Is.True,
                    "RELEASE QUALIFICATION UNAVAILABLE: no independently provisioned clean target/full native suite report. SDK/WSL and PATH hiding are not clean-runtime proof. Run scripts/qualify-clean-target.ps1 with an actual native suite driver.");
                cleanReport = JsonDocument.Parse(File.ReadAllText(reportPath!)).RootElement.Clone();
                Assert.That(cleanReport.GetProperty("cleanNoRuntime").GetBoolean(), Is.True);
                if (windows) Assert.That(cleanReport.GetProperty("environment").GetString(), Does.Contain("Windows 11"));
                if (macos) Assert.That(cleanReport.GetProperty("environment").GetString(), Does.StartWith("macOS 15"));
                if (windows ? OperatingSystem.IsWindows() : macos ? OperatingSystem.IsMacOS() : OperatingSystem.IsLinux())
                {
                    var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Environment.GetEnvironmentVariable("MERSTRIDE_BINARY")!)));
                    Assert.That(cleanReport.GetProperty("binarySha256").GetString(), Is.EqualTo(hash));
                }
                break;
            case "a bundled native acceptance fixture process": Assert.That(File.Exists(TestPaths.Helper), Is.True); break;
            case "I execute the native acceptance suite":
                Assert.That(cleanReport.GetProperty("scenariosSha256").GetString(), Does.Match("^[a-f0-9]{64}$")); break;
            case "all release-applicable scenarios pass":
                var expected = FeatureTests.Cases().Where(c => !c.Tags.Contains("@build") && !c.Steps.Any(s => s.StartsWith("the published native binary on a clean", StringComparison.Ordinal)))
                    .Select(c => $"{Path.GetRelativePath(FeatureTests.Root,c.File).Replace('\\','/')}:{c.Line} {c.Name}").ToArray();
                var actual = cleanReport.GetProperty("scenarios").EnumerateArray().ToArray();
                Assert.That(actual.Select(c => $"{c.GetProperty("feature").GetString()!.Replace('\\','/')}:{c.GetProperty("line").GetInt64()} {c.GetProperty("scenario").GetString()}"), Is.EquivalentTo(expected));
                foreach (var result in actual) Assert.That(result.GetProperty("result").GetProperty("status").GetString(), Is.EqualTo("passed"));
                break;
            case "an authorized valid release tag on main": releaseDefect = "none"; break;
            case "every required build, platform, architecture, acceptance, and licensing gate has passed for its commit": break;
            case "the release workflow publishes":
            case "the release publication gate runs":
                using (var build = new BuildWorld())
                {
                    build.Run("pwsh", "-NoProfile", "-File", "tests/fixtures/release.ps1", "-Defect", releaseDefect);
                    Assert.That(build.ExitCode, Is.Zero, "Controlled release boundary fixture; no remote publication.");
                    releaseChecked = true;
                }
                break;
            case "the release contains exactly the verified platform archives and supporting files from that run":
            case "their versions, commit identity, and checksums match":
            case "no new public release or overwritten published asset is produced": Assert.That(releaseChecked, Is.True); break;
            case "a published Native AOT release candidate": Assert.That(File.Exists(Environment.GetEnvironmentVariable("MERSTRIDE_BINARY")), Is.True); break;
            case "I inspect resolved assets, compiler references, native publish inputs, and release contents":
                using (var build = new BuildWorld())
                {
                    build.Run("pwsh", "-NoProfile", "-File", "scripts/distribution-audit.ps1");
                    Assert.That(build.ExitCode, Is.Zero);
                }
                break;
            case "no NsDepCop implementation or analyzer-only dependency is included as product code or runtime assets":
            case "no analyzer source or binary is bundled in the release":
            case "project-owned source has an MIT license":
            case "shipped dependencies have compatible permissive licenses and required notices":
            case "NsDepCop appears only in the separate build-tool inventory":
                Assert.That(File.ReadAllText(Path.Combine(FeatureTests.Root, "artifacts/audit/distribution.json")), Does.Contain("passed")); break;
            case "a release candidate and a documented reference machine": Assert.That(File.Exists(Environment.GetEnvironmentVariable("MERSTRIDE_BINARY")), Is.True); break;
            case "I measure its uncompressed executable and 30 warm status process starts":
                using (var build = new BuildWorld())
                {
                    build.Run("pwsh", "-NoProfile", "-File", "scripts/benchmark.ps1", "-Binary", Environment.GetEnvironmentVariable("MERSTRIDE_BINARY")!);
                    Assert.That(build.ExitCode, Is.Zero);
                }
                benchmark = JsonDocument.Parse(File.ReadAllText(Path.Combine(FeatureTests.Root, "artifacts/benchmark.json"))).RootElement.Clone(); break;
            case "executable size excluding separate symbols is at most 25 MiB": Assert.That(benchmark.GetProperty("bytes").GetInt64(), Is.LessThanOrEqualTo(25 * 1024 * 1024)); break;
            case "warm status latency P95 is below 150 milliseconds": Assert.That(benchmark.GetProperty("p95Milliseconds").GetDouble(), Is.LessThan(150)); break;
            case "first-start latency and the 15 MiB size target result are reported separately":
                Assert.That(benchmark.GetProperty("firstStartMilliseconds").GetDouble(), Is.GreaterThan(0));
                Assert.That(benchmark.GetProperty("sizeTarget15MiB").ValueKind, Is.AnyOf(JsonValueKind.True, JsonValueKind.False)); break;
            case "a valid workflow and an existing run with logs": Start(logs: true); break;
            case "I invoke validate": cli.Call("validate", cli.FilePath); break;
            case "I invoke status": cli.Call("status", runId); break;
            case "I invoke logs": cli.Call("logs", runId, "--activation", activation, "--stream", "stdout"); break;
            case "I invoke guide": cli.Call("guide"); break;
            case "I invoke version": cli.Call("version"); break;
            case "no workflow process starts and no run state is modified":
                Assert.That(cli.ExitCode, Is.Zero);
                var current = Snapshot(); Assert.That(current.Keys, Is.EquivalentTo(snapshot.Keys));
                foreach (var file in snapshot) Assert.That(current[file.Key], Is.EqualTo(file.Value), file.Key);
                break;
            case "a failed run": Start(failure: true); break;
            case "I query status with JSON output": cli.Call("status", runId); break;
            case "the CLI exits with code 0": Assert.That(cli.ExitCode, Is.Zero); break;
            case "the response status is failed": Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("failed")); break;
            case "a started run and its returned absolute state root": Start(); break;
            case "a fresh CLI process in another directory queries the run using that state root":
                cli.WorkingDirectory = Path.Combine(cli.DirectoryPath, "elsewhere");
                Directory.CreateDirectory(cli.WorkingDirectory);
                cli.Call("status", runId); break;
            case "the correct persisted run is returned":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("runId").GetString(), Is.EqualTo(runId));
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready")); break;
            default:
                if (step.StartsWith("a release attempt with ", StringComparison.Ordinal)) releaseDefect = step[23..];
                else throw new AssertionException($"Undefined required P07 step: {step}");
                break;
        }
    }
    public void Dispose() => cli.Dispose();
}
