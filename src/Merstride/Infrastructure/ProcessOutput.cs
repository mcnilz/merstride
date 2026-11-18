using System.Text;

namespace Merstride.Infrastructure;

public sealed record ProcessOutput(string ActivationId, string NodeId, ProcessStreamOutput Stdout, ProcessStreamOutput Stderr);
public sealed record ProcessStreamOutput(string Text, long ObservedBytes, bool Truncated);

internal sealed class ProcessConsoleStream(Stream stream)
{
    internal bool Unavailable { get; private set; }
    private bool needsLineBreak;

    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellation)
    {
        if (Unavailable) return;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            // Console handles may implement async writes using blocking OS writes.
            // Stop awaiting a stalled write when the process drain deadline expires.
            await WriteAsync(bytes).WaitAsync(cancellation);
            needsLineBreak = bytes.Span[^1] != (byte)'\n';
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Unavailable = true;
            throw;
        }
    }

    private async Task WriteAsync(ReadOnlyMemory<byte> bytes)
    {
        try { await stream.WriteAsync(bytes); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { Unavailable = true; }
    }

    internal void FinishLine(TextWriter output)
    {
        if (Unavailable || !needsLineBreak) return;
        output.WriteLine();
        needsLineBreak = false;
    }
}

internal sealed class ProcessOutputSink : IDisposable
{
    private const int PreviewBytes = 4096;
    private readonly FileStream? file;
    private readonly MemoryStream? preview;
    private readonly ProcessConsoleStream? console;
    private long observed;
    private long retained;

    internal ProcessOutputSink(string runDirectory, string path, ProcessConsoleStream? console, bool capture)
    {
        if (path.Length == 0) this.console = console;
        else
        {
            var target = Path.Combine(runDirectory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }
        if (capture) preview = new MemoryStream(PreviewBytes);
    }

    internal long Observed => observed;
    internal bool Truncated => observed > retained;

    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, int count, CancellationToken cancellation)
    {
        observed += count;
        if (bytes.IsEmpty) return;
        if (preview is not null)
        {
            var length = Math.Min(bytes.Length, PreviewBytes - (int)preview.Length);
            preview.Write(bytes.Span[..length]);
        }
        if (file is not null) await file.WriteAsync(bytes, cancellation);
        else if (console is not null) await console.WriteAsync(bytes, cancellation);
        retained += bytes.Length;
    }

    internal void Flush() => file?.Flush(true);

    internal ProcessStreamOutput Snapshot() => new(
        Encoding.UTF8.GetString(preview!.GetBuffer().AsSpan(0, (int)preview.Length)), observed, observed > preview.Length);

    public void Dispose()
    {
        file?.Dispose();
        preview?.Dispose();
    }
}
