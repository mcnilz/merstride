# Merstride

Revision 0.5 — specification draft — 2026-11-15.

Project name: **Merstride**, accepted on 2026-11-15. CLI: `merstride`. See [naming decision](docs/NAMING.md). A small native CLI executes workflows embedded in Markdown. Mermaid defines control flow; YAML defines node behavior. An existing coding agent handles explicit agent tasks.

P01–P04 implement native sequential process/agent/end workflows, persistent
start/status/advance/complete, selected context, idempotent completion receipts,
separate raw-byte logs, offline guide/version, and initial architecture/build policy
checks, multi-workflow selection, and supported authoring validation.
See [current status](docs/STATUS.md) for verified results and limitations.

Build on Windows x64 with .NET SDK 11.0.100, PowerShell 7, and the Native AOT C++ toolchain:

```powershell
pwsh -NoProfile -File scripts/build.ps1
$binary = Resolve-Path artifacts/native/merstride.exe
$state = Join-Path (Get-Location) '.merstride/runs'
& $binary guide --json
$run = & $binary start examples/single-end.md --workspace (Get-Location).Path --state-dir $state --json | ConvertFrom-Json
& $binary status $run.runId --state-dir $state --json
& $binary advance $run.runId --state-dir $state --json
```

Try a real process chain in this Git workspace:

```powershell
$run = & $binary start examples/process-chain.md --workspace (Get-Location).Path --state-dir $state --json | ConvertFrom-Json
$result = & $binary advance $run.runId --state-dir $state --json | ConvertFrom-Json
& $binary logs $run.runId --activation $result.lastActivation.id --stream stdout --state-dir $state --json
```

For an existing-agent task, start [the task example](examples/agent-task.md) and
advance to `waiting_agent`. Read the returned literal prompt, selected context, and
sorted allowed outcomes. After performing the task, write a UTF-8 JSON result such
as `{"outcome":"approved","data":{"summary":"Review completed"}}`, then run:

```powershell
& $binary complete $run.runId --request $current.request.id --result-file result.json --state-dir $state --json
& $binary advance $run.runId --state-dir $state --json
```

Here `$run` is the task's start response and `$current` its advance response.
Complete saves a receipt and moves only the cursor; the next advance executes it.
Repeated status/advance while waiting returns the same request. Retry a lost
completion response with the same request ID and equivalent payload; historical
receipts never complete a newer request. Optional start `--input-file` supplies a
frozen JSON object. Input and completion data are limited to 16 KiB and depth 16.

Process arguments are literal strings; PowerShell requires an explicit `pwsh`
command. Exact outcome edges precede `default`; unmatched outcomes fail the run.
Start and status execute nothing. Logs retain child bytes separately from CLI JSON;
timeout and output limits attempt process-tree termination. P02 has basic limits,
while full boundary and recovery qualification belongs to P05/P06. The unreleased
P02 state shape requires new runs; P01 saved runs are not migrated.

All current CLI output is protocol JSON, with or without `--json`. Unsupported commands
return usage errors; unsupported workflow syntax returns document diagnostics.
The complete review workflow example below belongs to later slices.

## Reading order

1. [Product specification](docs/SPEC.md)
2. [Execution contract](docs/CONTRACT.md)
3. [Workflow authoring guide](docs/AUTHORING.md)
4. [Agent guide](docs/AGENT-GUIDE.md)
5. [Acceptance testing](docs/TESTING.md) and `features/*.feature`
6. [Implementation plan](docs/IMPLEMENTATION.md)
7. [Repository start prompt](STARTPROMPT.md)

[Complete example](examples/review.md).

All project specifications, source identifiers, comments, tests, scenarios, tickets, documentation, CLI messages, and commit messages are written in English. User-authored workflow labels, prompts, inputs, and results may contain arbitrary supported UTF-8 text.

Revision 0.2 replaced the draft JSON metadata fence with YAML, added author and agent guides, and turned acceptance requirements into Gherkin scenarios with an automation plan. The metadata schema and CLI protocol remain draft version 1; this is not a migration for a released product.

Development follows the installed [Matt Pocock skills](https://github.com/mattpocock/skills). No skills are bundled or installed by this package. .NET 11 Native AOT with stable C# 15 is the accepted implementation; see docs/adr/0004-m01-dotnet11.md.

Revision 0.3 fixes MIT as the project license and selects NsDepCop for build-time architecture enforcement. See [Architecture and licensing](docs/ARCHITECTURE.md). Additional Gherkin scenarios verify forbidden dependencies and separation of the analyzer from release artifacts.

Revision 0.4 adds [GitHub build/release requirements](docs/GITHUB.md), an [established dependency policy](docs/DEPENDENCIES.md), naming research, and delivery acceptance scenarios. Workflows themselves will be implemented alongside the product; this remains a specification package.

Revision 0.5 adopts Merstride throughout the contract, examples, agent guide, namespace policy, and repository setup instructions.
