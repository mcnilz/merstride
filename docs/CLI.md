# Command-line usage

Use `merstride --help` or `merstride <command> -h` for English syntax, required
arguments/options, defaults, examples and exit codes. Every command has help.
Help, version and creation-guide read no workflow or state and launch no child process. They work
without a workspace, even with an unusable state path.

Every command prints readable English text by default, including when redirected.
Results go to stdout; errors and validation diagnostics/warnings go to stderr,
with severity, stable code and source location when available. Help hints accompany
usage errors; stack traces are hidden. A failed run's `status` still exits 0 and
shows its summary on stdout and failure reason on stderr.

Text `advance` also writes flushed live activity lines to stderr, including when
redirected. Each starts with the short run ID. Process lines identify the node and
visit, explain activation (launch pending) and the saved outcome. Separate numbered
`Flow #1: old -> new via ...` lines explain exact/default/unlabeled routing and
revisited destinations. Run-status changes and overall results are separate from
process exit codes. A saved completion announces acceptance and cursor movement once; retries show the
receipt without new activity. Inspection and repeated waiting/terminal advances
never replay transitions. Waiting/interrupted summaries explain the next action.
No-route results say the flow stays at the cursor. Explicit resolution describes
an unchanged cursor and no execution; retry requires a later advance and may repeat
external effects. Final summaries explain the status and overall result before
source/root details. Full activation IDs are secondary log selectors. See the
[before/after walkthrough](FLOW-OUTPUT.md) for branches, loops and recovery.

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

merstride status r_bad-id
Error INVALID_RUN_ID: Use a run ID from runs: r_ followed by 6..32 lowercase hexadecimal digits.
```

The last example writes its error to stderr and exits 4; the first two write
results to stdout and exit 0. Add `--json` for the protocol object instead.

Quote each path containing spaces as a single shell argument. After `--`, every
remaining token is a literal positional argument, including `--help`. All options
must precede it. For an option value beginning with `-`, use the equals form:
`--input-file=--input.json`. Response-file expansion (`@file`), parser directives,
abbreviations and single-character option bundles are disabled. An at-sign path
is an ordinary filename. Nothing implicitly launches a shell.

Bash walkthrough with `merstride` on PATH, earlier runs allowed, and a reviewed workflow named
`workflow with spaces.md`. Read standard output directly
and paste the request ID when prompted. No JSON utility is required.

```bash
set -euo pipefail
merstride --help
merstride start --help
merstride validate -- 'workflow with spaces.md'
merstride start 'workflow with spaces.md'
merstride advance 'workflow with spaces.md'
merstride status 'workflow with spaces.md'
```

Only if the output shows `Status: waiting_agent`, read `Request`, `Prompt`,
`Allowed outcomes` and `Context`, and perform the literal task with your existing
agent. After completing the review, if `approved` is the appropriate allowed
outcome, submit it (using the same Bash session):

```bash
read -r -p 'Paste the Request ID shown above: ' request
merstride complete 'workflow with spaces.md' --request "$request" --outcome approved
merstride advance 'workflow with spaces.md'
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

Start creates and selects a distinct ready run, retaining every earlier run.
Ordinary `status`, `advance`, `resolve` and `logs` use that selection, including
terminal runs. `status <file>` and `advance <file> [--workflow <id>]` continue the remembered run for
that source/workflow without changing project selection or rereading its source.
Multiple workflows in one file require --workflow. Paths are normalized absolute
paths with native case comparison; identical IDs in different files remain
distinct. Changed/missing files keep their frozen run; renamed paths are new
associations. `runs` lists source paths, workflow IDs, statuses and current or
discarding markers. `use <run-id>` switches selection deliberately. Explicit run
IDs override context; --file is an alternative spelling and cannot be combined with a positional selector. Ordinary explicit calls
do not change selection. Subdirectories and callers sharing a state root share
its context; explicit roots have independent context.

