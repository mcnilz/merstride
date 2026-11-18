using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class RtkWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private static readonly string[] Arguments = ["arguments", "two words", "", "a\"b", "Grüße", "$(touch injected)"];
    private string Bin => Path.Combine(cli.DirectoryPath, "tools with spaces");
    private string Invocation => Path.Combine(cli.DirectoryPath, "rtk-arguments.json");
    private string Effect => Path.Combine(cli.DirectoryPath, "arguments.json");
    private string expectedRtkCommand = "";
    private string Rtk => Path.Combine(Bin, OperatingSystem.IsWindows() ? "rtk.exe" : "rtk");
    private string runId = "";
    private int rejected;

    public void Execute(string step)
    {
        if (step.StartsWith("an RTK command with workflow setting ", StringComparison.Ordinal))
        {
            var settings = step.Split(' ');
            WriteWorkflow(settings[6], settings[^1]);
            return;
        }
        if (step.StartsWith("I start the RTK workflow and prepare ", StringComparison.Ordinal))
        {
            PrepareRtk(step.Split(' ')[^1]);
            Start();
            return;
        }
        if (step.StartsWith("I advance the RTK workflow expecting ", StringComparison.Ordinal))
        {
            Advance(step.Split(' ')[^1]);
            return;
        }
        if (step.StartsWith("the command execution is ", StringComparison.Ordinal))
        {
            CheckExecution(step.Split(' ')[^1]);
            return;
        }
        if (step.StartsWith("an RTK workflow with a ", StringComparison.Ordinal))
        {
            WriteWorkflow(form: step.Split(' ')[^2]);
            return;
        }
        if (step.StartsWith("an RTK workflow with invalid ", StringComparison.Ordinal))
        {
            WriteInvalid(step.Split(' ')[^1]);
            return;
        }
        switch (step)
        {
            case "an RTK workflow with the default settings": WriteWorkflow(); break;
            case "I start the RTK workflow and make RTK available": Start(); PrepareRtk("available"); break;
            case "I advance the RTK workflow": Advance("wrapped"); break;
            case "RTK runs the command once with its literal arguments": CheckExecution("wrapped"); break;
            case "I validate and start the invalid RTK workflow":
                foreach (var operation in new[] { "validate", "start" })
                {
                    cli.Call(operation, cli.FilePath);
                    Assert.That(cli.ExitCode, Is.EqualTo(3));
                    Assert.That(cli.ErrorCode, Is.EqualTo("INVALID_DOCUMENT"));
                    if (UsabilityAuditWorld.Complementary)
                        Assert.That(Regex.IsMatch(UsabilityAuditWorld.LastText, @"INVALID_DOCUMENT \(line [1-9]\d*, column [1-9]\d*\)"), Is.True);
                    else
                        Assert.That(cli.Response.GetProperty("error").GetProperty("location").GetProperty("line").GetInt32(), Is.Positive);
                    rejected++;
                }
                break;
            case "both operations reject the RTK document before any process runs":
                Assert.That(rejected, Is.EqualTo(2));
                Assert.That(File.Exists(Invocation) || File.Exists(Effect) || Directory.Exists(cli.State), Is.False);
                break;
            default: throw new AssertionException($"Undefined required step: {step}");
        }
    }

    private void WriteWorkflow(string workflow = "omitted", string node = "omitted", string form = "command")
    {
        Directory.CreateDirectory(Bin);
        cli.EnvironmentOverrides["PATH"] = Bin;
        var extension = OperatingSystem.IsWindows() ? ".exe" : "";
        var fixture = form switch
        {
            "ambiguous-name" => "init" + extension,
            "named-filter" => "git" + extension,
            _ => Path.GetFileName(TestPaths.Helper)
        };
        var path = Path.Combine(Bin, fixture);
        CopyFixture(path);
        var command = form switch
        {
            "ambiguous-name" => "init",
            "named-filter" => "git",
            _ => Path.Combine("tools with spaces", fixture)
        };
        expectedRtkCommand = form == "named-filter" ? "git" : path;
        string[] commandFields = [$"command: {AcceptanceJson.Serialize(command)}", $"args: {AcceptanceJson.Serialize(Arguments)}"];
        string[] script = ["script: |", $"  #!\"{command}\" inline_probe literal", "  script body"];
        string[] execution = form switch
        {
            "command" or "ambiguous-name" or "named-filter" => commandFields,
            "script" => script,
            "command-variant" => ["variants:", "  - " + commandFields[0], "    " + commandFields[1]],
            "script-variant" => ["variants:", "  - " + script[0], .. script[1..].Select(line => "    " + line)],
            _ => throw new AssertionException($"Unknown RTK execution form: {form}")
        };
        var rootSetting = workflow == "omitted" ? "" : $"rtk: {workflow}\n";
        var nodeSetting = node == "omitted" ? "" : $"    rtk: {node}\n";
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: rtk_process
            entry: work
            {{rootSetting}}nodes:
              work:
                kind: process
                timeoutSeconds: 5
            {{nodeSetting}}{{string.Join("\n", execution.Select(line => "    " + line))}}
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: rtk_process
            work["Work"]
            done["Done"]
            work -->|exit_0| done
            work -->|exit_7| done
            ```
            """);
    }

    private void Start()
    {
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
        Assert.That(File.Exists(Invocation) || File.Exists(Effect), Is.False);
        File.WriteAllText(cli.FilePath, "Workflow source replaced after start.");
    }

    private void PrepareRtk(string availability)
    {
        if (availability == "absent") return;
        if (availability == "quoted-empty")
        {
            CopyFixture(Path.Combine(cli.DirectoryPath, Path.GetFileName(Rtk)));
            cli.EnvironmentOverrides["PATH"] = "\"\"" + Path.PathSeparator + Bin;
            return;
        }
        if (availability == "unusable")
        {
            // A directory with the executable name on Windows, or a non-executable file on Unix.
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(Rtk);
            else File.WriteAllText(Rtk, "RTK must have execute permissions.");
            return;
        }
        Assert.That(availability is "available" or "failing", Is.True, $"Unknown RTK availability: {availability}");
        CopyFixture(Rtk);
        if (availability == "failing") cli.EnvironmentOverrides["MERSTRIDE_RTK_FIXTURE_FAIL"] = "1";
    }

    private void Advance(string execution)
    {
        Assert.That(execution is "wrapped" or "direct" or "legacy" or "failed" or "script-direct", Is.True,
            $"Unknown RTK execution: {execution}");
        if (execution == "legacy")
        {
            var file = Path.Combine(cli.State, runId, "state.json");
            var state = JsonNode.Parse(File.ReadAllBytes(file))!;
            foreach (var node in state["definition"]!["nodes"]!.AsArray()) node!.AsObject().Remove("useRtk");
            File.WriteAllText(file, state.ToJsonString());
        }
        cli.ExpectedChildren = execution == "wrapped" ? 2 : 1;
        cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
        Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(),
            Is.EqualTo(execution == "failed" ? "exit_7" : "exit_0"));
    }

    private void CheckExecution(string execution)
    {
        Assert.That(execution is "wrapped" or "direct" or "legacy" or "failed" or "script-direct", Is.True,
            $"Unknown RTK execution: {execution}");
        Assert.That(File.Exists(Invocation), Is.EqualTo(execution is "wrapped" or "failed"));
        if (execution is "wrapped" or "failed")
        {
            var invocation = File.ReadAllLines(Invocation);
            Assert.That(invocation.Length, Is.EqualTo(1));
            Assert.That(AcceptanceJson.DeserializeStrings(invocation[0]),
                Is.EqualTo(new[] { expectedRtkCommand }.Concat(Arguments).ToArray()));
        }
        Assert.That(File.Exists(Effect), Is.EqualTo(execution is not ("script-direct" or "failed")));
        if (execution == "script-direct")
        {
            var activation = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
            cli.ExpectedChildren = 0;
            cli.Call("logs", runId, "--activation", activation, "--stream", "stderr");
            Assert.That(cli.ExitCode, Is.Zero);
            Assert.That(cli.Response.GetProperty("log").GetString()!.TrimEnd(), Is.EqualTo("script body"));
        }
        else if (execution != "failed")
        {
            Assert.That(AcceptanceJson.DeserializeStrings(File.ReadAllText(Effect)), Is.EqualTo(Arguments[1..]));
        }
    }

    private void WriteInvalid(string field)
    {
        switch (field)
        {
            case "root-string": WriteWorkflow(workflow: "\"true\""); break;
            case "node-number": WriteWorkflow(node: "1"); break;
            case "node-null": WriteWorkflow(node: "null"); break;
            case "end-setting":
                WriteWorkflow();
                File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("kind: end", "kind: end\n    rtk: false"));
                break;
            case "variant-flag":
                WriteWorkflow(form: "command-variant");
                File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("- command:", "- rtk: true\n        command:"));
                break;
            default: throw new AssertionException($"Unknown invalid RTK field: {field}");
        }
        PrepareRtk("available");
    }

    private static void CopyFixture(string destination)
    {
        File.Copy(TestPaths.Helper, destination);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, File.GetUnixFileMode(TestPaths.Helper));
    }

    public void Dispose() => cli.Dispose();
}
