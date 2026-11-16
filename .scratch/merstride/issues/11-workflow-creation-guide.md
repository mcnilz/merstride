# P11: Add a compact workflow creation guide for agents

**What to build:** Let an existing agent invoke `merstride creation-guide` to
quickly author a valid Markdown workflow containing a marked Mermaid flowchart
and its associated YAML. Provide compact English instructions, one complete
copyable example and a short validation checklist in the installed native binary.
The guide must be sufficient without external documentation or example files.

**Blocked by:** None (can start immediately). P04 workflow authoring, P08 CLI
discovery and P09 preferred inline completion are complete; P10 repository
consolidation is also complete. P07's outstanding external release qualification
blocks publication, not this scope.

**Status:** complete — implemented and reviewed in the separately assigned implementation session.

**Approval and delivery boundary:** The user approved this single local planning
ticket after reviewing the creation-guide proposal, then explicitly added the
embedded-resource requirement. No further approval is needed to create the ticket.
This planning session must not implement, commit, push or publish anything.
Implementation and any local commit require a separately assigned implementation
session and its acceptance instructions. Do not create a platform-execution issue
or include that separately discussed work here.

**Acceptance scope:** Public published native CLI behavior and the public build
boundary. Add dedicated @P11 acceptance scenarios, retaining existing requirement
and scenario tags and relevant @A01 @A05 @A14 @A19 @A20 @A16 regression coverage.
The execution contract, supported authoring rules and established CLI discovery
decisions remain authoritative; this guide documents them without extending the
workflow language or changing execution semantics.

- [x] Add the argument-free `creation-guide` command alongside the existing
      operational `guide`. Explain its purpose in root help and command help,
      support `-h` / `--help`, and include it consistently in CLI discovery and
      user-facing command documentation. Preserve established global-option,
      help, usage-error and output conventions and existing guide behavior.
- [x] Return the creation instructions in a nonempty `guide` string under
      `--json`, in exactly one protocol-version-1 JSON object on stdout, with
      null status/error and exit code 0. Follow the existing guide's default
      output convention; help remains English text by default and a `guide`
      string in protocol JSON with `--json`. Keep incidental diagnostics out of
      the protocol stream.
- [x] Keep the guide content in a separate text file embedded as a .NET resource
      and load the displayed guide from that resource. The published Native AOT
      executable must contain it and work when distributed alone, with no external
      guide/content file, source checkout, runtime content download or new runtime
      dependency. Keep this content readable and maintainable separately from
      command implementation code; relocating the existing operational guide is
      not required by this ticket.
- [x] Make guide retrieval offline and self-contained: require no workflow file,
      run, workspace or state access; create or mutate no state; launch no child
      process and make no network request. Match established help/version discovery
      behavior with an unusable state path. Reading the instructions must not
      execute their example or depend on any example command being installed.
- [x] Explain UTF-8 Markdown pairing: column-one backtick fences, the exact
      `yaml merstride` info string, the marked `mermaid` fence and matching
      case-sensitive workflow IDs. Cover block order, ordinary ignored fences,
      enclosing fences, single-pair default selection, `--workflow <id>` for
      multiple pairs and rejection of missing, duplicate or conflicting pairs.
- [x] State the supported Mermaid subset compactly: `flowchart TD` / `LR`,
      explicit supported node declarations, one declaration or edge per line,
      supported arrow/label forms, comments, label restrictions and valid IDs,
      including reserved node-name restrictions. Identify common unsupported
      shapes, implicit nodes, chains, subgraphs, styling and directives rather
      than suggesting arbitrary Mermaid is executable.
- [x] Describe the supported YAML schema sufficiently to author without another
      document: required root fields and schema version, entry, node mapping and
      optional transition limit with default/range; required and optional fields
      and allowed values for process, agent and end nodes, including literal
      string arguments, process timeout range, literal prompts and valid context
      selector forms. Explain quoting numeric/boolean arguments, constrained YAML,
      and rejection of unknown fields, duplicate keys, wrong types, aliases,
      anchors, tags, merge keys and multiple documents.
