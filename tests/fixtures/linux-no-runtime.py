"""Minimal chroot smoke, not a substitute for the full clean-target release suite.

Run as root in a task-owned Linux checkout. Only native candidates and required
OS shared libraries enter the root; no SDK, CoreCLR, shell or package manager.
"""
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import tempfile

repo = pathlib.Path(__file__).resolve().parents[2]
root = pathlib.Path(tempfile.mkdtemp(prefix="merstride-no-runtime-"))


def copy_file(source):
    target = root / str(source).lstrip("/")
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target, follow_symlinks=True)


def call(*args):
    # No PATH manipulation: chroot changes the actual visible filesystem.
    command = ["chroot", "--userspec=1002:1002", str(root), "/app/merstride", *args,
               "--state-dir", "/workspace/state", "--json"]
    result = subprocess.run(command, capture_output=True, text=True, timeout=15)
    if result.returncode:
        raise RuntimeError((result.returncode, result.stdout, result.stderr))
    return json.loads(result.stdout)


try:
    root.chmod(0o755)
    (root / "app").mkdir()
    (root / "workspace").mkdir()
    os.chown(root / "workspace", 1002, 1002)
    for name in ("native/merstride", "fixture/test-process"):
        source = repo / "artifacts" / name
        shutil.copy2(source, root / "app" / source.name)
        dependencies = subprocess.check_output(["ldd", str(source)], text=True)
        for path in re.findall(r"(/[^\s()]+)", dependencies):
            copy_file(pathlib.Path(path))
    # ICU is loaded dynamically by the runtime, not listed by ldd.
    dynamic = list(pathlib.Path("/usr/lib/x86_64-linux-gnu").glob("libicu*.so.*"))
    dynamic += [pathlib.Path("/usr/lib/x86_64-linux-gnu/libssl.so.3"), pathlib.Path("/usr/lib/x86_64-linux-gnu/libcrypto.so.3")]
    for path in dynamic:
        copy_file(path)
        dependencies = subprocess.check_output(["ldd", str(path)], text=True)
        for dependency in re.findall(r"(/[^\s()]+)", dependencies):
            copy_file(pathlib.Path(dependency))
    workflow = '''```yaml merstride
schemaVersion: 1
id: clean
entry: work
nodes:
  work:
    kind: process
    command: /app/test-process
    args: [effect, native]
    timeoutSeconds: 5
  done:
    kind: end
    result: success
```
```mermaid
flowchart TD
%% merstride: clean
work["Native"]
done["Done"]
work -->|exit_0| done
```
'''
    (root / "workspace/workflow.md").write_text(workflow)
    guide = call("guide")
    version = call("version")
    run = call("start", "/workspace/workflow.md", "--workspace", "/workspace")
    status = call("status", run["runId"])
    advanced = call("advance", run["runId"])
    assert status["status"] == "ready" and advanced["status"] == "completed"
    assert (root / "workspace/effects.txt").read_text() == "native\n"
    files = sorted(str(p.relative_to(root)) for p in root.rglob("*") if p.is_file())
    assert not any("dotnet" in p.lower() or "coreclr" in p.lower() for p in files)
    report = dict(schemaVersion=1, result="passed", scope="isolated chroot native smoke only; full inventoried clean-target runtime qualification unavailable",
                  installedDotnet=False, productVersion=version, commands=["guide", "version", "start", "status", "advance"],
                  binarySha256=hashlib.sha256((root / "app/merstride").read_bytes()).hexdigest(), files=files,
                  environment=subprocess.check_output(["uname", "-a"], text=True).strip())
    (repo / "artifacts/linux-no-runtime-smoke.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report))
finally:
    assert root.parent == pathlib.Path(tempfile.gettempdir()) and root.name.startswith("merstride-no-runtime-")
    shutil.rmtree(root)
