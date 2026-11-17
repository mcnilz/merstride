# Following a run in the console

These representative native-CLI excerpts compare P17/P19 wording with P20.
Run/request IDs are normalized; long activation IDs and source/root/log-selector
details are omitted here. The branch uses the P20 native fixture; the loop and
recovery use native helpers with observable effects and controlled interruption.
Commands use Bash. Activity is stderr; selected final summary fields are stdout.
There is no ordering guarantee between separately captured streams.

## Branches and overall results

Before, the branch route appeared beside a long activation identity, without the
reason for choosing it. A process exit code was the only outcome explanation:

```text
Activity [r_000001, node first, activation a_...]: Activating process (launch pending).
Activity [r_000001, node first, activation a_...]: Process outcome saved: exit_0. Route: first -> second.
Activity [r_000001, node second, activation a_...]: Activating process (launch pending).
Activity [r_000001, node second, activation a_...]: Process outcome saved: exit_7. Route: second -> done.
Activity [r_000001, node done]: Run completed.
```

After `merstride advance branch.md >summary.txt 2>activity.txt`, activity.txt shows:

```text
[r_000001] Process first (visit 1): activating (launch pending).
[r_000001] Process first (visit 1): saved exit code 0 (exit_0).
[r_000001] Flow #1: first -> second via exact outcome exit_0.
[r_000001] Process second (visit 1): activating (launch pending).
[r_000001] Process second (visit 1): saved exit code 7 (exit_7).
[r_000001] Flow #2: second -> done via default (no exact match for exit_7).
[r_000001] Run ready -> completed at done: success end; overall result: success.
```

The selected stdout fields explain why exit 7 does not mean this run failed:

```text
Run: r_000001
Workflow: progression
Status: completed (finished successfully)
Cursor: done
Result: success
Transitions: 2
Last process: second - exit code 7 (exit_7)
Next: no further execution for this run. Start a new run to execute again.
```

An unlabeled process edge explicitly says it accepts any process outcome,
including failures. Timeout, output-limit and launch-failure results have English
explanations alongside their routing tokens. An agent may choose these same names;
its summary always calls them agent outcomes, without implying a process event.

## Agent loop and receipt retries

Before, a completion showed request -> outcome, but no source/destination:

```text
Activity [r_000001, node review, activation a_...]: Waiting for agent; request q_000002. Read the prompt and return an allowed outcome with complete.
Activity [r_000001, node work]: Completion accepted and saved: q_000002 -> revise. Call advance to execute the next node.
Activity [r_000001, node work, activation a_...]: Activating process (launch pending).
```

After the first `advance`, the process result and movement precede the handoff:

```text
[r_000001] Process work (visit 1): activating (launch pending).
[r_000001] Process work (visit 1): saved exit code 0 (exit_0).
[r_000001] Flow #1: work -> review via exact outcome exit_0.
[r_000001] Run ready -> waiting_agent at review (visit 1): waiting for agent; request q_000002. Flow stays here. Next: read the prompt on stdout and complete this request with an allowed outcome.
```

Read `Request`, `Prompt`, `Allowed outcomes` and `Context` on stdout. After doing
the task, a legitimate revise choice is submitted explicitly:

```bash
merstride complete loop.md --request q_000002 --outcome revise
merstride advance loop.md
```

The completion and subsequent advance produce these ordered stderr lines:

```text
[r_000001] Agent review (visit 1): completion q_000002 accepted and saved (revise).
[r_000001] Flow #2: review -> work via agent outcome revise. Revisit: next visit 2. Run waiting_agent -> ready; next node has not executed. Next: merstride advance r_000001.
[r_000001] Process work (visit 2): activating (launch pending).
[r_000001] Process work (visit 2): saved exit code 0 (exit_0).
[r_000001] Flow #3: work -> review via exact outcome exit_0. Revisit: next visit 2.
[r_000001] Run ready -> waiting_agent at review (visit 2): waiting for agent; request q_000003. Flow stays here. Next: read the prompt on stdout and complete this request with an allowed outcome.
```

Retrying q_000002/revise adds no activity or movement. Stdout shows its saved
receipt and the current waiting request q_000003. It cannot answer q_000003.
After doing that task and choosing approved, the next accepted completion moves
review -> done; a separate advance records overall success. Inspection and
repeated terminal advance add no activity. A ready cursor at an end node still
has `Result: not finished` until that explicit advance.

## Failure and explicit recovery

Before, failure/interruption chiefly named a state and code:

```text
Activity [r_000001, node work, activation a_...]: Process outcome saved: exit_7. No route taken.
Activity [r_000001, node work]: Run failed. Reason: NO_MATCHING_EDGE.
Activity [r_000002, node work]: Run interrupted. Inspect external effects, then explicitly resolve --retry or --fail.
```

A failed no-match run now explains outcome, unchanged cursor and overall failure:

```text
[r_000001] Process work (visit 1): saved exit code 7 (exit_7).
[r_000001] Flow stays at work; no route taken. No route matches exit_7 at work.
[r_000001] Run executing -> failed at work: overall result: failure. No route matches exit_7 at work. (NO_MATCHING_EDGE). Next: inspect status/logs; start a new run after correcting the cause.
```

`status` of this failed run exits 0 and prints `Result: failure` on stdout with
the cause on stderr. Terminal failure cannot be resolved; correct the cause and
start a new run. `NO_MATCHING_EDGE`, failure ends and transition limits retain
their existing execution exit 5.

For a different run whose executing writer was lost, read-only status projects
`Result: unresolved` and inspection advice without saving or announcing work.
The first advance records interruption without launching a process:

```text
[r_000002] Process work (visit 1): saved execution interrupted; effects uncertain (interrupted).
[r_000002] Flow stays at work; no route taken. External effects may already have happened.
[r_000002] Run executing -> interrupted at work: execution interrupted; overall result unresolved. Next: inspect external effects, then explicitly resolve --retry or --fail.
```

Inspect the actual external effects before deciding. If retry is appropriate:

```bash
merstride resolve r_000002 --retry
merstride advance r_000002
```

```text
[r_000002] Run interrupted -> ready at work: explicit retry allowed; flow stays here; no process executed. External effects may repeat. Next: merstride advance r_000002.
[r_000002] Process work (visit 2): activating (launch pending).
[r_000002] Process work (visit 2): saved exit code 0 (exit_0).
[r_000002] Flow #1: work -> done via exact outcome exit_0.
[r_000002] Run ready -> completed at done: success end; overall result: success.
```

Alternatively, `resolve --fail` saves failure without execution or movement.
Actual Ctrl+C/Windows Ctrl+Break retains exit 130 and the uncertain-effects advice.
Rejected result/completion/resolution saves never announce saved success or movement;
inspect status and permissions before retrying. A process may have affected the
outside world even when its result save failed.

## Capturing output and structured integration

Redirect stdout for summaries/prompts and stderr for progress/errors. Only activity
is ordered; stdout remains useful alone as a current result plus next action.
Output has no color/spinner or periodic noise. Process log bytes stay separate;
controls in human text are escaped visibly. Visit numbers count actual saved
activations, including interrupted attempts. Movement numbers count saved routing
transitions, across invocations; neither is a complete event-history API.

Use `--json` for AI/automation. It still returns exactly one protocol-version-1
object, including request/receipt/activation identities, without any human activity.
Keep the exact run/request IDs and state root; do not parse these human sentences.
