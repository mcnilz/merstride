using System.Text.Json;
using Merstride.Domain;

namespace Merstride.Application;

public static class AgentCompletion
{
    public static (RunState State, CompletionReceipt Receipt) Complete(RunState run, string requestId, string outcome, JsonElement data)
    {
        var historical = run.Receipts?.SingleOrDefault(r => r.RequestId == requestId);
        if (historical is not null)
        {
            if (historical.Outcome != outcome || JsonValues.Compact(historical.Data) != JsonValues.Compact(data))
                throw new MerstrideException("COMPLETION_CONFLICT", "The accepted request has a different payload.", 4);
            return (run, historical);
        }
        var request = run.Request;
        if (run.Status != "waiting_agent" || request is null || request.Id != requestId)
            throw new MerstrideException("INVALID_REQUEST", "Unknown current request identifier.", 4);
        if (!request.AllowedOutcomes.Contains(outcome, StringComparer.Ordinal))
            throw new MerstrideException("INVALID_OUTCOME", "Outcome is not allowed by the current request.", 4);
        var receipt = new CompletionReceipt(requestId, request.ActivationId, outcome, data, DateTimeOffset.UtcNow);
        var route = run.Definition.Edges.Single(e => e.Source == request.NodeId && e.Outcome == outcome);
        var activations = run.Activations.Select(a => a.Id == request.ActivationId
            ? a with { FinishedAt = receipt.AcceptedAt, Outcome = outcome } : a).ToArray();
        return (run with { Status = "ready", Cursor = route.Target, TransitionCount = run.TransitionCount + 1,
            Request = null, Activations = activations, Receipts = [.. run.Receipts ?? [], receipt] }, receipt);
    }
}
