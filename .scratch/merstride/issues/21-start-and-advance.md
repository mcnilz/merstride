# P21: Optionally start and advance a new run in one CLI invocation

**What to build:** Offer an explicit optional way to create a new run from a
workflow file and immediately perform its first advance within the same CLI
invocation. Save the extra agent/tool/API call and its token/context overhead
while retaining ordinary start as a nonexecuting operation.

**Blocked by:** None. Existing start and advance behavior supplies the baseline.
P19's contextual selection and P20's presentation improvements are independent;
the combined operation must target its newly created run directly and compose
with those tickets when complete.

**Status:** complete; coordinator accepted on 2026-11-17

**Approval and delivery boundary:** Authorized local issue breakdown on
2026-11-17 only. Implementation is not assigned or approved. No local commit,
push, publication or deployment is authorized by this capture.

**Subsequent implementation authorization (2026-11-17):** The user approved
sequential P19–P22 implementation, one chat per ticket. P19 and P20 are accepted
and committed as 35cec16 and e23d203. This session is assigned only P21. The
coordinator accepted the reviewable implementation on 2026-11-17 and authorized
exactly one local P21 commit after final evidence, privacy and scoped staging
checks. No push/publication/deployment or P22 work is authorized.
This later assignment supersedes the original capture-only restriction above
and the capture-only wording in the original acceptance criteria below.

## Implementation history

- 2026-11-17: Read the actual issue and normative/project guidance. Selected
  explicit `start <file> --advance`, retaining ordinary nonexecuting start.
  Native CLI is the agreed behavior seam; architecture/policy uses public build.
- First native red/green proves a two-process chain's actual effects and final
  status match separate start/advance, with the old binary rejecting the flag.
  A later text red/green proves distinct creation and initial-advance summaries.
- Shared the existing synchronous advance adapter, passing the exact new ID under
  its run lock after releasing the root selection lock. ADR 0017 records phase,
  cancellation and additive protocol-1 response choices. No state schema,
  process runner, engine, dependency/license or retention changes.
- Focused P21 checks pass 31 native CLI scenarios, including equivalent
  chains/handoffs/end results and limits, malformed/preparation/persistence errors,
  request-lock/result-save failure, same/different concurrent starts and actual
  supported interruption with explicit retry/fail.
- 2026-11-17: Final full Windows x64 gate passed the settled source: 654 tests /
  433 local feature cases / 418 independent native runtime cases, zero failures
  or skips and no build/publish warnings/errors. All 31 P21 cases pass in both
  hosts. Architecture/dependency/distribution/privacy, budgets and Mermaid pass.
  Native size 6.284 MiB; warm status P95 20.997 ms. The three separate recursive
  release-environment orchestrators remain outstanding, not skipped local tests.
- Actual README/CLI human Bash blocks, optional combined start, explicit JSON
  Bash integration, separate/combined review examples and embedded help/guides
  passed using the exact gate binary. Completion returns ready before explicit
  advance. Host restrictions and walkthrough-only newline assertions were
  corrected; product/feature/inventory source stayed fixed throughout the gate
  and subsequent walkthroughs. Only evidence/history documentation changed later.
- Independent Spec review: no findings. Independent Standards review: no hard
  standard violations; one nonblocking duplicated CLI exception-map judgment.
  The coordinator explicitly chose to retain that small duplication, without
  discretionary cleanup. No correctness issue or hard breach was waived.
- Raw focused TRX, full-gate/native reports and walkthrough evidence remain under
  ignored artifacts. No Linux/macOS/hosted/clean-target execution, precisely timed
  creation-window interruption, physical disk failure qualification, measured
  token savings or release readiness is claimed. P22 is untouched.
- 2026-11-17: Coordinator accepted P21 after independently reviewing StartCommand,
  RunExecution, phase/cancellation behavior, exact-run lock/selection boundaries,
  ADR 0017, issue evidence and diff whitespace. The nonexecuting default,
  one-response combined mode, durable-error identity and explicit recovery match
  the request. The precisely timed signal-window limitation remains explicit.
  Exactly one scoped local commit is authorized after final privacy/staging
  checks. The passing behavior full gate need not be repeated for acceptance edits.
- Final staged whitespace verification found one extra blank line at the new
  feature file's EOF, which was previously untracked. Removed that blank line
  and refreshed only its inventory hash. Scenario identities/definitions,
  production/test code and the verified native binary are unchanged. Final
  privacy and staged whitespace checks include all 26 P21 paths.

## Exact changed paths

