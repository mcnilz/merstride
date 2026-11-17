using System.Text.Json;
using System.Text;
using System.Globalization;
using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal sealed class CliOutput(bool json)
{
    private bool activityUnavailable;

    internal void Activity(RunState run, string message, Activation? activation = null)
    {
        if (json || activityUnavailable) return;
        try
        {
            Console.Error.WriteLine(Readable($"Activity [{run.RunId}, node {activation?.NodeId ?? run.Cursor}" +
                (activation is null ? "" : $", activation {activation.Id}") + $"]: {message}"));
            Console.Error.Flush();
        }
        // A closed progress pipe must not turn a saved transition into an execution failure.
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { activityUnavailable = true; }
    }
    internal static bool JsonRequested(string[] args) => args.TakeWhile(a => a != "--")
        .Any(a => a == "--json" || a.StartsWith("--json=", StringComparison.Ordinal));

    internal int Write(CliResponse response, int code = 0, string? logActivation = null)
    {
        if (json) Console.WriteLine(JsonSerializer.Serialize(response, JsonContext.Default.CliResponse));
        else WriteText(response, logActivation);
        return code;
    }

    private static void WriteText(CliResponse response, string? logActivation)
    {
        if (response.Guide is { } guide) Console.WriteLine(Readable(guide));
        else if (response.Version is { } version) Console.WriteLine($"Merstride {Readable(version)} (protocol {response.ProtocolVersion})");
        else if (response.Log is { } log)
        {
            Line("Run", response.RunId);
            Line("Activation", logActivation);
            Line("Stream", response.Stream);
            Line("Tail truncated", response.Truncated == true ? "yes" : "no");
            Console.WriteLine(); Console.WriteLine(Readable(log));
        }
        else if (response.RunId is not null) WriteRun(response);
        else if (response.WorkflowId is { } workflow) Line("Valid workflow", workflow);

        foreach (var diagnostic in response.Diagnostics ?? [])
            Console.Error.WriteLine($"{(diagnostic.Severity == "warning" ? "Warning" : "Error")} {Readable(diagnostic.Code)}{Location(diagnostic.Location)}: {Readable(diagnostic.Message)}");
        if (response.Error is { } error && !(response.Diagnostics ?? []).Any(d =>
                d.Code == error.Code && d.Message == error.Message && d.Location == error.Location))
            Console.Error.WriteLine($"Error {Readable(error.Code)}{Location(error.Location)}: {Readable(error.Message)}");
    }

    private static void WriteRun(CliResponse response)
    {
        Line("Run", response.RunId);
        Line("Status", response.Status);
        Line("Cursor", response.Cursor);
        Line("State root", response.StateRoot);
        if (response.TransitionCount is { } transitions) Line("Transitions", transitions.ToString(CultureInfo.InvariantCulture));
        if (response.LastActivation is { } activation)
            Line("Last activation", $"{activation.Id} (node: {activation.NodeId}, outcome: {activation.Outcome ?? "pending"})");
        if (response.Receipt is { } receipt) Line("Completion", $"{receipt.RequestId} -> {receipt.Outcome} (activation: {receipt.ActivationId})");
        if (response.Status == "interrupted")
            Console.WriteLine("Next: inspect external effects, then explicitly resolve --retry or --fail. Advance never replays interrupted work.");
        if (response.Request is not { } request) return;
        Console.WriteLine();
        Line("Request", request.Id);
        Line("Node", request.NodeId);
        Console.WriteLine("Prompt:"); Console.WriteLine(Readable(request.Prompt));
        Line("Allowed outcomes", string.Join(", ", request.AllowedOutcomes));
        Console.WriteLine("Next: perform the prompt's task, then complete this request with an allowed outcome. Call advance separately afterward.");
        Console.WriteLine("Context:");
        if (request.Context.Length == 0) Console.WriteLine("  (none)");
        foreach (var item in request.Context)
        {
            if (!item.Available) { Console.WriteLine($"  {Readable(item.Selector)}: unavailable"); continue; }
            var details = new List<string>();
            if (item.SourceActivationId is { } source) details.Add("activation: " + source);
            if (item.Truncated is { } truncated) details.Add("truncated: " + (truncated ? "yes" : "no"));
            if (item.ObservedBytes is { } bytes) details.Add("observed bytes: " + bytes.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine($"  {Readable(item.Selector)}{(details.Count == 0 ? "" : " (" + Readable(string.Join(", ", details)) + ")")}:");
            var value = item.Value.ValueKind == JsonValueKind.String ? item.Value.GetString()! : item.Value.GetRawText();
            Console.WriteLine(Readable(value));
        }
    }

    private static void Line(string label, string? value)
    {
        if (value is not null) Console.WriteLine($"{label}: {Readable(value)}");
    }

    private static string Location(SourceLocation? location) => location is null ? "" :
        $" (line {location.Line.ToString(CultureInfo.InvariantCulture)}, column {location.Column.ToString(CultureInfo.InvariantCulture)})";

    private static string Readable(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var rune in text.Replace("\r\n", "\n").EnumerateRunes())
        {
            if (rune.Value is not ('\n' or '\t') && (Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format))
            {
                foreach (var ch in rune.ToString())
                    result.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
            }
            else result.Append(rune.ToString());
        }
        return result.ToString();
    }
}
