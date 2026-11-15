using System.Text.Json.Serialization;
using Merstride.Domain;

namespace Merstride.Infrastructure;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RunState))]
[JsonSerializable(typeof(CliResponse))]
internal partial class JsonContext : JsonSerializerContext;

public sealed record CliResponse(
    int ProtocolVersion = 1, string? RunId = null, string? Status = null, Problem? Error = null,
    string? StateRoot = null, string? Cursor = null, int? TransitionCount = null,
    string? Guide = null, string? Version = null, string? WorkflowId = null, Diagnostic[]? Diagnostics = null);
