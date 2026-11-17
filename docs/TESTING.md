# Acceptance testing with Gherkin

The `.feature` files are the reviewable behavioral contract. The NUnit
runner executes local cases from these files against the published
native executable. The three clean-platform orchestration cases remain mandatory
separate release qualification and fail explicitly without actual evidence. See STATUS.md
for current evidence and limitations.

## Automation approach

Use English Gherkin (`Feature`, `Scenario`, `Scenario Outline`, `Given`, `When`, `Then`). Give every scenario stable requirement tags (`@A01` through `@A25`) and an owning implementation slice. Preserve tags when renaming prose.

NUnit 4 hosts explicit bindings over the official Cucumber Gherkin parser.
Development-only test dependencies are inventoried and do not ship with the product.

The runner MUST execute scenarios from the actual feature files. Do not maintain unrelated tests and claim the prose is automated. Undefined, ambiguous, pending, or skipped required steps fail the relevant release gate. Scenario Outline rows execute independently. Emit machine-readable test results linked to feature, scenario, example row, requirement, and platform. CI shows not-yet-implemented scenarios as not implemented, never passed.

When adding behavior, report remaining scenarios separately from executed cases. At final release, run every platform-applicable scenario. The release-environment scenario orchestrates this suite and is excluded from its own child invocation to avoid recursion.

## Test boundary and fixtures

The accepted behavioral seam is the public CLI: invoke the actual executable; assert JSON, exit codes, logs, process effects, and public status. Isolate each scenario in a temporary workspace and state directory. The separately published native test helper can echo arguments, emit bytes on both streams, choose an exit code, create a marker, wait on an explicit barrier, and spawn an attached child. It is test-only.

Architecture and policy checks use the public build boundary. The native
helper is `tests/Merstride.TestProcess`, published separately to `artifacts/fixture`.
It records effects/arguments, chooses exits, emits normal/raw/flood output,
checks closed stdin, and waits for timeout checks. It also supports an attached parent/child
readiness barrier, synchronized concurrent stream writers, labelled streams and
Unicode/invalid-byte output. PowerShell 7 is required for its explicit invocation case.
Windows Job accounting asserts exact CLI plus declared child counts per call; start,
status, guide, creation-guide, validate, and logs still require no children.

Run `pwsh -NoProfile -File scripts/build.ps1` for the complete local gate. Results are
`artifacts/tests/p07.trx`, `artifacts/tests/scenarios.json`, and
`artifacts/benchmark.json`. Native helper/toolchain packages reuse the existing
reviewed dependency inventory and are never published into the product directory.

Agent bindings use helper modes for a repairable check and large context output. Supplemental CLI cases check strict JSON/UTF-8, binary64
receipt equivalence, frozen input, selected bounded tails, and basic context/data
limits. Completion rejection tests
also compare authoritative state bytes, as required by the no-mutation contract.

Named Given steps construct complete minimal Markdown/YAML/Mermaid fixtures. Distinct end nodes in routing scenarios are observed through returned cursor/activation identity. Fixtures MUST not use production parser code to derive their expected answers. The test runner records child invocations externally; “no child process” assertions must not rely solely on an absent application log.

Crash tests synchronize through marker files/barriers, not arbitrary sleeps. Kill the engine separately from its helper, observe persisted public behavior, and clean up any orphan helpers in teardown. User interruption needs a platform-specific harness. Timeouts use an outer watchdog of 15 seconds for the one-second timeout fixture; document any platform adjustment. A scenario failing its watchdog fails rather than hangs the suite.

Depth counts the root JSON object as depth 1; each nested object/array increases depth by 1. Byte boundaries refer to compact serialized UTF-8 JSON. Required-size fixtures must calculate this explicitly. Tests for the input object use the same limits as completion data.

The limit suite runs all 14 expanded cases in 04-limits and supplemental CLI checks. The timeout
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
and retain external no-child accounting.

Before introducing an internal persistence fault hook, document and confirm that extra seam. Add fault scenarios for before process launch and after process exit/before commit when that seam exists; do not claim those precise crash windows are covered by a single uncontrolled kill test. The provided after-effect barrier scenario covers a real external ambiguity window already.

The recovery suite executes all seven actual recovery scenarios. A two-process barrier chain tests
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

