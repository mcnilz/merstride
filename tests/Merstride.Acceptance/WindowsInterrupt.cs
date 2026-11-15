using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Merstride.Acceptance;

// Give the test-only signal host a separate hidden console. Its descendants inherit it;
// broadcasting CTRL_BREAK cannot reach the NUnit host or any unrelated console.
internal static class WindowsInterrupt
{
    internal static Process Start(string helper, string binary, string runId, string state, string workspace)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("P06 signal qualification requires Windows.");
        var startup = new Startup { Size = Marshal.SizeOf<Startup>(), Flags = 1, ShowWindow = 0 };
        var command = new StringBuilder($"\"{helper}\" signal_host \"{binary}\" {runId} \"{state}\"");
        if (!CreateProcessW(helper, command, IntPtr.Zero, IntPtr.Zero, false, 0x10, IntPtr.Zero, workspace, ref startup, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try { var process = Process.GetProcessById(info.ProcessId); _ = process.Handle; return process; }
        finally { CloseHandle(info.Thread); CloseHandle(info.Process); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Startup
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public short ShowWindow, ReservedSize;
        public IntPtr ReservedData, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public int ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory,
        ref Startup startup, out ProcessInfo info);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
