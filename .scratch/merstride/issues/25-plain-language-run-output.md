# P25: Explain workflow progress and results in plain English

**What to build:** Make the default console output of Merstride understandable
to someone running a workflow for the first time. Show what is running, what
happened, how the workflow ended or paused, and what the caller needs to do next.
Keep routine success concise and explain decisions when they affect the result.

**Blocked by:** None. [P24](24-console-first-process-output.md) owns delivery of
the command's stdout and stderr. P25 owns the surrounding Merstride messages.

**Status:** complete

**Approval and delivery boundary:** Local issue capture only. Implementation and
a local commit require a separate assignment.

The follow-up request to implement the open issues assigns P25 for local
implementation, verification, review and a scoped commit after P24.

## Reported example

Run the checked-in process-chain example from the repository root:

```bash
dotnet run --project ./src/Merstride -- start ./examples/process-chain.md --advance
```

The user reported that the output still felt cryptic after the P17, P20 and P21
improvements. Selected lines from the reported output:

```text
[r_000005] Created new ready run; no node has executed. Initial advance requested.
[r_000005] Process status (visit 1): activating (launch pending).
[r_000005] Process status (visit 1): saved exit code 0 (exit_0).
[r_000005] Flow #1: status -> diff via exact outcome exit_0.
[r_000005] Run ready -> completed at done: success end; overall result: success.
Creation: saved new run
Phase: advance
Initial advance: finished; see the resulting run status below
Status: completed (finished successfully)
Cursor: done
Result: success
```

The workflow already supplies the labels `Inspect status` and `Inspect changes`,
but the transcript uses `status` and `diff` as step names and repeats success in
several places. The real Git output is absent from the example transcript because
P24 has yet to be implemented. Sample workflow messages could read:

```text
Starting Inspect a Git workspace (run r_000005)
Running Inspect status (git status --short)
Running Inspect changes (git diff --stat)
Finished successfully (2 steps, run r_000005).
```

This sample illustrates information and tone. The exact wording, how to identify
the workflow, and whether to include a safe command preview are design choices.
The actual Git output belongs at each command under P24.

## Acceptance criteria

- [x] The process-chain walkthrough shows which workflow started, which steps
      ran in order, and the overall result. Use meaningful step labels where
      authored, with node IDs as a fallback. Provide a fallback for saved runs
      that lack display metadata. Keep display names separate from identifiers
      used for routing, targeting or recovery.
- [x] Routine successful execution has a short final summary. Omit duplicate
      creation, phase, cursor, routing and success messages from the default
      success path. Keep a usable run reference. Offer an explicit way to find
      operational details such as state paths, route keys and activation IDs
      when someone needs them.
- [x] Explain a failure or pause in plain English with the affected step, cause
      and concrete next action. Cover fallbacks, loops and a failed command
      that the workflow handles and continues from. For agent handoffs, keep
      the prompt, allowed outcomes and the reference needed to complete the
      request easy to find. For interruption, explain uncertainty about external
      effects and the explicit recovery choices.
- [x] Long-running steps remain visible before completion. Keep progress
      truthful about saved state and work attempted. Preserve meaningful output
      under redirection, terminal-control escaping for Merstride messages,
      execution and recovery semantics, CLI exit codes and the explicit JSON
      contract. When combined with P24, output from the tool is easy to associate
      with its step and Merstride's final result is easy to find.
- [x] Update the contract, relevant decisions and walkthroughs. Record a real
      before-and-after transcript of the reported process chain from the public
      native CLI. In the new transcript, someone unfamiliar with Merstride can
      identify what ran and whether it worked without looking up internal terms.
      Add `@P25` native scenarios that verify the concise success path and a
      representative failure and handoff, while preserving existing tags and
      their execution assertions.

## Delivery notes

- Text execution uses a single-workflow Markdown heading and Mermaid labels from
  the frozen definition. Legacy runs and missing names fall back to IDs. The
  detailed layout is available with `--details` and saved identities with
  `status --json`.
- The standards review led to using the same route selection for execution and
  reporting, handling ATX heading whitespace and closing markers, and keeping
  each console channel's line separators independent. The ticket review added
  repeated agent visit numbers and corrected the next action for runs already
  ended by `resolve --fail`.
- The full Windows x64 gate passed 712 tests, 475 local feature cases and 460
  independent native cases. The before-and-after Git example is in the flow
  output walkthrough. Raw evidence remains under ignored artifacts.
