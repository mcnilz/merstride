# Acceptance testing with Gherkin

Revision 0.5. The `.feature` files are the reviewable behavioral contract. The NUnit
runner executes P01-P07 local cases from these files against the published
native executable. The two clean-platform orchestration cases remain mandatory
separate release qualification and fail explicitly without actual evidence. See STATUS.md
for current evidence and limitations.

## Automation approach

Use English Gherkin (`Feature`, `Scenario`, `Scenario Outline`, `Given`, `When`, `Then`). Give every scenario stable requirement tags (`@A01` through `@A25`) and an owning implementation slice. Preserve tags when renaming prose.

For the proposed .NET implementation, use NUnit 4 as the test host and choose a permissively licensed, maintained Gherkin parser/binding approach during P01. Validate its current license and compatibility then. Development-only Gherkin dependencies do not ship in the product and need not support Native AOT. Do not select a framework by name without checking it. Rust or Go must preserve the same features and public CLI assertions if chosen.

The runner MUST execute scenarios from the actual feature files. Do not maintain unrelated tests and claim the prose is automated. Undefined, ambiguous, pending, or skipped required steps fail the relevant release gate. Scenario Outline rows execute independently. Emit machine-readable test results linked to feature, scenario, example row, requirement, and platform. CI shows not-yet-implemented scenarios as not implemented, never passed.

During incremental development, select implemented slices; report remaining scenarios separately. At final release, run every platform-applicable scenario. The release-environment scenario orchestrates this suite and is excluded from its own child invocation to avoid recursion.

## Test boundary and fixtures

The proposed seam is the public CLI: invoke the actual executable; assert JSON, exit codes, logs, process effects, and public status. Isolate each scenario in a temporary workspace and state directory. Build a small native test helper in the chosen language that can echo arguments, emit bytes on both streams, choose an exit code, create a marker, wait on an explicit barrier, and spawn an attached child. It is test-only.

The accepted seams are the public CLI and the public build boundary. P02's native
helper is `tests/Merstride.TestProcess`, published separately to `artifacts/fixture`.
It currently records effects/arguments, chooses exits, emits normal/raw/flood output,
checks closed stdin, and waits for timeout checks. P05 adds an attached parent/child
readiness barrier, synchronized concurrent stream writers, labelled streams and
Unicode/invalid-byte output. PowerShell 7 is required for its explicit invocation case.
Windows Job accounting asserts exact CLI plus declared child counts per call; start,
status, guide, validate, and logs still require no children.

Run `pwsh -NoProfile -File scripts/build.ps1` for the complete local gate. Results are
`artifacts/tests/p07.trx`, `artifacts/tests/scenarios.json`, and
`artifacts/benchmark.json`. Supplemental safety regressions do not mark later
feature scenarios implemented. Native helper/toolchain packages reuse the existing
reviewed dependency inventory and are never published into the product directory.

P03 adds actual agent-feature bindings and helper modes for a repairable check and
large context output. Supplemental CLI cases check strict JSON/UTF-8, binary64
receipt equivalence, frozen input, selected bounded tails, and basic context/data
limits. Full exact boundary qualification remains P05. Completion rejection tests
also compare authoritative state bytes, as required by the no-mutation contract.

Named Given steps construct complete minimal Markdown/YAML/Mermaid fixtures. Distinct end nodes in routing scenarios are observed through returned cursor/activation identity. Fixtures MUST not use production parser code to derive their expected answers. The test runner records child invocations externally; “no child process” assertions must not rely solely on an absent application log.

Crash tests synchronize through marker files/barriers, not arbitrary sleeps. Kill the engine separately from its helper, observe persisted public behavior, and clean up any orphan helpers in teardown. User interruption needs a platform-specific harness. Timeouts use an outer watchdog of 15 seconds for the one-second timeout fixture; document any platform adjustment. A scenario failing its watchdog fails rather than hangs the suite.

Depth counts the root JSON object as depth 1; each nested object/array increases depth by 1. Byte boundaries refer to compact serialized UTF-8 JSON. Required-size fixtures must calculate this explicitly. Tests for the input object use the same limits as completion data.

P05 runs all 14 expanded cases in 04-limits and supplemental CLI checks. The timeout
test observes both helpers alive through retained OS process handles before releasing
their native readiness barrier, then checks termination; Windows Job accounting
also requires exactly the CLI plus two descendants. Its outer watchdog remains
15 seconds for the one-second timeout. Output writers synchronize after each stream
has written a byte and then write concurrently; exact 10 MiB and 10 MiB + 1 cases
qualify the cap separately from the larger flood. Retained log lengths are observed
directly. Data fixtures independently count eight JSON framing bytes, UTF-8 payload
bytes, and root/nested object/array depth. Context fixtures use an explicit public
JSON envelope to exercise exactly 32768 and 32769 assembled bytes, without calling
production sizing helpers. Targeted log reads compare state/log bytes before/after
and retain external no-child accounting. None of these claims P06 crash recovery.

