# P13: Human-readable CLI output by default

**What to build:** Make Merstride's default CLI output easy for people to read.
Without `--json`, present concise English text instead of a JSON protocol object.
Keep explicit `--json` as the stable machine-readable interface for agents and
scripts.

**Blocked by:** None (can start immediately). P08 CLI discovery, P09 inline
completion and P11 creation guide are complete. P12 process authoring is an
independent scope. P07's outstanding release qualification blocks publication,
not this implementation.

**Status:** complete — implemented in the separately assigned session on
2026-11-16 ("offene issues umsetzen"); full Windows local gate and both
standards/spec reviews passed. No new Linux/macOS execution evidence is claimed.

**Approval and delivery boundary:** The user requested a new local issue and
specified that the default output should become an easily readable form instead
of JSON. This session records that scope only. Implementation and any local
commit require a separately assigned implementation session and its acceptance
instructions. No push, publication or deployment is authorized.

**Current behavior:** Help already defaults to English text, but operational
commands, errors, version and both guides still return protocol JSON without
`--json`. The current contract explicitly requires this for creation-guide;
implementation must deliberately update that rule and the relevant CLI decision
rather than leave contradictory normative requirements.

**Acceptance scope:** Add dedicated @P13 scenarios through the published native
CLI. Preserve existing scenario tags and verify explicit JSON compatibility,
including P08 discovery, P09 completion and P11 creation-guide behavior.
Architecture and policy checks use the public build boundary.

- [x] Without `--json`, all commands produce readable English text, including
      validate, start, advance, status, complete, resolve, logs, guide,
      creation-guide, version and root --version. Existing textual help stays
      readable. The default must also apply when output is redirected; scripts
      opt into JSON explicitly rather than relying on terminal detection.
- [x] Use concise labels and readable grouping rather than serialized response
      envelopes. Show the information needed for the next action: run ID and
      state root after start; status and cursor where relevant; and request ID,
      node, literal prompt, allowed outcomes and available context when waiting
      for an agent. Preserve distinctions between unavailable and truncated data.
      Do not expand full history or full process logs in ordinary status output.
- [x] Render validation diagnostics with severity and source location where
      available. Render errors with an understandable message, stable error code
      and applicable location; usage errors include a useful help hint. Keep
      default stack traces hidden. Define and document stdout/stderr placement
      for text results and errors consistently.
- [x] Render guide and creation-guide as directly readable instructions, with
      real line breaks and a copyable workflow example rather than an escaped
      JSON string. Preserve their offline, embedded, read-only behavior and
      discovery's independence from workflow/state access.
- [x] Make selected log content directly readable while retaining stream,
      activation and truncation information where applicable. Define text
      rendering for multiline, Unicode and control characters without changing
      stored raw logs or flooding the terminal with unbounded output.
- [x] With explicit `--json`, every command, help/version alias and error returns
      exactly one existing protocol-version-1 JSON object on stdout, preserving
      response fields, values and escaping. No textual headers, progress output,
      ANSI sequences or incidental diagnostics enter the JSON stream. Preserve
      global-option placement and malformed/duplicate-option handling.
- [x] Output selection changes presentation only: preserve command effects,
      exit codes, run transitions, completion idempotency, recovery, storage and
      limits. Both output modes report the same underlying result. No workflow,
      persistence or protocol redesign is included.
- [x] Update the contract, relevant decisions, CLI help, embedded guides and
      examples consistently. Audit scripts and acceptance helpers that parse CLI
      output and make JSON consumption explicitly request `--json`. Explain the
      default-output change for existing callers without incrementing the JSON
      protocol version solely for a presentation change.
- [x] Verify representative success, waiting-agent, terminal, validation and
      usage/state-error cases in both modes, plus guides, logs and version
      discovery. Preserve the existing native acceptance and public build gates;
      record concise evidence and actual platform limitations before acceptance.

**Implementation design work:** Choose the exact text layout during
implementation and demonstrate representative output in acceptance evidence.
No table-rendering dependency, color scheme, interactive UI or additional output
format is prescribed by this ticket.

## Implementation evidence — 2026-11-16

The planning-only boundary above records ticket creation; the later user
assignment authorized implementation. Acceptance remains at the existing native
CLI/public build boundaries, without an internal renderer test seam.

- All commands default to UTF-8 English text even under redirected stdout.
  Results use stdout; located errors/diagnostics use stderr. Explicit --json
  retains the version-1 response model/serializer, including argument errors.
- Native red/green version/guide cases demonstrated the previous JSON default.
  A review finding for supplementary format characters was reproduced with
  U+E0001, corrected using Unicode scalar classification, and reverified alongside
  unchanged JSON prompt content. Both reviews then had zero outstanding findings.
- 33 dedicated P13 cases pass. Final Windows x64 local gate: 467 tests /
  298 local feature cases, zero failures or skips, no build/publish warnings or
  errors. Architecture, dependency, privacy, distribution and Mermaid gates pass.
- Selected context, completion receipts, unchanged raw logs, bounded tails and
  explicit interrupted retry/fail behavior are exercised in both output modes.
  Release inventory refreshed to 298 local / 283 runtime identities and 14 hashes;
  its controlled publication fixtures pass after rejecting stale source hashes.
- Native binary: 5.972 MiB; warm status P95: 24.814 ms. No dependencies or license
  inventories changed. Raw verification output remains under ignored artifacts.
- Representative actual default output: `Merstride 0.1.0 (protocol 1)`;
  `Valid workflow: smoke`; `Error INVALID_RUN_ID: Invalid run identifier.`
  The last goes to stderr with exit 4; successful examples use stdout/exit 0.
  Waiting output contains Run/Status/Cursor/Request/Node/Prompt/Allowed outcomes/
  Context labels and explicit unavailable/truncated metadata; logs include
  Activation/Stream/Tail truncated labels. See docs/CLI.md for usage examples.
- Windows verified; Linux not rerun for this slice; macOS unavailable. Existing
  P07 external release qualification remains incomplete. No push, publication or
  deployment was performed.
