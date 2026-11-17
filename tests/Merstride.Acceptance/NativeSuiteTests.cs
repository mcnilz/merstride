using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace Merstride.Acceptance;

public class NativeSuiteTests
{
    [Category("P15")]
    [TestCase("valid")]
    [TestCase("undefined-step")]
    [TestCase("unimplemented-scenario")]
    [TestCase("wrong-product")]
    public void NativeDriverReportsOnlyExecutedPassingScenarios(string fixture)
    {
        var directory = Path.Combine(FeatureTests.Root, "artifacts", "native-driver-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "features"));
        try
        {
            var name = OperatingSystem.IsWindows() ? "native-suite.exe" : "native-suite";
            var driver = Path.Combine(directory, name);
            File.Copy(Path.Combine(FeatureTests.Root, "artifacts", "suite", name), driver);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(driver, (UnixFileMode)493);
            File.WriteAllText(Path.Combine(directory, "features", "driver.feature"), $"""
                {(fixture == "unimplemented-scenario" ? "@P99" : "@P01")} @A20
                Feature: Native driver boundary fixture
                  Scenario: Execute the actual offline guide
                    Given an installed native binary with no workflow or run
                    When {(fixture == "undefined-step" ? "a deliberately undefined required step" : "I invoke guide with JSON output")}
                    Then the CLI exits with code 0 and returns a guide string
                """);
            var results = Path.Combine(directory, "results.json");
            var product = fixture == "wrong-product" ? TestPaths.Helper : Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ??
                Path.Combine(FeatureTests.Root, "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
            var start = new ProcessStartInfo(driver) { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "--binary", product, "--fixture", TestPaths.Helper, "--results", results, "--exclude-release-environment-orchestrator" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000)) { process.Kill(true); Assert.Fail("Native driver watchdog."); }
            Assert.That(process.ExitCode, Is.EqualTo(fixture == "valid" ? 0 : 1), stdout.Result + stderr.Result);
            if (fixture == "unimplemented-scenario")
            {
                Assert.That(File.Exists(results), Is.False, "Unimplemented cases must not emit a successful report.");
                return;
            }
            using var report = JsonDocument.Parse(File.ReadAllText(results));
            Assert.That(report.RootElement.GetArrayLength(), Is.EqualTo(1));
            var row = report.RootElement[0];
            Assert.That(row.GetProperty("feature").GetString(), Is.EqualTo("features/driver.feature"));
            Assert.That(row.GetProperty("line").GetInt32(), Is.EqualTo(3));
            Assert.That(row.GetProperty("scenario").GetString(), Is.EqualTo("Execute the actual offline guide"));
            Assert.That(row.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()), Is.EquivalentTo(new[] { "@P01", "@A20" }));
            Assert.That(row.GetProperty("result").GetProperty("status").GetString(), Is.EqualTo(fixture == "valid" ? "passed" : "failed"));
            if (fixture == "undefined-step") Assert.That(row.GetProperty("result").GetProperty("error").GetString(), Does.Contain("Undefined required step"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
