# P19: Select runs by workflow context, shorten IDs and manage obsolete state

**What to build:** Let a person start a new run from a workflow file and continue
the intended run without managing the state root or repeatedly copying IDs, even
when other runs exist. Make exceptional explicit selection practical with
user-facing run/request IDs of about eight characters, and provide a CLI route
for removing obsolete run state under an explicitly agreed retention policy.

**Blocked by:** No unfinished ticket. P18 is complete. The selection, identity and
retention decisions below must be resolved before implementation is assigned.

**Status:** ready-for-human

**Implementation status:** complete; coordinator accepted on 2026-11-17.

**Original capture boundary (superseded by the assignment below):** Authorized local issue breakdown on
2026-11-17 only. Implementation is not assigned or approved. No local commit,
push, publication or deployment is authorized by this capture. Preserve earlier
tickets and their history.

## Implementation assignment — 2026-11-17

The user subsequently approved P19–P22 sequentially in separate chats and delegated
routine design choices. This chat owns P19 only. Prepare implementation, native
tests and two-axis review for coordinator acceptance. Do not commit until the
coordinator requests it; no push, publish or deploy. Capture-time restrictions
below remain history and do not override this later assignment.

Decisions are in ADR 0015: remembered per-root and per-source/workflow context,
explicit --file/use overrides, canonical eight-character r_/q_ IDs with durable
nonreuse reservations, and deliberate single-run discard with no automatic deletion.
Completion requires a stable request ID and locates its original run independently
of changing current context. Exact stored IDs precede legacy prefix convenience.
Legacy full IDs/snapshots remain usable; new IDs are opaque protocol-1 identities,
and older binaries must not operate on newly adopted roots.

The coordinator subsequently relayed direct user approval for positional source
selection: status workflow.md and advance workflow.md. It now applies consistently
to complete/resolve/logs and the runs inventory filter. Bare r_ tokens are IDs;
other tokens are paths, with ./ or --file as explicit filename escapes. Human
examples lead with positional Markdown paths; omitted selection still uses context.

## Problem and prior context

The user tested the CLI with several runs in the same state root. Ordinary
continuation becomes cumbersome because an omitted run ID stops working, and
the user must repeatedly select a long ID or inspect/manage stored directories.
Runs from other workflow files should not make the ordinary path difficult.
Starting from a workflow file should create a new run; continuation should use
available workflow/current-run context to select that run unambiguously.

The user reports requesting shorter IDs in the last two ticket discussions and
still seeing very long generated IDs. The two most recent tickets were reviewed:
[P17](17-live-console-activity.md) covers console activity and explicitly leaves
state selection to [P18](18-simpler-project-workflow.md). P18 records long-ID
friction, but intentionally leaves exact ID length open; its delivered solution
keeps 34-character full IDs and adds ten-character suggestions (a two-character
type prefix plus eight hex digits). It omits the run ID only when exactly one run
directory exists, including terminal/corrupt/incomplete runs, and still requires
request selection. Neither ticket history should be rewritten to imply that
eight-character generated IDs or a current-run policy was already delivered.
[P16](16-bash-first-documentation.md) previously removed JSON extraction from
human examples but still required pasting IDs.

The contract already makes every successful start create a new, nonexecuting run.
This ticket must preserve and demonstrate that behavior with existing runs,
rather than confuse starting a new run with resuming one. ADR 0014 intentionally
has no current-run pointer, implicit latest selection, migration or deletion;
contextual selection and cleanup therefore need an explicit follow-up decision.

## Acceptance criteria

**Design direction:** Human and AI usability is the goal. Existing command
structure, state-selection concepts, ID presentation and output layout are not
constraints by default. Propose replacements when they produce a substantially
better experience. Existing contract/features and ADRs describe the baseline;
future approved implementation must deliberately update them when the design
changes, rather than silently violate them. Identify material compatibility and
migration decisions; this issue imposes no blanket backward-compatibility rule
and authorizes no implementation now.

- [x] Starting from a workflow file creates a distinct ready run when the state
      root already contains runs of that workflow and of other workflow files,
      including completed/failed runs. It neither resumes an old run nor executes
      a node. Retention effects follow only the separately agreed policy below.
