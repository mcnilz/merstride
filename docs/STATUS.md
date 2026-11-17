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
agents/scripts explicitly request protocol-1 JSON with `--json`.
Text errors/warnings use stderr; results, offline guides and bounded log tails use stdout.
Text advance now also shows live activity on stderr after successful state writes;
explicit JSON suppresses activity and keeps the existing protocol response.
Human activity separates process results, numbered source/destination movements
with route reasons and repeated visits, and run status/overall results. Summaries
explain the next explicit action; full activation IDs are secondary log selectors.
Project commands now discover the nearest `.merstride`/`.git` boundary, default
workspace/storage there and remember current and per-source/workflow runs.
Ordinary start creates and selects a new ready run, retaining earlier state.
Explicit `start <file> --advance` optionally performs that new run's first
synchronous advance in the same native invocation, using its exact identity.
Post-creation errors retain identity and durable progress without automatic replay.
Protocol 1 adds nullable start phase metadata; strict client allowlists must accept it.
Commands accept positional workflow paths or exact run IDs; ID-shaped filenames
use ./ or --file. New canonical run/request IDs have eight characters, and durable
reservations prevent reuse after explicit single-run discard. Legacy IDs/roots
remain usable. Ambiguous selections fail; historical receipts cannot complete new requests.

The product uses .NET 11 Native AOT and stable C# 15. Windows x64 and Linux x64
have local development-host verification. **Release qualification is incomplete
and publication fails closed.** No clean-target or token-saving claim follows
from a local build.

## Verification

P21 adds explicit optional `start <file> --advance`, using the same synchronous
advance adapter as the separate command and passing exactly the newly created ID.
Ordinary start remains ready/nonexecuting; completion still only moves the cursor.
ADR 0017 defines cancellation before the first-advance check, phase-aware partial
success and additive nullable protocol-1 `start` metadata. Failed creation executes
nothing; later errors retain identity, effects and durable progress without rollback,
automatic retry/replay or deletion. The state schema, engine, process runner,
dependency/license inventory and retention rules are unchanged.

On 2026-11-17 the settled P21 source passed the full Windows x64
`scripts/build.ps1` gate: **654 tests / 433 local feature cases / 418 independent
native runtime cases**, zero failures/skips and no build/publish warnings/errors.
All 31 dedicated P21 cases pass in both managed and native hosts. They compare
separate/combined chains, immediate/process-led handoffs, terminal success/failure,
input/workflow/workspace selection, limits and actual effects; reject malformed,
preparation and initial-persistence failures; preserve progress after request-lock
and result-save failures; isolate same/different concurrent starts and frozen
input/definitions; and prove actual supported interruption with explicit retry/fail.
The three recursive release-environment orchestrators remain independently
outstanding, outside the local 433 cases; they are not skipped local tests.
Architecture, dependency, distribution, privacy, native budgets and Mermaid gates
pass. Native size is **6.284 MiB**, warm status P95 **20.997 ms**.

Actual README/CLI human Bash blocks, optional combined start, the explicit JSON
Bash integration, separate/combined review examples and offline start help/guides
passed in isolated projects using the exact gate binary. Completion returns ready
before a separate advance finishes. Initial walkthrough-host restrictions and
walkthrough-only Windows newline/trailing-newline assertions were corrected.
Production/test code and scenario definitions stayed fixed throughout the final
gate and walkthroughs. Final staging removed an extra blank line at the new
feature file's EOF and refreshed its inventory hash; scenario identities and
the verified native binary are unchanged. Other final edits record evidence/history.
Independent Spec review found no gaps, wrong behavior or scope creep. Standards
review found no documented-rule violations and one nonblocking duplication note
about the CLI exception mappings. The coordinator explicitly chose to retain that
small duplication and avoid discretionary cleanup; there is no correctness finding.

Raw evidence remains in ignored `artifacts/` (P21 focused red/green TRX, full gate,
native reports and walkthrough). The coordinator accepted P21 on 2026-11-17 after
independent inspection and authorized its single scoped local commit. Implementation
is complete; final acceptance/history edits do not change the verified behavior.
No Linux/macOS, hosted or clean-target execution was added; the narrow
creation-to-advance signal window and physical disk failure are not precisely
fault-timed by the running-tree signal/filesystem fixtures. No measured token
savings, release readiness, push or publication is claimed. One native invocation
replaces two. Existing independent release qualification remains mandatory.

P20 makes the executed path and result readable without changing execution.
ADR 0016 deliberately replaces the human layout with ASCII `[run-id]` process,
numbered `Flow #...` and run-status lines. Exact/default/unlabeled route reasons,
visits/revisits, waiting/completion and unchanged-cursor recovery are explicit.
Overall results and next actions are separate from process exit codes. Agent
choices named exit_0, timeout, spawn_error, signal or output_limit remain agent
outcomes. Saves precede all durable claims; inspection and equivalent receipt
retries add no activity. Protocol 1, structured response types, engine/process
effects, locks, limits, state schema and dependency/license inventories are unchanged.

