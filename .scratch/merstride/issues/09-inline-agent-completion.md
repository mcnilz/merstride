# P09: Return agent outcomes directly through CLI parameters

**What to build:** Let the existing agent complete a waiting request by passing
its outcome and optional result data directly to the CLI, without creating a
completion file. The user requested this issue because requiring a file for a
simple state feedback adds unnecessary work.

**Blocked by:** P03 agent handoffs and P08 CLI parsing (both complete). No remaining
implementation blocker; P07 release qualification still blocks publication.

**Status:** complete — locally accepted for commit; Windows/Linux full gates and both review axes passed.

**Acceptance scope:** Add dedicated @P09 public native CLI scenarios; retain
@A03 @A04 @A06 @A07 @A08 @A09 @A17 @A20 regression coverage. This extends
completion input, not the engine's run-status model or transition rules.

- [x] Accept a completion through `complete <run-id> --request <id>
      --outcome <outcome>` without a result file. Default optional result data to
      the empty JSON object.
- [x] Support optional inline object data through `--data <json>`, including
      quoted spaces, Unicode and nested values. Document practical PowerShell
      quoting with a complete existing-agent example.
- [x] Retain `--result-file` for existing callers. Require exactly one input mode:
      file or inline outcome. Reject mixed modes, data without an inline outcome,
      missing values, duplicate options and malformed JSON without state mutation.
      Use existing usage/payload error conventions and document them explicitly.
- [x] Reuse the same completion validation, allowed outcomes, request checks,
      JSON object rules and data size/depth/numeric limits for both input modes.
      Do not add a second completion or persistence implementation.
- [x] Preserve atomic receipts, equivalent-payload idempotence and conflict
      handling across modes: an equivalent inline retry of a file completion,
      or the reverse, returns the original receipt without another transition.
- [x] A successful completion changes only the cursor and returns the existing
      ready response; it never executes the next node. Rejected or historical
      completions retain the existing guarantees. Run status is derived by the
      engine, not assigned arbitrarily through a parameter.
- [x] Demonstrate the file-free path through the actual published native CLI:
      start, advance to waiting_agent, complete using parameters, then explicitly
      advance. Verify JSON stdout, exit codes, state effects and child counts.
      Execute actual feature rows; undefined, ambiguous, pending or skipped
      required steps must fail.
- [x] Update command help, offline agent guide and usage/contract documentation
      for the added input mode. Make the parameter-only path the simple default
      example while keeping file-based use documented.
- [x] Make parameter-based completion the preferred path in most existing agent,
      context, limit and recovery scenarios/tests, as requested by the user.
      Keep targeted file-format and cross-mode compatibility coverage.
- [x] Retain .NET 11 Native AOT/C# 15, Cli-only parser boundaries and all existing
      build, dependency, distribution and native size/startup gates. Record review
      and actual verification evidence; do not waive unfinished P07 release work.

No arbitrary status editing, automatic advance, LLM integration, new dependency,
release publication or redesign of request/receipt persistence is included.
The user subsequently authorized implementation and clarified that inline
completion must dominate the normal examples and tests, not remain an optional
alternative that leaves the file-based path as the default.

Verification: final Windows and Linux gates each passed 361 tests / 192 local feature cases, including all 37 P09 cases. Both review axes reported zero actionable findings. Native budgets and privacy checks passed. See docs/STATUS.md for concise results, the initial Linux observation failure and remaining P07 release limitations. Raw evidence stays ignored under artifacts.
