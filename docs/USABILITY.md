# Cross-scenario usability review

Reviewed baseline: `910b7e7` (accepted P19–P21), plus the uncommitted P22
verification changes. The normative contract, ADRs 0015–0017 and all 21 feature
files define expectations. No product behavior or protocol is changed by P22.

[The review matrix](usability-scenario-review.tsv) contains every scenario and
every independently expanded example row, with its original identity, tags,
owner, real boundary, human and AI assessment, evidence and disposition. Its
assessment references below apply to that exact row's parameters and assertions,
not just to a representative happy path. `scripts/usability-audit.ps1` reconciles
the matrix against the existing release inventory, actual Cucumber parser output,
feature hashes and both execution reports. Missing, duplicate, stale or failed
rows fail the public build. This is one inventory with an assessment overlay.

The source contains 448 expanded rows: 445 local cases, including 15 public-build
cases, and 430 native runtime cases. Three additional recursive release-environment
rows remain mandatory, separate qualification. The observer and budget cases run
at their actual native boundaries; they are not invented interactive commands.
There are 138 scenario declarations: 92 ordinary scenarios and 46 outlines with
356 independent example rows. Of 428 product-CLI rows, 420 require readable and
structured primary-action evidence. Eight deliberately malformed/duplicate
`--json` syntax rows retain the contract's JSON usage-error format; their matrix
assessments explain why deleting the offending flag would change the requirement.
They still require actual native execution and actionable protocol errors.

## Assessments used by each matrix row

