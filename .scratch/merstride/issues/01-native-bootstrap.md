# P01: Native workflow bootstrap and build gates

**What to build:** Start, persist, inspect, and finish a single-end YAML/Mermaid workflow with a native executable and offline guide.

**Blocked by:** None (can start immediately).

**Status:** complete — accepted by the user; local commit authorized.

**Acceptance scope:** @P01; @A09 @A16 smoke, @A20, @A21, @A23, @A24. Initial native budgets and invalid YAML checks.

- [x] Execute bootstrap and offline guide through the published native CLI.
- [x] Reject invalid YAML and unimplemented syntax explicitly; establish initial locks and atomic persistence.
- [x] Execute real feature files with strict undefined-step failures and report later slices separately.
- [x] Prove forbidden and intended dependencies and detect ineffective NsDepCop enforcement.
- [x] Add Windows build workflow with verified official Action pins and dependency/policy gates.
- [x] Record Native AOT results, size, startup, review, limitations, and next slice.

Final verification: 22 tests passed, including 10 P01 feature cases; 89 later cases
reported not yet implemented. Native win-x64: 3.699 MiB; 30-start warm status P95
27.601 ms. Details and unavailable hosted/clean-runtime checks are in docs/STATUS.md.
