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

Text `advance` also writes flushed live activity lines to stderr, including when
redirected. Each identifies the run/node and, when applicable, activation. It shows
activation (launch pending), saved outcome/route, agent waiting and terminal or
interrupted state. A saved completion announces acceptance once; retries show the
receipt without new activity. Inspection and repeated waiting/terminal advances
never replay transitions. Waiting/interrupted summaries explain the next action.

Use `merstride advance "$run_id" --state-dir "$state_root" >summary.txt 2>activity.txt`
in Bash to separate the final summary from activity/errors. Ordering is preserved
within activity.txt; separate streams have no cross-stream ordering guarantee.
There are no periodic messages or streamed child logs. Activity is bounded by the
transition limit and uses the same escaping as summaries. A closed activity pipe
disables further activity without changing execution. See the
[README transcript](../README.md#quick-start) for a process chain and handoff.

**Existing scripts must add `--json` explicitly.** It returns exactly one unchanged
protocol-version-1 JSON object on stdout, including for help, version and errors.
Help/guides use `guide`; version/root `--version` use `version`. JSON errors remain
inside the response, with no duplicate text on stderr. No colors, ANSI sequences,
banners or progress messages are added. Protocol version 1 is unchanged.

Use `merstride creation-guide` for self-contained authoring instructions,
a complete Markdown workflow and a validation checklist. The text is embedded in
the native executable and needs no checkout, external content file or network.
Without `--json`, the argument-free command prints the instructions directly with
real line breaks and a copyable workflow. With `--json`, they are in a nonempty
`guide` string, with null status/error and exit 0. Both guides create no state and
ignore an unusable state path. Use `merstride guide` for the agent loop.
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

Bash walkthrough with `merstride` on PATH, no earlier project runs, and a reviewed workflow named
`workflow with spaces.md`. Read standard output directly
and paste the short request prefix when prompted. No JSON utility is required.

```bash
set -euo pipefail
merstride --help
merstride start --help
merstride validate -- 'workflow with spaces.md'
merstride start 'workflow with spaces.md'
merstride advance
merstride status
```

Only if the output shows `Status: waiting_agent`, read `Request`, `Prompt`,
`Allowed outcomes` and `Context`, and perform the literal task with your existing
agent. After completing the review, if `approved` is the appropriate allowed
outcome, submit it (using the same Bash session):

```bash
read -r -p 'Paste the short Request prefix shown above: ' request
merstride complete --request "$request" --outcome approved
merstride advance
```

Use `resolve --fail` instead of `--retry` to finish failed without executing.
Do not complete a run unless its response contains a waiting request, or resolve
unless it is interrupted. `start` creates ready without executing; `complete`
moves only the cursor. If you use an explicit `--state-dir`, reuse it on subsequent calls.
The examples stop on a nonzero CLI exit; a failed/interrupted advance must
be inspected before continuing. See the [agent guide](AGENT-GUIDE.md#bash-example)
for automatic JSON extraction and error/status handling, and [recovery](../examples/recovery.md) for
the separate interruption decision. Use native absolute paths on each OS;
Git Bash/Cygwin's `cygpath -am` supplies Windows paths. Workflow process arguments remain literal.

## Project storage and selection

Default workspace and storage use the nearest directory, walking upward from
cwd, with a `.merstride` or `.git` entry. A Git worktree/submodule `.git` file
counts; nested Git boundaries stop discovery. With no marker, use cwd. State
lives under `.merstride/runs`. A first start creates the project marker and adds
`/runs/` to `.merstride/.gitignore` if absent, retaining all existing bytes and
leaving parent ignore/config files alone. There is no configuration/environment
source. Explicit absolute `--workspace` overrides process cwd only; explicit
absolute `--state-dir` overrides storage only. Explicit external storage needs
its own ignore policy. Relative workflow paths remain relative to the caller.

Existing cwd-relative `.merstride` roots keep their state, and calls from child
directories find that boundary. No automatic move, merge, deletion or cleanup
occurs. Outside a project, start in the intended root first: before first use,
a markerless child is an independent cwd. If an older run lives elsewhere, use
its original absolute `--state-dir`. Summaries show the actual absolute root.

Run commands accept full IDs, or unique `r_` prefixes of 8..32 lowercase hex
digits. An omitted run selects exactly one full-ID run directory, including
terminal, corrupt and incomplete runs. Multiple matches yield AMBIGUOUS_RUN,
missing convenience matches RUN_NOT_FOUND, invalid syntax INVALID_RUN_ID (exit 4).
Full-ID calls retain their direct I/O errors (exit 6 for missing state). No newest
run or active-only fallback exists. Text prints a suggested eight-hex `Run prefix`
beside the full ID. Lengthen a prefix if it collides, or use the full ID.

`--request` is always mandatory; full IDs and `q_` prefixes of 8..32 lowercase
hex digits work. Selection under the run lock includes both current requests and
all historical receipts. Ambiguity yields AMBIGUOUS_REQUEST, invalid/missing
matches INVALID_REQUEST (exit 4). The short `Request prefix` never means
"whatever is waiting now": a historical retry returns only its original receipt,
or COMPLETION_CONFLICT for different data, and leaves newer requests waiting.
Automation can retain full IDs and explicit roots with unchanged protocol-1 JSON.

Missing state is not created by inspection or selection; use start for first
use. A missing snapshot, corrupt selected run, file/directory conflict or
unusable root returns exit 6 and never falls back to another root. A `.merstride`
file is a storage conflict; preserve it and choose an explicit root. Failed start
preparation can leave an empty marker/ignore rule, but never resets saved runs.
Concurrent starts can encounter exclusive ignore-file access; retry explicitly.
Discovery/help/guides remain independent of workflow and storage.

Prefer `complete <run-id> --request <id> --outcome <allowed-outcome>` for agent
feedback. Add `--data <json-object>` only when result data is needed. Pass the JSON
as one literal argument: in Bash, use single quotes around literal JSON. For
dynamic strings, build the object with `data=$(jq -cn --arg summary "$summary"
'{summary: $summary}')` and pass `--data "$data"`; `jq --arg` handles JSON escaping.
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
