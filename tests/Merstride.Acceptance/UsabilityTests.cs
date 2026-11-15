using NUnit.Framework;

namespace Merstride.Acceptance;

public class UsabilityTests
{
    [TestCase("Cli", true)]
    [TestCase("Domain", false)]
    [TestCase("Application", false)]
    [TestCase("Compilation", false)]
    [TestCase("Infrastructure", false)]
    public void ParserIsConfinedToCliAtThePublicBuildBoundary(string area, bool allowed)
    {
        using var build = new BuildWorld();
        build.ParserFixture(area);
        build.AssertParserBoundary(allowed);
    }

    [Test]
    public void StartHelpDoesNotRequireOrReadWorkflowAndWorkspace()
    {
        using var world = new CliWorld();
        world.Raw("start", "--help");
        Assert.That(world.ExitCode, Is.Zero);
        foreach (var term in new[] { "Usage:", "<file>", "--workspace", "required", "--input-file", "default", "Example:", "Exit codes:" })
            Assert.That(world.Stdout.ToLowerInvariant(), Does.Contain(term.ToLowerInvariant()));
        Assert.That(world.Stderr, Is.Empty);
        Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Is.Empty);
    }

    [Test]
    public void TopLevelHelpExplainsCommandsAndDoesNotCreateState()
    {
        using var world = new CliWorld();
        world.Raw("--help");
        Assert.That(world.ExitCode, Is.Zero);
        foreach (var term in new[] { "Usage:", "start", "complete", "--state-dir", "Examples:", "Exit codes:" })
            Assert.That(world.Stdout, Does.Contain(term));
        Assert.That(world.Stderr, Is.Empty);
        Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Is.Empty);
    }
}
