using Merstride.Domain;

namespace Merstride.Infrastructure;

public sealed class RunDisposal(RunStore store)
{
    public DiscardRecord Discard(string id)
    {
        CheckAncestors(store.Root);
        var catalog = new RunCatalog(store);
        using var selection = catalog.Lock();
        _ = catalog.Read(); // Refuse corrupt selection before moving any state.
        var journal = new DiscardJournal(store);
        var trash = journal.DirectoryPath;
        var original = store.RunDirectory(id);
        var staged = Path.Combine(trash, id);
        if (Directory.Exists(original) && Directory.Exists(staged))
            throw Refused("Both retained and staged state exist for this identity. Preserve both for investigation.");
        if (!Directory.Exists(original) && !File.Exists(Path.Combine(staged, "state.json")))
        {
            // The last state file or directory may already have been removed before
            // an interruption. Only the durable ownership record authorizes retry.
            CheckAncestors(trash);
            var record = journal.Read(id);
            catalog.Forget(id);
            Finish(staged, journal.RecordPath(id));
            return record;
        }
        var selectedStore = Directory.Exists(original) ? store : new RunStore(trash);
        var directory = selectedStore.RunDirectory(id);
        CheckAncestors(directory);
        var held = selectedStore.Lock(id, forDiscard: true);
        RunState run;
        DiscardRecord result;
        try
        {
            run = selectedStore.Read(id);
            Validate(directory, run);
            var pending = Path.Combine(directory, "discard.pending");
            if (!File.Exists(pending))
            {
                using var marker = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                marker.Flush(true);
            }
            // Windows cannot rename a directory with an open child, even with delete
            // sharing. The durable marker now refuses all new run writers.
            held.Dispose();
            if (directory == original)
            {
                Directory.CreateDirectory(trash);
                CheckAncestors(trash);
                Directory.Move(original, staged);
                directory = staged;
            }
            catalog.Forget(id);
            result = journal.Prepare(run);
            // Validate again after staging; never recursively follow arbitrary contents.
            var files = Validate(directory, run);
            foreach (var path in files.Where(p => Path.GetFileName(p) != "state.json")) File.Delete(path);
            var logs = Path.Combine(directory, "logs");
            if (Directory.Exists(logs)) Directory.Delete(logs);
            File.Delete(Path.Combine(directory, "state.json"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new MerstrideException("DISCARD_INCOMPLETE", $"Discard of {id} could not finish. State may be staged for removal; inspect runs and retry discard {id}. No other run was removed.", 6);
        }
        finally { held.Dispose(); }
        Finish(directory, journal.RecordPath(id));
        return result;
    }

    private static void Finish(string directory, string recordPath)
    {
        try
        {
            FileAttributes? attributes;
            try { attributes = File.GetAttributes(directory); }
            catch (FileNotFoundException) { attributes = null; }
            catch (DirectoryNotFoundException) { attributes = null; }
            if (attributes is { } present)
            {
                if ((present & FileAttributes.ReparsePoint) != 0 || (present & FileAttributes.Directory) == 0)
                    throw Refused("Discard recovery found a file or link instead of its empty directory; its content was preserved.");
                CheckAncestors(directory);
                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    throw Refused("Discard recovery found unexpected remaining content; preserve it for investigation.");
                Directory.Delete(directory);
            }
            File.Delete(recordPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new MerstrideException("DISCARD_INCOMPLETE", "Discard could not finish removing its empty stage or ownership record. Resolve the storage conflict and retry the same explicit discard ID.", 6); }
    }

    private static string[] Validate(string directory, RunState run)
    {
        CheckAncestors(directory);
        var files = new List<string>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = SafeAttributes(entry);
            var name = Path.GetFileName(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                if (name != "logs") throw Refused("Unexpected directory content; nothing in this run was removed.");
                foreach (var log in Directory.EnumerateFileSystemEntries(entry))
                {
                    var logAttributes = SafeAttributes(log);
                    if ((logAttributes & FileAttributes.Directory) != 0 || !run.Activations.Any(a =>
                        Path.GetFileName(log) == a.Id + ".stdout" || Path.GetFileName(log) == a.Id + ".stderr"))
                        throw Refused("Unexpected log content; preserve it for investigation.");
                    files.Add(log);
                }
            }
            else
            {
                if (name is not ("state.json" or "writer.lock" or "discard.pending") &&
                    !(name.Length == 36 && name.EndsWith(".tmp", StringComparison.Ordinal) && name.AsSpan(0, 32).IndexOfAnyExcept("0123456789abcdef") < 0))
                    throw Refused("Unexpected file content; nothing in this run was removed.");
                files.Add(entry);
            }
        }
        return files.ToArray();
    }

    private static FileAttributes SafeAttributes(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0) throw Refused("Discard refuses symbolic links or reparse points; preserve the linked content.");
        if ((attributes & FileAttributes.ReadOnly) != 0) throw new IOException("Read-only discard input.");
        return attributes;
    }

    private static void CheckAncestors(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw Refused("Discard refuses paths through symbolic links or reparse points.");
    }

    private static MerstrideException Refused(string message) => new("DISCARD_REFUSED", message, 6);
}
