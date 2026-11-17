using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal static class ResourceSelection
{
    internal static string Run(RunStore store, string? selector)
    {
        if (selector is not null && !Valid(selector, 'r'))
            throw new MerstrideException("INVALID_RUN_ID", "Use a full run ID or r_ followed by 8..32 lowercase hexadecimal digits.", 4);
        // Existing full-ID callers retain direct I/O and its exit-6 missing-state errors.
        if (selector?.Length == 34) return selector;
        try
        {
            if ((File.GetAttributes(store.Root) & FileAttributes.Directory) == 0)
                throw new MerstrideException("IO_ERROR", $"State root '{store.Root}' must be a directory. Preserve it and choose another absolute --state-dir.", 6);
        }
        catch (FileNotFoundException) { throw Missing(store); }
        catch (DirectoryNotFoundException) { throw Missing(store); }
        var candidates = Directory.EnumerateDirectories(store.Root).Select(Path.GetFileName)
            .Where(id => id is not null && Valid(id, 'r') && id.Length == 34 &&
                (selector is null || id.StartsWith(selector, StringComparison.Ordinal))).Cast<string>().Take(2).ToArray();
        return candidates.Length switch
        {
            0 => throw Missing(store),
            1 => candidates[0],
            _ => throw new MerstrideException("AMBIGUOUS_RUN", $"Multiple runs match in '{store.Root}'. Supply a full run ID or a longer unique r_ prefix; completed runs also count.", 4)
        };
    }

    internal static string Request(RunState run, string selector)
    {
        if (!Valid(selector, 'q')) throw new MerstrideException("INVALID_REQUEST", "Use a full request ID or q_ followed by 8..32 lowercase hexadecimal digits.", 4);
        if (selector.Length == 34) return selector;
        var ids = (run.Receipts ?? []).Select(r => r.RequestId)
            .Concat(run.Request is { } request ? [request.Id] : []).Distinct(StringComparer.Ordinal)
            .Where(id => id.StartsWith(selector, StringComparison.Ordinal)).Take(2).ToArray();
        return ids.Length switch
        {
            0 => throw new MerstrideException("INVALID_REQUEST", "No request matches this prefix. Inspect status and use its request ID; no completion was saved.", 4),
            1 => ids[0],
            _ => throw new MerstrideException("AMBIGUOUS_REQUEST", "Multiple current or historical requests match. Supply the full request ID or a longer unique q_ prefix.", 4)
        };
    }

    private static bool Valid(string value, char kind) => value.Length is >= 10 and <= 34 &&
        value[0] == kind && value[1] == '_' && value.AsSpan(2).IndexOfAnyExcept("0123456789abcdef") < 0;

    private static MerstrideException Missing(RunStore store) => new("RUN_NOT_FOUND",
        $"No matching run in '{store.Root}'. Start a workflow here, or select its original absolute --state-dir; existing state is never relocated.", 4);
}