On 2026-11-17 the complete reviewed Windows x64 gate passed 623 tests / 402 local
feature cases and all 387 independent native runtime cases, with zero failures
or skips and no build/publish warnings or errors. All 21 dedicated P20 cases and
P17's live-before-release barrier passed. Coverage includes ordered branch reasons,
loop visits/handoffs, historical receipt retries, outcome/result distinctions,
failure/limit explanations, real interruption, explicit retry/fail and rejected
result/completion/resolution saves, redirected escaping and single-response JSON.
Architecture, dependency, distribution, privacy, budgets and Mermaid checks passed.
Native size was 6.255 MiB; warm status P95 was 23.351 ms.

The actual README and CLI Bash blocks passed in isolated projects, including
readable handoff/completion and scoped discard. Process-chain/review examples,
branch transcript and both embedded guides/help passed against the native binary.
The versioned before/after walkthrough omits secondary storage/log details and
normalizes identities. Independent Standards and Spec reviews have no remaining
findings; the review identified process-like agent outcome names, now covered by
five native regressions, and consolidated save-rejection fixture setup.
Initial red tests, a restricted test-host connection, an obsolete P17 wording
assertion and the walkthrough driver's newline handling were resolved before the
final gate. Raw logs and environment data remain under ignored artifacts.
The coordinator accepted P20 on 2026-11-17 after independent inspection and
authorized its single scoped local commit. Implementation is complete. Final
acceptance/evidence edits do not change the verified behavior. No push,
publication or new release qualification is claimed. No new Linux/macOS, hosted
or independent clean-target execution was performed for this slice.

P19 adds workflow context, canonical short identities and deliberate single-run
discard. ADR 0015 defines lexical absolute source-file/workflow associations,
per-root selection, positional workflow paths, explicit overrides, terminal/stale
context behavior, concurrency and compatibility. A bare r_ token selects an ID;
other tokens are source paths, without existence guessing. `--file` remains an
escape/alternative. Source changes or disappearance do not change frozen runs.
Completion still requires a stable request ID and can find its original run
independently of changing current context. Exact IDs win over longer prefix
matches. Predictable r_/q_ plus six hex digits are eight characters total; durable
reservations prevent reuse, with documented length extension after 0xffffff.
Activation IDs remain full log/history identities. Legacy full IDs/snapshots work;
older binaries cannot operate on newly adopted short-ID/additive-state roots.

Retention is explicit. Start never deletes earlier runs. `runs [file]` identifies
sources/statuses; `use` switches deliberately; `discard <run-id>` removes one
selected run's state, logs and receipts. OS locks, validated owned paths, a durable
pending marker, staging and minimal ownership evidence protect cleanup/retries.
Corrupt, linked or unexpected contents are refused. Final empty-stage/record-only
retries work without manual directory management; unrelated runs are preserved.

On 2026-11-17 the final reviewed Windows x64 gate passed 602 tests / 381 local
feature cases and all 366 independent native runtime cases, with no failures or
skips. Its 43 P19 cases cover repeated starts, multiple files/pairs, frozen paths,
subdirectories/explicit roots, legacy state, stale/corrupt/locked context, delayed
completion/loop receipts, exact short/long-prefix collisions, reserved collisions,
length extension, positional paths/ID-shaped files/delimiters/conflicts, real
concurrent process locks and discard isolation/failure/recovery. Required unknown
steps and public build/policy checks remain enforced. Native builds/publishes had
no compiler/linker warnings or errors. Architecture, dependency, distribution,
privacy, native budgets and Mermaid checks passed. Native size was 6.240 MiB;
warm status P95 was 23.850 ms.

The actual README and CLI-guide Bash blocks passed with prior terminal state, positional source
selection, readable request completion and scoped discard. Both independent review
axes have no outstanding findings. Review fixes covered corrupt-context recovery,
inventory under staging conflicts, exact identity precedence, durable final-discard
recovery and refusal of staged file/link conflicts. An adjacent P12 assertion now
allows the new bookkeeping while still rejecting script files; its interpreter
arguments explicitly decode UTF-8 stdin, avoiding inherited Windows code-page
dependence. Product process transport/encoding and dependency inventories are unchanged.

Earlier development gates exposed the old filesystem expectation, help retrieval
of absent arguments, evolving feature hashes and overlapping Windows output-file
locks. All were resolved before the final sequential gate on unchanged source.
Raw logs remain under ignored artifacts. The coordinator accepted P19 on
2026-11-17 after independent inspection and authorized its scoped local commit.
Implementation is complete. No push, publication or new release qualification is claimed.
Final commit preparation removed only a surplus feature-file EOF blank line and
refreshed its inventory hash; all 43 P19 native cases passed again. Product
behavior is unchanged.
No Linux/macOS, hosted or independent clean-target execution was performed for P19.


