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

For binary downloads and platform requirements, see
[download and run](docs/RELEASING.md#download-and-run). Releases use SemVer tags
and GitHub Actions. Windows x64 and Linux x64 have local verification. The macOS
15+ Apple Silicon build, native acceptance, packaging and extracted-archive tests
without system .NET passed on GitHub-hosted runners; see [current evidence](docs/STATUS.md).
Overall release qualification and publication remain incomplete.

Build from source using the [development prerequisites](CONTRIBUTING.md#prerequisites)
and `pwsh -NoProfile -File scripts/build.ps1`. The examples below use Bash and
`jq` from the repository root (Git Bash on Windows). On Linux/macOS, use the
matching native build; on Windows, select `merstride.exe` as shown below. `jq`
is a caller-side JSON utility, not an engine dependency. Release qualification is still incomplete;
see [current status](docs/STATUS.md) before considering distribution.

First run a workflow with a single successful end node:

```bash
set -euo pipefail
workspace=$(pwd -P)
binary="$workspace/artifacts/native/merstride"
if [[ -f "$binary.exe" ]]; then
    binary="$binary.exe"
    workspace=$(pwd -W) # Git Bash: native Windows path for --workspace/--state-dir.
fi
state="$workspace/.merstride/runs"
"$binary" --help
"$binary" validate examples/single-end.md --json
run=$("$binary" start examples/single-end.md --workspace "$workspace" --state-dir "$state" --json)
run_id=$(jq -er '.runId' <<< "$run")
state_root=$(jq -er '.stateRoot' <<< "$run")
"$binary" status "$run_id" --state-dir "$state_root" --json
"$binary" advance "$run_id" --state-dir "$state_root" --json
```

`start` saves a ready run and executes nothing. `status` inspects it. `advance`
executes the selected node; this example returns `completed`.

For an existing-agent handoff, use the [agent task example](examples/agent-task.md):

```bash
run=$("$binary" start examples/agent-task.md --workspace "$workspace" --state-dir "$state" --json)
run_id=$(jq -er '.runId' <<< "$run")
state_root=$(jq -er '.stateRoot' <<< "$run")
current=$("$binary" advance "$run_id" --state-dir "$state_root" --json)
jq -e '.status == "waiting_agent"' <<< "$current"
jq '.request' <<< "$current"
request_id=$(jq -er '.request.id' <<< "$current")
```

The response is `waiting_agent`. Read `request.id`, `prompt`, `context` and
`allowedOutcomes`, then perform the requested review. Once the review is complete
and `approved` is the appropriate allowed outcome, return it directly:

```bash
"$binary" complete "$run_id" --request "$request_id" --outcome approved --state-dir "$state_root" --json
"$binary" advance "$run_id" --state-dir "$state_root" --json
```

Optional result data goes in one JSON argument, for example
`--data '{"summary":"Review completed"}'`; omitted data defaults to `{}`. Complete
saves a receipt and moves the cursor; the subsequent `advance` executes it.
Repeated inspection while waiting returns the same request. If a completion
response is lost, retry the same request ID with equivalent data. Inspect every
exit code and returned status in an integration; the setup enables Bash fail-fast
handling for this success path. See the [agent operating loop](docs/AGENT-GUIDE.md)
for handling failures and other statuses explicitly.

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
