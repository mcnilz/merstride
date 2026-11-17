namespace Merstride.Acceptance;

internal static class TestPaths
{
    internal static string Runtime => OperatingSystem.IsWindows() ? "win-x64" : OperatingSystem.IsMacOS() ? "osx-arm64" : "linux-x64";
    internal static string Helper => Environment.GetEnvironmentVariable("MERSTRIDE_FIXTURE") ??
        Path.Combine(FeatureTests.Root, "artifacts", "fixture", OperatingSystem.IsWindows() ? "test-process.exe" : "test-process");
}
