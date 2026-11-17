# P22: Verify human and AI usability across every existing scenario

**What to build:** Review and secure the existing scenario set for both people
using readable default output and AI consumers using the established structured
CLI integration. Produce traceable coverage and regression evidence for the
whole scenario set, including failures and exceptional paths.

**Blocked by:** P19 — Select runs by workflow context, shorten IDs and manage
obsolete state; P20 — Make flow progression and results clear in console output;
P21 — Optionally start and advance a new run in one CLI invocation.
Their final behavior and examples must exist before the audit can close.

**Status:** complete; coordinator accepted

**Approval and delivery boundary:** Authorized local issue breakdown on
2026-11-17 only. Implementation is not assigned or approved. No local commit,
push, publication or deployment is authorized by this capture.

**Approval update (2026-11-17):** The user explicitly approved implementation of
P19–P22 in separate chats, with coordinator acceptance and a local commit for
each. P19 (`35cec16`), P20 (`e23d203`) and P21 (`910b7e7`) are accepted and
committed. P22 is assigned for the full audit, regression coverage, guidance and
local evidence. This supersedes the capture-only restriction above and in the
original design direction below; that text is retained as history. Leave changes
uncommitted for coordinator acceptance and a subsequent commit instruction.
No push, publication or deployment is authorized.

**Coordinator acceptance (2026-11-17):** P22 is accepted after independent
inspection of the audit overlay/public gate, primary operation/mode/variant
evidence, controlled writers, issue history and raw results. The 448/445/430/3
totals and digests reconcile. The failed-mutation retry, direct-writer and discard
help evidence-integrity concerns are resolved, including the eight explicitly
justified malformed-format cases. The coordinator authorizes exactly one scoped
local commit after final privacy, whitespace and staging checks. This supersedes
the earlier instruction to leave changes uncommitted. No further source, test,
feature or inventory changes and no repeat of the unchanged-source full gate.
No push, publication or deployment is authorized.

## Problem and prior context

The user explicitly requests reviewing and securing ALL existing scenarios for
usability for humans and AI consumers. A successful quick start or a single JSON
smoke test cannot establish that. P08 covers CLI parsing/discovery, P13 readable
output, P17 activity and P18 project selection; P19 and P20 own the newly requested
behavior. P21 adds optional combined start/advance. This ticket owns comprehensive
review, coverage gaps and verification, without implementing those changes a
second time.

The feature files and normative contract are the source of truth. Existing
testing rules distinguish published-native-CLI behavior from architecture/policy
checks at the public build boundary and separate release-platform qualification.
Keep those distinctions while accounting for every scenario.

## Acceptance criteria

**Design direction:** Evaluate actual human and AI usability rather than fidelity
to old commands, selection concepts, ID display or output layout. P19/P20/P21 may
replace them through approved design. Verify the deliberately updated normative
contract/features and migration decisions, not accidental compatibility with all
legacy behavior. There is no blanket backward-compatibility requirement. This
issue authorizes review/design capture only, not implementation.

- [x] Establish a reproducible review inventory of every existing feature
      scenario and each expanded Scenario Outline row, preserving requirement
      tags and owning ticket. Record the reviewed source revision and include
      P19/P20/P21 additions in the final inventory. Reconcile coverage with the
      established scenario/release inventory so omissions are detectable.
- [x] For each scenario, record the human-use assessment, AI integration
      assessment, applicable execution boundary, evidence and disposition. A
      nonapplicable human/AI dimension needs a concrete reason, not an omitted
      row. Build-only and platform/release cases remain accounted for at their
      real boundaries; do not invent a human CLI path or claim unrun platforms.
- [x] Assess every applicable CLI scenario in readable default mode: commands
      are discoverable and practical; selection/context is clear; progression,
      result, errors and next actions are understandable; and normal use needs
      no JSON tools, long-ID copying or manual state-directory management.
      Exceptional paths retain safe explicit targeting and explain ambiguity.
- [x] Assess every applicable CLI scenario for AI consumers: explicit stable
      resource/request targeting, the agreed versioned structured response,
      exactly one response, documented stdout/stderr and exit semantics,
      actionable error codes, bounded context/log information, receipt idempotency and safe
      retries. An AI agent may read ordinary text, but automated integration
      must not depend on parsing human presentation. Existing explicit
      --json/protocol 1 is the baseline, with any deliberate replacement,
      versioning and migration decisions verified.
- [x] Cover discovery/help/authoring/validation; process chains, variants and
      scripts; branch/default routing and loops; handoffs/context/completion;
      rejection/conflicts/history; time/output/transition limits; concurrency,
      crashes/interruption/recovery; read-only status/logs; multiple workflows,
      context selection, short-ID collisions, cleanup/retention, optional combined
      start/advance and retained nonexecuting start; and malformed,
      missing or corrupt inputs/state. Do not replace negative/concurrent cases
      with a single happy-path walkthrough or count JSON-only checks as proof
      of readable text usability.
