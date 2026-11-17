# Current status

Merstride implements sequential native Markdown workflows with `process`, `agent`
and `end` nodes. Supported behavior includes multi-workflow selection, constrained
YAML/Mermaid validation, frozen definitions, literal process arguments, bounded
execution and selected context, persistent runs, idempotent completion receipts,
writer exclusion, concurrent snapshots and explicit crash recovery. English help
and the offline guide describe protocol version 1. Inline outcome parameters are
the preferred completion path; file completion remains compatible. The embedded
`creation-guide` supplies offline authoring instructions, a complete copyable
workflow and validation checklist without external content files.
Process authoring also supports shebang-selected inline scripts over UTF-8 stdin
and disjoint Windows/macOS/Linux execution variants, with no script-file transport
or interpreter provisioning. Direct command/args execution remains available.
All commands now default to readable English text, including redirected output;
agents/scripts explicitly request the unchanged protocol-1 JSON with `--json`.
Text errors/warnings use stderr; results, offline guides and bounded log tails use stdout.

The product uses .NET 11 Native AOT and stable C# 15. Windows x64 and Linux x64
have local development-host verification. **Release qualification is incomplete
and publication fails closed.** No clean-target or token-saving claim follows
from a local build.

## Verification

P16's documentation verification on 2026-11-17 passed on Windows x64 with
Bash 5.3.15 (`OSTYPE=cygwin`), jq 1.7 (`jq-1.7-dirty` as reported by the installed
utility), PowerShell 7 and SDK 11.0.100. The README, CLI and agent-guide Bash
blocks executed against the published native CLI, including paths containing
spaces, run/request extraction, inline completion and quoted multiline Unicode
JSON. Invalid run/JSON calls retained exit 4. Both recovery alternatives passed
against actual interrupted fixture runs after inspecting their external effects.
The Bash Mermaid commands and PowerShell privacy entry point also passed.
General examples now use Bash; PowerShell scripts and deliberate interpreter/
platform examples remain available. No runtime dependency or behavior changed.

The complete Windows local gate passed 520 tests / 299 local feature cases and
all 284 independent native runtime cases, with no failures/skips or build/publish
warnings/errors. Architecture, dependency, distribution, privacy and Mermaid
checks passed; native size was 5.974 MiB and warm status P95 was 29.477 ms.
Independent Standards and Spec reviews found no actionable findings. Raw logs
remain under ignored artifacts. No Linux/macOS execution, hosted run, publication
or new release qualification is claimed for P16; the three independent
clean-target orchestration cases remain separately required.