- [x] Demonstrate a readable start, status, advance, agent handoff, completion
      and final-advance path across separate CLI invocations with multiple stored
      runs. Appropriate workflow/current-run context selects the intended run
      without routine run-ID copying, JSON extraction or filesystem management.
      Define request handling as part of this path; explicit IDs should be rare,
      with safe explicit targeting still available for exceptional cases.
- [x] Define selection precedence, context scope and lifecycle, including two
      runs of the same workflow, different workflow files, commands from project
      subdirectories, explicit state roots, terminal runs, stale/missing context
      and simultaneous callers. Missing or conflicting context gives actionable
      guidance without changing a different run. Do not replace ambiguity with an
      undocumented global newest-run heuristic.
- [x] Present and accept practical user-facing run/request IDs of about eight
      characters in ordinary output and commands. Merely retaining prominent
      long IDs plus today's optional prefix suggestions does not meet this goal.
      Record exact length including any type prefix, uniqueness scope and
      collision handling; forced collisions must not overwrite or retarget a
      resource. Document any exceptional lengthening and decide the treatment
      of previously saved full IDs.
- [x] Keep request identity and receipt idempotency safe across loops, delayed
      callers, retries and changing current-run context. Historical completion
      must never complete a newer request. Define whether any context-only
      completion is safe before allowing it; stable explicit targeting remains
      available to AI callers and automation.
- [x] Provide a documented CLI way to discard obsolete state: an explicit
      cleanup operation, a start-time discard option, or an agreed combination.
      A user can identify the affected workflow/runs and removal result without
      manipulating directories. Apply the agreed retention scope, protect runs
      outside that scope, and define behavior for active/locked runs, corrupt
      state, stale context and cleanup failure. Never treat directory naming as
      permission to delete data or follow arbitrary paths outside the state root.
- [x] Record compatibility for saved full IDs, existing state, explicit roots,
      short selectors and protocol-1 JSON callers. Preserve explicit execution,
      locking, independent runs, interruption/recovery and offline Native AOT
      operation. Legacy commands, selectors and state formats may be replaced
      under the explicit compatibility/migration decision. Update the contract,
      ADR, help, embedded guide and Bash human walkthrough consistently;
      any incompatible change needs an explicit
      compatibility decision rather than a silent protocol/schema change.
- [x] Add dedicated @P19 scenarios through the published native CLI covering
      the multiple-workflow path, repeated starts, same-workflow ambiguity,
      context lifecycle, explicit overrides, forced ID collisions, request
      history, cleanup isolation/concurrency/failure and the chosen migration
      or compatibility behavior. Preserve existing tags and public-build policy
      checks; undefined,
      ambiguous, pending or skipped required steps fail. Record actual evidence
      and platform limitations without claiming release readiness.

## Decisions required before implementation

1. **Workflow identity:** How are runs associated with a source file and selected
   workflow within it? Decide path normalization, identical workflow IDs in
   different files, multiple workflows per file, changed/renamed/missing source
   files and frozen definitions. A source hash or workflow ID alone may not
   establish the intended source-file relationship.
2. **Current-run context:** Choose how context is supplied/remembered, its
   project/workflow/session scope, precedence relative to explicit selectors,
   switching behavior and concurrent-caller semantics. Do not prescribe a
   persisted pointer or a particular command/flag syntax before this decision.
3. **Short identity:** Choose shortened canonical generated IDs or stable public
   handles over full internal IDs, their alphabet and about-eight-character
   target, collision detection/retry or disambiguation, uniqueness domain and
   reuse rules after deletion. Decide which other displayed IDs, such as
   activation selectors for logs, need equivalent treatment.
4. **Retention default:** The user has not selected between retaining runs until
   explicit cleanup/discard and deleting old runs on start with an explicit keep
   option. Both are proposals; neither is an approved default.
5. **Cleanup scope:** Define obsolete/old, eligible statuses, whether one selected
   run or earlier runs of the same workflow are affected, and how retention maps
   to workflow identity. Deleting unrelated workflow states is not a requirement
   or authorized default. Any broader scope needs a separate explicit decision.
   Choose cleanup command versus start option (or both), safeguards, failure
   ordering and effects on associated logs, receipts and selection context.

**Out of scope:** P20's transition/result presentation, P21's optional explicit
combined start/advance, P22's full scenario audit, automatic replay or unrequested
advance, workflow-language redesign, remote storage, release qualification or
any implementation during issue capture.

## Verification and review — 2026-11-17

