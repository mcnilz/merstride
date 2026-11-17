using System.Globalization;
using System.Diagnostics;

namespace Merstride.Acceptance;

internal static class MacProcessObserver
{
    // A fresh kernel heartbeat drains event buffers before a snapshot. No process
    // polling is used to count launches: short-lived exec-success events are retained.
    private static string[] Snapshot()
    {
        var path = Environment.GetEnvironmentVariable("MERSTRIDE_PROCESS_TRACE")
            ?? throw new InvalidOperationException("macOS acceptance requires scripts/observe-macos.py kernel observation.");
        var watch = Stopwatch.StartNew();
        var initialHeartbeats = -1;
        var observedHeartbeats = 0;
        string[] lines = [];
        if (!SpinWait.SpinUntil(() =>
        {
            var text = File.ReadAllText(path);
            var end = text.LastIndexOf('\n');
            lines = end < 0 ? [] : text[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Contains("ERROR") || File.ReadAllText(path + ".stderr").Contains("drop", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("macOS process observation failed or lost events.");
            observedHeartbeats = lines.Count(line => line.EndsWith(" SYNC", StringComparison.Ordinal) &&
                long.TryParse(line.AsSpan(0, line.IndexOf(' ')), NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
            if (initialHeartbeats < 0) initialHeartbeats = observedHeartbeats;
            // Kernel and managed wall clocks need not share an epoch or adjustment.
            // Require new kernel records and a local monotonic buffer-drain interval.
            return observedHeartbeats > initialHeartbeats && watch.Elapsed >= TimeSpan.FromMilliseconds(100);
        }, 5000)) throw new InvalidOperationException($"macOS process observer stopped producing kernel heartbeats (initial={initialHeartbeats}, observed={observedHeartbeats}, records={lines.Length}, ready={lines.Contains("READY")}).");
        return lines;
    }

    internal static uint TotalProcesses() => checked((uint)Snapshot().Count(line => line.Contains(" EXEC ", StringComparison.Ordinal)));

    internal static int[] ActiveProcessIds()
    {
        var parents = new Dictionary<int, int>();
        var events = Snapshot().Select(line => line.Split(' '))
            .Where(parts => parts.Length >= 3 && long.TryParse(parts[0], out _))
            .OrderBy(parts => long.Parse(parts[0], CultureInfo.InvariantCulture));
        foreach (var parts in events)
        {
            var pid = int.Parse(parts[2], CultureInfo.InvariantCulture);
            if (parts[1] == "EXIT") parents.Remove(pid);
            else if (parts[1] is "FORK" or "EXEC") parents[pid] = int.Parse(parts[3], CultureInfo.InvariantCulture);
        }
        var ids = new HashSet<int> { Environment.ProcessId };
        while (true)
        {
            var next = parents.Where(pair => ids.Contains(pair.Value) && !ids.Contains(pair.Key)).Select(pair => pair.Key).ToArray();
            if (next.Length == 0) return ids.ToArray();
            ids.UnionWith(next);
        }
    }
}
