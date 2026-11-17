# P20: Make flow progression and results clear in console output

**What to build:** Let a person follow the path taken through a workflow and
understand each meaningful change, its reason, the final result and any required
next action directly from ordinary console output.

**Blocked by:** None. P13 readable output and P17 live activity are complete.
P19's selection/identity/cleanup behavior is independent; use established IDs
until that ticket changes their presentation.

**Status:** complete; coordinator accepted on 2026-11-17

**Approval and delivery boundary:** The initial 2026-11-17 capture authorized
breakdown only. The user subsequently approved sequential P19–P22 implementation,
one chat per ticket. P19 was accepted and committed as
35cec1650509ce5a265c19c04852347d6ef98266. This session owns P20 implementation
and verification only. The original no-commit boundary remained in force until
coordinator acceptance on 2026-11-17. The coordinator now authorizes exactly one
local P20 commit after final evidence/privacy/staging checks. No push, publication
or deployment is authorized. P21/P22 remain outside this session.

## Implementation history

- 2026-11-17: Read the actual ticket and normative baseline. Selected numbered
  ASCII flow movements with routing reasons, separate process/run results and
  existing node IDs/visit counts. Protocol 1 and execution/state boundaries stay
  unchanged; ADR 0016 records the human-layout compatibility decision.
- Native CLI red/green started with exact/default branch ordering; the prior
  binary failed the new meaningful progression assertions. Later rejection
  coverage also failed on missing next-action guidance before its text-only fix.
  Verification and independent review results will be recorded before acceptance.
- 2026-11-17: Implemented the CLI-only formatter and save-linked activity changes.
  Reviewed the legal agent outcomes exit_0, timeout, spawn_error, signal and
  output_limit; all five have native red/green regression coverage and remain
  literal agent choices. No engine, process runner, persistence schema, response
  model/serializer, dependency or license changes.
- Final Windows x64 `scripts/build.ps1` passed: **623 tests / 402 local feature
  cases / 387 independent native runtime cases**, no failures or skips and no
  build/publish warnings or errors. All 21 P20 scenarios and P17's controlled
  live barrier pass. Architecture, dependency, distribution, privacy, budgets
  and Mermaid checks pass. Native size **6.255 MiB**, warm status P95 **23.351 ms**.
- Actual README/CLI Bash blocks, process-chain/review examples, branch transcript
  and embedded guides/help passed against the native binary in isolated projects.
  Rejected result/completion/resolution saves produce no success/movement claim;
  actual interruption and retry/fail preserve effect and no-replay checks.
- Independent Standards review: no rule violations; optional duplicated
  save-rejection setup consolidated in a disposable native fixture and reviewed
  again with no remaining findings. Independent Spec review: no findings.
- Initial restricted-host connection, obsolete P17 string expectation and
  walkthrough-driver LF input were corrected; expected red regressions proved
  the old behavior fails the new requirements. Raw logs remain in ignored
  `artifacts/` (P20 logs, focused TRX, full gate and walkthrough), with concise
  evidence in docs/STATUS.md. No Linux/macOS, hosted or clean-target execution,
  release qualification, push, publication or deployment is claimed.
- 2026-11-17: Coordinator accepted P20 after independent inspection of activity
  and summaries, completion/resolution integration, ADR 0016, native scenarios,
  branch/loop/recovery transcripts and whitespace. The process-like agent outcome
  concern is resolved. Implementation is complete; exactly one scoped local
  commit is authorized. Final edits record acceptance only; verified behavior
  is unchanged and the complete gate need not be repeated.

## Exact changed paths

- `.scratch/merstride/issues/20-readable-flow-progression.md`
- `README.md`
- `docs/AGENT-GUIDE.md`
- `docs/CLI.md`
- `docs/CONTRACT.md`
- `docs/FLOW-OUTPUT.md`
- `docs/STATUS.md`
- `docs/TESTING.md`
- `docs/adr/0010-p13.md`
- `docs/adr/0013-p17.md`
- `docs/adr/0016-p20.md`
- `docs/release-scenario-inventory.json`
- `examples/process-chain.md`
- `examples/review.md`
- `features/20-flow-progression.feature`
- `src/Merstride/Cli/CliOutput.cs`
- `src/Merstride/Cli/CommandLine.cs`
- `src/Merstride/Cli/ConsoleActivity.cs`
- `src/Merstride/Cli/CreationGuide.txt`
- `src/Merstride/Cli/Program.cs`
- `tests/Merstride.Acceptance/ConsoleActivityWorld.cs`
- `tests/Merstride.Acceptance/FeatureTests.cs`
- `tests/Merstride.Acceptance/ProgressionWorld.cs`
- `tests/Merstride.Acceptance/RecoveryWorld.cs`

