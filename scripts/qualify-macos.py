"""Qualify a same-run macOS archive using native executables and no dotnet host.

Removal is confined to an explicitly opted-in disposable GitHub-hosted macOS VM.
PowerShell's private, self-contained implementation is a declared test prerequisite;
it is not a system .NET installation or the acceptance-suite host.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import sys
import tarfile

parser = argparse.ArgumentParser()
parser.add_argument("--archive", required=True, type=Path)
parser.add_argument("--fixture", required=True, type=Path)
parser.add_argument("--driver", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
parser.add_argument("--remove-hosted-dotnet", action="store_true")
args = parser.parse_args()
if sys.version_info < (3, 12):
    raise SystemExit("Qualification requires Python 3.12+.")
if sys.platform != "darwin" or platform.machine() != "arm64" or int(platform.mac_ver()[0].split('.')[0]) < 15:
    raise SystemExit("Qualification requires actual macOS 15+ Apple Silicon.")
if os.getuid() == 0:
    raise SystemExit("Run scenarios as the unprivileged runner user, never root.")
root = Path(__file__).resolve().parent.parent
args.output = args.output.resolve()
args.output.parent.mkdir(parents=True, exist_ok=True)
for old in (args.output, Path(str(args.output) + ".scenarios.json")):
    if old.exists():
        old.unlink()  # A failed attempt must not leave reusable success evidence.

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def require_arm64(path):
    with Path(path).open("rb") as file:
        header = file.read(8)
    if len(header) != 8 or struct.unpack("<II", header) != (0xFEEDFACF, 0x0100000C):
        raise RuntimeError("Expected a native arm64 Mach-O executable: " + str(path))

scan_roots = ["/Users", "/Library", "/Applications", "/usr", "/opt", "/private/var", "/private/tmp"]

def installations():
    # Use root for a complete scan, rather than accepting permission-denied gaps.
    result = subprocess.run(["sudo", "-n", "/usr/bin/find", *scan_roots,
        "(", "-name", "dotnet", "-o", "-name", "Microsoft.NETCore.App", "-o", "-name", "Microsoft.AspNetCore.App", ")"],
        check=True, text=True, capture_output=True)
    if result.stderr.strip():
        raise RuntimeError("Incomplete runtime-absence inventory: " + result.stderr)
    return sorted(set(result.stdout.splitlines()))

before = installations()
if args.remove_hosted_dotnet:
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted" or os.environ.get("RUNNER_OS") != "macOS":
        raise RuntimeError("Runtime removal is restricted to a disposable GitHub-hosted macOS job.")
    toolcache = Path(os.environ["RUNNER_TOOL_CACHE"]).resolve()
    if not str(toolcache).startswith("/Users/runner/"):
        raise RuntimeError("Unexpected hosted tool-cache location; do not remove it.")
    approved = [Path("/usr/local/share/dotnet"), Path("/usr/local/lib/dotnet"),
        Path("/opt/homebrew/share/dotnet"), Path.home() / ".dotnet", toolcache / "dotnet"]
    removed = []
    for location in approved:
        if not location.exists() and not location.is_symlink():
            continue
        resolved = location.resolve()
        if resolved not in approved and not resolved.is_relative_to(toolcache / "dotnet"):
            raise RuntimeError("Runtime location resolves outside approved installation roots: " + str(location))
        subprocess.run(["sudo", "-n", "/bin/rm", "-rf", "--", str(location)], check=True)
        removed.append(str(location))
    for link in (Path("/usr/local/bin/dotnet"), Path("/opt/homebrew/bin/dotnet")):
        if link.is_symlink() and any(link.resolve().is_relative_to(Path(path)) for path in removed):
            subprocess.run(["sudo", "-n", "/bin/rm", "--", str(link)], check=True)
after = installations()
absence = dict(schemaVersion=1, before=before, after=after, scanRoots=scan_roots,
    pathHost=shutil.which("dotnet"), image=os.environ.get("ImageOS"), imageVersion=os.environ.get("ImageVersion"))
Path(str(args.output) + ".runtime-inventory.json").write_text(json.dumps(absence, indent=2))
if after or shutil.which("dotnet"):
    raise RuntimeError("Standalone .NET installation remains; PATH hiding is insufficient.")

expected = {"merstride", "platform.json", "README.txt", "LICENSE", "dotnet-LICENSE.txt", "dotnet-THIRD-PARTY-NOTICES.txt",
    "YamlDotNet-LICENSE.txt", "System.CommandLine-LICENSE.txt", "shipped-dependencies.json"}
stage = args.output.parent / "extracted-macos"
stage.mkdir(exist_ok=False)
with tarfile.open(args.archive, "r:gz") as archive:
    members = archive.getmembers()
    if len(members) != len(expected) or {member.name for member in members} != expected or any(not member.isfile() for member in members):
        raise RuntimeError("Unexpected macOS archive contents.")
    binary_entry = next(member for member in members if member.name == "merstride")
    if binary_entry.mode & 0o111 != 0o111:
        raise RuntimeError("macOS archive lost executable permissions.")
    archive.extractall(stage, filter="data")
binary = stage / "merstride"
gate = json.loads((stage / "platform.json").read_text())
commit = os.environ["GITHUB_SHA"]
run_id = os.environ["GITHUB_RUN_ID"]
if (gate.get("result"), gate.get("commit"), gate.get("runId"), gate.get("rid"), gate.get("dirty")) != ("passed", commit, run_id, "osx-arm64", False):
    raise RuntimeError("macOS archive belongs to a different source/run or failed gate.")
for path, field in ((binary, "binarySha256"), (args.fixture, "fixtureSha256"), (args.driver, "nativeSuiteSha256")):
    require_arm64(path)
    if digest(path) != gate.get(field):
        raise RuntimeError("Native input differs from the same-run tested artifact: " + field)
    path.chmod(path.stat().st_mode | 0o111)
version = json.loads(subprocess.run([str(binary), "version", "--json"], check=True, text=True, capture_output=True).stdout)
if version.get("version") != gate["version"] or version.get("protocolVersion") != 1:
    raise RuntimeError("Extracted native version differs from the release candidate.")
scenario_path = Path(str(args.output) + ".scenarios.json")
subprocess.run([sys.executable, str(root / "scripts/observe-macos.py"), str(args.output.parent / "clean-process.trace"), "--",
    str(args.driver.resolve()), "--binary", str(binary), "--fixture", str(args.fixture.resolve()),
    "--exclude-release-environment-orchestrator", "--results", str(scenario_path)], check=True, cwd=root)
results = json.loads(scenario_path.read_text())
inventory = json.loads((root / "docs/release-scenario-inventory.json").read_text())
identities = [f'{row["feature"]}:{row["line"]} {row["scenario"]}' for row in results]
if sorted(identities) != sorted(inventory["cleanScenarios"]) or any(row["result"]["status"] != "passed" for row in results):
    raise RuntimeError("All inventoried runtime scenarios must pass on the actual clean target.")
hashes = {f"features/{path.name}": digest(path) for path in (root / "features").glob("*.feature")}
if hashes != inventory["featureHashes"]:
    raise RuntimeError("Feature source hashes differ from the release inventory.")
report = dict(schemaVersion=1, rid="osx-arm64", commit=commit, runId=run_id, version=gate["version"],
    binarySha256=digest(binary), fixtureSha256=digest(args.fixture), driverSha256=digest(args.driver),
    cleanNoRuntime=True, allApplicableScenarios=True, processTree=True, atomicSnapshots=True, referenceBudgets=False,
    environment=f"macOS {platform.mac_ver()[0]} arm64; GitHub {absence['image']} {absence['imageVersion']}",
    evidence="Same-run extracted archive; native suite; kernel process events; full standalone-runtime inventory retained in job artifacts.",
    runtimeInventorySha256=digest(str(args.output) + ".runtime-inventory.json"), featureHashes=hashes,
    scenariosSha256=digest(scenario_path), scenarios=results,
    limitation="Hosted timing is not reference-budget qualification. Separate exact-binary reference-budget evidence remains mandatory.")
args.output.write_text(json.dumps(report, indent=2) + "\n")
