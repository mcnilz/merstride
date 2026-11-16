# Merstride

Merstride is a small native CLI that executes workflows embedded in Markdown.
Mermaid defines control flow; YAML defines process commands, agent tasks and
terminal results. Use it for repeatable checks, repair loops and reviews while
keeping the workflow readable alongside your project documentation.

Execution is sequential and deterministic. Merstride runs process nodes until it
reaches an agent task, a terminal node or an error, then saves progress locally.
Your existing coding agent receives an explicit request with a literal prompt,
selected context and allowed outcomes. It performs the task with its own tools
and returns an outcome. Merstride has no built-in LLM integration, API key or
model session; you or your agent explicitly call `advance` to continue.

Commands print readable English text by default, including redirected output.
For example, `merstride creation-guide` prints a copyable authoring guide directly.
Agents and scripts must pass `--json` explicitly for the unchanged protocol-1
response; existing callers that relied on default JSON must add that flag.

## Quick start

Build from source using the [development prerequisites](CONTRIBUTING.md#prerequisites)
and `pwsh -NoProfile -File scripts/build.ps1`. The examples below use PowerShell
7.3+ from the repository root and the resulting Windows executable. On Linux,
use `artifacts/native/merstride` instead. Release qualification is still incomplete;
see [current status](docs/STATUS.md) before considering distribution.

First run a workflow with a single successful end node:

```powershell
$binary = (Resolve-Path artifacts/native/merstride.exe).Path
$workspace = (Get-Location).Path
$state = Join-Path $workspace '.merstride/runs'
& $binary --help
& $binary validate examples/single-end.md --json
$run = & $binary start examples/single-end.md --workspace $workspace --state-dir $state --json | ConvertFrom-Json
& $binary status $run.runId --state-dir $run.stateRoot --json
& $binary advance $run.runId --state-dir $run.stateRoot --json
```

`start` saves a ready run and executes nothing. `status` inspects it. `advance`
executes the selected node; this example returns `completed`.

For an existing-agent handoff, use the [agent task example](examples/agent-task.md):

```powershell
$run = & $binary start examples/agent-task.md --workspace $workspace --state-dir $state --json | ConvertFrom-Json
$current = & $binary advance $run.runId --state-dir $run.stateRoot --json | ConvertFrom-Json
$current.request | ConvertTo-Json -Depth 20
```

The response is `waiting_agent`. Read `request.id`, `prompt`, `context` and
`allowedOutcomes`, then perform the requested review. Once the review is complete
and `approved` is the appropriate allowed outcome, return it directly:

```powershell
& $binary complete $run.runId --request $current.request.id --outcome approved --state-dir $run.stateRoot --json
& $binary advance $run.runId --state-dir $run.stateRoot --json
```

Optional result data goes in one JSON argument, for example
`--data '{"summary":"Review completed"}'`; omitted data defaults to `{}`. Complete
saves a receipt and moves the cursor; the subsequent `advance` executes it.
Repeated inspection while waiting returns the same request. If a completion
response is lost, retry the same request ID with equivalent data. Inspect every
exit code and returned status in an integration; see the [agent operating loop](docs/AGENT-GUIDE.md).

Try the [inline scripts](examples/inline-scripts.md) for shared scripts and platform
variants, or the [process chain](examples/process-chain.md) for literal command arguments
and separate stdout/stderr logs, or the [review loop](examples/review.md) for
process/agent routing. Workflows execute with your privileges in the selected
workspace; review their commands before running them.

## Using Merstride

| Task | Reference |
| --- | --- |
| Write and validate workflows | `merstride creation-guide --json`, [authoring guide](docs/AUTHORING.md) and [examples](examples/) |
| Discover commands and exit codes | `merstride --help`, `merstride <command> --help`, [CLI usage](docs/CLI.md) |
| Integrate an existing agent or resume a run | `merstride guide --json`, [agent guide](docs/AGENT-GUIDE.md) |
| Handle interrupted execution | [Recovery example](examples/recovery.md) |
| Check exact behavior and limits | [Execution contract](docs/CONTRACT.md), [product scope](docs/SPEC.md) |
| Assess readiness and outstanding work | [Current status](docs/STATUS.md) |

## Developing Merstride

Start with [Contributing](CONTRIBUTING.md) for prerequisites, verification and the
approved-ticket workflow. [Architecture](docs/ARCHITECTURE.md),
[decisions](docs/adr/) and the [Gherkin acceptance features](features/) describe
the development boundaries. Project-owned code and documentation use the
[MIT license](LICENSE); shipped components retain their [third-party notices](THIRD-PARTY-NOTICES.md).