- [x] Explain essential graph/routing validation: one metadata definition per
      graph node and vice versa; an existing entry; all nodes reachable and able
      to reach an end; no YAML edges/next-node fields; end nodes have no outgoing
      edges. Processes use either one unlabeled edge (warning: ignores failures)
      or distinct labeled edges; exact results precede `default`, and unmatched
      results fail. Cover `exit_<n>`, `spawn_error`, `timeout`, `output_limit` and
      `signal`. Agent edges are labeled, distinct and have no `default`; their
      labels define the complete allowed-outcome set. Multiple incoming edges
      are alternatives, not joins; cycles require a finite transition budget.
- [x] Include one complete, directly copyable Markdown workflow with both real
      executable fences, matching IDs, a process node, an agent decision and
      reachable success/failure end nodes. Use explicit success and failure
      routes, practical literal arguments and valid context. State any prerequisite
      for actually running its process without requiring that prerequisite to
      retrieve the guide or validate the example. Do not substitute schema
      fragments, omitted sections, placeholders or external workflow references
      for the complete example.
- [x] End with a short authoring checklist culminating in
      `merstride validate <file> --json`, with `--workflow <id>` when needed.
      Clarify that validation launches nothing. Keep the focus on creating and
      validating workflows; refer to the operational `guide` for the execution
      loop without requiring it to understand the authoring instructions.
- [x] Prove the behavior through the published native CLI: retrieve the actual
      guide from an isolated directory without source/content files, verify its
      protocol, exit code and lack of effects, and extract its complete example
      into a temporary Markdown file and successfully validate it through that
      CLI without executing workflow processes. Cover root/command discovery,
      both help aliases, JSON help, global-option placement and representative
      invalid arguments. Exercise actual required feature scenarios; undefined,
      ambiguous, pending or skipped required steps must fail.
- [x] Update normative command/authoring documentation and acceptance scenarios
      together so the guide, help, contract and native behavior agree. Retain
      .NET 11 Native AOT/C# 15, parser boundaries and existing dependency,
      architecture, distribution, privacy and native budget gates. Use the public
      build boundary for resource/publish policy checks as needed. Run the relevant
      checks and full local gate during implementation; record concise results and
      limitations, keeping raw logs ignored. Complete standards/spec review without
      claiming that local checks satisfy outstanding P07 release qualification.

No workflow execution redesign, new schema/CLI execution platform, automatic
process launching, LLM integration, external documentation dependency, remote issue
publication, release publication or deployment is included.

## Completion evidence

The user assigned the open issues for implementation in this session. Repository
rules select one approved scope at a time; P11 was selected first. The required
implementation workflow includes a local commit on the current branch.

Added argument-free creation-guide, English root/command discovery and an embedded
text resource in the native binary. The guide describes the existing schema and
routing, includes a complete process/agent/success/failure workflow and ends with
a validation checklist. No workflow language or execution semantics changed.

The first published-native acceptance test failed with USAGE before implementation
and passed afterward. All 28 dedicated P11 cases pass, including standalone binary
retrieval, default/JSON output, help aliases, global placement, unusable state
paths, invalid arguments and extraction/validation of the actual embedded example.
External process counts and unchanged directory/binary checks passed. No new
runtime dependency was introduced; engine networking remains prohibited by the
architecture gate.

Windows x64 full local gate: 389 tests / 220 local feature cases, no failures or
skips; native publish without warnings/errors, architecture/dependency/distribution/
privacy, native budgets and Mermaid checks passed. Binary: 5.849 MiB; warm status
P95: 26.535 ms. Local documentation links passed. Standards review: zero actionable
findings. Spec review: zero actionable findings. Raw logs remain ignored.

Linux was not rerun for P11; no new Linux verification is claimed. Two mandatory
release-environment orchestration cases remain outside the local gate and require
the unfinished P07 qualification. P12 remains ready-for-agent and was not changed.
No push, release publication or deployment is authorized or performed.
