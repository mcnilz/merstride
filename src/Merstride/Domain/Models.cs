namespace Merstride.Domain;

public sealed record Workflow(string Id, string Entry, string Result, int MaxTransitions);
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
    string Workspace, string Input, int TransitionCount, Problem? Error)
{
    public string Cursor => Definition.Entry;
}
