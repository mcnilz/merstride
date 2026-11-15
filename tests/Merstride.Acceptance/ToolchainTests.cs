using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace Merstride.Acceptance;

public class ToolchainTests
{
    [TestCase("src/Merstride")]
    [TestCase("tests/Merstride.Acceptance")]
    [TestCase("tests/Merstride.TestProcess")]
    public void PublicBuildUsesNet11AndStableCSharp15(string project)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = FeatureTests.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "msbuild", project, "-nologo", "-getProperty:TargetFramework,LangVersion,NETCoreSdkVersion,Features" })
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000))
        {
            process.Kill(true);
            Assert.Fail("Toolchain evaluation exceeded 60-second watchdog.");
        }
        Assert.That(process.ExitCode, Is.Zero, stderr.Result);
        using var result = JsonDocument.Parse(stdout.Result);
        var properties = result.RootElement.GetProperty("Properties");
        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("TargetFramework").GetString(), Is.EqualTo("net11.0"));
            Assert.That(properties.GetProperty("LangVersion").GetString(), Is.EqualTo("15.0"));
            Assert.That(properties.GetProperty("NETCoreSdkVersion").GetString(), Is.EqualTo("11.0.100"));
            Assert.That(properties.GetProperty("Features").GetString(), Does.Not.Contain("updated-memory-safety-rules"));
        });
    }
}
