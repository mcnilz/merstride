# Command-line usage

Use `merstride --help` or `merstride <command> -h` for English syntax, required
arguments/options, defaults, examples and exit codes. All ten commands have help.
Help, version and creation-guide read no workflow or state and launch no child process. They work
without a workspace, even with an unusable state path.

Every command prints readable English text by default, including when redirected.
Results go to stdout; errors and validation diagnostics/warnings go to stderr,
with severity, stable code and source location when available. Help hints accompany
usage errors; stack traces are hidden. A failed run's `status` still exits 0 and
shows its summary on stdout and failure reason on stderr.

**Existing scripts must add `--json` explicitly.** It returns exactly one unchanged
protocol-version-1 JSON object on stdout, including for help, version and errors.
Help/guides use `guide`; version/root `--version` use `version`. JSON errors remain
inside the response, with no duplicate text on stderr. No colors, ANSI sequences,
banners or progress messages are added. Protocol version 1 is unchanged.

Use `merstride creation-guide --json` for self-contained authoring instructions,
a complete Markdown workflow and a validation checklist. The text is embedded in
the native executable and needs no checkout, external content file or network.
Without `--json`, the argument-free command prints the instructions directly with
real line breaks and a copyable workflow. With `--json`, they are in a nonempty
`guide` string, with null status/error and exit 0. Both guides create no state and
ignore an unusable state path. Use `merstride guide --json` for the agent loop.
Both commands support `-h` / `--help` and the global options described below.

`--json` and `--state-dir <absolute-path>` are global: put them before or after the
command, before any `--` delimiter. Command-specific options stay with their
command. Every option, including a boolean or a help alias, may occur only once.
Unknown options/commands, missing values or required arguments/options, extra
positionals and conflicting recovery flags return `USAGE`, exit 2 and a help hint.
Help waives required workflow/run/options, but malformed or duplicated options
still fail. `--version` cannot be combined with a command or help; use the `version`
command for command-form discovery.

Run summaries show run ID, state root, status, cursor, transitions and the latest
activation. Waiting-agent output adds request ID, node, literal prompt, allowed
outcomes and selected context, including unavailable/truncated distinctions.
Full history and full process logs stay out of ordinary status. Use `logs` to read
a selected tail (at most 65536 input bytes), with activation, stream and tail
truncation labels. Text preserves Unicode, newlines and tabs, normalizes CRLF to
LF, and renders other control/Unicode format characters as visible `\uXXXX`
escapes. Raw logs are unchanged; terminal control sequences do not execute.
Errors with `--json` before `--`, including duplicate/malformed JSON options,
remain JSON; `--json` after `--` is literal positional text.

Representative default output from the development binary:

```text
merstride version
Merstride 0.1.0 (protocol 1)

merstride validate examples/single-end.md
Valid workflow: smoke

merstride status bad-id
Error INVALID_RUN_ID: Invalid run identifier.
```

The last example writes its error to stderr and exits 4; the first two write
results to stdout and exit 0. Add `--json` for the protocol object instead.

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
# Return an allowed outcome directly; --data is optional and defaults to {}.
& $binary complete $run.runId --request $current.request.id --outcome approved --data '{"summary":"Review completed"}' --state-dir $run.stateRoot --json
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

Prefer `complete <run-id> --request <id> --outcome <allowed-outcome>` for agent
feedback. Add `--data <json-object>` only when result data is needed. Pass the JSON
as one literal argument: in PowerShell 7.3+ with standard native argument passing,
use single quotes around JSON or build a value with `ConvertTo-Json -Compress`.
This creates no completion file. Data uses the existing 16 KiB compact UTF-8,
depth-16 and safe-number limits. Invalid JSON/data or a rejected request/outcome
exits 4 without changing state; invalid option combinations exit 2.

For existing file-based callers, use `--result-file <file>` instead of `--outcome`
and `--data`. That UTF-8 JSON file still contains required `outcome` and optional
object `data`. Never combine the two modes. Equivalent parsed completions are
idempotent across modes and return the same receipt. Shell/OS argument-size limits
still apply; file mode remains useful for payloads unsuitable for command arguments.
Arguments may be retained in shell history or visible to local process inspection;
choose file mode when that matters for the data being returned.

Exit codes: 0 successful operation/inspection/help/version; 2 usage; 3 document
validation; 4 state/lock/completion conflict; 5 failed/interrupted advance or
`resolve --fail`; 6 I/O/corrupt storage/internal error; 130 user interruption.
Failed-run `status` returns 0. Child exit codes do not become CLI exit codes.

System.CommandLine 3.0.0 supplies parsing and generated help. It is confined to
Cli and adds no selected transitive runtime packages. See the
[decision](adr/0008-p08.md), [dependency review](dependency-inventory.json), and
[verification status](STATUS.md). Release publication remains blocked by
unfinished external qualification.
