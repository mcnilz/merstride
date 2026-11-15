using NUnit.Framework;

namespace Merstride.Acceptance;

public class CliTests
{
    [TestCase("duplicate")]
    [TestCase("malformed")]
    [TestCase("alias")]
    [TestCase("unknown")]
    [TestCase("unsupported")]
    public void RejectInvalidYamlBeforeCreatingState(string defect)
    {
        using var world = new CliWorld();
        var source = FeatureTests.Workflow;
        source = defect switch
        {
            "duplicate" => source.Replace("    kind: end", "    kind: end\n    kind: end"),
            "malformed" => source.Replace("entry: done", "entry: [unclosed"),
            "alias" => source.Replace("entry: done", "entry: *missing"),
            "unknown" => source.Replace("    kind: end", "    kind: end\n    surprise: true"),
            _ => source.Replace("    kind: end", "    kind: agent")
        };
        File.WriteAllText(world.FilePath, source);
        world.Call("start", world.FilePath, "--workspace", world.DirectoryPath);
        Assert.That(world.ExitCode, Is.EqualTo(3));
        var error = world.Response.GetProperty("error");
        Assert.That(error.GetProperty("location").GetProperty("line").GetInt32(), Is.Positive);
        Assert.That(error.GetProperty("location").GetProperty("column").GetInt32(), Is.Positive);
        Assert.That(Directory.Exists(world.State), Is.False);
    }

    [Test]
    public void ValidationReturnsLocatedErrorDiagnostics()
    {
        using var world = new CliWorld();
        File.WriteAllText(world.FilePath, FeatureTests.Workflow.Replace("result: success", "surprise: true"));
        world.Call("validate", world.FilePath);
        Assert.That(world.ExitCode, Is.EqualTo(3));
        var diagnostic = world.Response.GetProperty("diagnostics")[0];
        Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo("error"));
        Assert.That(diagnostic.GetProperty("location").GetProperty("line").GetInt32(), Is.Positive);
        Assert.That(diagnostic.GetProperty("location").GetProperty("column").GetInt32(), Is.Positive);
    }

    [Test]
    public void FailureEndStaysTerminalAndStatusRemainsSuccessful()
    {
        using var world = new CliWorld();
        File.WriteAllText(world.FilePath, FeatureTests.Workflow.Replace("result: success", "result: failure"));
        world.Call("start", world.FilePath, "--workspace", world.DirectoryPath);
        Assert.That(world.ExitCode, Is.Zero);
        var id = world.Response.GetProperty("runId").GetString()!;
        world.Call("advance", id);
        Assert.That(world.ExitCode, Is.EqualTo(5));
        Assert.That(world.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("WORKFLOW_FAILURE"));
        world.Call("advance", id);
        Assert.That(world.ExitCode, Is.EqualTo(5));
        world.Call("status", id);
        Assert.That(world.ExitCode, Is.Zero);
        Assert.That(world.Response.GetProperty("status").GetString(), Is.EqualTo("failed"));
    }

    [Test]
    public void UndefinedRequiredStepsFail()
    {
        using var world = new CliWorld();
        Assert.Throws<AssertionException>(() => world.Execute("a deliberately undefined required step"));
    }
}
