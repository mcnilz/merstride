using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

// Reuse the feature fixtures and their independent effect assertions. In this pass
// mutations execute as readable commands; JSON status observes the saved result.
// Only completion receipts are reissued, using their documented idempotent retry.
internal sealed class UsabilityAuditWorld : IDisposable
{
    private static readonly AsyncLocal<UsabilityAuditWorld?> current = new();
    internal static bool Complementary => current.Value is not null;
    internal static bool ConvertJson => current.Value?.convertJson == true;
    internal static string LastText => current.Value?.lastText ?? "";
    internal static int Probes => current.Value?.probes ?? 0;
    private string lastText = "";
    private int calls;
    private int probes;
    private bool convertJson = true;
    internal int Calls => calls;

    internal UsabilityAuditWorld() => current.Value = this;
    internal static void Structured(Action action)
    {
        var session = current.Value;
        if (session is null) { action(); return; }
        var previous = session.convertJson;
        try { session.convertJson = false; action(); }
        finally { session.convertJson = previous; }
    }

    internal void AssertExecuted() => Assert.That(calls, Is.Positive, "The readable pass must execute native commands, not only label the scenario reviewed.");
    internal static void ObserveOriginal(CliWorld cli, string[] args)
    {
        var session = current.Value!;
        session.calls++;
        session.lastText = cli.Stdout + cli.Stderr;
        if (cli.Stdout.StartsWith('{') || args.TakeWhile(a => a != "--").Any(a => a.StartsWith("--json", StringComparison.Ordinal))) return;
        var command = CliWorld.Command(args);
        var help = args.TakeWhile(a => a != "--").Any(a => a is "--help" or "-h" or "--version");
        if (command is not ("validate" or "status" or "logs" or "runs" or "guide" or "creation-guide" or "version" or "arguments" or "discovery") && !(help && cli.ExitCode == 0)) return;
        var text = cli.Stdout; var error = cli.Stderr; var exit = cli.ExitCode; var code = cli.ErrorCode;
        var response = cli.Response;
        var children = cli.ExpectedChildren;
        try
        {
            cli.ExpectedChildren = 0;
            cli.Direct(["--json", .. args], true, false);
            session.probes++;
            Assert.That(cli.ExitCode, Is.EqualTo(exit));
            CheckText(text, error, cli.Response, command, exit);
        }
        finally
        {
            cli.Stdout = text; cli.Stderr = error; cli.ExitCode = exit; cli.ErrorCode = code;
            cli.Response = response; cli.ExpectedChildren = children;
        }
    }

