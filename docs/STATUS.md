# Implementation status

P01 implemented and accepted by the user. Updated 2026-11-15.
The accepted specification is preserved. Local tickets for P01–P07 live in
`.scratch/merstride/issues/`; P01 has no prerequisite and subsequent blockers match
the approved plan. The user authorized a local P01 commit; nothing was pushed or published.

## What works

The win-x64 Native AOT executable validates a real marked YAML/Mermaid workflow
containing one end node, starts without executing it, persists a definition snapshot
and source hash, reads it from a fresh CLI process, and advances to success/failure.
Initial per-run OS-backed writer locks and same-directory atomic state replacement
are present. Repeated terminal advance is stable. Status of a failed run exits 0.
Malformed/duplicate/unsupported YAML and unsupported graph/command syntax fail
explicitly. Validation errors include severity and 1-based source locations.
All command responses are JSON, including errors; `--json` is supported.
Offline guide and version require no workflow or state.

Namespace and assembly boundaries are enforced by NsDepCop as build errors, with
positive and intentionally failing build fixtures. Missing configs, disabled analysis,
and global illegal-dependency suppression fail the architecture gate. The analyzer
remains build-only, with compile/runtime assets excluded. Runtime networking
namespaces are forbidden. The build has no reflection-based JSON serialization or
YAML object construction.

The Windows GitHub build workflow uses verified current stable official Action SHAs,
read-only permissions, pull_request events, and disabled credential persistence.
The aggregate gate requires successful required work. YAML-based policy checks reject
unknown/unpinned actions, quoted write permissions, failure-tolerant required checks,
and an ineffective aggregate command. Locked direct/transitive dependencies require
version-specific provenance and license review. MIT license, contribution/security
guidance, templates, weekly Dependabot configuration, and notices are present.

## Validation evidence

Run `pwsh -NoProfile -File scripts/build.ps1`. This executes locked restores,
workflow/dependency/architecture gates, Release build, Native AOT publish, acceptance
tests against the published executable, and 30-start native budget measurements.

Final complete local build: **22 passed, 0 failed, 0 skipped**, including all 10 P01
feature cases and 12 supplemental regressions. Release build produced zero warnings
and zero errors. Workflow, architecture, dependency, and native budget gates passed.

The NUnit runner uses the official Cucumber parser on the actual feature files.
All 99 expanded cases are inventoried. P01 selects 10 cases, including scenario-level
P01 tags inside later features. The remaining 89 cases are explicitly reported as
**not yet implemented**, never counted as passed or NUnit-skipped required tests.
Undefined required steps throw; a harness regression proves this failure behavior.

Executed P01 feature coverage:

| Feature | Cases | Evidence |
| --- | ---: | --- |
| 00-bootstrap | 1 | Start, fresh-process status, advance to completion |
| 06-interface | 1 | Offline guide text and no run/child effects |
| 07-architecture | 5 | Forbidden/intended compilation; three ineffective configurations |
| 08-delivery | 3 | Action pins, local fork workflow policy, undocumented dependency rejection |

Additional CLI regressions cover five invalid/unsupported YAML inputs, failure-end
terminal behavior, located diagnostic severity, and undefined-step failure. Four
workflow-policy mutation cases cover whitespace in uses, quoted write permissions,
ignored build failures, and an ineffective aggregate gate. These are supplemental
tests, not additional claims of feature-scenario implementation.

Windows Job accounting records process creation externally around each CLI call and
requires exactly one new process, including short-lived descendants. The application
does not infer absence of child effects from its own logs. Network absence has source
and architecture-review evidence; OS-level network tracing was not performed.

Machine-readable local outputs: `artifacts/tests/p01.trx`,
`artifacts/tests/scenarios.json`, `artifacts/benchmark.json`, and
`artifacts/environment.txt`. The benchmark records binary hash, bytes, all timing
samples, CPU/OS/SDK, and antivirus state. Selected red/green results also remain
locally under artifacts/tests. Generated artifacts are ignored by Git.

## Native measurements

Native AOT Release publish succeeds on Windows x64 with SDK 10.0.404 and runtime
10.0.15, without AOT/trimming warning suppression. Executable size excludes separate
PDB symbols. Final measurement:

| Measurement | Result |
| --- | ---: |
| Executable | 3,878,912 bytes / 3.699 MiB |
| 15 MiB target / 25 MiB gate | Both passed |
| First measured start (validate + persist) | 70.782 ms |
| First measured status | 30.030 ms |
| 30 warm status starts, nearest-rank P95 | 27.601 ms |
| Warm status P95 below 150 ms | Passed |

Version 0.1.0, RID win-x64, SHA-256:
`f7ae0a8f744360173e4aeb83dbcb4e85733ed22c6b34488638e3ded431db3a4c`.
The full timing sample is retained in [native benchmark evidence](evidence/p01-native-benchmark.json).
Resolved production assets contain no NsDepCop compile/runtime assets; native compiler
reference inputs contain no NsDepCop or DotNet.Glob implementation. Publish output is
only merstride.exe plus separate symbols. This is initial separation evidence, not
the complete P07 release-distribution/license gate.

Reference environment: Windows build 26100, Intel Core Ultra 7 155H, Defender real-time
protection enabled, no antivirus exclusion. The harness includes process creation,
stdout/stderr reading, and process exit. The first measured start performs validation
and persistence; first status is separate from 30 fresh warm status processes. These
are local process-start measurements, not a cold boot or clean-runtime qualification.

## Review and limitations

The installed code-review workflow ran independent Standards and Spec reviews against
initial commit e421fa377ad2f3f3f874676937f5f903b05a7d40, including untracked additions.
Findings about external child observation, missing diagnostic severity, and
failure-tolerant required workflow checks were fixed with regression coverage.
Follow-up review found zero remaining actionable Standards findings and no residual
P01 implementation blockers on the Spec axis.
Source-string build assertions were removed; fork evidence is explicitly scoped to
local YAML policy validation. The full local build script was executed independently.

Unavailable/unclaimed checks:

- Actual GitHub hosted fork execution, uploaded artifacts, image identity, and branch
  protection require a published remote; none was created or used. Local policy passes
  do not establish those hosted outcomes.
- Linux, a clean Windows 11 machine without .NET, and native release distribution/A22
  are P07 work. This development machine has the SDK/runtime installed.
- OS-level network tracing, physical disk-failure guarantees, broad crash/concurrency,
  process-tree interruption, and filesystem qualification were not performed.
- Full graph/YAML syntax, processes, agents, completions, logs, limits, and recovery
  commands remain intentionally unsupported. Full runtime notices and release
  archives/automation remain P07. Token savings have not been measured.
- The restricted environment blocked the Native AOT MSBuild task host and redirected
  temporary-directory benchmark access. Native publish/full build/measurements passed
  with approved access to the local compiler and ordinary temporary directories.

Next slice: **P02 — deterministic process chains**, now unblocked by acceptance of P01.
It adds real sequential subprocesses, literal arguments, routing, and separate logs;
no P02 implementation has started.

## Review instructions

1. Read the P01 ticket, ADR 0001, and this status; compare against the P01 feature tags.
2. Run the build script and inspect TRX, scenarios.json, and benchmark.json.
3. Try README's single-end example with the native binary. Inspect start/status before
   advancing; try guide and invalid YAML. The full review workflow is not implemented.
4. Review source, production NsDepCop configuration, policy scripts, Action provenance,
   lock files, and dependency inventory. Check that no test/analyzer package is a
   runtime reference. Review P07 distribution work separately before any release.
