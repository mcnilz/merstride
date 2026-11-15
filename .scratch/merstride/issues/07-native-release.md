# P07: Portable native distribution

**What to build:** Deliver verified Windows/Linux native artifacts with a usable agent interface and tested release automation.

**Blocked by:** P06.

**Status:** ready-for-agent

**Acceptance scope:** @P07, 06-interface and all applicable features; @A09 @A15 @A16 @A22 @A25. @A20 belongs to P01.

- [ ] Run all release-applicable scenarios on Windows 11 and Linux without an installed .NET runtime.
- [ ] Verify inspection semantics, cross-directory resume, native budgets, and token-benefit evidence.
- [ ] Audit all publish/link inputs and release contents for build-only analyzer separation and licenses.
- [ ] Add tested-tag release workflow with integrity checks and complete same-run archives/checksums/notices.
- [ ] Prevent publication after failed gates, wrong commits, missing platforms, invalid tags, or existing versions.
