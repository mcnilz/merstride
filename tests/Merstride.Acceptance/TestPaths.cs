namespace Merstride.Acceptance;

internal static class TestPaths
{
    internal static string Helper => Environment.GetEnvironmentVariable("MERSTRIDE_FIXTURE") ??
        Path.Combine(FeatureTests.Root, "artifacts", "fixture", OperatingSystem.IsWindows() ? "test-process.exe" : "test-process");
}
