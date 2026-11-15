namespace Merstride.Domain;

public sealed record Workflow(string Id, string Entry, Node[] Nodes, Edge[] Edges, int MaxTransitions, Diagnostic[] Diagnostics);
public sealed record Node(string Id, string Kind, string? Result, string? Command, string[] Args, int TimeoutSeconds);
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
    string Workspace, string Input, int TransitionCount, Problem? Error, string Cursor, Activation[] Activations);