The P12 suite executes `12-inline-scripts.feature` at the same native CLI seam.
Its Unix callers explicitly select TERM=dumb for deterministic PowerShell stdin
output. A separate TERM=xterm reproduction demonstrates interpreter-generated
control bytes; the engine still inherits the caller's environment and retains
raw logs. Existing unread-input and early-exit cases also guard against pipe
close/flush errors masking the process outcome.
It requires installed PowerShell 7, Python 3 and Bash for its real-interpreter
cases; these are test prerequisites, never product dependencies. Override
`MERSTRIDE_TEST_PYTHON` / `MERSTRIDE_TEST_BASH` with an executable name or path.
Windows defaults to `python` and Git's Bash under Program Files; Unix defaults
to `python3` and `bash`. Missing prerequisites fail rather than skip scenarios.
The separate native helper accepts a test-only stdin probe, preserving literal
argv, Unicode transport and exact child accounting. A script reports visible
workspace/state files while executing; probe args and stdin verify file-free
transport. These observations do not claim a global filesystem audit.
Recovery tests use the installed native helper as a shebang interpreter with
barriers/signals, checking uncertain effects, explicit retry, fresh activation IDs
and attached-tree termination. Real-interpreter cases check Unicode, separate
streams and exit routing; unread large stdin checks feeder cancellation. Platform
conditions are tested on the actual host, without simulating another OS.

The creation-guide suite executes 11-creation-guide.feature using only a copied
published executable in each isolated directory. It checks default text/explicit JSON,
unusable state paths, root/command help, aliases, global placement and invalid
arguments, with unchanged executable bytes and external no-child accounting.
It extracts the actual embedded example's YAML and Mermaid fences and validates
them through that executable without running Git or creating state. The native
copy proves content availability without external files; the architecture gate
continues to prohibit engine networking. Release inventory includes these cases.

Keep raw logs, TRX, traces, environment dumps and compiler/linker input lists under
ignored `artifacts/`. Version concise reviewed results and reproduction commands;
omit user directories, hostnames, hardware/security inventories and unrelated
installed software. Preserve required dependency inventories and license notices.
Clean-target provenance must still identify the actual platform/image and exact
tested source/binaries, without personal machine identities. Review external
qualification data before supplying it to a release workflow.

`pwsh -NoProfile -File scripts/repository-privacy.ps1` checks tracked file paths
and their current contents for raw logs, private home paths, private keys and
machine identity fields. The full build runs it and its disposable Git fixtures.
This focused guard complements a redacted full-history secret scan; it is not a
complete secret detector. Run `gitleaks git --log-opts="--all" --redact=100` before
publication. Raw scanner reports also stay under ignored `artifacts/`.

Record binary hash/version, OS, fixture version, scenario results, native publish output, license inventory, executable size, and timing distribution. Run core tests against the published binary on clean target machines. The test harness may run externally; the target product environment has no installed language runtime. PowerShell-specific scenarios require explicitly provisioned PowerShell and must report that prerequisite; all other core fixtures use native helpers.

A check of feature syntax is not a behavioral pass. A native publish on one OS does not prove another platform. Benchmark and token-benefit measurements are separate from deterministic assertions.

## Architecture and distribution gates

A21 maps to 07-architecture: positive/negative compilation fixtures and effective enforcement. A22 maps to the same feature: MIT release and build-only analyzer separation. These `@build` checks use the public compiler/build/publish boundary and are separate from the runtime CLI suite. The clean-runtime platform suite excludes build-only checks; the overall release gate requires both suites.

A23: official Actions, SHA pins, and PR isolation. A24: established dependency review. A25: tested-tag release integrity and publication gates. These are in 08-delivery.feature. Policy checks combine automated allowlist/lock/reference checks with a version-controlled reviewed dependency inventory; maintenance maturity cannot be inferred automatically from a package name.

P14 supplements those build-boundary checks with disposable local Git repositories
invoking the public release `Tag` mode. Stable/prerelease and annotated/lightweight
tags must export their expected version; malformed tags, build metadata, wrong
commits and missing main ancestry must not export one. Workflow mutations prove
that moving or missing checkout refs are rejected. Publication fixtures use the
controlled GitHub CLI boundary; these tests create no remote tags/releases and
are not clean-platform qualification.

Public MSBuild property checks require SDK 11.0.100, net11.0, stable C# 15 and
no memory-safety opt-in across product, test harness and native helper.

The CLI usability suite binds every expanded case in 09-cli-usability.feature to raw native CLI calls.
German locale settings qualify English help/errors; JSON parsing rejects extra
stdout content. Discovery checks unchanged directory entries and external process
counts; native start/status qualifies literal delimited paths and equals-form
input options. Five public compilation fixtures confine the parser to Cli.
Release inventory and clean-target expected identities include the added cases.
The p07.trx filename stays stable for release freshness checks.

