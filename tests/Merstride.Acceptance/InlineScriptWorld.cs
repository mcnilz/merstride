using System.Text.Json;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class InlineScriptWorld : IDisposable
{
    private readonly CliWorld cli = new();
    private string runId = "";
    private string diagnostic = "";
    private string[] probeArgs = [];
    private RecoveryWorld? recovery;
    private const string ProbeBody = "line one\nGrüße 🌍\n";
    private static string HostPlatform => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";

    internal InlineScriptWorld()
    {
        // PowerShell's stdin command mode otherwise emits terminal controls on Unix.
        // Select a plain terminal in the caller; the engine must inherit it unchanged.
        if (!OperatingSystem.IsWindows()) cli.EnvironmentOverrides["TERM"] = "dumb";
    }

    internal void Write(string execution, string route = "exit_0", int timeout = 5)
    {
        File.WriteAllText(cli.FilePath, $$"""
            ```yaml merstride
            schemaVersion: 1
            id: inline
            entry: work
            nodes:
              work:
                kind: process
                timeoutSeconds: {{timeout}}
            {{string.Join('\n', execution.Split('\n').Select(l => "    " + l))}}
              done:
                kind: end
                result: success
            ```
            ```mermaid
            flowchart TD
            %% merstride: inline
            work["Work"]
            done["Done"]
            work -->|{{route}}| done
            ```
            """);
    }

    internal void Execute(string step)
    {
        const string defect = "an inline workflow with defect ";
        const string selection = "a process with ";
        const string unavailable = "an inline execution with ";
        const string probe = "an inline probe using ";
        const string limit = "an inline interpreter with ";
        const string installed = "an inline script for installed ";
        if (step.StartsWith(defect, StringComparison.Ordinal)) { InvalidDefinition(step[defect.Length..]); return; }
        if (step.StartsWith(selection, StringComparison.Ordinal)) { Selection(step[selection.Length..^9]); return; }
        if (step.StartsWith(unavailable, StringComparison.Ordinal)) { Unavailable(step[unavailable.Length..]); return; }
        if (step.StartsWith(probe, StringComparison.Ordinal)) { Probe(step[probe.Length..^7]); return; }
        if (step.StartsWith(limit, StringComparison.Ordinal)) { Limit(step[limit.Length..^9]); return; }
        if (step.StartsWith(installed, StringComparison.Ordinal)) { Installed(step[installed.Length..]); return; }
        const string obeys = "inline execution obeys the ";
        if (step.StartsWith(obeys, StringComparison.Ordinal))
        { AssertLimit(step[obeys.Length..^6]); return; }
        switch (step)
        {
            case "an inline recovery fixture": recovery = new RecoveryWorld(inline: true); break;
            case "an inline probe that exits before reading a large body":
                Write(Script(new string('x', 200000), "\"" + TestPaths.Helper + "\" exit 7"), "exit_7"); break;
            case "inline execution preserves exit_7": Completed("exit_7"); break;
            case "an uncovered platform without a spawn_error edge":
                Unavailable("no-matching-variant");
                File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("|spawn_error|", "|exit_0|")); break;
            case "advance fails with NO_MATCHING_EDGE and exit 5":
                Assert.That(cli.ExitCode, Is.EqualTo(5));
                Assert.That(cli.Response.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("NO_MATCHING_EDGE"));
                Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo("spawn_error")); break;
            case "I validate and start the invalid inline workflow":
                foreach (var args in new[] { new[] { "validate", cli.FilePath }, new[] { "start", cli.FilePath, "--workspace", cli.DirectoryPath } })
                {
                    cli.Call(args);
                    Assert.That(cli.ExitCode, Is.EqualTo(3), cli.Stdout);
                    var error = cli.Response.GetProperty("error");
                    Assert.That(error.GetProperty("code").GetString(), Is.EqualTo("INVALID_DOCUMENT"));
                    Assert.That(error.GetProperty("location").GetProperty("line").GetInt32(), Is.GreaterThan(1));
                    Assert.That(error.GetProperty("location").GetProperty("column").GetInt32(), Is.Positive);
                }
                break;
            case "both operations reject the document with a location and no state or children":
                Assert.That(Directory.Exists(cli.State), Is.False); break;
            case "only the applicable variant executes":
                Completed("exit_0");
                Assert.That(Log("stdout"), Is.EqualTo("selected" + Environment.NewLine)); break;
            case "the interpreter produces Unicode logs and exit_7":
                Completed("exit_7");
                Assert.That(Log("stdout").Replace("\r\n", "\n"), Is.EqualTo("Grüße 🌍\n"));
                Assert.That(Log("stderr").Replace("\r\n", "\n"), Is.EqualTo("separate error\n")); break;
            case "I advance the unavailable inline workflow": Advance(0); break;
            case "spawn_error includes the stable execution diagnostic":
                Completed("spawn_error"); Assert.That(Log("stderr"), Does.StartWith(diagnostic + ":")); break;
            case "the probe observes exact arguments and UTF-8 stdin without a script file":
                Completed("exit_0");
                Assert.That(AcceptanceJson.DeserializeStrings(Log("stdout")), Is.EqualTo(probeArgs));
                Assert.That(Log("stderr"), Is.EqualTo(ProbeBody));
                Assert.That(Directory.GetFiles(cli.DirectoryPath, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(cli.DirectoryPath, p))
                    .Where(p => !p.StartsWith("state" + Path.DirectorySeparatorChar, StringComparison.Ordinal)),
                    Is.EquivalentTo(new[] { "workflow.md" }.Concat(Directory.GetFiles(cli.DirectoryPath, "*", SearchOption.AllDirectories)
                        .Where(p => Path.GetFileName(p).StartsWith("probe", StringComparison.Ordinal))
                        .Select(p => Path.GetRelativePath(cli.DirectoryPath, p)))));
                break;
            case "an inline PowerShell workflow with Unicode output and workspace effects":
                Write("""
                    script: |
                      #!pwsh -NoLogo -NoProfile -NonInteractive -Command -
                      [Console]::WriteLine('Grüße 🌍')
                      [Console]::Error.WriteLine('separate error')
                      [IO.File]::WriteAllText((Join-Path (Get-Location) 'cwd.txt'), (Get-Location).Path)
                      [IO.File]::WriteAllText((Join-Path (Get-Location) 'files.json'), (ConvertTo-Json -Compress -InputObject @(Get-ChildItem -Recurse -File | ForEach-Object { $_.Name })))
                    """);
                break;
            case "I start the inline workflow and replace its source":
                Start(); File.WriteAllText(cli.FilePath, "changed source"); break;
            case "start has executed nothing":
                Assert.That(File.Exists(Path.Combine(cli.DirectoryPath, "cwd.txt")), Is.False); break;
            case "I advance the inline workflow": Advance(); break;
            case "the frozen script runs once with separate logs and no script file":
                Completed("exit_0");
                Assert.That(Log("stdout"), Is.EqualTo("Grüße 🌍" + Environment.NewLine));
                Assert.That(Log("stderr"), Is.EqualTo("separate error" + Environment.NewLine));
                Assert.That(File.ReadAllText(Path.Combine(cli.DirectoryPath, "cwd.txt")), Is.EqualTo(cli.DirectoryPath));
                var files = AcceptanceJson.DeserializeStrings(File.ReadAllText(Path.Combine(cli.DirectoryPath, "files.json")))!;
                Assert.That(files, Is.EquivalentTo(new[] { "workflow.md", "cwd.txt", "state.json", "writer.lock" }
                    .Concat(Directory.GetFiles(Path.Combine(cli.State, runId, "logs")).Select(Path.GetFileName))));
                cli.ExpectedChildren = 0;
                cli.Call("advance", runId);
                Assert.That(cli.ExitCode, Is.Zero);
                break;
            default:
                if (recovery is not null) recovery.Execute(step);
                else throw new AssertionException($"Undefined required inline-script step: {step}");
                break;
        }
    }

    private static string Script(string body, string interpreter = "pwsh -NoLogo -NoProfile -NonInteractive -Command -") =>
        "script: |\n  #!" + interpreter + "\n" + string.Join('\n', body.TrimEnd('\n').Split('\n').Select(l => "  " + l));

    private void Selection(string selection)
    {
        var condition = selection switch
        {
            "single-platform" or "direct" => "platforms: " + HostPlatform + "\n  ",
            "multi-platform" => "platforms: [" + HostPlatform + ", " + (HostPlatform == "windows" ? "macos" : "windows") + "]\n  ",
            "unconditional" => "",
            _ => throw new AssertionException("Unknown variant fixture.")
        };
        var execution = selection == "direct"
            ? "command: pwsh\nargs: [-NoLogo, -NoProfile, -NonInteractive, -Command, \"[Console]::WriteLine('selected')\"]"
            : Script("[Console]::WriteLine('selected')");
        Write("variants:\n- " + condition + execution.Replace("\n", "\n  "));
    }

    private void Unavailable(string unavailable)
    {
        diagnostic = unavailable switch
        {
            "missing-interpreter" => "INTERPRETER_UNAVAILABLE",
            "unlaunchable-path" => "PROCESS_UNAVAILABLE",
            "no-matching-variant" => "NO_PLATFORM_VARIANT",
            _ => throw new AssertionException("Unknown unavailable fixture.")
        };
        Write(unavailable == "no-matching-variant"
            ? "variants:\n- platforms: " + (HostPlatform == "windows" ? "linux" : "windows") + "\n  command: never-launch-this\n  args: []"
            : Script("never execute this", unavailable == "missing-interpreter" ? "merstride-missing-interpreter-62ad" : "./missing/interpreter"), "spawn_error");
        cli.Call("validate", cli.FilePath);
        Assert.That(cli.ExitCode, Is.Zero, "Validation never probes interpreter availability: " + cli.Stdout);
    }

    private void Probe(string syntax)
    {
        var suffix = OperatingSystem.IsWindows() ? ".exe" : "";
        var directory = Path.Combine(cli.DirectoryPath, "tools with spaces");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "probe" + suffix);
        File.Copy(TestPaths.Helper, target);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, File.GetUnixFileMode(TestPaths.Helper));
        cli.EnvironmentOverrides["PATH"] = directory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        probeArgs = syntax == "env" ? [] : ["two words", "Grüße", "$(touch injected)", "a;b", "", "trailing\\"];
        var arguments = " inline_probe \"two words\" Grüße '$(touch injected)' 'a;b' '' trailing\\";
        var shebang = syntax switch
        {
            "absolute" or "crlf" => "\"" + TestPaths.Helper + "\"" + arguments,
            "relative" or "quoted-spaces" => "\"./tools with spaces/probe" + suffix + "\"" + arguments,
            "env" => "/usr/bin/env probe",
            "env-S" => "/usr/bin/env -S probe" + arguments,
            _ => throw new AssertionException("Unknown shebang fixture.")
        };
        Write(Script(ProbeBody, shebang));
        if (syntax == "crlf") File.WriteAllText(cli.FilePath, File.ReadAllText(cli.FilePath).Replace("\n", "\r\n"));
    }

    private void Limit(string limit)
    {
        var body = limit switch
        {
            "timeout" => "Start-Sleep -Seconds 30",
            "output" => "[Console]::OpenStandardOutput().Write([byte[]]::new(11534336))",
            "unread-stdin" => new string('x', 200000),
            _ => throw new AssertionException("Unknown inline limit fixture.")
        };
        Write(Script(body, limit == "unread-stdin" ? "\"" + TestPaths.Helper + "\" wait" : "pwsh -NoLogo -NoProfile -NonInteractive -Command -"),
            limit == "output" ? "output_limit" : "timeout", limit == "output" ? 10 : 1);
    }

    private void Installed(string interpreter)
    {
        if (interpreter == "Python")
            Write(Script("import sys\nprint('Grüße 🌍')\nprint('separate error', file=sys.stderr)\nsys.exit(7)",
                "\"" + (Environment.GetEnvironmentVariable("MERSTRIDE_TEST_PYTHON") ?? (OperatingSystem.IsWindows() ? "python" : "python3")) + "\" -X utf8 -"), "exit_7");
        else if (interpreter == "Bash")
            Write(Script("printf 'Grüße 🌍\\n'\nprintf 'separate error\\n' >&2\nexit 7",
                "\"" + (Environment.GetEnvironmentVariable("MERSTRIDE_TEST_BASH") ?? (OperatingSystem.IsWindows()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git/usr/bin/bash.exe") : "bash")) + "\" --noprofile --norc"), "exit_7");
        else throw new AssertionException("Unknown installed interpreter fixture.");
    }

    private void AssertLimit(string limit)
    {
        Completed(limit == "output" ? "output_limit" : "timeout");
        if (limit == "output")
        {
            var activation = cli.Response.GetProperty("lastActivation");
            var retained = new[] { "stdoutPath", "stderrPath" }.Sum(p =>
                new FileInfo(Path.Combine(cli.State, runId, activation.GetProperty(p).GetString()!)).Length);
            Assert.That(retained, Is.EqualTo(10485760));
        }
    }

    private void InvalidDefinition(string defect)
    {
        var valid = Script("[Console]::WriteLine('ok')");
        var variants = "variants:\n- platforms: windows\n  command: ignored\n  args: []\n- platforms: ";
        Write(defect switch
        {
            "script-and-command" => valid + "\ncommand: pwsh",
            "script-and-args" => valid + "\nargs: []",
            "variants-and-command" => "variants: []\ncommand: pwsh",
            "empty-variants" => "variants: []",
            "missing-execution" => "",
            "missing-shebang" => "script: |\n  echo hello\n  echo world",
            "empty-shebang" => "script: |\n  #!\n  body",
            "unclosed-quote" => "script: |\n  #!\"pwsh\n  body",
            "folded-script" => valid.Replace("script: |", "script: >"),
            "quoted-script" => "script: \"#!pwsh\\necho hello\\n\"",
            "empty-body" => "script: |-\n  #!pwsh",
            "env-options" => Script("body", "/usr/bin/env -i pwsh"),
            "env-assignment" => Script("body", "/usr/bin/env NAME=value pwsh"),
            "env-arguments-without-S" => Script("body", "/usr/bin/env pwsh -Command -"),
            "unknown-platform" => "variants:\n- platforms: Windows\n  " + valid.Replace("\n", "\n  "),
            "empty-platforms" or "null-platforms" or "duplicate-platform" => "variants:\n- platforms: " +
                (defect == "empty-platforms" ? "[]" : defect == "null-platforms" ? "null" : "[linux, linux]") + "\n  " + valid.Replace("\n", "\n  "),
            "overlapping-variants" => variants + "[windows, linux]\n  command: ignored\n  args: []",
            "equivalent-conditions" => variants + "[windows]\n  command: ignored\n  args: []",
            "unconditional-overlap" => variants.Replace("- platforms: windows\n  ", "- ") + "windows\n  command: ignored\n  args: []",
            "unknown-variant-field" => "variants:\n- command: ignored\n  args: []\n  surprise: true",
            _ => throw new AssertionException("Unknown inline defect.")
        });
    }

    internal void Start()
    {
        cli.ExpectedChildren = 0;
        cli.Call("start", cli.FilePath, "--workspace", cli.DirectoryPath);
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout);
        runId = cli.Response.GetProperty("runId").GetString()!;
    }

    internal void Advance(int children = 1)
    {
        cli.ExpectedChildren = children;
        cli.Call("advance", runId);
    }

    internal void Completed(string outcome)
    {
        Assert.That(cli.ExitCode, Is.Zero, cli.Stdout);
        Assert.That(cli.Response.GetProperty("status").GetString(), Is.EqualTo("completed"));
        Assert.That(cli.Response.GetProperty("lastActivation").GetProperty("outcome").GetString(), Is.EqualTo(outcome));
    }

    internal string Log(string stream)
    {
        cli.ExpectedChildren = 0;
        cli.Call("status", runId);
        var activation = cli.Response.GetProperty("lastActivation").GetProperty("id").GetString()!;
        cli.Call("logs", runId, "--activation", activation, "--stream", stream);
        Assert.That(cli.ExitCode, Is.Zero);
        return cli.Response.GetProperty("log").GetString()!;
    }

    public void Dispose() { recovery?.Dispose(); cli.Dispose(); }
}
