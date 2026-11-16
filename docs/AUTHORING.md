# Workflow authoring guide

Run `merstride creation-guide` for directly readable offline authoring instructions,
a complete copyable workflow and a validation checklist embedded in the binary.
It executes nothing and needs no workflow, workspace, state or external files.
Use `--json` explicitly when consuming its `guide` string from an agent or script.
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

## Inline scripts and platforms

Instead of `command`/`args`, use `script: |` with a first-line shebang selecting
an already installed stdin-capable interpreter. For example:

```yaml
shared:
  kind: process
  timeoutSeconds: 10
  script: |
    #!/usr/bin/env -S python3 -X utf8 -
    print('Grüße 🌍')
platform:
  kind: process
  timeoutSeconds: 10
  variants:
  - platforms: windows
    script: |
      #!pwsh -NoLogo -NoProfile -NonInteractive -Command -
      Write-Output 'Windows'
  - platforms: [macos, linux]
    script: |
      #!bash --noprofile --norc
      printf 'macOS/Linux\n'
```

These fragments require Python 3 as `python3` on each selected platform, plus
PowerShell 7 on Windows and Bash on macOS/Linux. No interpreter is installed,
bundled or substituted. A shared script is portable only where its interpreter
and behavior are available. See the [complete inline example](../examples/inline-scripts.md).

An omitted `platforms` condition means all three platforms. Overlapping variants
are errors at validation, including an unconditional variant beside a specific
one. Conditions accept one lowercase name or a unique nonempty array; unknown
names and mixed execution forms fail. No applicable variant at advance routes
`spawn_error`, with `NO_PLATFORM_VARIANT` in the activation's stderr.

Shebang tokens use space/tab separators and single/double quotes for grouping;
backslashes stay literal, without shell escapes or expansion. Quote paths with
spaces, for example `#!"./tools with spaces/python.exe" -X utf8 -`. Relative
paths resolve against the saved workspace; bare names search inherited PATH
(Windows tries `.exe`, then `.com` for extensionless names; no PATHEXT/current
directory fallback). `env NAME` and `env -S NAME ARG...` resolve a bare name
directly without running env; other env forms fail validation.

The body after the shebang is sent as UTF-8 stdin then EOF, with no intermediate
script file. YAML literal-block chomping applies and line endings normalize to
LF. Authors supply the interpreter's stdin flags; PowerShell needs `-Command -`,
Python can use `-`, and Bash can consume stdin. File-only or interactive modes are
unsuitable; interpreter syntax/flag errors retain ordinary exit routing. There
is no separate data stdin. Missing/unlaunchable interpreters produce `spawn_error`
with `INTERPRETER_UNAVAILABLE` / `PROCESS_UNAVAILABLE` in stderr. Validation never
probes interpreter availability. All scripts and variants freeze at start and
share the existing logs, context, time/output/transition limits and recovery rules.

On Unix, PowerShell's `-Command -` may emit terminal control sequences despite
`-NonInteractive` and redirected output. For plain output, invoke advance from
a shell with `TERM=dumb`, for example `TERM=dumb merstride advance <run-id> --json`
(also pass the saved `--state-dir`). Merstride inherits that environment unchanged
and preserves raw process output; it does not filter interpreter control bytes.
An interpreter that exits before reading the complete body still retains its
own exit outcome; stdin pipe cleanup does not turn it into a storage error.

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
