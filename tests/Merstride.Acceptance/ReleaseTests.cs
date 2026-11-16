using NUnit.Framework;

namespace Merstride.Acceptance;

public class ReleaseTests
{
    [Category("P14")]
    [TestCase("v1.2.3", "1.2.3", "lightweight")]
    [TestCase("v1.2.3", "1.2.3", "annotated")]
    [TestCase("v0.2.0-rc.1", "0.2.0-rc.1", "annotated")]
    [TestCase("v1.2.3", "1.2.3", "wrong-commit")]
    [TestCase("v1.2.3", "1.2.3", "outside-main")]
    [TestCase("1.2.3", "", "lightweight")]
    [TestCase("v01.2.3", "", "lightweight")]
    [TestCase("v1.2", "", "lightweight")]
    [TestCase("v1.2.3-rc.01", "", "lightweight")]
    [TestCase("v1.2.3+build.1", "", "lightweight")]
    public void ReleaseTagExportsVersionOnlyForValidSource(string tag, string expectedVersion, string context)
    {
        using var build = new BuildWorld();
        build.Run("pwsh", "-NoProfile", "-File", "tests/fixtures/release-tag.ps1",
            "-Tag", tag, "-ExpectedVersion", expectedVersion, "-Context", context);
        Assert.That(build.ExitCode, Is.Zero);
    }

    [TestCase("valid")]
    [TestCase("changed-head")]
    [TestCase("changed-worktree")]
    [TestCase("changed-tests")]
    [TestCase("changed-distribution")]
    [TestCase("changed-benchmark")]
    [TestCase("changed-binary")]
    public void PackageChecksSourceAndEvidenceFreshness(string defect)
    {
        using var build = new BuildWorld();
        var rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        build.Run("pwsh", "-NoProfile", "-File", "tests/fixtures/package.ps1", "-Defect", defect,
            "-ResultFile", Path.Combine(FeatureTests.Root, "artifacts", "tests", "package", rid + "-" + defect + ".json"));
        Assert.That(build.ExitCode, Is.Zero);
    }

    [TestCase("matching draft")]
    [TestCase("wrong draft")]
    [TestCase("corrupt draft")]
    [TestCase("missing qualification")]
    [TestCase("invalid tag")]
    [TestCase("incomplete experiment")]
    [TestCase("failed clean scenario")]
    [TestCase("prerelease draft")]
    public void PublicationChecksDraftIdentityAndQualification(string defect)
    {
        using var build = new BuildWorld();
        build.Run("pwsh", "-NoProfile", "-File", "tests/fixtures/release.ps1", "-Defect", defect);
        Assert.That(build.ExitCode, Is.Zero);
    }
}