| Feature/profile | Human assessment | AI integration assessment |
| --- | --- | --- |
| 00 | Ready creation is visibly distinct from execution; the selected run can be inspected and explicitly advanced. | Exact returned identity/root survives another process; ready has no activation and terminal advance has the expected status. |
| 01 | Workflow selection is explicit; every invalid definition/size gives a located diagnostic before effects; literal prompts and frozen source remain understandable. | Selection, diagnostic locations, schema/graph bounds and no-mutation/effect assertions use the native boundary, including each defect row. |
| 02 | Chains expose process results, route and overall result separately; ordinary start does nothing; literal arguments need no shell quoting invented by the engine. | Exact/default/spawn/no-match outcomes, cursor, separate raw streams, literal argv and effect order are independently asserted. |
| 03 | Waiting output contains the literal task, allowed choices and only selected context; completion moves the cursor and the next action remains explicit. | Stable requests, limited context, no-mutation rejections, equivalent receipts, conflicts and later loop requests prevent unsafe retries. |
| 04 | Timeout/output/transition/context/data limits explain the stopping point; targeted logs remain readable and bounded. | Exact limits, Unicode tails, metadata, byte/depth boundaries, process-tree termination and unchanged read-only state are asserted separately for each row. |
| 05 | An uncertain external effect requires inspection and explicit retry/fail; inspection and advance do not replay it; locks and corrupt state fail visibly. | Real barriers, writer exclusion, independent runs, actual crashes/signals, retained activations, atomic reader combinations and unchanged corrupt bytes are the evidence. |
| 06 | Offline guide and read-only inspection work without setup; failed status is a successful inspection; explicit roots work across directories. | One protocol response, exact identities/root, read-only snapshots and separate execution versus inspection exits are checked. Native budgets are measured separately. |
| 07 | Product human mode is not applicable: these rows invoke the compiler, architecture gate or distribution audit, not a workflow command. Located compiler/gate diagnostics are their public evidence. | Product protocol is not applicable: these rows check public build results, dependency boundaries, publish inputs and licensing; there is no product request/receipt. |
| 08 | Product human mode is not applicable: these rows review Actions/dependency/publication controls through public build scripts and disposable release fixtures. | Product protocol is not applicable: controlled release tooling checks identities, artifacts and refusal to publish. It does not establish actual hosted execution or publication. |
| 09 | English help explains commands, placement, delimiters, defaults and exits; quoting spaces and option-like filenames is practical without JSON tools. Usage failures include help and have no effects. | Explicit protocol selection, one response, globals, malformed options and literal paths are asserted independently for every argument row. Deliberately malformed JSON switches retain their specified protocol error format. |
| 10 | File-free completion uses the displayed short request and an allowed outcome; optional structured data is explicitly an integration concern. Invalid modes/data reject without advancing. | Inline/file receipt equivalence, Unicode literal arguments, all byte/depth/number boundaries and historical retries are checked without completing newer work. |
| 11 | The standalone binary supplies readable authoring/help and a complete example even with unusable storage; retrieval executes nothing. | The same embedded instructions are available in a protocol guide string; the extracted example validates through the native executable without state or children. |
| 12 | Script/variant errors are located before execution; interpreters are explicit prerequisites; missing execution explains spawn_error and logs, never silently substitutes a shell. | Frozen stdin/argv, exact platform variant, Unicode/separate streams, early exit, limits and uncertain activation recovery retain independent effect assertions. |
| 13 | Default redirected output is readable, escaped and bounded; errors/warnings are on stderr; handoffs, receipts, logs and recovery have concrete next actions. | Explicit JSON preserves values, single-response/stream rules and safe receipts. Output-format-specific rows also retain their original native assertions. |
| 15 | Product human mode is not applicable: this native test-fixture/OS-observer row observes 33 short-lived launches, not a product CLI task. | Product protocol is not applicable: retained external process events qualify the observer used by both modes; no request/receipt or simulated OS exists. |
| 17 | Live activation precedes barrier release; saved outcomes/routes are truthful, bounded and separate from prompts/raw child data; inspection/retries do not announce new work. | The explicit JSON handoff/completion companion checks one response without progress; process/effect, lock, save and signal evidence remains at the same native boundary. |
| 18 | Project/subdirectory defaults remove routine state-root and run-ID setup; ambiguity and unusable selections fail closed while preserving existing files. | Explicit roots/identities, prefixes, request conflicts, workspace/log ownership and all negative selection rows retain native state/effect assertions. |
| 19 | Current/per-workflow selection uses positional paths; short IDs are available for exceptions; runs/use/discard avoid manual directory management and preserve unrelated data. | Exact identities beat prefixes; reservations survive discard; delayed/historical completion targets the original request; locks, corruption, pending discard and ownership refusals remain actionable and safe. |
| 20 | Numbered node movements, exact/default/unlabeled reasons and visits distinguish process outcomes from run results, including failure and process-like agent choices. | The structured loop/receipt companion preserves protocol 1 and literal outcomes; semantic cursor/effect assertions and failed-save/interrupt controls complement presentation checks. |
| 21 | Explicit combined start distinguishes saved creation from initial execution and retains identity/progress on failure; ordinary start remains ready. | One combined response carries additive start metadata and targets only the newly created identity despite concurrent selection changes; failures/signals never trigger automatic replay. |
| 22 | Runs/use/discard help works without a selector, valid storage or operational effects. | Explicit JSON help supplies one protocol guide, including global placement and optional selector forms. |

The observer row and native-budget row have concrete noninteractive boundaries.
Clean-platform rows require actual matching external reports. Their human and AI
assessments are conditional on running the complete native suite there; a Windows
development-host pass does not discharge any clean-target row.

## Execution and evidence interpretation

The original feature steps first run through the actual published executable.
A second pass reuses their fixture parameters and independent semantic, process,
filesystem and no-mutation assertions. JSON-only command actions execute in
readable default mode in that pass. Successful start/advance/resolve/use are
observed with a separate read-only JSON status, never by executing the mutation
again. Successful completion uses the documented exact-request receipt retry.
Rejected mutations retain the original text error and exit; they are never
automatically reissued to acquire JSON. Successful discard has no post-state:
its actual text and independent removal/survivor assertions are the evidence.

Read-only commands can be inspected in both presentations. Deliberately malformed
format switches keep their specified JSON error behavior, since stripping the
offending switch would change the test request. Text-focused cases retain their
original assertions and execute structured semantic counterparts using the same
scenario's fixtures and parameters. The matrix declares primary command names
and required modes for each row. Typed operation records distinguish actual
actions from observation probes, and the build requires both presentations of
every declared command in both hosts. `@variant` declarations separately identify
controlled writers, forced crashes, signals, concurrent readers, selection races
and interpreter-mediated completion. An ordinary call to the same command cannot
substitute for those operations. A positive count of later status calls
cannot qualify a controlled writer.
Controlled writers, signals and combined-start operations use their real native
barriers and mode-specific output assertions. No private persistence hook is used.

