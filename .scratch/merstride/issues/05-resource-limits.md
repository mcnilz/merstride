# P05: Bounded execution and context

**What to build:** Bound loops, process time/output, structured data, and agent context at exact limits.

**Blocked by:** M01 (.NET 11 migration), after accepted P04.

**Status:** ready-for-agent

**Acceptance scope:** @P05, 04-limits; @A04 @A06 @A12 @A16.

- [ ] Enforce timeout and output routes and transition boundaries with watchdogs.
- [ ] Return bounded valid Unicode log tails and targeted read-only logs.
- [ ] Reject excessive context, data size/depth, duplicate JSON keys, and unsafe numbers.
