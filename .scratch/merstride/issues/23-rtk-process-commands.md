# P23: Use RTK for workflow commands when available

**What to build:** Merstride should automatically run eligible process commands
through Rust Token Killer (RTK) when it is available on `PATH`. Authors can set
`rtk: true` or `rtk: false` at the workflow level and override it on individual
process nodes. Workflows remain runnable without RTK.

**Blocked by:** None.

**Status:** complete

**Approval and delivery boundary:** Local issue capture only. Implementation and
a local commit require a separate assignment.

**Assignment (2026-11-18):** The user invoked the implementation workflow for
P23, authorizing implementation, verification, review and one scoped local commit
on the current branch.

## Acceptance criteria

- [x] Accept optional YAML booleans at the workflow root and on a process node.
      An explicit node value overrides either workflow value. A node without
      a value inherits the workflow setting. For new runs, both values omitted
      means enabled. Reject non-boolean values during workflow validation.
- [x] Apply the effective setting to `command`/`args`, including that form in
      the selected platform variant. The same setting leaves `script` executions
      direct, including inline scripts in variants. Classify execution by its
      declared YAML form. An interpreter or script executable declared through
      `command` counts as a command and can be opted out at node level.
- [x] At execution time, use RTK from the inherited `PATH` when enabled and
      available. Otherwise launch the original command directly, with no error
      or warning for unavailable RTK. Preserve the workspace, environment and
      argument boundaries without inserting a shell. RTK may change captured
      output. Apply existing process limits and outcome routing, and never
      rerun the original command after an RTK-wrapped attempt fails.
- [x] Freeze the RTK configuration with the run definition. Existing saved runs
      created before RTK support retain direct execution. Validation and ordinary
      start remain nonexecuting and work without an RTK installation.
- [x] Update the workflow contract and authoring guidance with the default,
      override rules and script scope. Add `@P23` behavior scenarios against the
      published native CLI covering RTK available and absent, overrides in both
      directions, literal arguments, platform variants and script exclusion.
      Control RTK availability in the test environment and assert which process
      ran as well as its observable effects.

## Delivery evidence

- The first published-native-CLI test failed against the P22 executable and
  passed after P23 added runtime dispatch. The completed suite has 20 P23 cases
  exercised in readable and structured modes in both test hosts.
- Windows x64 full development gate passed on 2026-11-18 with 702 tests,
  465 local feature cases and all 450 independent native cases. No failures or
  skips. The 468-row inventory includes the three external release cases.
  Architecture, dependency, distribution, privacy, budgets and Mermaid passed.
- An installed RTK 1.1.0 walkthrough verified actual Git filtering, disabled and
  absent fallback, literal dispatch to an executable named `init`, and offline
  authoring help. No dependencies or license inventories changed.
- Independent Standards and Spec reviews found RTK-owned subcommand collisions,
  ambient RTK interference with baseline fixtures, quoted-empty PATH entries and
  unchecked Gherkin operands. Resolved paths, test PATH isolation and explicit
  fixture validation addressed these. Test tools sharing a directory with RTK
  require separate PATH entries, as documented in the testing guide.
- Raw logs and test reports stay under ignored `artifacts/`. This is Windows
  development-host evidence. Other platforms and release qualification remain open.