    internal static void Invoke(CliWorld cli, string[] args)
    {
        var session = current.Value!;
        var options = args.TakeWhile(a => a != "--").ToArray();
        if (options.Count(a => a == "--json") > 1 || options.Any(a => a.StartsWith("--json=", StringComparison.Ordinal)))
        {
            // This requirement deliberately selects protocol errors for malformed
            // format switches. Removing them would test a different request.
            cli.Direct(args, true, false); session.calls++; return;
        }
        var textArgs = args.TakeWhile(a => a != "--").Where(a => a != "--json")
            .Concat(args.SkipWhile(a => a != "--")).ToArray();
        var command = CliWorld.Command(textArgs);
        var help = textArgs.TakeWhile(a => a != "--").Any(a => a is "--help" or "-h");
        // Successful discard has no readable state afterwards. Observe its source
        // before removal; the owning fixture independently checks removal/survivors.
        JsonElement prior = default;
        if (command == "discard" && !help)
        {
            var index = Array.IndexOf(textArgs, "discard");
            cli.Direct(["status", textArgs[index + 1], .. StateOption(textArgs), "--json"], true);
            session.probes++;
            prior = cli.Response;
        }
        cli.Direct(textArgs, false, false);
        var text = cli.Stdout;
        var activity = cli.Stderr;
        var exit = cli.ExitCode;
        var errorCode = cli.ErrorCode;
        session.calls++;
        session.lastText = text + activity;
        Assert.That(text + activity, Does.Not.Contain('\u001b').And.Not.Contain('\0'));
        Assert.That(text.Length + activity.Length, Is.LessThan(1000000), "Readable output remains bounded.");
        var match = Regex.Match(text, "(?m)^Run: (r_[0-9a-f]+)\\r?$");
        var compactMatch = Regex.Match(text, "(?m)^(?:Finished successfully|Paused at |Failed at |Interrupted at )[^\\r\\n]*\\brun (r_[0-9a-f]+)\\)\\.\\r?$");
        var compact = !match.Success && compactMatch.Success;
        var mutating = !help && command is "start" or "advance" or "resolve" or "use";
        var children = cli.ExpectedChildren;
        try
        {
            cli.ExpectedChildren = 0;
            if (command == "discard" && !help && exit == 0)
            {
                Assert.That(text, Does.Contain("Discarded run: ").And.Contain("Other runs were retained."));
                cli.Response = prior;
                // No synthetic protocol response is created. Callers inspect the
                // real pre-discard status and independently assert postconditions.
                cli.Stdout = prior.GetRawText(); cli.Stderr = "";
            }
            else if (mutating && (match.Success || compact))
            {
                var selector = (compact ? compactMatch : match).Groups[1].Value;
                var state = StateOption(args);
                if (!compact)
                {
                    var root = Regex.Match(text, "(?m)^State root: (.+)\\r?$").Groups[1].Value.TrimEnd('\r');
                    Assert.That(root, Is.Not.Empty);
                    state = ["--state-dir", root];
                }
                cli.Direct(["status", selector, .. state, "--json"], true);
                session.probes++;
                Assert.That(cli.ExitCode, Is.Zero, cli.Stdout + cli.Stderr);
            }
            else if (help && exit == 0 || command is not ("start" or "advance" or "resolve" or "use" or "discard" or "complete") || command == "complete" && exit == 0)
            {
                // Inspection and rejected requests have no effects. A successful
                // complete is the public, stable-ID receipt retry, never advance.
                cli.Direct(args, true);
                session.probes++;
                Assert.That(cli.ExitCode, Is.EqualTo(exit), "Text and structured command exits agree.");
            }
            else
            {
                // A failed mutation may already have saved state or external
                // effects. Preserve its actual output; never execute it twice.
                Assert.That(errorCode, Is.Not.Empty, "The original readable failure has an actionable code.");
                cli.Response = default;
            }
            if (cli.Response.ValueKind == JsonValueKind.Object) CheckText(text, activity, cli.Response, help && exit == 0 ? "help" : command, exit, compact);
            cli.ExitCode = exit;
            cli.ErrorCode = errorCode;
        }
        finally { cli.ExpectedChildren = children; }
    }

    private static string[] StateOption(string[] args)
    {
        var index = Array.IndexOf(args, "--state-dir");
        return index < 0 ? [] : ["--state-dir", args[index + 1]];
    }

