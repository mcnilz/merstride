# Workflow authoring guide

Run `merstride creation-guide --json` for compact offline authoring instructions,
a complete copyable workflow and a validation checklist embedded in the binary.
It executes nothing and needs no workflow, workspace, state or external files.
Use [the complete example](../examples/review.md) as another starting point. The [contract](CONTRACT.md) is normative.

## Where behavior belongs

| Concern | Location |
| --- | --- |
| Sequence, alternatives, loops, allowed agent outcomes | Mermaid edges |
| Stable identity | Same node ID in Mermaid and YAML |
| Display label | Mermaid node label |
| Command, arguments, timeout | YAML process metadata |
| Task instructions and selected context | YAML agent metadata |
| Success or failure termination | YAML end metadata |
| Background explanation for humans | Ordinary Markdown prose |

Use one `yaml merstride` block per workflow. A central node map avoids ambiguous per-node blocks and makes missing/duplicate definitions detectable. Do not encode routing twice. YAML is configuration, not a scripting language.

## Executable nodes

```yaml
check:
  kind: process
  command: dotnet
  args: [test, --no-restore]
  timeoutSeconds: 120
script:
  kind: process
  command: pwsh
  args: [-NoProfile, -NonInteractive, -File, scripts/check.ps1]
  timeoutSeconds: 60
```

These are node-map fragments, not full workflows. Arguments are literal strings. Quote values such as `"123"` and `"true"`. Do not add shell quoting around a path containing spaces: an array item already represents one argument. Use an explicit shell only when the workflow needs it. Programs inherit the start workspace and environment; process output does not become a command.

Route normal success with `exit_0`, specific failures with `exit_1`, and remaining outcomes with `default`. Use explicit failure handling; an unlabeled edge also follows failures. An unavailable executable is a runtime `spawn_error`, not a validation-time process probe.

## Agent nodes

Use `prompt: |` for readable multiline instructions. Explain what must be inspected, what counts as success, and when each outgoing edge label should be returned. Request only useful context selectors. The agent can use its own tools under the original task authorization; the engine does not grant additional permission.

Context selectors expose the latest completed node activation, which may be from an earlier loop iteration. Check availability and activation IDs. A status excerpt cannot replace inspecting an actual diff during code review.

## Validate before execution

A document supports multiple marked pairs: select one with `--workflow <id>`, or omit the
option when exactly one pair exists. Block order is irrelevant. Only column-one
backtick fences execute; enclosing longer/tilde fences hide examples. Keep the
node declaration token contiguous as `id["Label"]`; indentation and whitespace
between header/edge tokens are allowed. Official Mermaid reserves the lowercase
node IDs `end`, `subgraph`, `style`, `class`, `flowchart`, `graph`, and `interpolate`; choose an
alternative such as `finish` or `work`. This compatibility interpretation and its
actual renderer evidence are recorded in ADR 0003 and MERMAID-VALIDATION.md.

Validation enforces 1 MiB of UTF-8 Markdown, 100 nodes and 250 edges. It does not
launch commands or probe whether an executable is installed. Start freezes the
selected compiled definition and the source hash; later source edits do not change
that run. Workspace scripts themselves remain live.

Run `merstride validate workflow.md --json`. Unknown YAML keys, unsupported Mermaid syntax, missing IDs, or invalid routes fail before execution. A successful validation does not establish that commands are safe, installed, or semantically correct.

Use workflow-specific Gherkin scenarios when authoring important workflows: successful path, expected failure, repair loop, and blocked outcome. Product-engine acceptance features in this package test engine semantics; they do not prove every future workflow's business behavior.
