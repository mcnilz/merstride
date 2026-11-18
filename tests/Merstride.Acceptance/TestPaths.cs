namespace Merstride.Acceptance;

internal static class TestPaths
{
    internal static string Runtime => OperatingSystem.IsWindows() ? "win-x64" : OperatingSystem.IsMacOS() ? "osx-arm64" : "linux-x64";
    internal static string Helper => Environment.GetEnvironmentVariable("MERSTRIDE_FIXTURE") ??
        Path.Combine(FeatureTests.Root, "artifacts", "fixture", OperatingSystem.IsWindows() ? "test-process.exe" : "test-process");

    internal static string RtkFreePath(string? path) => string.Join(Path.PathSeparator,
        (path ?? "").Split(Path.PathSeparator).Where(entry => !ContainsRtk(entry.Trim('"'))));

    private static bool ContainsRtk(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return false;
        return OperatingSystem.IsWindows()
            ? File.Exists(Path.Combine(directory, "rtk.exe")) || File.Exists(Path.Combine(directory, "rtk.com"))
            : File.Exists(Path.Combine(directory, "rtk"));
    }
}

#if !NATIVE_SUITE
[NUnit.Framework.SetUpFixture]
public sealed class AcceptanceEnvironment
{
    private string? originalPath;

    [NUnit.Framework.OneTimeSetUp]
    public void IsolateRtk()
    {
        originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", TestPaths.RtkFreePath(originalPath));
    }

    [NUnit.Framework.OneTimeTearDown]
    public void RestorePath() => Environment.SetEnvironmentVariable("PATH", originalPath);
}
#endif