    private static void CheckText(string text, string activity, JsonElement response, string command, int exit, bool compact = false)
    {
        if (command == "discard" && exit == 0) return;
        if (response.GetProperty("error") is { ValueKind: JsonValueKind.Object } error)
            Assert.That(activity, Does.Contain("Error " + error.GetProperty("code").GetString()));
        if (response.GetProperty("diagnostics") is { ValueKind: JsonValueKind.Array } diagnostics)
            foreach (var diagnostic in diagnostics.EnumerateArray())
            {
                Assert.That(activity, Does.Contain(diagnostic.GetProperty("code").GetString()!));
                if (diagnostic.GetProperty("location").ValueKind != JsonValueKind.Null)
                    Assert.That(activity, Does.Contain("line ").And.Contain("column "));
            }
        if (response.GetProperty("guide") is { ValueKind: JsonValueKind.String } guide)
            Assert.That(text.Replace("\r\n", "\n").TrimEnd('\n'), Is.EqualTo(Escape(guide.GetString()!).TrimEnd('\n')));
        else if (response.GetProperty("version") is { ValueKind: JsonValueKind.String } version)
            Assert.That(text, Does.Contain("Merstride " + version.GetString()).And.Contain("protocol 1"));
        else if (response.GetProperty("log") is { ValueKind: JsonValueKind.String } log)
            Assert.That(text.Replace("\r\n", "\n"), Does.Contain(Escape(log.GetString()!)).And.Contain("Tail truncated:"));
        else if (response.GetProperty("runs") is { ValueKind: JsonValueKind.Array } runs)
        {
            foreach (var run in runs.EnumerateArray())
                Assert.That(text, Does.Contain(run.GetProperty("runId").GetString()!).And.Contain(run.GetProperty("status").GetString()!));
            Assert.That(text, Does.Contain("merstride use <run-id>").And.Contain("merstride discard <run-id>"));
        }
        else if (response.GetProperty("runId") is { ValueKind: JsonValueKind.String } id)
        {
            if (compact)
            {
                var result = response.GetProperty("status").GetString() switch
                {
                    "completed" => "Finished successfully",
                    "waiting_agent" => "Paused at ",
                    "interrupted" => "Interrupted at ",
                    "failed" => "Failed at ",
                    _ => throw new AssertionException("Unexpected compact execution status.")
                };
                Assert.That(text, Does.Contain(result).And.Contain("run " + id.GetString() + ")."));
                if (result != "Finished successfully") Assert.That(text, Does.Contain("Next:"));
            }
            else
            {
                Assert.That(text, Does.Contain("Run: " + id.GetString()).And.Contain("Next:"));
                foreach (var field in new[] { ("status", "Status"), ("cursor", "Cursor") })
                    if (response.GetProperty(field.Item1).ValueKind == JsonValueKind.String)
                        Assert.That(text, Does.Contain(field.Item2 + ": " + response.GetProperty(field.Item1).GetString()));
                if (response.GetProperty("transitionCount").ValueKind == JsonValueKind.Number)
                    Assert.That(text, Does.Contain("Transitions: " + response.GetProperty("transitionCount").GetInt32().ToString(CultureInfo.InvariantCulture)));
            }
            if (response.GetProperty("request") is { ValueKind: JsonValueKind.Object } request)
            {
                Assert.That(text.Replace("\r\n", "\n"), Does.Contain("Request: " + request.GetProperty("id").GetString())
                    .And.Contain(Escape(request.GetProperty("prompt").GetString()!)));
                foreach (var outcome in request.GetProperty("allowedOutcomes").EnumerateArray())
                    Assert.That(text, Does.Contain(outcome.GetString()!));
                foreach (var context in request.GetProperty("context").EnumerateArray())
                {
                    var selector = context.GetProperty("selector").GetString()!;
                    Assert.That(text, Does.Contain(selector));
                    if (!context.GetProperty("available").GetBoolean())
                        Assert.That(text, Does.Contain(selector + ": unavailable"));
                    else
                    {
                        var value = context.GetProperty("value");
                        if (value.ValueKind == JsonValueKind.String)
                            Assert.That(text.Replace("\r\n", "\n"), Does.Contain(Escape(value.GetString()!)));
                        else
                        {
                            var header = Regex.Match(text, "(?m)^  " + Regex.Escape(selector) + "(?: \\([^\\r\\n]*\\))?:\\r?\\n");
                            Assert.That(header.Success, Is.True);
                            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text[(header.Index + header.Length)..]));
                            using var observed = JsonDocument.ParseValue(ref reader);
                            Assert.That(JsonElement.DeepEquals(observed.RootElement, value), Is.True, "Readable selected structured context retains its semantic value.");
                        }
                        if (context.GetProperty("truncated").ValueKind == JsonValueKind.True)
                            Assert.That(text, Does.Contain("truncated: yes"));
                    }
                }
            }
            if (response.GetProperty("receipt") is { ValueKind: JsonValueKind.Object } receipt)
                Assert.That(text, Does.Contain("Completion: " + receipt.GetProperty("requestId").GetString()).And.Contain(receipt.GetProperty("outcome").GetString()!));
        }
        else if (response.GetProperty("workflowId") is { ValueKind: JsonValueKind.String } workflow)
            Assert.That(text, Does.Contain("Valid workflow: " + workflow.GetString()));
        else Assert.That(text, Is.Empty);
    }

    private static string Escape(string text)
    {
        var value = new StringBuilder();
        foreach (var rune in text.Replace("\r\n", "\n").EnumerateRunes())
            if (rune.Value is not ('\n' or '\t') && (Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format))
                foreach (var character in rune.ToString()) value.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            else value.Append(rune.ToString());
        return value.ToString();
    }

    public void Dispose() => current.Value = null;
}