## Findings and resolutions

All findings below are verification/guidance gaps in P22 scope. No product defect
requiring a P19–P21 behavior change was found. Reproduce the exact case via its
matrix identity and `scripts/build.ps1`; the public policy controls also run as
`UsabilityAuditTests` in the local test host.

| Finding | Affected case/mode and reproducible failure | Resolution evidence |
| --- | --- | --- |
| U01 — P1, missing readable semantic coverage | JSON-only authoring defect rows, process routes, agent rejection/history, limits, selection and recovery did not prove the readable action's effects. | Complementary default-mode actions reuse each expanded row's independent semantic/effect assertions; mutations are observed safely without automatic repetition. |
| U02 — P1, asymmetric presentation coverage | P17 outcome rows, P13 handoff/recovery and P20 branch/loop/recovery actions could run twice as text while setup/status supplied JSON. | Exact-parameter structured companions reuse public fixtures, assert route/status/outcome/transition and effects, and cover rejected completion/result/resolution saves and real barriers. |
| U03 — P1, insufficient evidence gate | Positive invocation counts could pass with only setup/inspection, another operation variant or a missing primary mode. | Declared commands/variants/modes are checked against typed action evidence separately in managed and native reports. Controlled missing-text, missing-JSON, observation-only, missing-command and wrong-variant reports must fail; outputs remain private fixture artifacts. |
| U04 — P2, offline management help gap | Converted `discard --help --json` was treated by the audit as actual discard, probing a selector and expecting deletion. Runs/use/discard help lacked dedicated inventory rows. | Help bypasses discard observation/deletion assumptions. Twelve @P22 rows check selector-free and selector-bearing help in both modes with unusable storage and unchanged empty workspaces. Required steps failed before binding and pass with native assertions. |
| U05 — P2, unasserted direct writer response | A closed-pipe JSON writer could succeed using only its exit and later status evidence. | Its actual stdout must be one protocol-1 object with completed/transition semantics. Other direct writers/signals assert their actual protocol response, streams and semantic outcome. |

There is no waiver for a failed applicable local usability case. External release
qualification remains outstanding at its actual boundary, as identified per row.

Raw NUnit/native reports, command transcripts and walkthroughs stay under ignored
`artifacts/`. The final [status summary](STATUS.md) records the actual gate results.
To reproduce locally from the prerequisite-equipped platform terminal:

```bash
pwsh -NoProfile -File scripts/build.ps1
pwsh -NoProfile -File scripts/usability-audit.ps1
```

On Windows use native PowerShell for the build entry point, as documented in
[Contributing](../CONTRIBUTING.md); use Bash for the CLI walkthroughs below.

## Practical operating guidance

For a person, read `merstride --help` and `merstride creation-guide`, validate the
Markdown, then use `start workflow.md` to prepare or `start workflow.md --advance`
to execute. `status workflow.md` and `advance workflow.md` keep independent
workflows clear. Current context permits the selector to be omitted. Complete
using the displayed short request and an allowed choice, then explicitly advance.
Use `runs`, `use <run-id>` and deliberate `discard <run-id>` for retained work.
`--file` disambiguates filenames beginning `r_`; `./r_000001` is also a path.

For automation, request `--json`, retain the exact runId, request ID and stateRoot,
check the CLI exit and protocolVersion, and tolerate additive response fields.
Do not parse the human sentences or assume UUID-length IDs. Completion retries
must reuse the original request and equivalent payload, even after context changes.
Use explicit run/file targeting for independent concurrent work. Status/logs are
read-only and bounded; select an activation/stream and tail size for more detail.

After interruption or an I/O refusal, inspect exact-run status, logs and external
effects before deciding what to do. Advance never replays uncertain work. Explicit
resolve --retry permits a later attempt that may repeat external effects;
resolve --fail stops it. A failed combined start can retain a new run: repeating
start creates another identity. Recover the retained identity in its original root.

No new Linux/macOS, clean-target, hosted-control, physical-disk or narrow
creation-to-advance signal-window qualification is established by this audit.
There is no measured token-saving percentage. See [release status](STATUS.md).
