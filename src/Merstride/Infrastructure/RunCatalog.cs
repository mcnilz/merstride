using System.Text.Json;
using Merstride.Domain;

namespace Merstride.Infrastructure;

public sealed record WorkflowSelection(string SourcePath, string WorkflowId, string RunId);
public sealed record SelectionContext(string? CurrentRun, WorkflowSelection[] Workflows);
public sealed record RunSummary(string RunId, string Status, string? SourcePath, string? WorkflowId, bool Current, bool Discarding = false, Problem? Error = null);

public sealed class RunCatalog(RunStore store)
{
    private string ContextPath => Path.Combine(store.Root, "context.json");
    public static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public FileStream Lock()
    {
        Directory.CreateDirectory(store.Root);
        return RunStore.Exclusive(Path.Combine(store.Root, "context.lock"), "CONTEXT_LOCKED", "Another caller is changing run selection. Retry explicitly, or use an explicit run ID.");
    }

    public SelectionContext? Read()
    {
        try
        {
            using var file = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var context = JsonSerializer.Deserialize(file, JsonContext.Default.SelectionContext);
            if (context is null || context.Workflows is null ||
                context.CurrentRun is { } current && !ResourceIds.Valid(current, 'r') ||
                context.Workflows.Any(w => w is null || !ResourceIds.Valid(w.RunId, 'r') ||
                    string.IsNullOrEmpty(w.WorkflowId) || string.IsNullOrEmpty(w.SourcePath) || !Path.IsPathFullyQualified(w.SourcePath)) ||
                context.Workflows.GroupBy(w => w.SourcePath, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                    .Any(g => g.GroupBy(w => w.WorkflowId, StringComparer.Ordinal).Any(workflows => workflows.Count() > 1)))
                throw new JsonException();
            return context;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (JsonException) { throw new MerstrideException("CORRUPT_CONTEXT", "Run selection context is corrupt. Use runs and an explicit run ID; no other run was selected.", 6); }
    }

    // Caller holds the root context lock. Explicit use can repair corrupt context.
    public void Select(RunState run, bool repair = false)
    {
        SelectionContext context;
        try { context = Read() ?? new(null, []); }
        catch (MerstrideException ex) when (repair && ex.Problem.Code == "CORRUPT_CONTEXT") { context = new(null, []); }
        var workflows = context.Workflows.Where(w => run.SourcePath is null ||
            !w.SourcePath.Equals(run.SourcePath, PathComparison) || w.WorkflowId != run.Definition.Id).ToArray();
        Save(new(run.RunId, run.SourcePath is null ? workflows : [.. workflows, new(run.SourcePath, run.Definition.Id, run.RunId)]));
    }

    public string? Current(string? sourcePath, string? workflowId)
    {
        var context = Read();
        if (sourcePath is null)
        {
            if (context is null) return null;
            return context.CurrentRun ?? throw Missing("No current run is selected.");
        }
        var matches = (context?.Workflows ?? []).Where(w => w.SourcePath.Equals(sourcePath, PathComparison) &&
            (workflowId is null || w.WorkflowId == workflowId)).ToArray();
        if (matches.Length == 0) throw Missing("No run is selected for this workflow file.");
        if (matches.Length > 1) throw new MerstrideException("AMBIGUOUS_WORKFLOW", "This file has runs for multiple workflows. Add --workflow <id>, or use an explicit run ID from runs.", 4);
        var selected = matches[0];
        var run = ReadSelected(selected.RunId);
        if (run.SourcePath is null || !run.SourcePath.Equals(sourcePath, PathComparison) || run.Definition.Id != selected.WorkflowId)
            throw Missing("The workflow selection no longer matches its saved run.");
        return run.RunId;
    }

    public RunState ReadSelected(string id)
    {
        try { return store.Read(id); }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            if (Directory.Exists(store.RunDirectory(id))) throw;
            throw Missing("The selected run is missing.");
        }
    }

    public string[] RunIds()
    {
        if (!Directory.Exists(store.Root))
        {
            if (File.Exists(store.Root)) throw new IOException("State root is a file.");
            return [];
        }
        return Directory.EnumerateDirectories(store.Root).Select(Path.GetFileName)
            .Where(id => ResourceIds.Valid(id, 'r')).Cast<string>().Order(StringComparer.Ordinal).ToArray();
    }

    public RunSummary[] List(string? sourcePath, string? workflowId, out Diagnostic[] diagnostics)
    {
        string? current = null;
        var warnings = new List<Diagnostic>();
        try { current = Read()?.CurrentRun; }
        catch (MerstrideException ex) when (ex.Problem.Code == "CORRUPT_CONTEXT")
        { warnings.Add(new("warning", ex.Problem.Code, ex.Problem.Message, null)); }
        var trash = new DiscardJournal(store);
        string[] stagedIds = [];
        try
        {
            stagedIds = trash.Ids();
            if (File.Exists(trash.DirectoryPath))
                warnings.Add(new("warning", "DISCARD_STORAGE_CONFLICT", "Discard staging path is a file. Preserve its contents and resolve this storage conflict before retrying pending discards.", null));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { warnings.Add(new("warning", "DISCARD_STORAGE_CONFLICT", "Cannot inventory staged discards. Retained runs are listed; resolve staging storage permissions before retrying.", null)); }
        diagnostics = warnings.ToArray();
        return RunIds().Select(id => (Id: id, Store: store, Discarding: false))
            .Concat(stagedIds.Select(id => (Id: id, Store: new RunStore(trash.DirectoryPath), Discarding: true))).Select(item =>
        {
            var id = item.Id;
            try
            {
                if (item.Discarding && !File.Exists(Path.Combine(item.Store.RunDirectory(id), "state.json")))
                {
                    var discarded = trash.Read(id);
                    return new RunSummary(id, "discarding", discarded.SourcePath, discarded.WorkflowId, id == current, true);
                }
                var discarding = item.Discarding || File.Exists(Path.Combine(item.Store.RunDirectory(id), "discard.pending"));
                var run = discarding ? item.Store.Read(id) : item.Store.Status(id);
                return new RunSummary(id, discarding ? "discarding" : run.Status, run.SourcePath, run.Definition.Id, id == current, discarding);
            }
            catch (MerstrideException ex) { return new RunSummary(id, "unreadable", null, null, id == current, item.Discarding, Error: ex.Problem); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return new RunSummary(id, "unreadable", null, null, id == current, item.Discarding, Error: new("IO_ERROR", "Cannot read this run's state.")); }
        }).Where(r => (sourcePath is null || r.SourcePath?.Equals(sourcePath, PathComparison) == true) &&
            (workflowId is null || r.WorkflowId == workflowId)).ToArray();
    }

    public void Forget(string id)
    {
        var context = Read();
        if (context is null) return;
        Save(new(context.CurrentRun == id ? null : context.CurrentRun, context.Workflows.Where(w => w.RunId != id).ToArray()));
    }

    private void Save(SelectionContext context) => RunStore.AtomicWrite(ContextPath, stream =>
        JsonSerializer.Serialize(stream, context, JsonContext.Default.SelectionContext));

    private static MerstrideException Missing(string message) => new("STALE_CONTEXT", message + " Run merstride runs, then merstride use <run-id>, or start a new workflow. No fallback was selected.", 4);
}
