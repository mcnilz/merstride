using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Merstride.Acceptance;

// The host's job inherits every CLI descendant, including processes too short-lived to poll.
// No kill limits are set; this is observation through the OS, not an application log.
internal static class ProcessObserver
{
    private static readonly SafeFileHandle? Job = CreateObserver();

    internal static uint? TotalProcesses()
    {
        if (Job is null) return null;
        if (!QueryInformationJobObject(Job, 1, out var info, Marshal.SizeOf<Accounting>(), IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return info.TotalProcesses;
    }

    internal static int[] ActiveProcessIds()
    {
        if (Job is null) return [];
        const int size = 65536;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!QueryProcessIds(Job, 3, buffer, size, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var count = Marshal.ReadInt32(buffer, 4);
            return Enumerable.Range(0, count).Select(i => checked((int)Marshal.ReadIntPtr(buffer, 8 + i * IntPtr.Size))).ToArray();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static SafeFileHandle? CreateObserver()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job.IsInvalid || !AssignProcessToJobObject(job, GetCurrentProcess()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return job;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Accounting
    {
        public long TotalUserTime, TotalKernelTime, ThisPeriodTotalUserTime, ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount, TotalProcesses, ActiveProcesses, TotalTerminatedProcesses;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass, out Accounting information, int size, IntPtr returnLength);
    [DllImport("kernel32.dll", EntryPoint = "QueryInformationJobObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryProcessIds(SafeFileHandle job, int informationClass, IntPtr information, int size, IntPtr returnLength);
}