- Final unchanged-source sequential Windows x64 gate passed: 602 tests, 381 local
  feature cases and all 366 independent native runtime cases; zero failures/skips.
  All 43 P19 cases passed. Architecture, dependency, distribution, privacy, native
  budgets and Mermaid checks passed, with no compiler/linker warnings or errors.
  Size: 6.240 MiB; warm status P95: 23.850 ms.
- The final focused check passed 103 cases. Actual README and CLI-guide Bash blocks passed
  with earlier terminal state, positional workflow selection, readable completion
  and scoped cleanup. Raw reports/logs stay under ignored artifacts.
- Standards: no remaining actionable findings. Fixed null/duplicate-context
  recovery, inventory under staging conflicts, and the AtomicWrite name.
- Spec: no remaining actionable findings. Fixed exact-ID prefix precedence,
  final-stage discard recovery with minimal ownership evidence, staged file/link
  refusal and help handling for missing/conflicting operational arguments.
- P12's adjacent filesystem expectation includes new bookkeeping while still
  refusing script files. Its test-only interpreter invocation explicitly decodes
  UTF-8 stdin, eliminating inherited Windows console code-page dependence.
  Product process transport/encoding is unchanged.
- Earlier exploratory gates exposed stale filesystem expectations, missing-help
  retrieval, evolving scenario hashes and overlapping Windows build-file locks.
  The final sequential gate includes all fixes and the approved positional syntax.
- No dependency/lock/license changes. No Linux/macOS, hosted or independent
  clean-target execution, release-readiness claim, local commit, push, publication
  or deployment. P20–P22 were not changed. Stop for coordinator acceptance.

### Exact changed paths

- `.scratch/merstride/issues/19-contextual-run-selection.md`
- `README.md`
- `docs/AGENT-GUIDE.md`
- `docs/CLI.md`
- `docs/CONTRACT.md`
- `docs/STATUS.md`
- `docs/TESTING.md`
- `docs/adr/0015-p19.md`
- `docs/release-scenario-inventory.json`
- `features/18-project-workflow.feature`
- `features/19-contextual-selection.feature`
- `src/Merstride/Application/WorkflowEngine.cs`
- `src/Merstride/Cli/CliOutput.cs`
- `src/Merstride/Cli/CommandLine.cs`
- `src/Merstride/Cli/CreationGuide.txt`
- `src/Merstride/Cli/Program.cs`
- `src/Merstride/Cli/ResourceSelection.cs`
- `src/Merstride/Domain/Models.cs`
- `src/Merstride/Domain/ResourceIds.cs`
- `src/Merstride/Infrastructure/DiscardJournal.cs`
- `src/Merstride/Infrastructure/IdentityAllocator.cs`
- `src/Merstride/Infrastructure/JsonContext.cs`
- `src/Merstride/Infrastructure/RunCatalog.cs`
- `src/Merstride/Infrastructure/RunDisposal.cs`
- `src/Merstride/Infrastructure/RunStore.cs`
- `tests/Merstride.Acceptance/ContextSafetyWorld.cs`
- `tests/Merstride.Acceptance/ContextWorld.cs`
- `tests/Merstride.Acceptance/FeatureTests.cs`
- `tests/Merstride.Acceptance/InlineScriptWorld.cs`
- `tests/Merstride.Acceptance/ProjectWorld.cs`
- `tests/Merstride.Acceptance/ReadableOutputWorld.cs`

## Coordinator acceptance and local delivery — 2026-11-17

The coordinator accepted P19 after independently inspecting positional selectors,
exact short-ID targeting, the catalog/allocator, staged cleanup and recovery,
the issue/evidence and changed paths, and diff whitespace. The required native
gate and both review axes are green. Implementation is complete.

The coordinator explicitly authorized one local commit containing exactly the
31 scoped paths above, including this issue and excluding the unrelated untracked
P20/P21/P22 issues. This documentation-only acceptance update changes no product
behavior; the unchanged-source suite is not repeated. Repository privacy and
whitespace checks remain required immediately before committing. No push,
publication or deployment is authorized or performed.

The final staged whitespace check found one surplus blank line at the feature
file's end. Removing it changed no scenario or product behavior; its inventory
hash was refreshed. All 43 P19 native CLI cases passed again without failures or
skips. The full unchanged-behavior gate above remains the implementation evidence.
