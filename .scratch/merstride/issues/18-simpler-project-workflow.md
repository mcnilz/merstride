# P18: Simplify project setup and run selection in the CLI walkthrough

**What to build:** Let a user start, inspect, advance and complete a workflow in
a project with much less setup and shell glue. Provide practical run/request
selection and predictable project state handling so the README demonstrates
ordinary use without JSON extraction, repeated long-ID copying or manually
carrying a state-directory argument through every command.

**Blocked by:** None (can start immediately). P08 CLI discovery, P09 inline
completion, P13 readable output and P16 Bash/readable examples are complete.
P17's live console feedback is independent. Outstanding release qualification
blocks publication, not this follow-up.

**Status:** complete

**Approval and delivery boundary:** The user authorized local issue capture on
2026-11-17 after trying the README walkthrough. This ticket records the requested
scope only. Implementation and any local commit require a separately assigned
implementation session and its acceptance instructions. No commit, push,
publication or deployment is authorized by this capture.

**Problem and existing overlap:** The user's walkthrough experience involved
capturing output, extracting IDs with jq, handling overly long generated IDs and
manually configuring state storage. [P16](16-bash-first-documentation.md)'s latest
follow-up already removes jq from the human walkthrough; the current README
prints readable output and asks users to paste Run/Request IDs. It still prepares
workspace/state variables and repeats the state argument. The contract already
defaults state storage to .merstride/runs beneath the calling cwd, and this
repository ignores .merstride/. Merely adding that existing default or repeating
the documentation conversion would not address the remaining workflow friction.

[P08](08-cli-usability.md) owns completed parsing/help improvements,
[P09](09-inline-agent-completion.md) already supplies file-free completion, and
[P13](13-readable-default-output.md) supplies readable output. Build on those
results without duplicating them or requiring JSON tools for human use.

**Acceptance scope:** Add dedicated @P18 public native CLI scenarios for any
new behavior. Preserve existing scenario tags and contract guarantees; use the
public build boundary for architecture/policy checks.

- [x] Deliver and document one coherent simple project path through start,
      status, advance to an agent request, inline complete and final advance.
      The basic path uses readable default output, no output capture or jq,
      no completion file, and substantially less setup and repeated identifier/
      state-path handling. Demonstrate the actual before/after commands.
- [x] Reduce the burden of long generated run/request IDs with a documented
      selection mechanism suitable for both people and existing agents.
      Retain explicit full-ID targeting and stable resource identity; decide
      whether convenience comes from input selectors, display handles, generated
      IDs or another mechanism rather than assuming a particular ID scheme.
- [x] Selection is unambiguous and safe across multiple runs and requests,
      including loops and completion retries. Ambiguous, invalid or stale
      convenience selections fail clearly without mutation rather than choosing
      a resource silently. Preserve request identity, receipt idempotency and
      the rule that a historical completion cannot complete a newer request.
- [x] Make ordinary project state storage predictable without manual per-call
      configuration. Define project-boundary/discovery rules, any configuration
      source and precedence, and behavior from project subdirectories and
      outside a project. Retain explicit absolute --state-dir overrides and
      explain the resolved state root in readable output.
- [x] Keep mutable state separate from tracked project content. If adopting an
      automatically created project directory, define how it is Git-ignored
      without overwriting existing ignore/configuration content. Do not assume
      that a directory name containing tmp authorizes deletion of persisted
      runs. Do not silently relocate, overwrite or discard existing state.
- [x] Define compatibility with existing cwd-relative state roots, saved full
      IDs and explicit-state callers. Distinguish first-use state creation from
      a missing selected run or unusable, corrupt or conflicting state. Failures
      produce actionable errors without silently falling back to a different
      root or mutating another project. Opaque IDs must not become arbitrary
      filesystem paths.
- [x] Preserve command effects, exit codes, locks, limits, explicit recovery,
      Native AOT/offline operation and existing protocol-1 JSON callers. Start
      still executes nothing, complete still changes only receipt/cursor, and
      discovery/help/guides remain independent of workflow/state access.
