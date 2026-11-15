# Merstride

Revision 0.5 — specification draft — 2026-11-15.

Project name: **Merstride**, accepted on 2026-11-15. CLI: `merstride`. See [naming decision](docs/NAMING.md). A small native CLI executes workflows embedded in Markdown. Mermaid defines control flow; YAML defines node behavior. An existing coding agent handles explicit agent tasks.

P01 implements a native single-end YAML workflow, persistent start/status/advance,
offline guide/version, and initial architecture/build policy checks. Later slices are
not yet implemented. See [current status](docs/STATUS.md) for verified results and limitations.

Build on Windows x64 with .NET SDK 10.0.404, PowerShell 7, and the Native AOT C++ toolchain:

```powershell
pwsh -NoProfile -File scripts/build.ps1
$binary = Resolve-Path artifacts/native/merstride.exe
$state = Join-Path (Get-Location) '.merstride/runs'
& $binary guide --json
$run = & $binary start examples/single-end.md --workspace (Get-Location).Path --state-dir $state --json | ConvertFrom-Json
& $binary status $run.runId --state-dir $state --json
& $binary advance $run.runId --state-dir $state --json
```

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

Development follows the installed [Matt Pocock skills](https://github.com/mattpocock/skills). No skills are bundled or installed by this package. .NET 10 Native AOT is the proposed implementation; Rust or Go remain alternatives if measured constraints require a change.

Revision 0.3 fixes MIT as the project license and selects NsDepCop for build-time architecture enforcement. See [Architecture and licensing](docs/ARCHITECTURE.md). Additional Gherkin scenarios verify forbidden dependencies and separation of the analyzer from release artifacts.

Revision 0.4 adds [GitHub build/release requirements](docs/GITHUB.md), an [established dependency policy](docs/DEPENDENCIES.md), naming research, and delivery acceptance scenarios. Workflows themselves will be implemented alongside the product; this remains a specification package.

Revision 0.5 adopts Merstride throughout the contract, examples, agent guide, namespace policy, and repository setup instructions.