Before introducing an internal persistence fault hook, document and confirm that extra seam. Add fault scenarios for before process launch and after process exit/before commit when that seam exists; do not claim those precise crash windows are covered by a single uncontrolled kill test. The provided after-effect barrier scenario covers a real external ambiguity window already.

P06 executes all seven actual recovery scenarios. A two-process barrier chain tests
writer exclusion for advance, complete and resolve while a separate run completes.
Crash tests terminate only the engine after an external marker; the helper remains
alive until explicit cleanup. Read-only status compares authoritative bytes before
and after projection. Retry/fail calls require no new descendants according to
Windows Job accounting, and retry history retains the uncertain activation ID.
Truncated and structurally corrupt snapshots are refused by readers and writers.

The Windows signal test launches a native test-only host in a separate hidden
console, sends real CTRL_BREAK_EVENT, and requires exit 130 and both attached
helpers to terminate. Helpers explicitly ignore the signal themselves. Job evidence
counts the signal host, CLI, two helpers and one independently identified conhost.
The harness never broadcasts into the NUnit host's console. Each marker wait has
a 10-second watchdog; CLI operations retain their 15-second watchdog. Teardown
recovers private PID markers, verifies helper images, retains OS handles and kills
only task-owned leftovers after failure. No detached descendants are qualified.

A supplemental interruption check marks only its own authoritative state file
read-only while the real attached process tree is running. Ctrl+Break still exits
130 and returns interrupted JSON; replacement failure leaves executing bytes intact,
and fresh status projects interruption without rewriting them. Attributes are
restored in finally. This exercises a public filesystem refusal, not a private
fault hook or a physical disk failure guarantee.

Four independent native readers race a twelve-process barrier chain. Every query
must succeed with one JSON object and an explicit valid cursor/status/transition
combination; external Job accounting requires exactly all reader CLIs, the writer
and twelve helpers. Windows replacement sharing/name races have a bounded 100 ms
open retry; malformed JSON never retries or resets. This is local filesystem
evidence, not physical disk failure or precise internal fault-window qualification.

## Coverage map

| Requirement | Observable behavior | Feature file |
| --- | --- | --- |
| A01 | Workflow selection and Markdown fences | 01-authoring |
| A02 | Process chains, routing, stream separation | 02-processes |
| A03 | Agent repair loop | 03-agents |
| A04 | Context selection, availability, truncation | 03-agents, 04-limits |
| A05 | Invalid documents and graph limits | 01-authoring |
| A06 | Invalid completions and data boundaries | 03-agents, 04-limits |
| A07 | Idempotent completion | 03-agents |
| A08 | Conflicting completion | 03-agents |
| A09 | Resume across CLI processes and directories | 03-agents, 06-interface |
| A10 | Locks, independent runs, atomic reader visibility | 05-recovery |
| A11 | Crash, explicit recovery, interrupt, corrupt storage | 05-recovery |
| A12 | Time, transition, output, context limits | 04-limits |
| A13 | Literal arguments and explicit PowerShell | 02-processes |
| A14 | Frozen compiled workflow | 01-authoring |
| A15 | Native platform and performance gates | 06-interface |
| A16 | Read-only operations and start semantics | 02-processes, 04-limits, 06-interface |
| A17 | Request identity across loop iterations | 03-agents |
| A18 | Output cannot alter execution instructions | 02-processes |
| A19 | Constrained YAML and multiline instructions | 01-authoring |
| A20 | Offline agent guide | 06-interface |

## Release evidence

Record binary hash/version, OS, fixture version, scenario results, native publish output, license inventory, executable size, and timing distribution. Run core tests against the published binary on clean target machines. The test harness may run externally; the target product environment has no installed language runtime. PowerShell-specific scenarios require explicitly provisioned PowerShell and must report that prerequisite; all other core fixtures use native helpers.

A check of feature syntax is not a behavioral pass. A native publish on one OS does not prove another platform. Benchmark and token-benefit measurements are separate from deterministic assertions.

## Architecture and distribution gates

A21 maps to 07-architecture: positive/negative compilation fixtures and effective enforcement, owned by P01. A22 maps to the same feature: MIT release and build-only analyzer separation, owned by P07. These `@build` checks use the public compiler/build/publish boundary and are separate from the runtime CLI suite. The clean-runtime platform suite excludes build-only checks; the overall release gate requires both suites.

A23: official Actions, SHA pins, and PR isolation (P01). A24: established dependency review (P01). A25: tested-tag release integrity and publication gates (P07). These are in 08-delivery.feature. Policy checks combine automated allowlist/lock/reference checks with a version-controlled reviewed dependency inventory; maintenance maturity cannot be inferred automatically from a package name.

M01 preserves all 61 P01–P04 feature cases and adds three public MSBuild property
checks for effective SDK 11.0.100, net11.0, stable C# 15 and no memory-safety opt-in
across product, test harness and native helper. See STATUS.md for the 165-test gate
and migration evidence. The global language skill is not part of the test inputs.
