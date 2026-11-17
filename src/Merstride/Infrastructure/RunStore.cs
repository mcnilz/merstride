using System.Text.Json;
using System.Text.RegularExpressions;
using Merstride.Domain;
using Merstride.Application;

namespace Merstride.Infrastructure;

public sealed partial class RunStore(string root) : IRunPersistence, IContextLogs
{
    public string Root { get; } = root;

    public void Create(RunState state)
    {
        if (Directory.Exists(RunDirectory(state.RunId)))
            throw new MerstrideException("ID_COLLISION", "This run identity already exists; it was not overwritten.", 4);
        Directory.CreateDirectory(RunDirectory(state.RunId));
        using var held = Lock(state.RunId);
        Write(state);
    }

    public FileStream Lock(string id, bool forDiscard = false)
    {
        var held = Exclusive(Path.Combine(RunDirectory(id), "writer.lock"), "RUN_LOCKED", "Another writer holds the run lock.");
        if (!forDiscard && File.Exists(Path.Combine(RunDirectory(id), "discard.pending")))
        {
            held.Dispose();
            throw new MerstrideException("DISCARD_PENDING", "This run is being discarded. Inspect runs and retry its explicit discard; it cannot execute again.", 4);
        }
        return held;
    }

    public static FileStream Exclusive(string path, string code, string message)
    {
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        // FileShare.None exposes raw EWOULDBLOCK: 11 on Linux, 35 on macOS.
        catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33 or 11 ||
            OperatingSystem.IsMacOS() && ex.HResult == 35)
        { throw new MerstrideException(code, message, 4); }
    }

    public RunState Read(string id)
    {
        try
        {
            // Readers retain the opened snapshot while allowing same-directory atomic replacement.
            using var snapshot = OpenSnapshot(Path.Combine(RunDirectory(id), "state.json"));
            var state = JsonSerializer.Deserialize(snapshot, JsonContext.Default.RunState);
            if (state is null || state.RunId != id || state.Definition is null || state.Workspace is null ||
                state.SourceHash is null || state.ToolVersion is null || state.Input is null || state.TransitionCount < 0 ||
                state.Status is not ("ready" or "executing" or "interrupted" or "completed" or "failed" or "waiting_agent") ||
                state.Definition.Nodes is null || state.Definition.Edges is null || state.Activations is null ||
                state.Definition.Nodes.Any(n => n is null) ||
                !Identifier().IsMatch(state.Cursor ?? "") || !state.Definition.Nodes.Any(n => n.Id == state.Cursor) ||
                state.Activations.Any(a => a is null || !ActivationId().IsMatch(a.Id ?? "") ||
                    !state.Definition.Nodes.Any(n => n.Id == a.NodeId)) ||
                state.Status == "executing" && (state.Activations.Length == 0 ||
                    state.Activations[^1].NodeId != state.Cursor || state.Activations[^1].FinishedAt is not null ||
                    state.Definition.Nodes.Single(n => n.Id == state.Cursor).Kind != "process"))
                throw new JsonException("Invalid state.");
            return state;
        }
        catch (JsonException) { throw new MerstrideException("CORRUPT_STATE", "Run state is corrupt; it was not modified.", 6); }
    }

    private static FileStream OpenSnapshot(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
            // Windows ReplaceFile can briefly hide or own the destination handle. Retry
            // opening only; a missing file still fails after 100 ms and never resets a run.
            catch (IOException ex) when (OperatingSystem.IsWindows() && (ex.HResult & 0xffff) is 2 or 32 or 33 && attempt < 20)
            { Thread.Sleep(5); }
        }
    }

    public RunState Status(string id)
    {
        if (File.Exists(Path.Combine(RunDirectory(id), "discard.pending")))
            throw new MerstrideException("DISCARD_PENDING", "This run is being discarded. Inspect runs and retry its explicit discard; it cannot execute again.", 4);
        var state = Read(id);
        if (state.Status != "executing") return state;
        FileStream held;
        try { held = Lock(id); }
        catch (MerstrideException ex) when (ex.Problem.Code == "RUN_LOCKED") { return state; }
        using (held)
        {
            // Reread after excluding writers: a completed commit may have won the race.
            state = Read(id);
            return state.Status == "executing" ? WorkflowEngine.Interrupted(state) : state;
        }
    }

    public void Write(RunState state)
    {
        AtomicWrite(Path.Combine(RunDirectory(state.RunId), "state.json"), stream => JsonSerializer.Serialize(stream, state, JsonContext.Default.RunState));
    }

    public static void AtomicWrite(string target, Action<FileStream> write)
    {
        var dir = Path.GetDirectoryName(target)!;
        var temp = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(true);
            }
            if (File.Exists(target)) File.Replace(temp, target, null);
            else File.Move(temp, target);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public string ReadLog(RunState run, string activationId, string stream, int tailBytes, out bool truncated)
    {
        if (!ActivationId().IsMatch(activationId) || !run.Activations.Any(a => a.Id == activationId))
            throw new MerstrideException("INVALID_ACTIVATION", "Unknown activation identifier.", 4);
        using var file = new FileStream(Path.Combine(RunDirectory(run.RunId), "logs", $"{activationId}.{stream}"),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        truncated = file.Length > tailBytes;
        var length = (int)Math.Min(file.Length, tailBytes);
        file.Seek(-length, SeekOrigin.End);
        var bytes = new byte[length];
        file.ReadExactly(bytes);
        // Drop partial leading UTF-8 continuation bytes when cutting a tail.
        var offset = 0;
        if (truncated) while (offset < bytes.Length && (bytes[offset] & 0xc0) == 0x80) offset++;
        return System.Text.Encoding.UTF8.GetString(bytes.AsSpan(offset));
    }

    public string RunDirectory(string id)
    {
        if (!RunId().IsMatch(id)) throw new MerstrideException("INVALID_RUN_ID", "Invalid run identifier.", 4);
        return Path.Combine(Root, id);
    }

    [GeneratedRegex("^r_[0-9a-f]{6,32}$", RegexOptions.CultureInvariant)] private static partial Regex RunId();
    [GeneratedRegex("^a_[0-9a-f]{32}$", RegexOptions.CultureInvariant)] private static partial Regex ActivationId();
    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex Identifier();
}