- [x] Close gaps in public-boundary assertions and user guidance, reusing
      existing fixtures where useful and adding @P22 scenarios only when needed.
      Each applicable mode must exercise the actual native executable with
      scenario-relevant semantic and effect assertions; golden text alone is
      insufficient. Preserve existing tags and independent example-row results.
      Required undefined, ambiguous, pending or skipped steps fail the gate.
- [x] Record findings with reproducible cases, affected scenario/mode, severity
      and resolution evidence. Product defects in P19/P20/P21 stay with their
      owning scope; newly discovered behavior changes require explicit follow-up scope.
      The audit is not complete while a required usability failure is merely
      listed as unverified or silently waived. Report blockers honestly.
- [x] Execute the complete applicable local verification and representative
      multi-step Bash human and structured AI walkthroughs, including failure
      and recovery. Version a concise coverage/results summary with actual
      limitations; keep raw transcripts/logs under ignored artifacts. Retain
      public-build policy checks, dependency/privacy inventories and separate
      release qualification. No unsupported platform or release-readiness claim.

**Open decisions:** P19's unresolved selection/identity/retention policies block
the final expectations for those cases. Decide the smallest maintainable form
for the review matrix and evidence links; do not duplicate or replace the native
scenario inventory with an unrelated checklist. Any additional product scope
found by the audit needs its own explicit disposition before assignment.

**Out of scope:** Reimplementing P19/P20/P21, unsolicited protocol redesign or
replacement of the public test boundary, relaxing failed scenarios to pass,
simulated platform qualification, unrelated
refactoring, remote publication or deployment.

## Delivery evidence — 2026-11-17

- Reviewed accepted baseline `910b7e7` plus the P22 verification changes.
  `docs/USABILITY.md` records findings U01–U05, resolutions and practical guidance;
  `docs/usability-scenario-review.tsv` accounts for all 448 expanded rows with
  original tags/owners, boundary, both assessments and declared action variants.
- Inventory: 138 declarations (92 ordinary; 46 outlines / 356 example rows),
  21 feature hashes; 445 local cases = 430 native + 15 public-build cases.
  Three recursive external release-environment rows remain separate and mandatory.
  The 428 CLI rows include 420 requiring both primary modes and eight explicit
  malformed-format protocol cases with concrete recorded reasons.
- Full settled Windows x64 build passed 682 tests and all 430 independent native
  runtime cases, zero failures/skips and no build/publish warnings/errors.
  Architecture, dependency, distribution, privacy, matrix/mode reconciliation,
  budgets and Mermaid passed. Size 6.284 MiB; warm status P95 21.114 ms.
  Source-input digest:
  `6ba74b9e797164f7f17b1612239dd720261541096954ef917e8157d2d4f2904f`.
- Sixteen public audit controls cover incomplete/duplicate/misclassified review,
  missing modes/commands, observation substitution and wrong operation variants.
  Twelve new @P22 management-help rows failed on undefined required bindings
  before native bindings were added, then passed in both execution hosts.
- Actual Bash human and explicit JSON/jq walkthroughs passed against the gate
  binary: discovery, nonexecuting/combined start, path selection, handoff/rejection,
  loop and historical/conflicting receipts, deliberate cleanup, retained unrelated
  state, real after-effect engine loss and explicit retry/fail recovery. Human
  commands use no JSON tools; marker counts and exits prove effects/no replay.
- Raw evidence stays in ignored `artifacts/`: `p22-final-build.log`,
  `tests/p07.trx`, `tests/scenarios.json`, `tests/native-scenarios.json`,
  `tests/usability-audit.json`, local gate/benchmark records and the two Bash
  transcripts under `p22-walkthrough-1794948764/`.
- Independent Standards and Spec source/matrix reviews found no remaining
  actionable findings. Both closing result/prose reviews also found no material
  mismatch: counts, digests, metrics and actual walkthrough claims match the raw
  evidence, and qualification/privacy limits remain explicit.
- Source/tests/features/inventory stayed fixed throughout the final gate and
  walkthroughs. No product behavior or dependency/license change. No Linux/macOS,
  hosted/clean-target, physical-disk or narrow pre-advance signal-window
  qualification, token-saving percentage or release-readiness claim.
- The coordinator accepted the complete scope and authorized one local commit
  after final evidence edits and privacy/whitespace/staging checks. Historical
  capture-only and pre-acceptance restrictions are superseded by the dated
  approval and acceptance updates above. No push, publication or deployment.
