using Merstride.Domain;
using Merstride.Infrastructure;

namespace Merstride.Cli;

internal static class ResourceSelection
{
    // Reserve bare r_ tokens for IDs without probing source-file existence. A
    // path prefix (./, ../, absolute) or --file selects an ID-shaped filename.
    internal static bool IsRunToken(string value) => value.StartsWith("r_", StringComparison.Ordinal) &&
        !value.Contains('/') && !value.Contains('\\');

    internal static string Run(RunStore store, string? selector, string? sourcePath = null, string? workflowId = null)
    {
        if (selector is not null && !Valid(selector, 'r'))
            throw new MerstrideException("INVALID_RUN_ID", "Use a run ID from runs: r_ followed by 6..32 lowercase hexadecimal digits.", 4);
        // Existing full-ID callers retain direct I/O and its exit-6 missing-state errors.
        if (selector?.Length == 34) return selector;
        if (selector is null)
        {
            var catalog = new RunCatalog(store);
            if (catalog.Current(sourcePath, workflowId) is { } selected)
                return catalog.ReadSelected(selected).RunId;
        }
        try
        {
            if ((File.GetAttributes(store.Root) & FileAttributes.Directory) == 0)
                throw new MerstrideException("IO_ERROR", $"State root '{store.Root}' must be a directory. Preserve it and choose another absolute --state-dir.", 6);
        }
        catch (FileNotFoundException) { throw Missing(store); }
        catch (DirectoryNotFoundException) { throw Missing(store); }
        var ids = Directory.EnumerateDirectories(store.Root).Select(Path.GetFileName)
            .Where(id => id is not null && Valid(id, 'r')).Cast<string>().ToArray();
        // An issued identity is exact, even when another identity extends it.
        if (selector is not null && ids.Contains(selector, StringComparer.Ordinal)) return selector;
        var candidates = ids
            .Where(id => id is not null && Valid(id, 'r') &&
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
        if (!Valid(selector, 'q')) throw new MerstrideException("INVALID_REQUEST", "Use a request ID: q_ followed by 6..32 lowercase hexadecimal digits.", 4);
        if (selector.Length == 34) return selector;
        var all = RequestIds(run);
        if (all.Contains(selector, StringComparer.Ordinal)) return selector;
        var ids = all
            .Where(id => id.StartsWith(selector, StringComparison.Ordinal)).Take(2).ToArray();
        return ids.Length switch
        {
            0 => throw new MerstrideException("INVALID_REQUEST", "No request matches this prefix. Inspect status and use its request ID; no completion was saved.", 4),
            1 => ids[0],
            _ => throw new MerstrideException("AMBIGUOUS_REQUEST", "Multiple current or historical requests match. Supply the full request ID or a longer unique q_ prefix.", 4)
        };
    }

    internal static string CompletionRun(RunStore store, string requestSelector)
    {
        if (!Valid(requestSelector, 'q')) throw new MerstrideException("INVALID_REQUEST", "Use the stable request ID shown by the handoff; no completion was saved.", 4);
        var candidates = new RunCatalog(store).RunIds().Select(id => (Id: id, Requests: RequestIds(store.Read(id)))).ToArray();
        var exact = candidates.Where(c => c.Requests.Contains(requestSelector, StringComparer.Ordinal)).Select(c => c.Id).ToArray();
        var matches = exact.Length > 0 ? exact : candidates.Where(c => c.Requests.Any(q => q.StartsWith(requestSelector, StringComparison.Ordinal))).Select(c => c.Id).ToArray();
        return matches.Length switch
        {
            0 => throw new MerstrideException("INVALID_REQUEST", "No stored request matches. Use the original --state-dir and request ID; inspect runs/status. No completion was saved.", 4),
            1 => matches[0],
            _ => throw new MerstrideException("AMBIGUOUS_REQUEST", "Multiple runs contain matching requests. Supply the original run ID and exact request ID; no completion was saved.", 4)
        };
    }

    private static string[] RequestIds(RunState run) => (run.Receipts ?? []).Select(r => r.RequestId)
        .Concat(run.Request is { } request ? [request.Id] : []).Distinct(StringComparer.Ordinal).ToArray();

    internal static string DiscardRun(RunStore store, string selector)
    {
        if (!Valid(selector, 'r')) throw new MerstrideException("INVALID_RUN_ID", "discard requires an explicit run ID from runs.", 4);
        var live = new RunCatalog(store).RunIds();
        if (live.Contains(selector, StringComparer.Ordinal)) return selector;
        var ids = live.Concat(new DiscardJournal(store).Ids()).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Contains(selector, StringComparer.Ordinal)) return selector;
        var matches = ids.Where(id => id.StartsWith(selector, StringComparison.Ordinal)).Take(2).ToArray();
        return matches.Length switch
        {
            0 => throw Missing(store),
            1 => matches[0],
            _ => throw new MerstrideException("AMBIGUOUS_RUN", "Multiple runs match. Use an exact run ID from runs; nothing was discarded.", 4)
        };
    }

    private static bool Valid(string value, char kind) => ResourceIds.Valid(value, kind);

    private static MerstrideException Missing(RunStore store) => new("RUN_NOT_FOUND",
        $"No matching run in '{store.Root}'. Start a workflow here, or select its original absolute --state-dir; existing state is never relocated.", 4);
}
