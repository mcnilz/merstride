# P03: Existing-agent task handoffs

**What to build:** Pause for an existing agent, accept results, and resume deterministic work across CLI processes.

**Blocked by:** P02.

**Status:** complete; accepted by coordinator; local commit authorized

**Acceptance scope:** @P03, 03-agents; @A03 @A04 @A06 @A07 @A08 @A09 @A17.

- [x] Return stable current requests, literal prompts, selected context, and sorted allowed outcomes.
- [x] Validate completions without mutation on rejection; preserve idempotent receipts and detect conflicts.
- [x] Give loop activations fresh request IDs and expose context availability and source identity.
- [x] Match the agent guide to actual protocol behavior.

Evidence: docs/STATUS.md P03 section, artifacts/tests/p03.trx,
artifacts/tests/scenarios.json, and docs/STATUS.md (summary; raw logs are not versioned)
Independent Standards review: no documented violations, one non-blocking selector
modeling observation. Spec review: zero actionable findings; optional null-exit-code
context test added and passed. Coordinator independently reran the full gate:
78 passed, 0 failed, 0 skipped. P04 is unblocked.
