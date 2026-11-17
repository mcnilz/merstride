using System.Text.Json;
using Merstride.Domain;

namespace Merstride.Infrastructure;

public sealed record DiscardRecord(string RunId, string? SourcePath, string WorkflowId);

// Minimal ownership evidence survives removal of the snapshot, but contains no
// input, process logs, prompts or completion receipts. Removed only after cleanup.
public sealed class DiscardJournal(RunStore store)
{
    public string DirectoryPath => Path.Combine(store.Root, ".trash");
    public string RecordPath(string id) => Path.Combine(DirectoryPath, id + ".discard.json");

    public string[] Ids()
    {
        // A staging conflict must not hide retained or pending live runs.
        if (!Directory.Exists(DirectoryPath)) return [];
        return Directory.EnumerateDirectories(DirectoryPath).Select(Path.GetFileName)
            .Concat(Directory.EnumerateFiles(DirectoryPath, "*.discard.json").Select(path => Path.GetFileName(path)[..^13]))
            .Where(id => ResourceIds.Valid(id, 'r')).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public DiscardRecord Read(string id)
    {
        var path = RecordPath(id);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new MerstrideException("DISCARD_REFUSED", "Discard recovery refuses a linked ownership record.", 6);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var record = JsonSerializer.Deserialize(file, JsonContext.Default.DiscardRecord);
            if (record is null || record.RunId != id || !ResourceIds.Valid(record.RunId, 'r') ||
                string.IsNullOrEmpty(record.WorkflowId) || record.SourcePath is not null && !Path.IsPathFullyQualified(record.SourcePath))
                throw new JsonException();
            return record;
        }
        catch (JsonException) { throw new MerstrideException("DISCARD_REFUSED", "Discard ownership record is corrupt; its contents were preserved.", 6); }
    }

    public DiscardRecord Prepare(RunState run)
    {
        var record = new DiscardRecord(run.RunId, run.SourcePath, run.Definition.Id);
        if (File.Exists(RecordPath(run.RunId)))
        {
            if (Read(run.RunId) != record)
                throw new MerstrideException("DISCARD_REFUSED", "Conflicting discard ownership records; preserve both for investigation.", 6);
        }
        else RunStore.AtomicWrite(RecordPath(run.RunId), stream => JsonSerializer.Serialize(stream, record, JsonContext.Default.DiscardRecord));
        return record;
    }
}
