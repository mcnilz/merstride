# Native verification and release

## Versions and release tags

Release tags use `vMAJOR.MINOR.PATCH`, for example `v1.2.3`, or a SemVer
prerelease such as `v1.3.0-rc.1`. Increment PATCH for compatible fixes, MINOR for
compatible features, and MAJOR for incompatible public behavior. During `0.y.z`
development the API is unstable; document incompatible changes in release notes.
The CLI JSON protocol version is a separate compatibility contract.
Numeric components and numeric prerelease identifiers cannot have leading zeros.
Build metadata (`+build.1`) is deliberately excluded from release tags: every
published version has one unambiguous tag and immutable asset set. Both annotated
and lightweight tags are accepted; use annotated tags for maintainer releases.
There is no automatic version bump or version-file edit: the validated tag without
`v` is passed to the build and must equal the native CLI's reported version.

## Maintainer procedure

Before the first release, configure the GitHub repository's `main` branch and
`origin`, enable Actions, and require the aggregate build gate for contributions.
Restrict creation/update/deletion of `v*` tags to authorized maintainers through
repository rulesets. The scripts check tag ancestry on `origin/main`; authorization
comes from repository settings, not from the tag spelling. Allow GitHub Actions
to create releases with the final job's `contents: write` permission. Fork/PR
builds retain read-only permissions and cannot publish.

1. Prepare the candidate commit on `main`, update release notes, and choose an
   unused SemVer version. Verify the full Windows/Linux/macOS gates for that source.
2. Collect the exact-commit/binary qualification and experiment reports described
   below. Configure `MERSTRIDE_WINDOWS_QUALIFICATION`,
   `MERSTRIDE_LINUX_QUALIFICATION`, `MERSTRIDE_MACOS_REFERENCE_BUDGET` and
   `MERSTRIDE_TOKEN_EXPERIMENT` as repository
   variables. A green hosted SDK build is insufficient qualification.
3. After explicit release authorization, fetch `origin/main`, create an annotated
   tag on the qualified commit and push only that tag. For example, replacing
   the example version and commit with the reviewed values:

   ```bash
   git fetch origin main --tags
   git tag -a v1.2.3 QUALIFIED_COMMIT_SHA -m "Merstride 1.2.3"
   git push origin refs/tags/v1.2.3
   ```

4. The tag push starts Release: validate tag, build/test/package Windows, Linux
   and macOS, qualify the extracted macOS archive without system dotnet, aggregate
   their results, verify the same-run assets and qualification,
   then create a draft, verify uploaded bytes and publish. Prerelease tags produce
   GitHub prereleases. Every checkout uses the event's exact source commit.
5. Check the workflow result and the release manifest/checksums. Missing proof or
   any required failure blocks publication. If evidence was missing, supply the
   correct reports and rerun the failed workflow. Matching drafts can resume;
   existing public releases and mismatched draft assets cannot be overwritten.
   Fix a published version with a new patch tag; do not move/reuse its tag.

These commands document the authorized maintainer procedure; implementing or
testing release automation does not itself authorize a push or publication.

## Download and run

Once a qualified release exists, choose its archive in the repository's GitHub
Releases page. Prereleases may require selecting the specific release rather
than the latest stable release. The current distribution targets are:

| Target | Archive | Runtime requirements |
| --- | --- | --- |
| Windows x64 | `merstride-VERSION-win-x64.zip` | Windows 11 x64; no installed .NET runtime |
| Linux x64 | `merstride-VERSION-linux-x64.tar.gz` | Compatible glibc Linux x64, ICU and OpenSSL; no installed .NET runtime |
| macOS 15+ Apple Silicon candidate | `merstride-VERSION-osx-arm64.tar.gz` once qualified | Hosted native/clean-target path verified; no installed .NET runtime |

