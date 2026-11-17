# Start and first advance in one invocation

Use the explicit optional `--advance` flag when creation should immediately run
the workflow. It performs the same first synchronous advance as the separate
command: several process nodes can run before an agent handoff, terminal result
or error. Plain start still creates a ready run without executing any node.
Complete still saves only a receipt and cursor movement; advance again explicitly.

With `merstride` on PATH, these Bash alternatives reach the first review request
in `examples/review.md`. Use one alternative, read the actual request and perform
its task before completing it. Human walkthroughs need no JSON utility.

```bash
# Separate preparation and execution: two native invocations.
merstride start examples/review.md
merstride advance examples/review.md
```

```bash
# Explicit immediate execution: one native invocation.
merstride start examples/review.md --advance
```

Both alternatives retain earlier runs. The second creates/selects a distinct run
and advances exactly that ID, even if other starts change current/workflow
selection while it executes. A start without `--advance` remains useful for
preparing or inspecting a run before execution. Input/workflow/workspace/state
options and frozen snapshots are shared by both alternatives.

Representative combined text for a two-process workflow, with illustrative IDs
and secondary source/root/log details omitted:

```text
stderr:
[r_000003] Created new ready run; no node has executed. Initial advance requested.
[r_000003] Process first (visit 1): activating (launch pending).
[r_000003] Process first (visit 1): saved exit code 0 (exit_0).
[r_000003] Flow #1: first -> second via exact outcome exit_0.
[r_000003] Process second (visit 1): activating (launch pending).
[r_000003] Process second (visit 1): saved exit code 0 (exit_0).
[r_000003] Flow #2: second -> done via exact outcome exit_0.
[r_000003] Run ready -> completed at done: success end; overall result: success.

stdout:
Creation: saved new run
Phase: advance
Initial advance: finished; see the resulting run status below
Run: r_000003
Workflow: combined
Status: completed (finished successfully)
Cursor: done
Result: success
Transitions: 2
Last process: second - exit code 0 (exit_0)
Next: no further execution for this run. Start a new run to execute again.
```

Activity order is guaranteed within stderr, without cross-stream ordering.
Creation says only that the ready snapshot was saved. It never claims context
selection or execution succeeded. A failed workflow still says initial advance
`finished` because the engine returned normally; its status/result/error and exit
code describe the failure. JSON emits no human activity.

## Automated JSON integration

This Bash example requires `jq`, uses explicit protocol-1 JSON and preserves a
nonzero CLI exit alongside its coherent response. Run it in the repository or
adapt the workflow path. On Windows, Git Bash's `cygpath` supplies native absolute
paths. A single Merstride invocation both creates and first advances; jq reads
the response locally and makes no extra engine/tool call.

```bash
set -euo pipefail
workspace=$(pwd -P)
case "$OSTYPE" in msys*|cygwin*) workspace=$(cygpath -am "$workspace") ;; esac
state="$workspace/.merstride/runs"
code=0
if reply=$(merstride start examples/review.md --advance \
    --workspace "$workspace" --state-dir "$state" --json); then
    code=0
else
    code=$?
fi
printf '%s\n' "$reply"
# Inspect identity, phase, final status/request and error even when code is nonzero.
jq '{runId, stateRoot, start, status, request, error}' <<< "$reply"
if (( code != 0 )); then
    printf 'CLI stopped with exit %s; inspect this exact run before recovery.\n' "$code" >&2
    exit "$code"
fi
run_id=$(jq -er '.runId' <<< "$reply")
state_root=$(jq -er '.stateRoot' <<< "$reply")
case $(jq -er '.status' <<< "$reply") in
    waiting_agent) jq '.request' <<< "$reply" ;;
    completed) printf 'Run completed.\n' ;;
    *) printf 'Unexpected successful status; inspect the response.\n' >&2; exit 6 ;;
esac
```

Retain exact IDs/root and follow the [agent loop](AGENT-GUIDE.md). Update strict
response allowlists to accept additive `start` metadata; other responses have
null `start`. Existing fields and protocol version stay 1. There is no saved-state
schema migration. One native invocation replaces two; token/context savings have
not been measured.

## Partial success, cancellation and recovery

| Phase/result | What remains | Next action |
| --- | --- | --- |
| Argument/validation/input/preparation/initial-save failure | No node executed; initial creation was not confirmed | Correct the cause; inspect existing state if persistence failed |
| Creation saved, selection save failed | New ready run, exact ID/root in response; no initial advance | Restore storage access, inspect that ID; use/advance explicitly |
| Cancellation observed at the check before acquiring the advance lock | New ready run, INTERRUPTED error and exit 130, `advance: not_started` | Advance that ID explicitly when intended; no resolve needed |
| First advance returns waiting/terminal | Normal saved request/status, `advance: finished` | Fulfill the request or report the terminal result; failure exits 5 |
| Operational advance error | Exact ID/root and durable progress; fresh status if readable, otherwise null | Inspect status/logs/effects, then continue or resolve explicitly |
| Signal after the pre-advance check | Ordinary interruption, exit 130; a recovery-save error says so | Inspect effects and explicitly resolve interrupted work |
| Forced engine termination | Response may be absent; saved run remains | Inspect runs/status in the original root; never assume rollback |

The supported signal handler begins before combined preparation. Cancellation
observed before creation creates nothing. Once creation starts, its durable
creation/selection attempt completes before the pre-advance check. A later signal
uses ordinary advance semantics even if it arrives before the first activation.
`start.created` confirms the initial ready snapshot; `phase` names where the
operation stopped. `advance: error` means an operational exception after entering
the engine; refusal before engine entry remains `not_started`. Creation and
external effects form no atomic transaction. No run deletion, rollback, automatic
retry or replay occurs. Repeating start always creates another run.

For example, request-allocation lock failure after a successful process may return
exit 4, `created: true`, `phase: advance`, `advance: error`, `status: ready`, and
cursor at the agent. Restoring access and advancing that exact run creates its
request without repeating the process. A rejected process-result save instead
leaves uncertain effects and projects interrupted; inspect before explicit resolve.
Run locks, time/output/transition/context limits, error codes, log selection and
completion receipts retain ordinary advance semantics.

See [the contract](CONTRACT.md#optional-initial-execution), [decision](adr/0017-p21.md)
and [actual verification scope](STATUS.md). Native interruption tests exercise a
running attached tree, not precisely timed signals between individual saves.