The inline completion suite binds every row of 10-inline-completion.feature to the native CLI, including
PowerShell JSON argument passing, invalid modes/payloads, exact byte/depth limits,
and receipts retried across file/inline modes while a newer request waits. The
normal agent/context/receipt helper, completion boundary helper, and
competing-completion checks prefer inline parameters. File-schema/UTF-8,
legacy default-data and cross-mode cases deliberately retain file input. Raw
results remain local under ignored artifacts; no environment dumps are versioned.
Linux tracing requests seccomp-BPF filtering with the same follow-forks/execve
observation: unrelated syscalls no longer incur a ptrace stop when supported.
This reduces interference with real process timeouts; neither product deadlines
nor externally asserted process counts are relaxed. The tracer can fall back to
ordinary observation if kernel filtering is unavailable; required tests still run.

P13 executes `13-readable-output.feature` through redirected native CLI streams,
proving the readable default without terminal detection. Discovery, validation,
usage/state errors, waiting-agent prompts/outcomes, selected truncated/unavailable
context, completion receipts, terminal inspection, log tails/control bytes and
explicit recovery are checked alongside JSON compatibility. Raw log/state bytes
are compared before/after read-only output. The existing CliWorld/benchmark,
release and clean-Linux JSON consumers request --json explicitly; mixed-mode P11
checks retain their scenario tags. No renderer-internal test seam is introduced.


## P15 native suite and macOS observation

The full build also publishes `tests/Merstride.NativeSuite` to `artifacts/suite`
and executes its independent Native AOT driver against the published CLI and
fixture. It reads the actual Gherkin files, preserves tags/outline rows, excludes
only `@build` and the three recursive release-environment orchestrators, and fails
on unimplemented/undefined steps or any assertion. NUnit 4 remains the managed
host; a deliberately limited native assertion adapter avoids NUnit's unsupported
reflection/dynamic-code paths. Negative controls guard mismatched values,
collection multiplicity/order, conjunction/negation, lengths and bounds. Shared
fixture JSON uses generated metadata; no Native AOT warnings are suppressed.
Every native scenario result is written to `artifacts/tests/native-scenarios.json`.
The internal native-driver artifact retains project/.NET notices and the MIT
licenses for Gherkin 43.0.0 and Cucumber.Messages 35.0.0, fetched from their
reviewed source commits. These test-only components are excluded from product
archives and the shipped-product inventory.

P15's runtime scenario launches one native fixture with 32 immediate-exit children
and requires all 33 launches after exit. It runs on every platform. Windows Job
and Linux execve/proc accounting are retained. macOS runs the unprivileged suite
through `python3 scripts/observe-macos.py TRACE -- EXECUTABLE ...`: an initial
stopped-child barrier attaches an external privileged DTrace observer before
execution. Successful execs are retained; fork/exit events identify live ancestry.
Fresh kernel heartbeats drain buffered events, and errors, event loss or stale
heartbeats fail. No process polling or relaxed count is substituted. Actual hosted
probe availability and macOS execution passed in hosted run 44024602627 for
commit `39f864d`; new candidates must pass the same gates.
Freshness compares newly appended heartbeat records with the preceding snapshot
and a 100 ms local monotonic drain interval, without comparing wall clocks across
the kernel and managed runtime. Missing heartbeat errors include bounded counts
and readiness state, never raw trace content.
The observer uses a positional root PID instead of DTrace process attachment.
The launch barrier releases only after kernel probes are ready; the root's kernel
exit event ends observation. This keeps observation independent of host execs.
The macOS harness supplies a physical `TMPDIR` so PowerShell and .NET use the
same workspace spelling. Its Python fixture uses the installed framework's
`Python.app` interpreter when available, avoiding the extra exec performed by
the framework's bin launcher; `MERSTRIDE_TEST_PYTHON` still takes precedence.
The one-interpreter launch assertion is unchanged.

Mac builds first execute that single short-lived-launch scenario under the same
external observer, with a 120-second outer harness watchdog. Failure stops the
build and reports its bounded assertion message; success is followed by the full
unchanged managed and native suites. The preflight is never release qualification
and omits no required scenario. GitHub phase notices identify the last started
build boundary even if the job is forcibly cancelled; no raw logs or environment
inventory is copied into those public notices.

The separate hosted macOS clean-target Release job runs `scripts/qualify-macos.py` using
only the downloaded native suite, helper and extracted product. It verifies actual
absence of standalone .NET installations, exact arm64/source/run/version/hash
identities, all 284 runtime scenario identities/statuses and current feature hashes.
It retains raw runtime inventories/traces in job artifacts and emits compact proof
for release assembly. Reference-machine budgets remain separate mandatory evidence.
