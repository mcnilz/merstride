using Merstride.Domain;

namespace Merstride.Cli;

internal static class ExecutionNarration
{
    internal static string WorkflowName(RunState run) => run.Definition.DisplayName ?? run.Definition.Id;

    internal static string Step(RunState run, string id) => run.Definition.Nodes.Single(node => node.Id == id).DisplayName ?? id;

    internal static int Visits(RunState run, string id) => run.Activations.Count(activation => activation.NodeId == id);

    internal static string ProcessResult(Activation activation) => activation.Outcome switch
    {
        "exit_0" => "finished successfully",
        "spawn_error" => "could not start",
        "timeout" => "timed out",
        "output_limit" => "exceeded its output limit",
        "signal" => "was stopped by a signal",
        "interrupted" => "was interrupted",
        _ => $"exited with code {activation.ExitCode}"
    };

    internal static string Failure(RunState run, Problem error) => error.Code switch
    {
        "NO_MATCHING_EDGE" when run.Activations.LastOrDefault() is { } process =>
            $"{Step(run, process.NodeId)} {ProcessResult(process)}, and the workflow has no route for that result.",
        "WORKFLOW_FAILURE" => "The workflow reached a step marked as failure.",
        "TRANSITION_LIMIT" => "The workflow reached its limit on how many times it can move between steps.",
        "CONTEXT_LIMIT" => "The instructions and selected context are too large for the agent request.",
        "INTERRUPTED" when run.Status == "interrupted" => "Execution stopped before Merstride could confirm the result. The step may already have had external effects.",
        "INTERRUPTED" => "The run was ended after an interruption. The step may already have had external effects.",
        _ => error.Message
    };

    internal static string Next(RunState run) => run.Error?.Code switch
    {
        "NO_MATCHING_EDGE" => "Fix the command or add a route for its result, then start a new run.",
        "TRANSITION_LIMIT" => "Check the workflow for repeated steps or adjust its transition limit, then start a new run.",
        "CONTEXT_LIMIT" => "Shorten the prompt or select less context, then start a new run.",
        "INTERRUPTED" when run.Status == "interrupted" => $"Inspect external effects. Use merstride resolve {run.RunId} --retry to allow a later advance, or merstride resolve {run.RunId} --fail to end the run. Retrying can repeat effects.",
        "INTERRUPTED" => "Inspect external effects before starting a new run.",
        _ => "Review what led to this step, correct the cause and start a new run."
    };
}
