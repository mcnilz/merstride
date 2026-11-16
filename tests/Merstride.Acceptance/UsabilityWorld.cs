using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Merstride.Acceptance;

internal sealed class UsabilityWorld : IDisposable
{
    private readonly CliWorld world = new();
    private string command = "", format = "", filename = "";

    internal void Execute(string step)
    {
        if (step == "an isolated CLI discovery directory") return;
        var help = Regex.Match(step, "^I request (root|validate|start|advance|status|complete|resolve|logs|guide|version) help using (--help|-h) as (text|json)$");
        if (help.Success)
        {
            command = help.Groups[1].Value; format = help.Groups[3].Value;
            string[] prefix = command == "root" ? [] : [command];
            world.Raw([.. prefix, help.Groups[2].Value, "--state-dir", "not-absolute", .. format == "json" ? new[] { "--json" } : []]);
            return;
        }
        if (step.StartsWith("I invoke the literal CLI arguments ", StringComparison.Ordinal))
        {
            world.Raw(step["I invoke the literal CLI arguments ".Length..].Replace("{state}", world.State).Split(';'));
            return;
        }
        if (step.StartsWith("a valid workflow whose filename is ", StringComparison.Ordinal))
        {
            filename = step["a valid workflow whose filename is ".Length..];
            File.WriteAllText(Path.Combine(world.DirectoryPath, filename), FeatureTests.Workflow);
            return;
        }
        if (step is "I validate with globals before the command and a literal delimiter" or "I validate with globals after the command and a literal delimiter")
        {
            string[] globals = ["--json", "--state-dir", world.State];
            world.Raw(step.Contains("before", StringComparison.Ordinal)
                ? [.. globals, "validate", "--", filename] : ["validate", .. globals, "--", filename]);
            return;
        }
        switch (step)
        {
            case "I validate the at-sign path without a delimiter":
                world.Raw("validate", filename, "--json"); break;
            case "I start with an equals-form input path and options before the delimiter":
                File.WriteAllText(Path.Combine(world.DirectoryPath, "--input data.json"), "{}");
                world.Raw("--json", "start", "--workspace", world.DirectoryPath, "--state-dir", world.State, "--input-file=--input data.json", "--", filename); break;
            case "a ready run is returned without workflow execution":
                Assert.That(world.ExitCode, Is.Zero);
                var run = Protocol();
                Assert.That(run.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(run.GetProperty("transitionCount").GetInt32(), Is.Zero);
                Assert.That(run.GetProperty("lastActivation").ValueKind, Is.EqualTo(JsonValueKind.Null));
                world.Call("status", run.GetProperty("runId").GetString()!);
                Assert.That(world.ExitCode, Is.Zero);
                Assert.That(world.Response.GetProperty("status").GetString(), Is.EqualTo("ready")); break;
            case "useful English help succeeds without side effects":
                Assert.That(world.ExitCode, Is.Zero);
                var text = format == "json" ? Protocol().GetProperty("guide").GetString()! : world.Stdout;
                foreach (var term in new[] { "Usage:", "Options:", "--state-dir", "default", "Examples:", "Exit codes:", "PowerShell", "--json" })
                    Assert.That(text, Does.Contain(term));
                if (command != "root") Assert.That(text, Does.Contain("merstride " + command).And.Not.Contain("--version"));
                if (command is "start" or "validate") Assert.That(text, Does.Contain("<file>"));
                if (command == "start") Assert.That(text, Does.Contain("--workspace").And.Contain("--input-file"));
                if (command == "complete") Assert.That(text, Does.Contain("--request").And.Contain("--outcome").And.Contain("--data").And.Contain("--result-file"));
                if (command == "logs") Assert.That(text, Does.Contain("65536").And.Contain("--activation"));
                if (command == "resolve") Assert.That(text, Does.Contain("Exactly one of --retry or --fail"));
                NoEffects(); break;
            case "one JSON usage error includes a help hint without side effects":
                Assert.That(world.ExitCode, Is.EqualTo(2));
                var error = Protocol().GetProperty("error");
                Assert.That(error.GetProperty("code").GetString(), Is.EqualTo("USAGE"));
                Assert.That(error.GetProperty("message").GetString(), Does.Contain("--help"));
                NoEffects(); break;
            case "one JSON version response succeeds without side effects":
                Assert.That(world.ExitCode, Is.Zero);
                Assert.That(Protocol().GetProperty("version").GetString(), Does.Match("^[0-9]+\\.[0-9]+\\.[0-9]+"));
                NoEffects(); break;
            case "validation succeeds without creating state":
                Assert.That(world.ExitCode, Is.Zero);
                Assert.That(Protocol().GetProperty("workflowId").GetString(), Is.EqualTo("smoke"));
                Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Has.Length.EqualTo(1));
                Assert.That(world.Stderr, Is.Empty); break;
            default: throw new AssertionException($"Undefined required usability step: {step}");
        }
    }

    private JsonElement Protocol()
    {
        using var document = JsonDocument.Parse(world.Stdout);
        var response = document.RootElement.Clone();
        Assert.That(response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(world.Stderr, Is.Empty);
        return response;
    }

    private void NoEffects()
    {
        Assert.That(world.Stderr, Is.Empty);
        Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Is.Empty);
    }

    public void Dispose() => world.Dispose();
}