Download `SHA256SUMS` alongside the archive and compare its entry before
extraction. PowerShell: `Get-FileHash ./merstride-1.2.3-win-x64.zip -Algorithm SHA256`.
Linux: `sha256sum --check --ignore-missing SHA256SUMS` (the archive must be present;
the command must report it as OK). Checksums establish consistency with the
release's published assets; they are not signatures.

On Windows, extract the ZIP and run `./merstride.exe version` and
`./merstride.exe guide`. On Linux, extract with
`tar -xzf merstride-1.2.3-linux-x64.tar.gz`, then run `./merstride version` and
`./merstride guide`. The archive preserves executable permissions. Keep the
included notices. A qualified macOS archive uses the same extraction/run commands
with `osx-arm64` in its filename; verify it with `shasum -a 256`. Add `--json` for scripts; use an absolute state directory to
resume runs from another working directory. Workflow commands may require their
own tools; the Merstride engine does not install them.

## macOS hosted candidate and reference review

P15 prepares `osx-arm64` on the standard GitHub-hosted `macos-15` Apple Silicon
runner, with Xcode 16.4 and SDK 11.0.100 installed by the existing pinned official
setup action. The minimum candidate OS is macOS 15; Native AOT's target triple is
explicitly bound to 15.0. The runner checks architecture and actual tooling before
building. No personal Mac or self-hosted runner is required.

To conserve hosted resources, macOS build/package and clean-target jobs run only
in the tag Release workflow. Ordinary push, pull-request and manual Build runs
require Windows and Linux, including on `codex/p15-macos-native-release`.
The P15 branch's push trigger remains available for those checks. Release requires
all three platform builds and the separate macOS clean-target job; prior branch
artifacts cannot authorize a new release candidate.