- `.scratch/merstride/issues/21-start-and-advance.md`
- `README.md`
- `docs/AGENT-GUIDE.md`
- `docs/CLI.md`
- `docs/CONTRACT.md`
- `docs/SPEC.md`
- `docs/START-AND-ADVANCE.md`
- `docs/STATUS.md`
- `docs/TESTING.md`
- `docs/adr/0017-p21.md`
- `docs/release-scenario-inventory.json`
- `examples/process-chain.md`
- `features/21-start-and-advance.feature`
- `src/Merstride/Cli/CliOutput.cs`
- `src/Merstride/Cli/CommandLine.cs`
- `src/Merstride/Cli/CreationGuide.txt`
- `src/Merstride/Cli/Program.cs`
- `src/Merstride/Cli/RunExecution.cs`
- `src/Merstride/Cli/StartCommand.cs`
- `src/Merstride/Infrastructure/JsonContext.cs`
- `tests/Merstride.Acceptance/FeatureTests.cs`
- `tests/Merstride.Acceptance/RecoveryWorld.cs`
- `tests/Merstride.Acceptance/StartAdvanceSafetyWorld.cs`
- `tests/Merstride.Acceptance/StartAdvanceWorld.cs`
- `tests/Merstride.Acceptance/WindowsInterrupt.cs`
- `tests/Merstride.TestProcess/Program.cs`

The unrelated P22 issue remains unmodified/untracked. Implementation is complete
and accepted; only the single authorized local P21 commit remains. No hard blocker,
push, publication, deployment or subsequent ticket work is authorized here.

## Problem and existing semantics

The minimum useful progression currently needs start followed by advance, which
costs an additional invocation for a person or an agent. The user explicitly
requests an optional combined operation, not a change to the default.

The contract defines start as validation, frozen-definition/input preparation
and durable creation of a ready run without node execution. Advance is
synchronous and can traverse multiple nodes until waiting_agent, terminal
state or error. "First advance" means one call with those existing semantics,
not only one node/transition, a background job or repeated automatic advancing.
ADRs 0013/0014 establish truthful activity, explicit execution and independent
run targeting; the combined operation must retain those guarantees.

## Acceptance criteria

**Design direction:** Choose the best human and AI experience, including a new
command structure if useful. Existing CLI concepts and layouts are not fixed
constraints. Treat normative contract/features and ADRs as the baseline for
deliberate updates, and record material compatibility/migration choices instead
of assuming blanket backward compatibility. The explicit user requirement to
retain nonexecuting start and make combined execution optional remains binding.
This capture permits design proposals, not implementation.

- [x] Expose an explicit opt-in combined start/first-advance operation in one
      native CLI invocation. Ordinary start remains available, ready and
      nonexecuting; the combined mode does not become the default. Choose and
      document command/option syntax without prescribing a flag in this issue.
- [x] With equivalent workflow selection, workspace, state root, input and
      limits, the combined path has the same persisted run, execution effects,
      stopping conditions and final status as successful start followed by one
      ordinary advance of that new run, apart from fresh IDs/timestamps. It may
      traverse a process chain and stop at an agent handoff or terminal result.
      Complete still does not execute the next node.
- [x] Always advance exactly the run just created, even when other runs of the
      same or different workflows exist or concurrent starts occur. Do not
      rediscover an implicit current/latest run between the two phases. Apply
      P19's eventual retention/context rules consistently without introducing
      another deletion policy here.
- [x] Invalid arguments, invalid workflow/input, failed preparation or failed
      initial persistence do not advance any run or launch workflow processes.
      Once creation succeeds, advance failures leave the newly created run and
      truthful durable progress available for status and existing explicit
      recovery; do not roll back external effects, erase the run, retry or replay.
      Identify the created run even when its first advance fails.
- [x] Preserve advance's locking, limits, cancellation, interruption and error/
      exit-code behavior, including success/waiting, workflow failure, state
      conflict, I/O error and user interruption. Define interruption between
      creation and advance and partial-success reporting. Do not promise an
      atomic transaction spanning state creation and external effects.
- [x] Readable output clearly distinguishes new-run creation, initial
      progression and the resulting waiting/terminal/error state and next action,
      using the established live activity and P20 presentation when available.
      Structured mode returns one coherent result with the created run identity
      and final status/error, without two concatenated
      responses or unsolicited text. Use explicit --json/protocol 1 as the
      existing baseline; decide and document additional fields, any incompatible
      changes/versioning and migration for separate and combined operations.
- [x] Update contract, ADR, help, embedded guide and Bash human/explicit JSON
      integration examples together. Show both nonexecuting start and combined
      use, explaining execution consequences. Demonstrate the one-invocation
      saving; a caller-side shell wrapper making two CLI calls does not qualify.
- [x] Add @P21 public native CLI scenarios comparing separate and combined paths
      for process chains, immediate agent handoff, terminal success/failure,
      invalid input/preparation, advance failure, cancellation/recovery,
      multiple/concurrent runs, text and JSON. Assert state/process effects and
      safe targeting, not only output. Preserve existing tags and public-build
      checks; undefined, ambiguous, pending or skipped required steps fail.
      Record actual evidence and limitations without claiming release readiness.

**Open design choices:** Exact syntax; the combined structured response
shape; phase-specific diagnostics/exit mapping where creation only partially
succeeds; cancellation handling between durable creation and advance. Preserve
the agreed optional execution boundary in every choice.

**Out of scope:** Changing start's default, automatically advancing after complete,
background execution, automatic retries/replay, a new retention policy, P19's
selection/ID design, P20's overall output redesign, P22's full audit or release
publication.
