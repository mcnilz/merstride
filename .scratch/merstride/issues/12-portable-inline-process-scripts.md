# P12: Portable process variants and shebang-selected inline scripts

**What to build:** Let workflow authors keep portable process behavior together
in one process node: preserve direct command/args execution and add multiline
inline scripts as YAML block scalars, with optional variants conditioned on one
or multiple platforms among Windows, macOS and Linux. Select an inline script's
installed interpreter from its shebang, including on Windows, and execute the
script directly without a separate user-authored script or an intermediate script
file. Prefer a shared portable script where practical; Bash is one possible
interpreter, never a mandated or assumed installed dependency.

**Blocked by:** None (can start immediately). P02 process execution, P04
authoring validation, P05 limits and P06 recovery are complete; P08 CLI discovery
and P09 inline completion are also complete. P11 workflow creation guide is an
independent scope, not a prerequisite. P07's outstanding release qualification
blocks publication, not this implementation.

**Status:** complete — implemented in the separately assigned session on
2026-11-16 ("offene issues umsetzen"); full Windows local gate and both
standards/spec reviews passed. Linux/macOS execution evidence remains unavailable
for this slice; release qualification remains separate.

**Approval and delivery boundary:** The user approved this second local planning
ticket with "Passt", after clarifying that an execution definition without a
platform condition applies to all platforms. The approved scope includes the
behavior below; no further approval is needed merely to record this ticket.
This session is planning only: no implementation, commits, push, publication or
deployment. Implementation and any local commit require a separately assigned
implementation session and its acceptance instructions.

**Acceptance scope:** Add dedicated @P12 scenarios at the published native CLI
boundary. Preserve existing scenario tags and relevant @A01 @A02 @A05 @A09 @A10
@A11 @A12 @A13 @A14 @A16 @A18 @A19 regressions. Architecture and policy checks
use the public build boundary. This is one end-to-end process-authoring and
execution slice, not a general expression language or workflow-engine redesign.

**Implementation design work:** The exact YAML field names/shape, platform
identifiers, precedence or overlap-rejection policy, and interpreter
resolution/invocation rules are deliberately not prescribed by this planning
ticket. Define and record them consistently in the contract, relevant decisions,
examples and acceptance scenarios during implementation, within the approved
behavior. Do not present a speculative schema as already supported.

- [x] Preserve existing process nodes using command/args, including literal OS
      argument passing without implicit shell interpretation, expansion or manual
      quoting. Keep existing workflows valid and their process behavior unchanged.
      Define and validate the relationship between direct execution, inline script
      execution and variants; conflicting or malformed definitions must not be
      silently interpreted.
- [x] Support a multiline inline script in a YAML block scalar. Its shebang
      selects the interpreter, including on Windows where the engine must not
      rely on native shebang dispatch. Support arbitrary installed interpreters
      rather than a Bash-only or shell-only allowlist; Bash, PowerShell and Python
      must be documented examples. Do not require a separate script file or write
      an intermediate script file to execute the inline content.
- [x] Define deterministic interpreter resolution and invocation: accepted shebang
      forms and interpreter arguments, bare names/PATH and explicit or relative
      paths, workspace-relative resolution, paths containing spaces, applicable
      env-style shebang handling, multiline/Unicode and line-ending handling, and
      how script content reaches the interpreter without a script file. Address
      interpreter-specific inline invocation needs without injecting an implicit
      fallback shell or reinterpreting literal direct-command arguments. Unsupported
      shebang/invocation forms must fail clearly instead of running another
      interpreter.
- [x] Allow a process node to provide platform-dependent execution variants.
      A condition may name a single platform or multiple platforms: for example,
      PowerShell on Windows and Bash on macOS/Linux. An execution definition with
      no platform condition is applicable to all platforms. Document how this
      interacts with more specific variants; do not silently redefine absence of
      a condition to mean a fallback that applies only when others do not match.
- [x] Specify and test deterministic selection or rejection for overlapping
      platform conditions, including an unconditional definition overlapping a
      platform-specific one, duplicate/equivalent conditions, unsupported platform
      values and no matching variant. Define at which operation validation or
      selection occurs and its stable diagnostic/outcome and CLI exit semantics.
      Do not silently choose by incidental enumeration order or run a variant for
      another platform. Conditions are limited to platform applicability; do not
      introduce arbitrary executable conditions or a general expression language.
