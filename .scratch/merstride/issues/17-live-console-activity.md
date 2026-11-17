# P17: Show execution activity and state transitions in the console

**What to build:** While Merstride executes a workflow, show readable console
feedback about what it is doing and the transitions it makes. A person or an
existing agent following the console should understand progress without waiting
for the final summary or opening log files.

**Blocked by:** None (can start immediately). P02 process execution, P03 agent
handoffs, P06 recovery and P13 readable output are complete. P18's simpler
project workflow is independent. Outstanding release qualification blocks
publication, not this follow-up.

**Status:** complete

**Implementation session:** Assigned by the user's "nächstes issue umsetzen"
request on 2026-11-17. Implement P17 only, using the already agreed published
native CLI and public build seams. No local commit, push or publication requested.
The user's subsequent "commit" follow-up authorizes a local P17 commit only.

**Approval and delivery boundary:** The user authorized local issue capture on
2026-11-17 after trying the README walkthrough. This ticket records the requested
scope only. Implementation and any local commit require a separately assigned
implementation session and its acceptance instructions. No commit, push,
publication or deployment is authorized by this capture.

**Problem and existing overlap:** Default output is readable after
[P13](13-readable-default-output.md), but it remains too sparse during execution.
The current contract explicitly prohibits progress output, and the P13 decision
keeps its renderer separate from execution. This follow-up deliberately extends
that presentation behavior; it does not reopen P13's completed text/JSON work.
Process log retention and selected log reading already exist under P02. The user
requested direct console visibility, not additional file-based logging.

**Acceptance scope:** Add dedicated @P17 public native CLI scenarios. Preserve
existing scenario tags, execution guarantees and the public build boundary for
architecture/policy verification.

- [x] Default text execution reports the active node and activity as it happens,
      including process activation before a long-running process finishes.
      A multi-node advance exposes its meaningful transitions in order rather
      than only the last activation. Identify the run/node sufficiently to make
      feedback understandable when reading captured console output.
- [x] Explain process outcomes and routing, agent handoff/waiting, accepted
      completion and terminal success/failure in concise English. Distinguish
      current activity from completed work and explain the next required caller
      action when execution stops for an agent or interruption.
- [x] Keep feedback truthful to execution and persistence: never imply that an
      unlaunched process ran, a rejected completion was accepted, or an
      uncommitted transition was durably saved. Inspection and repeated calls
      must not invent new execution or duplicate transitions as new work.
- [x] Keep start nonexecuting, complete limited to receipt/cursor changes, and
      advance synchronous. Preserve exit codes, limits, locking, completion
      idempotency, interruption and explicit recovery; console feedback must
      not introduce automatic advance, retry or replay.
- [x] Define text stdout/stderr placement and redirected-output behavior.
      Preserve readable final summaries, bounded output and control-character
      escaping. Do not flood the console with full process logs or periodic
      noise; raw stdout/stderr logs and their existing limits remain unchanged.
- [x] Explicit --json retains exactly one unchanged protocol-version-1 response
      on stdout, existing error/diagnostic behavior and no unsolicited progress
      text. Text feedback must not corrupt machine consumption or require
      agents to parse decorated JSON.
- [x] Update the contract's current no-progress rule, relevant decision, help,
      embedded guides and human examples consistently. Demonstrate what the
      reader sees during a process chain and an agent handoff. This is console
      feedback, not a new log-file subsystem or event-history API.
- [x] Verify live feedback through the published native CLI using a controlled
      long-running process: observe its activity message before releasing the
      process to finish. Cover ordered transitions, waiting, errors/interruption,
      redirected text, terminal/control characters and JSON compatibility.
      Required Gherkin steps must fail when undefined, ambiguous, pending or
      skipped. Retain existing gates and record actual platform limitations.

**Open implementation choices:** Exact wording, event granularity, stream
placement and the execution-to-presentation interface remain design work. No
spinner, color, terminal detection, dependency or new persisted event format is
prescribed. Choose a bounded, verifiable design that respects the existing
module boundaries and demonstrates live visibility.

**Out of scope:** File-based logging enhancements, full child-output streaming,
an interactive UI, workflow/state-machine redesign, P18's resource selection and
state discovery, or release qualification/publication.

## Implementation and verification — 2026-11-17

- ADR 0013 records a CLI-only persistence decorator. Each activity message follows
  a successful write; activation explicitly says launch is pending. Text activity
  uses flushed stderr, final summaries stdout; explicit JSON suppresses activity.
  No application/process/state-schema or dependency changes were required.
- Completion acceptance is announced after saving, never for rejection or an
  equivalent retry. Inspection and repeated waiting/terminal advance do not
  replay activity. Waiting/interrupted summaries explain explicit next actions.
- Thirteen @P17 public native CLI cases passed, including a barrier proving
  pre-completion visibility, ordered chains/handoffs, receipts, process failures,
  limits, real user interruption, control escaping/raw logs, JSON compatibility,
  failed persistence and a closed activity pipe. Rejected completion compares
  authoritative bytes. The initial native tracer failed before implementation.
- The final Windows x64 full gate passed 533 tests / 312 local feature cases and
  all 297 independent native runtime cases, with no failures/skips or build/publish
  warnings/errors. Architecture, dependency, distribution, privacy, budgets and
  Mermaid checks passed. Product size: 5.979 MiB; warm status P95: 25.03 ms.
- README process-chain and agent-task walkthroughs and embedded activity guidance
  were exercised against the native CLI. Contract, help, guides, README, status,
  testing/release documentation and the scenario inventory are consistent.
- Independent Standards and Spec reviews have no remaining findings. Standards
  follow-ups added rejected-completion byte comparison and consolidated test
  process launch setup. A readiness barrier prevents process-count races.
- Raw evidence remains under ignored artifacts, including p17-red-host.log,
  p17-focused.log, p17-examples.log and p17-final-gate.log. The initial restricted
  test host could not connect; local process permissions enabled the real tests.
  A competing incremental test build hit the active suite's file lock; final
  verification ran serially and passed. No Linux/macOS, hosted or clean-target
  execution is claimed. No commit, push, publication or deployment was performed.

## Local delivery follow-up — 2026-11-17

The user subsequently requested a local commit of P17. Include this completed
ticket and its implementation/evidence summaries; leave the independent P18
capture untouched. Push, publication and deployment remain unauthorized.
