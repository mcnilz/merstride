using NUnit.Framework;

namespace Merstride.Acceptance;

public class RecoveryTests
{
    [Test]
    public void UserInterruptionStillReturns130WhenRecoveryStateCannotBeReplaced()
    {
        using var world = new RecoveryWorld();
        world.InterruptWithReadOnlyState();
    }

    [TestCase("executing-without-activation")]
    [TestCase("null-activation")]
    [TestCase("unknown-cursor")]
    public void CorruptSnapshotsAreRefusedByReadersAndWriters(string defect)
    {
        using var world = new RecoveryWorld();
        world.RejectCorruptSnapshot(defect);
    }

    [TestCase("ready")]
    [TestCase("completed")]
    public void ResolutionRequiresAnInterruptedRun(string status)
    {
        using var cli = new CliWorld();
        File.WriteAllText(cli.FilePath, FeatureTests.Workflow);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        var id = cli.Response.GetProperty("runId").GetString()!;
        if (status == "completed") cli.Call("advance", id);
        var path = Path.Combine(cli.State, id, "state.json");
        var snapshot = File.ReadAllBytes(path);
        foreach (var option in new[] { "--retry", "--fail" })
        {
            cli.Call("resolve", id, option);
            Assert.That(cli.ExitCode, Is.EqualTo(4));
            Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("INVALID_STATE"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(snapshot));
        }
    }

    [TestCase("missing")]
    [TestCase("both")]
    [TestCase("duplicate")]
    [TestCase("wrong-command")]
    public void ResolutionOptionsAreExclusiveAndScoped(string defect)
    {
        using var cli = new CliWorld();
        cli.Call(defect switch
        {
            "missing" => ["resolve", "r_invalid"],
            "both" => ["resolve", "r_invalid", "--retry", "--fail"],
            "duplicate" => ["resolve", "r_invalid", "--retry", "--retry"],
            _ => ["status", "r_invalid", "--retry"]
        });
        Assert.That(cli.ExitCode, Is.EqualTo(2));
        Assert.That(Directory.Exists(cli.State), Is.False);
    }
}
