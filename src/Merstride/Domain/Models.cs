using System.Text.Json;

namespace Merstride.Domain;

public sealed record Workflow(string Id, string Entry, Node[] Nodes, Edge[] Edges, int MaxTransitions, Diagnostic[] Diagnostics);
public sealed record Node(string Id, string Kind, string? Result, string? Command, string[] Args, int TimeoutSeconds,
    AgentTask? Agent = null);
public sealed record AgentTask(string Prompt, string[] Context);
public sealed record AgentRequest(string Id, string ActivationId, string NodeId, string Prompt, string[] AllowedOutcomes, ContextItem[] Context);
public sealed record ContextItem(string Selector, bool Available, JsonElement Value, string? SourceActivationId = null,
    bool? Truncated = null, long? ObservedBytes = null);
public sealed record CompletionReceipt(string RequestId, string ActivationId, string Outcome, JsonElement Data, DateTimeOffset AcceptedAt);
public sealed record Edge(string Source, string Target, string? Outcome);
public sealed record Activation(string Id, string NodeId, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt,
    string? Outcome, int? ExitCode, string StdoutPath, string StderrPath,
    long StdoutBytes = 0, long StderrBytes = 0, bool StdoutTruncated = false, bool StderrTruncated = false);
public sealed record SourceLocation(int Line, int Column);
public sealed record Problem(string Code, string Message, SourceLocation? Location = null);
public sealed record Diagnostic(string Severity, string Code, string Message, SourceLocation? Location);
public sealed class MerstrideException(string code, string message, int exitCode, SourceLocation? location = null) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
    public Problem Problem { get; } = new(code, message, location);
}

public sealed record RunState(
    string RunId, string Status, Workflow Definition, string SourceHash, string ToolVersion,
    string Workspace, string Input, int TransitionCount, Problem? Error, string Cursor, Activation[] Activations,
    AgentRequest? Request = null, CompletionReceipt[]? Receipts = null);