P18 adds CLI-only project discovery and resource selection without changing the
engine, saved-state schema or dependencies. ADR 0014 specifies boundaries,
independent absolute overrides, legacy cwd-relative storage, durable state and
preserved ignore/config content. Start maintains a final local `/runs/` ignore
rule, including when existing rules negate an earlier one. Text suggests short
prefixes alongside full IDs; protocol-1 JSON retains full resource identities.

On 2026-11-17 the reviewed Windows x64 gate passed 559 tests / 338 local feature
cases and all 323 independent native runtime cases without failures or skips.
The 26 P18 cases cover project/subdirectory/Git-file boundaries, explicit and
legacy roots, spaces, actual process cwd/logs, multi-run/prefix ambiguity,
current/historical request collisions, loop receipt retries, invalid/stale
selections, missing/corrupt/conflicting state, offline discovery and existing
ignore/config preservation. Architecture, dependency, distribution, privacy,
budgets and Mermaid checks passed, with no native build/publish warnings/errors.
Native size was 6.026 MiB; warm status P95 was 25.828 ms.

The actual simplified README Bash blocks passed against the native binary,
including inline completion by short request prefix in a project path with
spaces. The user's follow-up removes PATH instructions and shows a small
executable Mermaid/YAML pair directly. The revised commands, native validation
of README.md and the added README diagram render all passed separately after
the full gate. Standards and Spec reviews have no remaining findings.

The initial restricted test host could not connect; native tests passed with
local process permissions. An earlier full run was invalidated when review
changes altered feature hashes during execution; the final run used unchanged
feature sources and refreshed inventory. Raw evidence remains under ignored
artifacts. No Linux/macOS, hosted or clean-target execution, local commit, push,
publication or new release qualification is claimed for this slice.

P17 adds live text activity on stderr while retaining final stdout summaries and
the explicit protocol-1 JSON response. A CLI persistence decorator reports only
successful writes: activation with launch pending, saved outcomes/routes, agent
waiting and terminal/interrupted state. Completion acceptance follows its save;
inspection and equivalent retries do not announce new work. Waiting/interrupted
summaries explain the next caller action. No process/engine/state-schema or
dependency change is involved.

On 2026-11-17 the Windows x64 gate passed 533 tests / 312 local feature cases
and all 297 independent native runtime cases without failures/skips. The 13 P17
cases prove live visibility before releasing a controlled process, ordered chains,
handoffs/receipts, errors/limits, real interruption, control escaping/raw logs,
JSON compatibility, failed-save truthfulness and closed activity pipes. README
process-chain and agent-task examples plus embedded activity guidance passed
against the published native CLI. Architecture, dependency, distribution,
privacy and Mermaid checks passed. Both review axes have no remaining findings.
The repeated final gate includes the reviewed test changes, with no build/publish
warnings/errors; native size was 5.979 MiB and warm status P95 was 25.03 ms.
Raw evidence stays under ignored artifacts. No Linux/macOS execution, hosted run,
publication or release qualification is claimed for this slice.

The P16 human-output follow-up on 2026-11-17 makes README, CLI, recovery and
general authoring/discovery examples use readable default output. Walkthroughs
reuse displayed IDs through explicit prompts; jq is optional for the separately
labeled automated JSON integration example. On Windows x64/Bash 5.3.15, the
actual README and CLI blocks passed with displayed IDs supplied to their read
prompts. Both interrupted-run recovery decisions, readable invalid-ID errors,
authoring/discovery and the retained JSON integration also passed. Raw evidence
is under ignored artifacts; no new Linux/macOS execution is claimed.

An attempted Windows full gate from Bash exposed Unix tar rejecting native
drive-letter archive paths in a controlled release fixture. That run was stopped;
Windows build instructions now explicitly use a native PowerShell terminal to
retain Windows archive tooling. The general CLI examples remain in Bash.
The repeated complete Windows gate passed from native PowerShell: 520 tests /
299 local feature cases and all 284 independent native runtime cases, with no
failures/skips or build/publish warnings/errors. Architecture, dependency,
distribution, privacy and Mermaid checks passed; native size was 5.974 MiB and
warm status P95 31.343 ms. Both independent review axes, including the platform
clarification, found no actionable findings. The three independent clean-target
orchestration cases remain separately required; no publication was performed.

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
  candidate commit and binary hashes: all 387 runtime cases, plus the separately
  mandatory 15 build-only cases. A minimal Linux chroot smoke is insufficient.
- Run the real external-agent token experiment with the same agent/model/task
  and equivalent results. No token totals or savings percentage are established.
- Verify hosted GitHub execution, fork isolation and branch/tag controls. Static
  policy checks and simulated release fixtures do not prove hosted outcomes.

Use [release verification](RELEASING.md) for collection commands and provenance.
The three release-environment scenarios fail explicitly with
`RELEASE QUALIFICATION UNAVAILABLE` while independent evidence is absent. They
remain mandatory; local success does not waive them.
