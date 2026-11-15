# P06: Explicit recovery and independent runs

**What to build:** Prevent silent replay after crashes and exclude concurrent writers while preserving atomic reader snapshots.

**Blocked by:** P05.

**Status:** complete; accepted by coordinator; local commit authorized

**Acceptance scope:** @P06, 05-recovery; @A10 @A11.

- [x] Verify same-run exclusion and independent-run progress with real process barriers.
- [x] Detect interruption after external effects without rewriting state on reads.
- [x] Require explicit retry/fail resolution and preserve interrupted activation identity.
- [x] Verify user interruption and process-tree termination on local Windows x64 and refuse corrupt storage. Linux remains unqualified.

The final full gate passed 217 tests with zero failures/skips, including all seven
actual recovery feature cases and ten supplemental guards. Feature inventory is
82 passed / 99, with 17 later cases explicitly not yet implemented. Independent
Standards and Spec reviews report no remaining findings after fixes. Native size
is 4.991 MiB; warm status P95 is 30.677 ms. See docs/STATUS.md, ADR 0006 and
docs/STATUS.md (summary; raw logs are not versioned) for reproduction, evidence and unqualified environments.
Implementation remains uncommitted for coordinator acceptance. No P07 work started.
