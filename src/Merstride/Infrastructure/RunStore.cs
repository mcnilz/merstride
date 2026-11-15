using System.Text.Json;
using System.Text.RegularExpressions;
using Merstride.Domain;

namespace Merstride.Infrastructure;

public sealed partial class RunStore(string root)
{
    public string Root { get; } = root;

    public void Create(RunState state)
    {
        Directory.CreateDirectory(RunDirectory(state.RunId));
        using var held = Lock(state.RunId);
        Write(state);
    }

    public FileStream Lock(string id)
    {
        try { return new FileStream(Path.Combine(RunDirectory(id), "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33 or 11)
        { throw new MerstrideException("RUN_LOCKED", "Another writer holds the run lock.", 4); }
    }

    public RunState Read(string id)
    {
        try
        {
            var state = JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(RunDirectory(id), "state.json")), JsonContext.Default.RunState);
            if (state is null || state.RunId != id || state.Definition is null || state.Workspace is null ||
                state.SourceHash is null || state.ToolVersion is null || state.Input != "{}" || state.TransitionCount != 0 ||
                state.Status is not ("ready" or "completed" or "failed") ||
                state.Definition.Result is not ("success" or "failure") || !Identifier().IsMatch(state.Definition.Entry ?? ""))
                throw new JsonException("Invalid state.");
            return state;
        }
        catch (JsonException) { throw new MerstrideException("CORRUPT_STATE", "Run state is corrupt; it was not modified.", 6); }
    }

    public void Write(RunState state)
    {
        var dir = RunDirectory(state.RunId);
        var temp = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, JsonContext.Default.RunState);
                stream.Flush(true);
            }
            File.Move(temp, Path.Combine(dir, "state.json"), true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private string RunDirectory(string id)
    {
        if (!RunId().IsMatch(id)) throw new MerstrideException("INVALID_RUN_ID", "Invalid run identifier.", 4);
        return Path.Combine(Root, id);
    }

    [GeneratedRegex("^r_[0-9a-f]{32}$", RegexOptions.CultureInvariant)] private static partial Regex RunId();
    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex Identifier();
}
