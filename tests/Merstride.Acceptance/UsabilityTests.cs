using NUnit.Framework;

namespace Merstride.Acceptance;

public class UsabilityTests
{
    [Test]
    public void AgentGuideSeparatesStepsAndReferenceSectionsInTextAndJson()
    {
        using var world = new CliWorld();
        world.Raw("guide", "--state-dir", "not-absolute");
        Assert.That(world.ExitCode, Is.Zero);
        Assert.That(world.Stderr, Is.Empty);
        var text = world.Stdout.Replace("\r\n", "\n").TrimEnd('\n');
        foreach (var section in new[] { "Quick start", "Agent handoffs", "Process execution", "Logs and limits", "Recovery" })
            Assert.That(text, Does.Contain("\n\n" + section + "\n"), $"Separate readable section: {section}");
        Assert.That(text, Does.Contain("\n  merstride start ").And.Contain("\n  merstride complete "));

        world.Raw("guide", "--json", "--state-dir", "not-absolute");
        Assert.That(world.ExitCode, Is.Zero);
        Assert.That(world.Stderr, Is.Empty);
        using var response = System.Text.Json.JsonDocument.Parse(world.Stdout);
        Assert.That(response.RootElement.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(response.RootElement.GetProperty("guide").GetString()!.Replace("\r\n", "\n"), Is.EqualTo(text));
        Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Is.Empty);
    }

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
