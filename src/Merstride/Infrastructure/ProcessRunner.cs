using System.ComponentModel;
using System.Diagnostics;
using Merstride.Application;
using Merstride.Domain;

namespace Merstride.Infrastructure;

public sealed class ProcessRunner(string runDirectory, CancellationToken interruption = default) : IProcessRunner
{
    public Activation Execute(Node node, string workspace, Activation activation) =>
        ExecuteAsync(node, workspace, activation).GetAwaiter().GetResult();

    private async Task<Activation> ExecuteAsync(Node node, string workspace, Activation activation)
    {
        Directory.CreateDirectory(Path.Combine(runDirectory, "logs"));
        using var stdout = File.Create(Path.Combine(runDirectory, activation.StdoutPath));
        using var stderr = File.Create(Path.Combine(runDirectory, activation.StderrPath));
        var command = node.Command!;
        if (!Path.IsPathFullyQualified(command) && (command.Contains('/') || command.Contains('\\')))
            command = Path.GetFullPath(command, workspace);
        var start = new ProcessStartInfo(command)
        {
            UseShellExecute = false, WorkingDirectory = workspace,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in node.Args) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (interruption.IsCancellationRequested)
            return activation with { FinishedAt = DateTimeOffset.UtcNow, Outcome = "interrupted" };
        try { process.Start(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { return activation with { FinishedAt = DateTimeOffset.UtcNow, Outcome = "spawn_error" }; }
        process.StandardInput.Close();
        var cause = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(node.TimeoutSeconds));
        using var registration = timeout.Token.Register(() => cause.TrySetResult("timeout"));
        using var interruptRegistration = interruption.Register(() => cause.TrySetResult("interrupted"));
        using var drain = new CancellationTokenSource();
        var budget = new OutputBudget(cause);
        var output = CopyAsync(process.StandardOutput.BaseStream, stdout, budget, drain.Token);
        var error = CopyAsync(process.StandardError.BaseStream, stderr, budget, drain.Token);
        var finished = Task.WhenAll(process.WaitForExitAsync(), output, error);
        await Task.WhenAny(finished, cause.Task);
        if (cause.Task.IsCompleted)
        {
            drain.CancelAfter(TimeSpan.FromSeconds(2));
            try { process.Kill(true); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
            // Detached descendants may retain pipes; never wait indefinitely for those handles.
            process.WaitForExit(2000);
        }
        await Task.WhenAll(output, error);
        timeout.CancelAfter(Timeout.Infinite);
        stdout.Flush(true); stderr.Flush(true);
        var limited = cause.Task.IsCompleted;
        var code = limited ? (int?)null : process.ExitCode;
        return activation with {
            FinishedAt = DateTimeOffset.UtcNow,
            Outcome = limited ? await cause.Task : code < 0 ? "signal" : $"exit_{code}", ExitCode = code,
            StdoutBytes = output.Result, StderrBytes = error.Result,
            StdoutTruncated = output.Result > stdout.Length, StderrTruncated = error.Result > stderr.Length
        };
    }

    private static async Task<long> CopyAsync(Stream source, Stream target, OutputBudget budget, CancellationToken cancellation)
    {
        var bytes = new byte[16384];
        long observed = 0;
        try
        {
            int count;
            while ((count = await source.ReadAsync(bytes, cancellation)) != 0)
            {
                observed += count;
                var retain = budget.Reserve(count);
                if (retain > 0) await target.WriteAsync(bytes.AsMemory(0, retain), cancellation);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        return observed;
    }

    private sealed class OutputBudget(TaskCompletionSource<string> cause)
    {
        private long observed;
        public int Reserve(int count)
        {
            var total = Interlocked.Add(ref observed, count);
            if (total > 10 * 1024 * 1024) cause.TrySetResult("output_limit");
            return (int)Math.Clamp(10 * 1024 * 1024 - (total - count), 0, count);
        }
    }
}