P21/P22 issue files remain unmodified and untracked. There are no unresolved
findings or hard blockers. Coordinator acceptance authorizes the single local
P20 commit; no subsequent scope or remote action is authorized.

## Problem and prior context

After trying the CLI, the user still finds transitions cryptic. They want visibly
clear progression, for example old state -> new state, so they can understand
what happened, the route through the flow and its result. The arrow is an
illustration of the need, not a mandated character, layout or exact wording.

[P13](13-readable-default-output.md) supplies readable summaries and
[P17](17-live-console-activity.md) supplies live activity. P17 already prints
activation, saved process outcome/route, agent waiting and terminal/interrupted
state. This follow-up improves the human explanation and visual organization;
simply adding progress messages again would duplicate completed work. ADRs 0010
and 0013 establish truthful persistence-linked messages, text stream placement,
bounded output, escaping and unchanged explicit JSON behavior.

## Acceptance criteria

**Design direction:** Optimize for human and AI usability, not preservation of
the existing layout or command vocabulary. Replace old concepts when a clearer
design warrants it. The contract/features and ADRs are the normative baseline to
update deliberately in a future approved implementation. Make material protocol,
command and migration choices explicit; backward compatibility is not a blanket
requirement and no behavior change is authorized by this issue capture.

- [x] Show the meaningful progression in execution order so a reader can
      distinguish where the run was, where it moved and why. Distinguish a
      workflow node/cursor transition from a run-status change and a process
      result; avoid unexplained state codes or long identifier-heavy sentences
      as the sole human explanation. Keep enough identity to distinguish runs.
- [x] Make branching, loops and repeated node visits understandable, including
      exact/default routing, agent waiting/accepted completion, terminal success
      and failure, interrupted execution and explicit recovery. Explain staying
      at a node or taking no route when relevant; do not imply a transition that
      did not occur. Show the path as it happens without an unbounded history
      dump or a new persisted event-history subsystem.
- [x] Clearly distinguish an outcome from the overall run result. Waiting and
      interruption explain the next caller action; errors explain the relevant
      cause and available recovery/selection action. Read-only status of a failed
      run remains a successful inspection and clearly describes that failure.
- [x] Demonstrate coherent before/after transcripts for a multi-node branch, a
      looping agent handoff and a failure/recovery path. The chosen format is
      legible with redirected streams and plain terminals, uses readable English
      and preserves control-character escaping, bounded output and stream roles.
      No color, spinner, interactive UI or exact arrow glyph is prescribed.
- [x] Preserve live visibility before a long process completes, truthful
      activation-before-launch wording, announcement only after successful
      authoritative saves, and no replay on inspection/retries. Preserve
      execution effects, exit codes, locks, limits and receipt identity. Do not
      add automatic execution/recovery or claim uncertain effects succeeded.
- [x] Keep a clear structured integration mode for AI/automation with exactly
      one response and no unsolicited human activity text. Use existing explicit
      --json/protocol 1 as the baseline; any replacement or incompatible change
      requires a deliberate protocol/version and migration decision. AI callers
      can target resources stably and parse results without reading decorated
      text. Update help, guides, examples, contract and ADR together.
- [x] Add dedicated @P20 native CLI scenarios with meaningful assertions for
      ordered source/destination/reason/result and next-action information,
      including branches, loops, waiting, retries, failure, interruption,
      rejected saves, redirection/control characters and the agreed structured
      integration/version behavior. Preserve P17's live barrier and existing
      tags; required undefined,
      ambiguous, pending or skipped steps fail. Record actual local evidence.

**Open design choices:** Choose wording, grouping, labels and ASCII/Unicode
representation through representative transcripts. Decide whether existing node
IDs suffice or labels can improve clarity, recording any necessary definition
or persistence changes deliberately. Define how stdout summaries and stderr activity stay
understandable when captured separately; no total cross-stream ordering promise.

**Out of scope:** P19's context selection, short-ID scheme and retention policy;
P21's optional combined start/advance and P22's all-scenario audit; a visual flow
editor, full history API, raw child-output streaming, workflow execution changes
or release qualification/publication.
