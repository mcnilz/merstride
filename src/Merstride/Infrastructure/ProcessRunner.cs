using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Merstride.Application;
using Merstride.Domain;

namespace Merstride.Infrastructure;

internal sealed class ProcessRunner(string runDirectory, ProcessConsoleStream? consoleOut, ProcessConsoleStream? consoleError,
    Action<ProcessOutput>? report, CancellationToken interruption = default) : IProcessRunner
{
    public Activation Execute(Node node, string workspace, Activation activation)
    {
        using var stdout = new ProcessOutputSink(runDirectory, activation.StdoutPath, consoleOut, report is not null);
        using var stderr = new ProcessOutputSink(runDirectory, activation.StderrPath, consoleError, report is not null);
        try { return ExecuteAsync(node, workspace, activation, stdout, stderr).GetAwaiter().GetResult(); }
        finally
        {
            report?.Invoke(new(activation.Id, node.Id, stdout.Snapshot(), stderr.Snapshot()));
        }
    }

    private async Task<Activation> ExecuteAsync(Node node, string workspace, Activation activation,
        ProcessOutputSink stdout, ProcessOutputSink stderr)
    {
        var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "unsupported";
        var selected = node.Variants?.SingleOrDefault(v => v.Platforms.Contains(platform, StringComparer.Ordinal));
        if (node.Variants is not null && selected is null)
            return await LaunchFailure(activation, stderr, "NO_PLATFORM_VARIANT: No execution variant applies to this platform.");
        var script = selected?.Script ?? node.Script;
        var command = selected?.Command ?? node.Command!;
        var args = selected?.Args ?? node.Args;
        if (script is not null && !Path.IsPathFullyQualified(command) && !command.Contains('/') && !command.Contains('\\'))
        {
            var resolved = FindOnPath(command, workspace);
            if (resolved is null)
                return await LaunchFailure(activation, stderr, "INTERPRETER_UNAVAILABLE: Shebang interpreter was not found on PATH.");
            command = resolved;
        }
        if (!Path.IsPathFullyQualified(command) && (command.Contains('/') || command.Contains('\\')))
            command = Path.GetFullPath(command, workspace);
        var rtk = node.UseRtk && script is null ? FindOnPath("rtk", workspace, executableOnly: true) : null;
        var rtkCommand = rtk is null ? null : CommandForRtk(command, workspace);
        var start = new ProcessStartInfo(rtkCommand is null ? command : rtk!)
        {
            UseShellExecute = false, WorkingDirectory = workspace,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (rtkCommand is not null) start.ArgumentList.Add(rtkCommand);
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (interruption.IsCancellationRequested)
            return activation with { FinishedAt = DateTimeOffset.UtcNow, Outcome = "interrupted" };
        try { process.Start(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return script is null && node.Variants is null
                ? activation with { FinishedAt = DateTimeOffset.UtcNow, Outcome = "spawn_error" }
                : await LaunchFailure(activation, stderr, "PROCESS_UNAVAILABLE: Executable could not be launched.");
        }
        var cause = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(node.TimeoutSeconds));
        using var registration = timeout.Token.Register(() => cause.TrySetResult("timeout"));
        using var interruptRegistration = interruption.Register(() => cause.TrySetResult("interrupted"));
        using var drain = new CancellationTokenSource();
        var input = FeedAsync(process, script, drain.Token);
        var budget = new OutputBudget(cause);
        var output = CopyAsync(process.StandardOutput.BaseStream, stdout, budget, drain.Token);
        var error = CopyAsync(process.StandardError.BaseStream, stderr, budget, drain.Token);
        var finished = Task.WhenAll(process.WaitForExitAsync(), input, output, error);
        await Task.WhenAny(finished, cause.Task);
        if (cause.Task.IsCompleted)
        {
            drain.CancelAfter(TimeSpan.FromSeconds(2));
            try { process.Kill(true); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
            // Detached descendants may retain pipes; never wait indefinitely for those handles.
            process.WaitForExit(2000);
        }
        await Task.WhenAll(input, output, error);
        timeout.CancelAfter(Timeout.Infinite);
        stdout.Flush(); stderr.Flush();
        var limited = cause.Task.IsCompleted;
        var code = limited ? (int?)null : process.ExitCode;
        return activation with {
            FinishedAt = DateTimeOffset.UtcNow,
            Outcome = limited ? await cause.Task : code < 0 ? "signal" : $"exit_{code}", ExitCode = code,
            StdoutBytes = stdout.Observed, StderrBytes = stderr.Observed,
            StdoutTruncated = stdout.Truncated, StderrTruncated = stderr.Truncated
        };
    }

    private async Task<Activation> LaunchFailure(Activation activation, ProcessOutputSink stderr, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message + "\n");
        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(interruption);
        delivery.CancelAfter(TimeSpan.FromSeconds(2));
        try { await stderr.WriteAsync(bytes, bytes.Length, delivery.Token); }
        catch (OperationCanceledException) when (delivery.IsCancellationRequested) { }
        stderr.Flush();
        return activation with { FinishedAt = DateTimeOffset.UtcNow, Outcome = "spawn_error", StderrBytes = stderr.Observed, StderrTruncated = stderr.Truncated };
    }

    private static string? FindOnPath(string name, string workspace, bool executableOnly = false)
    {
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var path = entry.Trim('"');
            if (string.IsNullOrWhiteSpace(path)) continue;
            var directory = Path.GetFullPath(path, workspace);
            foreach (var suffix in OperatingSystem.IsWindows() && !Path.HasExtension(name) ? new[] { ".exe", ".com" } : new[] { "" })
            {
                var candidate = Path.Combine(directory, name + suffix);
                if (File.Exists(candidate) && (!executableOnly || IsExecutable(candidate))) return candidate;
            }
        }
        return null;
    }

    private static string? CommandForRtk(string command, string workspace)
    {
        if (Path.IsPathFullyQualified(command)) return command;
        var executable = FindOnPath(command, workspace, executableOnly: true);
        if (executable is null) return null;

        // Bare names select RTK subcommands. Only use that dispatch for filters
        // intended for the executable of the same name. A path ensures passthrough
        // for other executables, including ones named like RTK's own operations.
        return command switch
        {
            "ast-grep" or "bun" or "bunx" or "cargo" or "ctest" or "curl" or "deno" or "docker" or "dotnet" or
            "find" or "gh" or "git" or "glab" or "go" or "grep" or "jest" or "kubectl" or "ls" or "mvn" or
            "npm" or "npx" or "php" or "pip" or "playwright" or "pnpm" or "prettier" or "prisma" or "psql" or
            "pytest" or "rg" or "ruff" or "tsc" or "vitest" or "wc" or "wget" => command,
            _ => executable
        };
    }

    private static bool IsExecutable(string path)
    {
        if (OperatingSystem.IsWindows()) return true;
        try
        {
            const UnixFileMode execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & execute) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static async Task FeedAsync(Process process, string? script, CancellationToken cancellation)
    {
        try
        {
            if (script is not null)
                await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(script), cancellation);
        }
        // An interpreter may exit without consuming all input. Its exit still determines routing.
        catch (IOException) { }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            // Closing can flush pipe buffers after the interpreter has closed its reader.
            try { process.StandardInput.Close(); }
            catch (IOException) { }
        }
    }

    private static async Task CopyAsync(Stream source, ProcessOutputSink target, OutputBudget budget, CancellationToken cancellation)
    {
        var bytes = new byte[16384];
        try
        {
            int count;
            while ((count = await source.ReadAsync(bytes, cancellation)) != 0)
            {
                await target.WriteAsync(bytes.AsMemory(0, budget.Reserve(count)), count, cancellation);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
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
