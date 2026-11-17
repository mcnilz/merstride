using System.Text.Json.Serialization;
using Merstride.Domain;

namespace Merstride.Infrastructure;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RunState))]
[JsonSerializable(typeof(CliResponse))]
[JsonSerializable(typeof(SelectionContext))]
[JsonSerializable(typeof(DiscardRecord))]
internal partial class JsonContext : JsonSerializerContext;

public sealed record CliResponse(
    int ProtocolVersion = 1, string? RunId = null, string? Status = null, Problem? Error = null,
    string? StateRoot = null, string? Cursor = null, int? TransitionCount = null,
    string? Guide = null, string? Version = null, string? WorkflowId = null, Diagnostic[]? Diagnostics = null,
    Activation? LastActivation = null, string? Log = null, string? Stream = null, bool? Truncated = null,
    AgentRequest? Request = null, CompletionReceipt? Receipt = null, string? SourcePath = null,
    RunSummary[]? Runs = null, bool? Discarded = null);
