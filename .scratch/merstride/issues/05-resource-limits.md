# P05: Bounded execution and context

**What to build:** Bound loops, process time/output, structured data, and agent context at exact limits.

**Blocked by:** M01 (.NET 11 migration), after accepted P04.

**Status:** complete; accepted by coordinator; local commit authorized

**Acceptance scope:** @P05, 04-limits; @A04 @A06 @A12 @A16.

- [x] Enforce timeout and output routes and transition boundaries with watchdogs.
- [x] Return bounded valid Unicode log tails and targeted read-only logs.
- [x] Reject excessive context, data size/depth, duplicate JSON keys, and unsafe numbers.

Actual 04-limits scenarios and 21 supplemental boundary checks pass locally against
the native binary (35 total). Full reusable gate: 200 passed, zero failed/skipped;
75 of 99 actual feature cases passed, 24 later cases explicitly not yet implemented.
Native size 4.972 MiB; warm status P95 33.757 ms; two-helper chain P95 116.065 ms,
20 ordered effects. Official Mermaid checks passed. Independent Standards and Spec
reviews against accepted M01 M01 migration found no actionable blockers. Optional timeout
failure-cleanup hardening and its path comparison fix are complete; the final
75-feature rerun passed. See docs/STATUS.md, ADR 0005 and preserved evidence.

Reproduce with `pwsh -NoProfile -File scripts/build.ps1`. Linux, clean-runtime,
hosted GitHub/network tracing and P06/P07 checks remain unclaimed. All changes are
approved for local commit after independent coordinator verification (200 tests,
75 feature cases). Next slice is P06; this chat stops at P05.
