using System.Text.Json;
using System.Text;
using System.Globalization;
using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal sealed class CliOutput(bool json)
{
    private bool activityUnavailable;

    internal void Activity(RunState run, string message)
    {
        if (json || activityUnavailable) return;
        try
        {
            Console.Error.WriteLine(Readable($"[{run.RunId}] {message}"));
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
        if (response.Start is { } start)
        {
            Line("Creation", start.Created ? "saved new run" : "not completed; no initial advance");
            Line("Phase", start.Phase);
            Line("Initial advance", start.Advance switch
            {
                "finished" => "finished; see the resulting run status below",
                "interrupted" => "interrupted; inspect external effects before explicit recovery",
                "error" => "stopped by an error; saved progress was retained, without automatic retry",
                _ => "not started; no node executed by this invocation"
            });
        }
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
        else if (response.Runs is { } runs)
        {
            Line("State root", response.StateRoot);
            if (runs.Length == 0) Console.WriteLine("No retained runs match. Start a workflow to create one.");
            foreach (var run in runs)
                Console.WriteLine(Readable($"{run.RunId}  {run.Status}{(run.Current ? "  (current)" : "")}  {run.WorkflowId ?? "unknown workflow"}  {run.SourcePath ?? "source unavailable (legacy or unreadable state)"}"));
            Console.WriteLine("Select: merstride use <run-id>. Remove one run deliberately: merstride discard <run-id>.");
        }
        else if (response.Discarded == true)
        {
            Line("Discarded run", response.RunId);
            Line("Workflow", response.WorkflowId);
            Line("Source", response.SourcePath);
            Line("State root", response.StateRoot);
            Console.WriteLine("Removed this run's state, logs and receipts. Other runs were retained.");
        }
        else if (response.RunId is not null) WriteRun(response);
        else if (response.WorkflowId is { } workflow) Line("Valid workflow", workflow);

        foreach (var diagnostic in response.Diagnostics ?? [])
            Console.Error.WriteLine($"{(diagnostic.Severity == "warning" ? "Warning" : "Error")} {Readable(diagnostic.Code)}{Location(diagnostic.Location)}: {Readable(diagnostic.Message)}");
        if (response.Error is { } error && !(response.Diagnostics ?? []).Any(d =>
                d.Code == error.Code && d.Message == error.Message && d.Location == error.Location))
        {
            Console.Error.WriteLine($"Error {Readable(error.Code)}{Location(error.Location)}: {Readable(error.Message)}");
            var next = error.Code switch
            {
                "INVALID_OUTCOME" or "INVALID_REQUEST" => "Next: inspect status and use the original request ID and an allowed outcome; no new completion was accepted.",
                "COMPLETION_CONFLICT" => "Next: inspect status and the original receipt; retry its accepted payload or handle the newer request separately.",
                "RUN_NOT_FOUND" or "AMBIGUOUS_RUN" or "STALE_CONTEXT" or "CORRUPT_CONTEXT" or "INVALID_RUN_ID" =>
                    "Next: inspect merstride runs in the original state root; use an exact run ID or merstride use <run-id>, or start the intended workflow.",
                "IO_ERROR" => "Next: check the original workflow/state path and permissions, then inspect status before retrying; external effects may already have happened.",
                _ => null
            };
            if (next is not null) Console.Error.WriteLine(next);
        }
    }

    private static void WriteRun(CliResponse response)
    {
        Line("Run", response.RunId);
        Line("Workflow", response.WorkflowId);
        Line("Status", (response.Status ?? "unavailable; inspect this exact run in the original state root") + (response.Status switch
        {
            "ready" => " (ready to advance; next node has not executed)",
            "executing" => " (process activation in progress)",
            "waiting_agent" => " (waiting for your agent's completion)",
            "completed" => " (finished successfully)",
            "failed" => " (finished with failure)",
            "interrupted" => " (external effects uncertain; explicit decision required)",
            _ => ""
        }));
        Line("Cursor", response.Cursor);
        Line("Result", response.Status switch
        {
            "completed" => "success", "failed" => "failure", "interrupted" => "unresolved",
            null => "unknown", _ => "not finished"
        });
        if (response.TransitionCount is { } transitions) Line("Transitions", transitions.ToString(CultureInfo.InvariantCulture));
        if (response.LastActivation is { } activation)
            Line(activation.StdoutPath.Length == 0 ? "Last agent visit" : "Last process", $"{activation.NodeId} - {Outcome(activation)}");
        if (response.Receipt is { } receipt) Line("Completion", $"{receipt.RequestId} - {receipt.Outcome} (saved receipt; retries do not move the flow)");
        if (response.Status == "interrupted")
            Console.WriteLine("Next: inspect external effects, then explicitly resolve --retry or --fail. Advance never replays interrupted work.");
        else if (response.Status == "ready")
            Console.WriteLine($"Next: merstride advance {Readable(response.RunId!)}. Completion and retry resolution never execute the next node.");
        else if (response.Status == "completed")
            Console.WriteLine("Next: no further execution for this run. Start a new run to execute again.");
        else if (response.Status == "failed")
            Console.WriteLine("Next: inspect the failure reason and logs; correct the cause and start a new run. Only interrupted runs support resolve.");
        else if (response.Status == "executing")
            Console.WriteLine("Next: wait for the current advance to finish; status can inspect it without executing work.");
        else if (response.Status is null)
            Console.WriteLine("Next: inspect status/logs for this exact run and state root after restoring access; do not repeat start to recover it.");
        Console.WriteLine();
        Line("Source", response.SourcePath);
        Line("State root", response.StateRoot);
        if (response.LastActivation is { } last && last.StdoutPath.Length > 0)
            Line("Log activation", last.Id);
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

    internal static string Outcome(Activation activation)
    {
        // Agent outcomes may legally use process tokens such as timeout or exit_0.
        // Process activations always own log paths; agent visits never do.
        if (activation.StdoutPath.Length == 0)
            return activation.Outcome is { } choice ? "agent outcome " + choice : "pending";
        return activation.Outcome switch
        {
            null => "pending",
            "spawn_error" => "process could not be launched (spawn_error)",
            "timeout" => "process time limit reached (timeout)",
            "output_limit" => "process output limit reached (output_limit)",
            "signal" => "process ended by signal (signal)",
            "interrupted" => "execution interrupted; effects uncertain (interrupted)",
            var outcome when outcome.StartsWith("exit_", StringComparison.Ordinal) => $"exit code {activation.ExitCode} ({outcome})",
            var outcome => outcome
        };
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
