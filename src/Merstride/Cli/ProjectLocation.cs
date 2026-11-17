using System.Text;
using Merstride.Domain;

namespace Merstride.Cli;

internal sealed record ProjectLocation(string Workspace, string StateRoot, bool DefaultState)
{
    internal static ProjectLocation Discover(string? explicitRoot)
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        var project = current;
        for (var directory = current; directory is not null; directory = directory.Parent)
        {
            if (Exists(Path.Combine(directory.FullName, ".merstride")) || Exists(Path.Combine(directory.FullName, ".git")))
            { project = directory; break; }
        }
        var result = new ProjectLocation(project.FullName, explicitRoot is null ? Path.Combine(project.FullName, ".merstride", "runs") : Absolute(explicitRoot), explicitRoot is null);
        if (result.DefaultState && Exists(Path.Combine(project.FullName, ".merstride")) &&
            (File.GetAttributes(Path.Combine(project.FullName, ".merstride")) & FileAttributes.Directory) == 0)
            throw new MerstrideException("IO_ERROR", $"Project state marker '{Path.Combine(project.FullName, ".merstride")}' must be a directory. Preserve its content and choose an explicit --state-dir.", 6);
        return result;
    }

    internal void Prepare()
    {
        if (!DefaultState) return;
        var directory = Path.GetDirectoryName(StateRoot)!;
        Directory.CreateDirectory(directory);
        // Append a narrow ignore rule; retain every byte of existing project content.
        var ignore = Path.Combine(directory, ".gitignore");
        using var file = new FileStream(ignore, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var reader = new StreamReader(file, Encoding.UTF8, true, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() == "/runs/") return;
        file.Seek(0, SeekOrigin.End);
        var bytes = reader.CurrentEncoding.GetBytes((text.Length > 0 && !text.EndsWith('\n') ? "\n" : "") + "/runs/\n");
        file.Write(bytes);
        file.Flush(true);
    }

    private static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    internal static string Absolute(string path) => Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) :
        throw new MerstrideException("USAGE", "Path must be absolute. Run merstride --help for usage.", 2);
}
