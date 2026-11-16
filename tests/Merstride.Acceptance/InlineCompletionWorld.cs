using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace Merstride.Acceptance;

internal sealed class InlineCompletionWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "", requestId = "";
    private int transitions;
    private string[] originalFiles = [];
    private byte[] snapshot = [];
    private JsonElement receipt;
    private string nextRequest = "";
    private string StateFile => Path.Combine(cli.State, runId, "state.json");

    internal void Execute(string step)
    {
        const string optionsPrefix = "I submit completion options ";
        if (step.StartsWith(optionsPrefix, StringComparison.Ordinal))
        {
            var options = step[optionsPrefix.Length..];
            cli.Call(["complete", runId, "--request", requestId, .. options == "missing" ? Array.Empty<string>() : options.Split(';')]);
            return;
        }
        const string dataPrefix = "I complete it with inline data ";
        if (step.StartsWith(dataPrefix, StringComparison.Ordinal))
        {
            var data = step[dataPrefix.Length..] switch
            {
                "16384 compact UTF-8 bytes" => "{\"x\":\"" + new string('x', 16376) + "\"}",
                "16385 compact UTF-8 bytes" => "{\"x\":\"" + new string('x', 16377) + "\"}",
                "16384 Unicode UTF-8 bytes" => "{\"x\":\"" + new string('é', 8188) + "\"}",
                var depth when depth.StartsWith("nesting depth ", StringComparison.Ordinal) => Nested(int.Parse(depth[14..], System.Globalization.CultureInfo.InvariantCulture)),
                var literal => literal
            };
            cli.Call("complete", runId, "--request", requestId, "--outcome", "finish", "--data", data);
            return;
        }
        const string resultPrefix = "inline completion has result ";
        if (step.StartsWith(resultPrefix, StringComparison.Ordinal))
        {
            var result = step[resultPrefix.Length..];
            if (result == "accepted")
            {
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(transitions + 1));
            }
            else Reject(result, 4);
            return;
        }
        if (step.StartsWith("inline completion fails with ", StringComparison.Ordinal))
        {
            var words = step.Split(' ');
            Reject(words[4], int.Parse(words[7], System.Globalization.CultureInfo.InvariantCulture)); return;
        }
        foreach (var action in new[] { "I first complete using ", "I repeat the old completion using ", "I conflict with the old completion using " })
        {
            if (!step.StartsWith(action, StringComparison.Ordinal)) continue;
            var conflict = action.Contains("conflict", StringComparison.Ordinal);
            Submit(step[action.Length..], conflict ? "{\"n\":2}" : action.Contains("first", StringComparison.Ordinal)
                ? "{\"n\":1,\"items\":[1,true]}" : "{\"items\":[1,true],\"n\":1.0}");
            if (action.Contains("first", StringComparison.Ordinal))
            {
                Assert.That(cli.ExitCode, Is.Zero);
                receipt = cli.Response.GetProperty("receipt").Clone();
            }
            return;
        }
        switch (step)
        {
            case "a waiting inline-completion request": Wait(); break;
            case "I complete it using only an outcome parameter":
                cli.Call("complete", runId, "--request", requestId, "--outcome", "finish"); break;
            case "I complete it with nested Unicode inline data":
                cli.Call("complete", runId, "--request", requestId, "--outcome", "retry", "--data",
                    """{ "note": "Grüße with spaces and 'quotes'", "nested": { "items": [1,true] } }"""); break;
            case "I complete it with nested Unicode inline data through PowerShell": PowerShell(); break;
            case "the receipt contains the literal inline data":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stderr, Is.Empty);
                AssertData(cli.Response.GetProperty("receipt").GetProperty("data"));
                Assert.That(Directory.GetFileSystemEntries(cli.DirectoryPath).Order(), Is.EqualTo(originalFiles)); break;
            case "the next request exposes the same literal data":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
                var context = cli.Response.GetProperty("request").GetProperty("context")[0];
                Assert.That(context.GetProperty("available").GetBoolean(), Is.True);
                AssertData(context.GetProperty("value")); break;
            case "I complete it with an unknown request ID":
                cli.Call("complete", runId, "--request", "q_missing", "--outcome", "finish"); break;
            case "I complete it with an undeclared outcome":
                cli.Call("complete", runId, "--request", requestId, "--outcome", "undeclared"); break;
            case "a new request is waiting":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
                nextRequest = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
                Assert.That(nextRequest, Is.Not.EqualTo(requestId));
                snapshot = File.ReadAllBytes(StateFile); break;
            case "its original receipt returns and the new request stays unchanged":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("receipt").GetRawText(), Is.EqualTo(receipt.GetRawText()));
                Assert.That(cli.Response.GetProperty("request").GetProperty("id").GetString(), Is.EqualTo(nextRequest));
                Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot)); break;
            case "I inspect inline completion help and the agent guide":
                cli.Call("complete", "--help");
                Assert.That(cli.ExitCode, Is.Zero);
                var help = cli.Response.GetProperty("guide").GetString()!;
                Assert.That(help, Does.Contain("--outcome").And.Contain("--data").And.Contain("Preferred"));
                cli.Call("guide"); break;
            case "both explain outcome parameters and optional JSON data before file compatibility":
                Assert.That(cli.ExitCode, Is.Zero);
                var guide = cli.Response.GetProperty("guide").GetString()!;
                Assert.That(guide, Does.Contain("--outcome").And.Contain("--data"));
                Assert.That(guide.IndexOf("--outcome", StringComparison.Ordinal), Is.LessThan(guide.IndexOf("--result-file", StringComparison.Ordinal))); break;
            case "the run is ready with empty receipt data and no result file":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Stderr, Is.Empty);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready"));
                Assert.That(cli.Response.GetProperty("cursor").GetString(), Is.EqualTo("done"));
                Assert.That(cli.Response.GetProperty("transitionCount").GetInt32(), Is.EqualTo(transitions + 1));
                Assert.That(cli.Response.GetProperty("receipt").GetProperty("data").EnumerateObject(), Is.Empty);
                Assert.That(Directory.GetFileSystemEntries(cli.DirectoryPath).Order(), Is.EqualTo(originalFiles));
                cli.Call("status", runId);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("ready")); break;
            case "I explicitly advance the inline-completion run": cli.Call("advance", runId); break;
            case "the inline-completion run is completed":
                Assert.That(cli.ExitCode, Is.Zero);
                Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed")); break;
            default: throw new AssertionException($"Undefined required inline completion step: {step}");
        }
    }

    private static string Nested(int depth) => string.Concat(Enumerable.Repeat("{\"x\":", depth)) + "0" + new string('}', depth);

    private void PowerShell()
    {
        var script = Path.Combine(Path.GetTempPath(), "merstride-inline-" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            File.WriteAllText(script, """
                param($Binary, $Run, $Request, $Root)
                $PSNativeCommandArgumentPassing = 'Standard'
                $data = @{ note = "Grüße with spaces and 'quotes'"; nested = @{ items = @(1, $true) } } | ConvertTo-Json -Depth 4 -Compress
                & $Binary complete $Run --request $Request --outcome retry --data $data --state-dir $Root --json
                exit $LASTEXITCODE
                """);
            var binary = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ?? Path.Combine(FeatureTests.Root,
                "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
            var start = new ProcessStartInfo("pwsh") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", script, binary, runId, requestId, cli.State }) start.ArgumentList.Add(arg);
            var before = ProcessObserver.TotalProcesses();
            using var process = Process.Start(start) ?? throw new AssertionException("PowerShell did not start.");
            process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000)) { process.Kill(true); Assert.Fail("PowerShell exceeded the CLI watchdog."); }
            cli.ExitCode = process.ExitCode;
            cli.Stderr = stderr.GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(stdout.GetAwaiter().GetResult());
            cli.Response = document.RootElement.Clone();
            Assert.That(cli.Response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
            if (before.HasValue) Assert.That(ProcessObserver.TotalProcesses() - before, Is.EqualTo(2), "Only PowerShell and the CLI launch.");
        }
        finally { File.Delete(script); }
    }

    private void Submit(string mode, string data)
    {
        if (mode == "inline") cli.Call("complete", runId, "--request", requestId, "--outcome", "retry", "--data", data);
        else if (mode == "file")
        {
            var path = Path.Combine(cli.DirectoryPath, "compatibility.json");
            File.WriteAllText(path, "{\"outcome\":\"retry\",\"data\":" + data + "}");
            cli.Call("complete", runId, "--request", requestId, "--result-file", path);
        }
        else throw new AssertionException($"Undefined completion mode: {mode}");
    }

    private void Reject(string code, int exit)
    {
        Assert.That(cli.ExitCode, Is.EqualTo(exit));
        Assert.That(cli.Stderr, Is.Empty);
        Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(code));
        Assert.That(File.ReadAllBytes(StateFile), Is.EqualTo(snapshot));
    }

    private static void AssertData(JsonElement data)
    {
        Assert.That(data.GetProperty("note").GetString(), Is.EqualTo("Grüße with spaces and 'quotes'"));
        var items = data.GetProperty("nested").GetProperty("items");
        Assert.That(items.GetArrayLength(), Is.EqualTo(2));
        Assert.That(items[0].GetInt32(), Is.EqualTo(1));
        Assert.That(items[1].GetBoolean(), Is.True);
    }

    private void Wait()
    {
        File.WriteAllText(cli.FilePath, """
            ```yaml merstride
            schemaVersion: 1
            id: inline
            entry: task
            nodes:
              task:
                kind: agent
                prompt: Return an allowed outcome.
                context: [nodes.task.data]
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: inline
            task["Task"]
            done["Done"]
            task -->|retry| task
            task -->|finish| done
            ```
            """);
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero);
        runId = cli.Response.GetProperty("runId").GetString()!;
        cli.Call("advance", runId);
        Assert.That(cli.ExitCode, Is.Zero);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("waiting_agent"));
        requestId = cli.Response.GetProperty("request").GetProperty("id").GetString()!;
        transitions = cli.Response.GetProperty("transitionCount").GetInt32();
        originalFiles = Directory.GetFileSystemEntries(cli.DirectoryPath).Order().ToArray();
        snapshot = File.ReadAllBytes(StateFile);
    }

    public void Dispose() => cli.Dispose();
}
