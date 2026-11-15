# Command-line usage

Use `merstride --help` or `merstride <command> -h` for English syntax, required
arguments/options, defaults, examples and exit codes. All nine commands have help.
Help and version read no workflow or state and launch no child process. They work
without a workspace, even with an unusable state path.

Help normally prints text. With `--json`, it returns one protocol-version-1 JSON
object with the help in `guide`, null `error` and exit 0. `version` and the root
`--version` option always return the existing JSON version response. Operational
commands and errors continue to return one JSON object with or without `--json`;
there are no banners, ANSI sequences, progress messages or stack traces on stdout.

`--json` and `--state-dir <absolute-path>` are global: put them before or after the
command, before any `--` delimiter. Command-specific options stay with their
command. Every option, including a boolean or a help alias, may occur only once.
Unknown options/commands, missing values or required arguments/options, extra
positionals and conflicting recovery flags return `USAGE`, exit 2 and a help hint.
Help waives required workflow/run/options, but malformed or duplicated options
still fail. `--version` cannot be combined with a command or help; use the `version`
command for command-form discovery.

Quote each path containing spaces as a single shell argument. After `--`, every
remaining token is a literal positional argument, including `--help`. All options
must precede it. For an option value beginning with `-`, use the equals form:
`--input-file=--input.json`. Response-file expansion (`@file`), parser directives,
abbreviations and single-character option bundles are disabled. An at-sign path
is an ordinary filename. Nothing implicitly launches a shell.

PowerShell example for inspection and a complete existing-agent loop:

```powershell
$binary = 'C:\tools\merstride.exe'
$workspace = (Get-Location).Path
$state = Join-Path $workspace '.merstride/runs'
& $binary --help
& $binary start --help --json
& $binary --json validate -- 'workflow with spaces.md'
$run = & $binary start 'workflow with spaces.md' --workspace $workspace --state-dir $state --json | ConvertFrom-Json
$current = & $binary advance $run.runId --state-dir $run.stateRoot --json | ConvertFrom-Json
& $binary status $run.runId --state-dir $run.stateRoot --json
# If waiting_agent: perform the literal requested task with your existing agent.
# Write an allowed outcome and optional object data to a UTF-8 completion file.
& $binary complete $run.runId --request $current.request.id --result-file 'agent result.json' --state-dir $run.stateRoot --json
& $binary advance $run.runId --state-dir $run.stateRoot --json
# If interrupted: inspect external effects, then choose exactly one explicit resolution.
& $binary resolve $run.runId --retry --state-dir $run.stateRoot --json
& $binary advance $run.runId --state-dir $run.stateRoot --json
```

Use `resolve --fail` instead of `--retry` to finish failed without executing.
Do not complete a run unless its response contains a waiting request, or resolve
unless it is interrupted. `start` creates ready without executing; `complete`
moves only the cursor. Reuse the returned absolute `stateRoot` on subsequent calls.
On Linux use an absolute Linux workspace/state path; ordinary single-quoted paths
work in PowerShell there too. The workflow's process arguments remain literal.

Exit codes: 0 successful operation/inspection/help/version; 2 usage; 3 document
validation; 4 state/lock/completion conflict; 5 failed/interrupted advance or
`resolve --fail`; 6 I/O/corrupt storage/internal error; 130 user interruption.
Failed-run `status` returns 0. Child exit codes do not become CLI exit codes.

System.CommandLine 3.0.0 supplies parsing and generated help. It is confined to
Cli and adds no selected transitive runtime packages. See the
[decision](adr/0008-p08.md), [dependency review](dependency-inventory.json), and
[verification status](STATUS.md). Release publication remains blocked by P07's
unfinished external qualification.