Hosted Build run [44024602627](https://github.com/mcnilz/merstride/actions/runs/44024602627)
passed on 2026-11-17 for commit `39f864d` and internal version `0.1.0`:
actual macOS 15+ arm64 native publishes, managed acceptance, independent native
acceptance, package audit/budgets/Mermaid, and packaging all succeeded. The separate
`macos-clean` job extracted the same-run archive, verified actual standalone .NET
absence and native input/source/version identities, and passed all 284 inventoried
runtime scenarios through the native driver. The aggregate gate succeeded.
Windows/Linux were intentionally skipped in that historical verification run.
The final resource policy restores both for every ordinary Build and runs macOS
build/package plus independent clean-target qualification only for tag releases.
Release retains its mandatory all-platform and no-system-.NET checks.
The final resource-policy change passed the complete Windows gate: 520 tests and
all 284 native runtime cases, no failures/skips, with both review axes clear.

P15 is complete within its hosted macOS implementation/verification scope. This
supersedes the earlier missing macOS path, not P07's outstanding Windows/Linux
clean-target reports, exact-binary reference-budget evidence or external token
experiment. No release tag or publication was created. The evidence applies to
the tested commit; subsequent candidates need fresh same-source/same-run gates.

Earlier macOS diagnosis runs exposed an exact SDK debug-symbol allowlist omission,
a DTrace process attachment ending across launcher exec, raw macOS lock errno 35
being misclassified, and interpreter/temporary-path fixture differences. The final
fixes preserve exact child accounting, timeouts, interruption/recovery and snapshot
assertions. The observer retains short-lived kernel events, uses a positional root
PID with a READY barrier, and fails on stale heartbeats, errors or lost events.
The rebuilt Windows native binary also passed all 97 affected regressions. Both
incremental review axes reported no outstanding findings. Raw evidence remains
in ignored artifacts and the GitHub run's retained artifacts.

The Windows x64 local gate on 2026-11-16 passed 506 tests / 299 local feature
cases, plus all 284 runtime cases through the independently published Native AOT
suite driver. No failures/skips or native build/publish warnings/errors occurred.
Architecture, dependency, distribution, privacy and Mermaid gates passed. Size
was 5.974 MiB; warm status P95 was 30.334 ms. Native-driver assertion failures
were detected in its first run, corrected and reverified. The new short-lived
launch case observes one helper and all 32 children after exit.

Linux x64 passed its complete 502-test / 299-local-feature gate and all 284 native
runtime cases with no failures/skips or native build/publish warnings/errors.
All policy, dependency, distribution, privacy, budget and Mermaid checks passed;
size was 6.064 MiB and warm status P95 13.349 ms. The final native-driver and
tag-free-workflow follow-ups passed separately: all 284 native runtime cases and
33 affected driver/workflow/package checks, with no failures/skips or native
publish warnings/errors. Windows also passed all 33 affected checks. Independent
Standards and Spec reviews of the implementation and follow-up found no
actionable findings on either axis.

Initial read-only Build verification exercised the separate macOS no-system-.NET
stage using an internal 0.1.0 package. That path supplied the evidence above;
the final resource policy confines both macOS jobs to tag Release. Native driver artifacts retain the additional
Cucumber MIT notices; product archives keep their existing runtime notices.

The prepared macOS path checks real arm64/tooling, retains external DTrace events,
preserves existing interruption/limits/recovery/snapshot assertions, and verifies
the extracted archive with actual standalone-runtime absence and exact same-run
native product/helper/driver identities. Publication requires all three platform
archives and separate reference-machine budget evidence. Controlled release and
workflow regressions passed; they do not establish macOS or hosted qualification.
Official osx-arm64 compiler package metadata/hash/MIT license and complete notices
were reviewed; Windows/Linux product lock contents are unchanged. Raw evidence
remains under ignored artifacts. The authorized P15 branch pushes and hosted runs
are recorded above; no publication occurred.

On 2026-11-16, the P12 Linux CI correction passed the complete Windows x64 and
Linux x64 local gates: each executed 480 tests / 298 local feature cases with no
failures/skips or native build/publish warnings/errors. The six Linux failures in
hosted Build run 43865608040 were reproduced locally, together with the related
retry case. Handling IOException from stdin close/flush restored early-exit,
timeout and retry outcomes; declaring TERM=dumb in Unix test callers removed
PowerShell-generated terminal modes from their expected plain output. All 45 P12
native scenarios pass. A separate native check proves TERM inheritance and raw
log preservation for both xterm and dumb; production environment/log semantics
are unchanged. Contract, embedded guides, authoring notes and ADR 0009 explain
the calling convention. Existing feature tags and scenario inventory are unchanged.

Architecture, dependency, distribution, privacy and Mermaid checks pass. The Linux
Mermaid steps were repeated on officially checksummed Node 22.23.5 after the
initial run warned about its preinstalled Node 18; the native suites did not
require rerunning for this development-only tool correction. Windows/Linux sizes
are 5.974/6.064 MiB, with warm status P95 23.598/6.392 ms. Independent Standards
and Spec reviews found zero actionable findings. No dependency/notice changes,
macOS execution or corrected hosted rerun is claimed. The earlier hosted Build
passed Windows and failed Linux; local corrected results do not change that run.
P07 release qualification remains incomplete. No push or publication occurred;
raw evidence stays under ignored artifacts.

On 2026-11-16, P14's tag-release completion passed the full Windows x64 local
gate with the example product version `0.2.0-rc.1`: 480 tests / 298 local feature
cases, no failures/skips or build/publish warnings/errors. The published native
CLI reports that exact prerelease version with protocol 1. All release checkouts
now select the event commit, enforced through public workflow policy; a moving
checkout mutation exposed the missing restriction before the fix. Ten new public
Tag-mode tests and two checkout-policy mutations pass; all 37 focused release/
policy checks pass. Existing package, integrity, qualification, draft and
no-overwrite tests remain green, as do privacy, architecture, dependency,
distribution and Mermaid gates. Four unchanged official action pins were
reverified against their stable release tags. No dependency/notice changes.

P14 documents SemVer/tag and download procedures and defers macOS distribution
with a concrete osx-arm64 follow-up: the current non-Windows acceptance path
requires Linux strace and /proc; macOS native acceptance, locks/audits and
qualification remain absent. See ADR 0011 and RELEASING.md. No new Linux/macOS or
hosted execution evidence is claimed. P07 clean-target/experiment prerequisites
remain outstanding; no remote tag or release was created. Raw logs stay under
ignored artifacts. Standards/spec review results are recorded in the local ticket.
Both independent reviews found zero actionable findings. Native size was
5.973 MiB and warm status P95 was 29.356 ms, within the existing budgets.

On 2026-11-16, P13's guide-formatting follow-up passed the complete Windows x64 local gate: 468 tests /
298 local feature cases, no failures or skips, native publish with no
warnings/errors, architecture/dependency/distribution/privacy checks and Mermaid
compatibility including the new inline example. The 33 dedicated P13 cases verify
redirected text defaults, explicit JSON, offline discovery, located diagnostics,
errors/exit codes, waiting-agent context, completion idempotency, terminal state,
bounded log tails/raw-byte preservation and retry/fail recovery. A native red/green
case also proves supplementary Unicode format characters are visibly escaped
without changing JSON prompt content. Standards/spec reviews have no remaining
findings. The release inventory now lists 298 local / 283 runtime identities with
all 14 current feature hashes; stale-hash publication fixtures failed before the
inventory update and pass in the final gate. This is controlled local testing,
not actual publication.

The guide follow-up reproduced the native operational guide's missing paragraph
breaks. It now separates quick start, handoffs, receipts, process execution,
logs/limits, recovery and discovery, with numbered steps and indented command
templates. The added native regression fails on the previous text block and
checks readable sections plus identical decoded guide content in JSON. Both
review axes found no outstanding findings. No execution or parser changes.

The 45 dedicated P12 cases remain passing and cover
frozen scripts/variants, multiline Unicode, literal shebang arguments, absolute/
relative/PATH resolution, env forms, malformed definitions, overlap rejection,
missing interpreter/platform coverage, streams, exit routing, bounded input/output,
timeouts, early exit, interruption, explicit retry and retained activation history.
Actual installed interpreters were PowerShell 7.6.10, Python 3.12.10 and Git Bash
5.3.15 on Windows; Git Bash execution is not Linux evidence. The complete portable
example validated and executed on Windows. Both standards/spec reviews had no
outstanding findings. P11's 28 offline-guide cases and all prior local regressions
remain in the passing suite.
The executable measured 5.973 MiB; warm `status` P95 was 20.993 ms, within the
25 MiB / 150 ms gates.

The most recent prior full Linux x64 (WSL2) verification passed 361 tests / 192
local feature cases with no failures/skips and met size/status gates. Linux was
not rerun for P11, P12 or P13; these slices have no new Linux execution evidence.
No macOS environment was available and no macOS execution is claimed. P12's macOS/Linux
variant names and shared code do not qualify those targets. These are
development-host results, not clean-target or cold-boot qualification.

Reproduce the complete local gate with
`pwsh -NoProfile -File scripts/build.ps1` on each platform. Raw logs, TRX, traces,
audits and timing records stay under ignored `artifacts/`. See
[Testing](TESTING.md) for assertions and [Contributing](../CONTRIBUTING.md) for tools.

## Outstanding release work

- Re-run the verified macOS native and clean-target gates for each exact release
  candidate; NUnit remains a separate .NET-hosted gate.
- Collect actual Windows 11 x64 and Linux x64 clean-platform reports for the exact
  candidate commit and binary hashes: all 284 runtime cases, plus the separately
  mandatory 15 build-only cases. A minimal Linux chroot smoke is insufficient.
- Run the real external-agent token experiment with the same agent/model/task
  and equivalent results. No token totals or savings percentage are established.
- Verify hosted GitHub execution, fork isolation and branch/tag controls. Static
  policy checks and simulated release fixtures do not prove hosted outcomes.

Use [release verification](RELEASING.md) for collection commands and provenance.
The three release-environment scenarios fail explicitly with
`RELEASE QUALIFICATION UNAVAILABLE` while independent evidence is absent. They
remain mandatory; local success does not waive them.