The optional positional selector is shared by status, advance, complete, resolve
and logs. It is interpreted lexically: bare r_ tokens without path separators
select run IDs; every other token is a source path, including extensionless names.
The source need not still exist. Malformed r_ IDs return INVALID_RUN_ID (4);
unknown source associations STALE_CONTEXT (4); invalid path syntax USAGE (2).
Use ./r_000001 or --file r_000001 for a filename that looks like an ID. Quote
paths containing spaces; -- permits dash-prefixed literal paths. No filesystem
existence guessing changes the meaning. --workflow requires a source path.
Extra positionals or positional/--file combinations return USAGE (2).
runs [file] [--workflow <id>] filters inventory by source; --file is equivalent.
use/discard require explicit run IDs and never infer a workflow deletion target.

New run/request IDs have eight characters: `r_`/`q_` plus six lowercase hex
digits. IDs are predictable, opaque, not secrets, and permanently reserved within
their root even after discard. At 0xffffff allocations they lengthen (maximum
sixteen hex digits). Collisions skip occupied reservations/directories without
overwriting state; crashes can consume IDs. Activation/log IDs remain full.
Legacy full IDs and unique prefixes with six or more hex digits remain accepted.
Exact stored IDs win over prefix convenience even if longer IDs begin with them.
Ambiguous prefixes return AMBIGUOUS_RUN/AMBIGUOUS_REQUEST (4); use exact IDs in
automation. Protocol-1 JSON keeps opaque IDs and gains additive source/inventory/
discard fields. Do not assume UUID length. Existing saved snapshots/full IDs work;
older binaries cannot read newly adopted P19 roots. No automatic relocation.

--request remains mandatory. Without a run/file selector, completion finds that
request across current requests and historical receipts in this root, regardless
of changing current-run context. Unreadable states fail the search closed; an
explicit run ID bounds it. Historical retries return only their original receipt
or COMPLETION_CONFLICT; they never complete newer requests. There is no latest
request selector. Keep exact run/request IDs and explicit roots in automation.

Missing/stale/cleared selection returns STALE_CONTEXT (4), corrupt context
CORRUPT_CONTEXT (6), with recovery guidance and no fallback. `runs` remains usable
for inventory; `use` can repair corrupt selection. Only legacy roots with no
context file retain exactly-one-run fallback. Multiple runs without context return
AMBIGUOUS_RUN (4); missing convenience matches RUN_NOT_FOUND (4). Missing selected
snapshots/corrupt state/storage conflicts fail closed. Direct full-ID missing
snapshots retain I/O exit 6. A .merstride file is an I/O conflict; preserve it and
choose an explicit root. Inspection never creates state or launches children.

Start/use/discard serialize context changes; competing writers return
CONTEXT_LOCKED (4). Request allocation may return IDENTITY_LOCKED (4); retry
explicitly. A command resolves one run before locking it; later context changes
cannot redirect it. Atomic context-save failure can leave a new ready run visible
in runs; recover with use. For simultaneous workflows, prefer --file or exact IDs.

### Deliberate cleanup

Start retains earlier runs. Inspect `runs [file]`, then permanently
remove one chosen ID with `discard <run-id>`. This removes only that run's state,
logs and receipts. It does not kill processes or undo workspace effects; inspect
uncertain/interrupted work first. Ready/waiting/interrupted/terminal runs can be
discarded deliberately. Held locks return RUN_LOCKED (4). Corrupt/unreadable state,
unexpected files/directories and symlinks/reparse points are refused (6).

Discard marks the validated run under its lock, preventing new execution with
DISCARD_PENDING (4), then stages it under this root's .trash. Successful removal
clears only its context references, retaining other runs and identity reservations.
Missing current selection requires start/use; no survivor is silently selected.
Failure is reported (6), with no success claim; runs shows staged discards and
the same explicit discard ID retries them. Unknown content remains for
investigation. Authoritative state is removed last; a minimal ownership record
allows retries even if only the empty stage or record remains. It contains no
logs or completion data and is removed after cleanup. Inventory warns about
corrupt context/staging conflicts while retaining readable live rows.
See [the decision](adr/0015-p19.md).

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