Hosted run [44024602627](https://github.com/mcnilz/merstride/actions/runs/44024602627)
passed the actual build, managed/native suites, package, separate no-system-.NET
extracted-archive qualification and aggregate gate for commit `39f864d` on
2026-11-17. This supersedes P14's missing macOS execution-path deferral. It does
not authorize publication or supply the outstanding Windows/Linux clean-target,
exact-binary reference-budget or external token-experiment evidence. Every release
candidate still requires its own same-source, same-run platform evidence.
The macOS job and archive are mandatory in the prepared release graph; they cannot
be omitted to publish just Windows/Linux. Intel/universal binaries, signing and
notarization remain outside this scope.

The build runs the existing managed acceptance suite and the independent native
runtime suite under external DTrace process lifecycle observation. A separate
`macos-clean` job downloads the same-run archive, native fixture and native driver.
It removes only approved standalone .NET installation roots on that disposable
host, inventories all runtime/host locations under the writable system roots using
sudo, and fails on a remaining installation or incomplete scan. It checks the
arm64 Mach-O headers, archive entries/permissions, source/run/version and input
hashes, runs the extracted binary, then executes every inventoried runtime case
using the native driver. PowerShell's private self-contained runtime is a declared
interpreter prerequisite and does not host the suite. Missing DTrace privileges,
probes or events fail the job; no polling fallback or disabled assertion is allowed.

The clean report is collected from the current run's `macos-clean` artifact.
Configure `MERSTRIDE_MACOS_REFERENCE_BUDGET` separately with exact-commit/binary
reference-machine evidence: `role: reference-machine`, `environment`, `evidence`,
`commit`, `binarySha256`, executable `bytes`, and 30 positive millisecond `samples`.
The 25 MiB and P95 <150 ms budgets remain enforced; hosted build timings cannot
substitute for that designated reference measurement. Windows/Linux clean reports
and the token experiment remain required. Raw traces, runtime scans and provenance
stay in job artifacts; review them before sharing because they contain local paths.

Reviewed runner labels, image tooling and pricing on 2026-11-16 against the official
[runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners),
[Apple Silicon image inventory](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-arm64-Readme.md)
and [Actions pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).
Standard public-repository runners are free; private-repository usage consumes
plan allowance and can incur charges (the published standard macOS rate was
USD 0.062/minute). Only the release workflow uses the two macOS jobs,
each bounded to 30 minutes. Check account allowance and current prices before
authorizing hosted runs. No new marketplace action or product dependency is added.

Reviewed [DailyWingetNotify's release workflow](https://github.com/mcnilz/DailyWingetNotify/blob/c8007fbffd9b9ea704e6c382afce5edb193ebeac/.github/workflows/release.yml)
and its MIT license at revision `c8007fbffd9b9ea704e6c382afce5edb193ebeac` on
2026-11-16. Its tag-to-version, Native AOT archive and prerelease conventions
match this release procedure. No source code was copied and no dependency was
added. Merstride retains its reviewed action pins, separate platform gates,
restricted publication permission and immutable releases rather than adopting
the reference's asset replacement behavior.

## Verification and qualification

Run `pwsh -NoProfile -File scripts/build.ps1` on Windows, Linux or a qualified macOS host with SDK 11.0.100,
PowerShell 7, native compiler/linker, Node/npm and a headless browser. On Windows,
use a native PowerShell terminal to retain Windows archive tooling. Linux also
requires strace; set `MERSTRIDE_BROWSER=/usr/bin/google-chrome`. Keep platform
checkouts, obj/bin, package lock and artifacts paths separate. Linux uses
packages.linux-x64.lock.json; macOS uses packages.osx-arm64.lock.json;
Windows keeps packages.lock.json. The native driver has one lock file per RID. Do not replace one
platform's resolved assets with the other's. Native binaries require OS libraries;
Linux validation/start needs ICU and OpenSSL in addition to ldd-visible libraries.

The local gate executes 338 actual nonrecursive feature cases plus supplemental
regressions and policy tests. It records native hashes, actual compiler/ILC/link/
publish inputs, notices, TRX/scenario results and 30-start native
benchmarks. A dirty checkout is recorded and cannot produce a release package.
The first start is separate from warm P95; hosted timing is informative. The
reference-machine qualification remains required for publication. Raw diagnostics
stay under ignored `artifacts/`; version only reviewed summaries without private
paths or machine identities. Benchmarks record RID, binary identity and timings,
without hardware/security inventories. CI uploads compact test outcomes (including
failed runs), gate and benchmark summaries;
release platform uploads include the required platform archives. The internal
macOS verification artifact also carries the native helper, suite driver and
their notices; the separate clean-target job retains raw qualification evidence.

Run `pwsh -NoProfile -File scripts/build.ps1 -ReleaseQualification` to include the three clean-target
orchestration cases. Without independently collected reports these **fail**;
there are no skipped/pending/undefined required steps or implicit approval.
`MERSTRIDE_CLEAN_WINDOWS_REPORT`, `MERSTRIDE_CLEAN_LINUX_REPORT` and
`MERSTRIDE_CLEAN_MACOS_REPORT` identify those
reports. A successful local gate alone is not release readiness.

Clean-target collection: provision a new Windows 11 x64 or Linux x64 VM without
an installed .NET SDK/runtime. Retain image identity and provisioning logs. Copy
the exact native binary, native fixture, feature sources and an independently
available native suite driver, then run `pwsh -NoProfile -File scripts/qualify-clean-target.ps1 -Binary
<binary> -Fixture <fixture> -SuiteDriver <driver> -Commit <sha> -Output <report>`.
The driver must implement all 323 inventoried nonrecursive runtime cases and emit the scenario array
defined by docs/release-scenario-inventory.json. The build publishes that driver from tests/Merstride.NativeSuite to artifacts/suite;
it parses the actual feature files and shares their CLI fixture worlds. Its
assertion adapter supports only the explicitly used constraint subset, fails on
unsupported operations, and includes negative controls; NUnit remains the managed
host. Both runners execute the same runtime scenarios during every local gate. The 15 build-only
cases run independently on the build host and stay mandatory. The harness is
concrete collection/validation scaffolding, not completed clean-suite evidence.
PowerShell is a collection tool, not an engine dependency. Do not hide dotnet on
PATH or treat a hosted SDK runner as a clean image.

`tests/fixtures/linux-no-runtime.py` can run as root in a task-owned Linux checkout
to exercise guide/version/start/status/advance and a native process inside a
minimal chroot with only OS libraries. It runs the engine as an unprivileged user,
retains an exhaustive filesystem inventory and removes only its unique temporary
root. This is genuine no-installed-.NET smoke, **not** the full 323-case runtime suite or
Windows qualification.

Token experiment: supply an actual external-agent driver to
`pwsh -NoProfile -File scripts/token-experiment.ps1 -Driver <driver> -Output <report>`. It runs two tasks
(deterministic-chain, test-fix-loop), manual/engine variants, five runs each,
alternating order. The same agent/model/task identity and equivalent results are
required. Record real calls, available input/output token counts (null if
unavailable), elapsed time, failures and cache effects. The CLI never invokes an
LLM. This environment provides no comparable external-agent accounting driver;
no token totals or saving percentages are asserted.

Collect raw platform and experiment reports for the exact final source commit
and binary hashes. Include the complete per-platform
scenario arrays, featureHashes, environment/provenance and explicit processTree,
atomicSnapshots and referenceBudgets evidence. Token samples remain embedded.
Release validation checks all identities; missing proof blocks publication.
Qualification collection must occur against the final tagged source, not an older
commit whose metadata happens to have the same version.

Supply compact raw JSON through separately configured repository variables
`MERSTRIDE_WINDOWS_QUALIFICATION`, `MERSTRIDE_LINUX_QUALIFICATION`, and
`MERSTRIDE_TOKEN_EXPERIMENT`. Each platform report includes its own commit; the
release gate compares it to the tested tag commit. Separate variables keep each
report within GitHub's per-variable size limit. Values pass through environment
variables as data, never script interpolation. Set them after qualifying a clean
candidate commit, then trigger the authorized tag validation or rerun its failed
workflow. No source modification is needed to embed the commit's own SHA. The
tracked qualification JSON is an intentionally incomplete local example. The
read-only verify job embeds validated external input in the same-run verified
asset bundle. Publish consumes that bundle's qualification.json and never rereads
mutable repository variables.

Workflow steps are repository-owned scripts: `release.ps1 -Mode Tag`, per-platform
`Package`, then `Assemble`, `Validate`, and finally `Publish`. Versions and source/
run identities come from GitHub's validated tag context. Supporting files include
the manifest, SHA256SUMS, raw qualification, notices, inventory and release notes.
Only same-run successful archives are used. No asset clobber option exists.
Matching drafts resume only after downloaded bytes match; published versions are
refused. Locally reproduce control behavior with `pwsh -NoProfile -File
tests/fixtures/release.ps1 -Defect <defect>`; these use local Git and a fake gh
endpoint and never publish a remote release.

Package freshness is tested independently through `pwsh -NoProfile -File
tests/fixtures/package.ps1 -Defect <defect>`. Cases are valid, changed-head,
changed-worktree, changed-tests, changed-distribution, changed-benchmark and
changed-binary. They copy the actual public scripts and notices into disposable
clean Git repositories, then use synthetic candidate/evidence bytes. These are
control-flow tests, not native qualification. Run all 15 affected checks with
`dotnet test tests/Merstride.Acceptance -c Release --no-restore --filter
FullyQualifiedName~ReleaseTests` on each platform. Package refuses stale source or
gate evidence before creating staging or an archive.

Before any real release, verify origin/main, protected maintainer tag rules and
the required aggregate branch check. Repository settings and hosted execution
remain unverified. Release scripts do not configure GitHub settings or rename
local branches. Publication requires separate authorization.
