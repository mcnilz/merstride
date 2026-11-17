"""Run an unprivileged suite under external kernel process-event observation."""
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time

if sys.argv[1:2] == ["--child"]:
    os.kill(os.getpid(), signal.SIGSTOP)
    os.execvp(sys.argv[2], sys.argv[2:])

if sys.platform != "darwin" or len(sys.argv) < 4 or sys.argv[2] != "--":
    raise SystemExit("Usage: observe-macos.py TRACE -- EXECUTABLE [ARG ...] (macOS only)")
trace = Path(sys.argv[1]).resolve()
trace.parent.mkdir(parents=True, exist_ok=True)
trace.write_text("")
errors = Path(str(trace) + ".stderr")
os.environ["MERSTRIDE_PROCESS_TRACE"] = str(trace)
# Use a physical workspace root so .NET and PowerShell agree across /var's
# /private/var symlink. Product working-directory behavior stays unchanged.
os.environ["TMPDIR"] = os.path.realpath(tempfile.gettempdir()) + os.sep
# Framework Python's bin launcher execs Python.app. Test the installed stdin
# interpreter directly, preserving the exact one-interpreter launch assertion.
framework_python = Path(sys.base_prefix) / "Resources/Python.app/Contents/MacOS/Python"
os.environ.setdefault("MERSTRIDE_TEST_PYTHON", str(framework_python) if framework_python.is_file() else sys.executable)
child = subprocess.Popen([sys.executable, __file__, "--child", *sys.argv[3:]])
observer = None
try:
    _, status = os.waitpid(child.pid, os.WUNTRACED)
    if not os.WIFSTOPPED(status):
        raise RuntimeError("Suite launch barrier failed.")
    with errors.open("w") as stderr:
        observer = subprocess.Popen([
            "sudo", "-n", "/usr/sbin/dtrace", "-q", "-s",
            str(Path(__file__).with_name("macos-processes.d")), "-o", str(trace), str(child.pid)
        ], stderr=stderr)
        deadline = time.monotonic() + 15
        while "READY\n" not in trace.read_text():
            if observer.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError("macOS kernel observation unavailable: " + errors.read_text())
            time.sleep(0.02)
        os.kill(child.pid, signal.SIGCONT)
        watchdog = os.environ.get("MERSTRIDE_PROCESS_WATCHDOG_SECONDS")
        result = child.wait(timeout=int(watchdog) if watchdog else None)
        observer.wait(timeout=15)
    diagnostics = errors.read_text()
    if observer.returncode != 0 or "drop" in diagnostics.lower() or "ERROR" in trace.read_text():
        raise RuntimeError("macOS process observation failed or lost events: " + diagnostics)
    raise SystemExit(result)
finally:
    if child.poll() is None:
        child.kill()
        child.wait()
    if observer is not None and observer.poll() is None:
        subprocess.run(["sudo", "-n", "kill", "-TERM", str(observer.pid)], check=True)
        observer.wait(timeout=15)
