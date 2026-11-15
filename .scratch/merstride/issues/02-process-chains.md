# P02: Deterministic process chains

**What to build:** Run sequential process nodes and route actual outcomes without model calls.

**Blocked by:** P01; user acceptance of P01.

**Status:** complete

**Implementation status:** accepted by coordinator under user delegation; local commit authorized; P03 unblocked.

**Acceptance scope:** @P02, 02-processes; @A02 @A13 @A16 @A18.

- [x] Execute chains and route exact/default outcomes including launch failure.
- [x] Pass literal arguments, use explicit PowerShell, and retain separate real logs.
- [x] Start executes nothing; hostile output remains data.
- [x] Measure native process feasibility.

Evidence: complete local build passed 40 tests, including all 21 P01/P02 feature
cases; 78 later cases explicitly not implemented. Native win-x64 binary 4.557 MiB;
warm status P95 32.730 ms; ten two-helper chains P95 112.310 ms, with 20 ordered
effects checked. See docs/STATUS.md and docs/STATUS.md (summary; raw logs are not versioned)
Standards review: no documented violations, one accepted low-priority domain-model
observation. Spec review: zero actionable findings. Coordinator independently reran
the complete 40-test gate successfully and approved the local commit.
No P03 implementation or remote publication. Full later boundary/recovery/platform
suites remain unclaimed.