- [x] Specify and test clear deterministic behavior for a missing/malformed
      shebang and an unavailable/unlaunchable interpreter, including diagnostic
      location where applicable, stable error/outcome and routing behavior.
      Preserve established process outcomes where applicable and explicitly
      document any necessary extension. Validation must launch and download
      nothing; do not probe interpreter availability by executing it.
- [x] Use already installed interpreters only. Do not download, install, bundle
      or silently substitute an interpreter or introduce a universal shell
      dependency. State per-example prerequisites. A shared script is portable
      only where its interpreter and script behavior are available; Bash is not
      automatically installed or inherently portable across all three platforms.
- [x] Preserve the selected absolute workspace as child cwd and preserve
      workspace-relative resource behavior and inherited environment semantics.
      Define the inline-content transport and its stdin behavior explicitly;
      retain closed stdin for existing direct commands. Freeze inline script
      content and variant definitions with the run snapshot so source edits do
      not change an existing run. Existing workspace script files remain live.
- [x] Preserve one sequential activation and existing outcome routing, transition
      limits, separate bounded stdout/stderr logs and context tails, timeout and
      output-limit precedence, best-effort attached-tree termination and user
      interruption. Interpreter invocation must stay under these same limits;
      it must not create an unbounded feeder or bypass process accounting.
      Preserve persist-before-launch, writer exclusion, atomic snapshots and
      explicit retry/fail recovery with no automatic replay after interruption.
- [x] Provide compact authoring examples for a shared shebang-selected script
      and platform variants, covering Bash, PowerShell and Python and a condition
      naming both macOS and Linux. Show the unconditional all-platform meaning
      and preserve a simple direct command/args example. Keep contract, authoring
      instructions, relevant guides/help and examples consistent; update P11's
      delivered guide if it exists, without making P11 a blocker.
- [x] Prove actual behavior with the published native CLI: start executes
      nothing; advance selects and executes the intended installed interpreter;
      multiline content, arguments, Unicode, logs, exit outcomes and workspace
      effects are observable. Cover single-platform, multi-platform and
      unconditional definitions, overlap policy, no match, missing shebang and
      missing interpreter, direct-command compatibility, frozen definitions,
      limits and interruption/recovery. Verify absence of intermediate script
      files through observable filesystem/process evidence rather than an
      internal implementation assertion.
- [x] Record actual Windows, macOS and Linux evidence separately, including
      interpreter prerequisites and versions. A publish on one OS or simulated
      platform selection is not execution evidence for another. Identify unavailable
      environments and unexecuted cases honestly; do not mark required undefined,
      ambiguous, pending or skipped steps as passed or claim macOS/native release
      qualification from Windows/Linux results.
- [x] During implementation run relevant checks and the full applicable local
      gate, then complete standards/spec review. Retain .NET 11 Native AOT/C# 15,
      parser/architecture boundaries, dependency/license inventories, privacy,
      distribution and native budgets. Keep raw logs under ignored artifacts and
      record concise safe results and limitations. Existing P07 external release
      requirements remain outstanding; this ticket does not authorize release
      publishing, new hosted release targets or deployment.

No mandatory Bash dependency, automatic interpreter provisioning, output-to-code
interpolation, arbitrary platform expressions, parallel-node execution, LLM
integration, remote issue publication or release publication is included.

## Implementation evidence — 2026-11-16

The planning-only boundary above records the ticket-creation session. The user's
later implementation assignment authorized this implementation session; the
existing published-native-CLI and public-build acceptance seams were retained.

- Contract and ADR 0009 define script/variants forms, disjoint platform sets,
  universal omitted conditions, literal shebang tokens, explicit stdin invocation
  flags, deterministic PATH/workspace resolution and existing spawn_error routing.
- Full Windows x64 local gate: 434 tests / 265 local feature cases, zero failures
  or skips, including 45 new P12 cases. Native compilation/publish had no warnings
  or errors; architecture, dependency, privacy, distribution and Mermaid gates passed.
- Actual interpreters: PowerShell 7.6.10, Python 3.12.10, Git Bash 5.3.15 on Windows.
  The complete portable example validated and executed; the embedded authoring
  example remains covered by P11. Native size 5.957 MiB, warm status P95 19.180 ms.
- Independent standards and spec reviews reported no findings. No dependencies
  or license inventories changed. Raw logs remain in ignored artifacts.
- Linux: P12 not executed; prior Linux baseline evidence remains unchanged.
  macOS: no available environment, no execution evidence. Git Bash on Windows
  does not qualify either target. External P07 release gates remain outstanding.
- P13 is a separate open ticket. No push, publication or deployment was performed.