- [x] Update the README, CLI help, relevant embedded guides, contract and
      decisions together. Keep Bash as the general example language, explain
      executable/platform setup only where needed, and reserve JSON extraction
      for explicitly labeled automated integration examples.
- [x] Execute the simplified README path against the published native CLI.
      Verify full-ID/explicit-root compatibility, multiple-run ambiguity,
      stale-request and retry behavior, project/subdirectory discovery, paths
      with spaces, missing/corrupt state and existing ignore/config content.
      Required steps must fail when undefined, ambiguous, pending or skipped.
      Retain existing gates and record actual platform evidence and limitations.

**Open implementation choices:** Project-level default configuration and an
automatically selected Git-ignored project directory are candidate approaches,
not decisions. The user's approximate "Merstride TMP" wording does not establish
a directory name, temporary-storage lifecycle or cleanup policy. Choose and
record project discovery, configuration precedence, identifier/selector behavior
and compatibility rules during implementation; no exact directory convention,
ID length, alias syntax or implicit latest-run policy has been approved here.

**Out of scope:** P17 console activity, automatic execution/completion/replay,
automatic deletion or migration of runs, a general configuration framework,
workflow-language changes, LLM integration or release qualification/publication.

## Implementation session — 2026-11-17

The user assigned implementation with "nächstes issue umsetzen". The approved
scope is P18; the existing published-native-CLI and public-build test seams apply.
The session does not authorize a local commit, push, publication or deployment.
The review baseline is `2c49f9c`; review includes uncommitted and new files.

ADR 0014 records project discovery, independent overrides, durable state and
preserved ignore/config content, optional unique-run selection, stable prefixes
and compatibility rules. No configuration framework or state-schema change.

## Verification and delivery — 2026-11-17

- The initial native tracer failed on the old required workspace argument, then
  passed. Twenty-six @P18 @A09 native cases now cover the accepted scope,
  including controlled run/request collisions, historical retry/conflict across
  loop iterations, actual workspace effects/logs, project/subdirectory/Git-file
  boundaries, explicit/legacy roots, spaces and missing/corrupt/conflicting state.
- Existing ignore/config bytes remain intact. The review's negated-ignore case
  failed before the fix, then passed with a final `/runs/` rule. There are no
  environment/config sources, latest/current request aliases or state migrations.
- Reviewed Windows x64 full gate: 559 tests / 338 local feature cases and all
  323 independent native runtime cases passed without failures/skips. Native
  build/publish reported no warnings/errors. Architecture, dependency,
  distribution, privacy, budgets and Mermaid gates passed. Size: 6.026 MiB;
  warm status P95: 25.828 ms. No dependency or notice changes.
- Actual README Bash blocks passed with short request selection in a project
  path containing spaces. User follow-up removes PATH instructions and embeds
  a small executable Mermaid/YAML example. Revised commands, native README
  validation and the added README chart render passed after the full gate.
- Both independent review axes have no remaining findings after the initial
  requirement-tag and negated-ignore corrections. Review baseline: `2c49f9c`.
- Raw evidence stays under ignored artifacts: p18-red-native.log,
  p18-ignore-red.log, p18-reviewed-focused.log, p18-final-gate.log,
  p18-readme.log and p18-readme-mermaid.log. An initial restricted test host
  failed to connect; local process permissions enabled actual native testing.
  The first full gate's feature hashes were invalidated by concurrent review
  edits; the final gate used unchanged feature sources and refreshed inventory.
- No new Linux/macOS, hosted or clean-target execution is claimed. P07 release
  qualification remains incomplete. No local commit, push, publication or
  deployment was performed. Stop at this completed P18 scope.

## Local delivery follow-up — 2026-11-17

The user subsequently requested a local commit of P18. Include this completed
ticket and its implementation/evidence summaries, including the requested
README chart/YAML example and removal of PATH instructions. Push, publication
and deployment remain unauthorized.
