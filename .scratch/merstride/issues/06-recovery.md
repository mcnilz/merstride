# P06: Explicit recovery and independent runs

**What to build:** Prevent silent replay after crashes and exclude concurrent writers while preserving atomic reader snapshots.

**Blocked by:** P05.

**Status:** ready-for-agent

**Acceptance scope:** @P06, 05-recovery; @A10 @A11.

- [ ] Verify same-run exclusion and independent-run progress with real process barriers.
- [ ] Detect interruption after external effects without rewriting state on reads.
- [ ] Require explicit retry/fail resolution and preserve interrupted activation identity.
- [ ] Verify user interruption and process-tree termination per platform and refuse corrupt storage.
