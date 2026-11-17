using System.Diagnostics;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class ProcessObservationWorld : IDisposable
{
    private uint baseline;
    internal void Execute(string step)
    {
        switch (step)
        {
            case "the published native process fixture and external platform observer":
                Assert.That(File.Exists(TestPaths.Helper), Is.True);
                baseline = ProcessObserver.TotalProcesses() ?? throw new InvalidOperationException("Required observer unavailable.");
                break;
            case "the fixture launches 32 short-lived native descendants":
                using (var process = Process.Start(new ProcessStartInfo(TestPaths.Helper) { ArgumentList = { "observer_burst", "32" }, UseShellExecute = false })!)
                {
                    Assert.That(process.WaitForExit(15000), Is.True, "Native observer probe watchdog.");
                    Assert.That(process.ExitCode, Is.Zero);
                }
                break;
            case "all 33 native launches are observed after they have exited":
                Assert.That(ProcessObserver.TotalProcesses() - baseline, Is.EqualTo(33)); break;
            default: throw new AssertionException("Undefined required P15 step: " + step);
        }
    }
    public void Dispose() { }
}
