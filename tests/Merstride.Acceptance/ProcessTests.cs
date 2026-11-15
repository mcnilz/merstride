using NUnit.Framework;

namespace Merstride.Acceptance;

public class ProcessTests
{
    [Test]
    public void UnlabeledProcessEdgeWarnsThatFailuresAreIgnored()
    {
        using var world = new ProcessWorld();
        world.UnlabeledEdgeWarnsAndAcceptsFailure();
    }

    [Test]
    public void RawChildBytesArePreservedInSeparateLogs()
    {
        using var world = new ProcessWorld();
        world.RawStreamsStayUnchanged();
    }

    [Test]
    public void ChildStdinIsClosed()
    {
        using var world = new ProcessWorld();
        world.StdinIsClosed();
    }

    [Test]
    public void FrozenChainAndTerminalAdvanceCannotReplayEffects()
    {
        using var world = new ProcessWorld();
        world.TerminalAdvanceDoesNotReplay();
    }

    [Test]
    public void RelativeNativeExecutableReceivesDifficultLiteralArguments()
    {
        using var world = new ProcessWorld();
        world.RelativeExecutableAndDifficultArguments();
    }

    [Test]
    public void ProcessOutputCannotFillStorageWithoutBound()
    {
        using var world = new ProcessWorld();
        world.OutputIsBounded();
    }

    [Test]
    public void ProcessTimeoutCannotHangAdvance()
    {
        using var world = new ProcessWorld();
        world.TimeoutIsBounded();
    }
}
