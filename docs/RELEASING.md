# Native verification and release

Run `pwsh -NoProfile -File scripts/build.ps1` on Windows or Linux with SDK 11.0.100,
PowerShell 7, native compiler/linker, Node/npm and a headless browser. Linux also
requires strace; set `MERSTRIDE_BROWSER=/usr/bin/google-chrome`. Keep platform
checkouts, obj/bin, package lock and artifacts paths separate. Linux uses
packages.linux-x64.lock.json; Windows keeps packages.lock.json. Do not replace one
platform's resolved assets with the other's. Native binaries require OS libraries;
Linux validation/start needs ICU and OpenSSL in addition to ldd-visible libraries.

The local gate executes 155 actual nonrecursive feature cases plus supplemental
regressions and policy tests. It records native hashes, actual compiler/ILC/link/
publish inputs, notices, TRX/scenario results, machine identity and 30-start native
benchmarks. A dirty checkout is recorded and cannot produce a release package.
The first start is separate from warm P95; hosted timing is informative. The
reference-machine qualification remains required for publication.

Run `scripts/build.ps1 -ReleaseQualification` to include the two clean-target
orchestration cases. Without independently collected reports these **fail**;
there are no skipped/pending/undefined required steps or implicit approval.
`MERSTRIDE_CLEAN_WINDOWS_REPORT` and `MERSTRIDE_CLEAN_LINUX_REPORT` identify those
reports. A successful local gate alone is not release readiness.

Clean-target collection: provision a new Windows 11 x64 or Linux x64 VM without
an installed .NET SDK/runtime. Retain image identity and provisioning logs. Copy
the exact native binary, native fixture, feature sources and an independently
available native suite driver, then run `scripts/qualify-clean-target.ps1 -Binary
<binary> -Fixture <fixture> -SuiteDriver <driver> -Commit <sha> -Output <report>`.
The driver must implement all 140 inventoried nonrecursive runtime cases and emit the scenario array
defined by docs/release-scenario-inventory.json. This repository does not currently
provide that full native driver; its NUnit host requires .NET. The 15 build-only
cases run independently on the build host and stay mandatory. The harness is
concrete collection/validation scaffolding, not completed clean-suite evidence.
PowerShell is a collection tool, not an engine dependency. Do not hide dotnet on
PATH or treat a hosted SDK runner as a clean image.

`tests/fixtures/linux-no-runtime.py` can run as root in a task-owned Linux checkout
to exercise guide/version/start/status/advance and a native process inside a
minimal chroot with only OS libraries. It runs the engine as an unprivileged user,
retains an exhaustive filesystem inventory and removes only its unique temporary
root. This is genuine no-installed-.NET smoke, **not** the full 140-case runtime suite or
Windows qualification.

Token experiment: supply an actual external-agent driver to
`scripts/token-experiment.ps1 -Driver <driver> -Output <report>`. It runs two tasks
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

Before any real release, configure origin/main, protected maintainer tag rules and
the required aggregate branch check. Origin already references
git@github.com:mcnilz/merstride.git; the user reported pushing P01. Actual repository
settings and hosted execution have not been verified or changed by this task.
Release scripts do not rename the current local master checkout or configure
GitHub settings. No remote publication is authorized by P07 implementation.
