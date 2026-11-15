# P07: Portable native distribution

**What to build:** Deliver verified Windows/Linux native artifacts with a usable agent interface and tested release automation.

**Blocked by:** P06 (accepted at P06 implementation).

**Status:** local implementation accepted by coordinator; release criteria and native full-suite driver remain open

**Acceptance scope:** @P07, 06-interface and all applicable features; @A09 @A15 @A16 @A22 @A25. @A20 belongs to P01.

- [ ] Run all release-applicable scenarios on Windows 11 and Linux without an installed .NET runtime.
- [ ] Verify inspection semantics, cross-directory resume, native budgets, and token-benefit evidence.
- [x] Audit actual compiler/ILC/link/publish inputs and controlled archive contents for build-only analyzer separation and complete permissive notices.
- [x] Add tested-tag release workflow with integrity checks and same-run archives/checksums/manifest/notices implementation; no remote publication.
- [x] Test fail-closed publication after failed gates, wrong commits, missing platforms, invalid tags, inconsistent drafts/manifest, missing qualification or existing versions.

Earlier Windows and Linux full runs each passed 252 tests / 97 actual feature cases,
zero failures/skips. These were not the final exact source on both platforms: Linux
lacked the final Package freshness guard. See the acceptance follow-up below.
Clean-target orchestration remains required: both cases were
explicitly run and failed as unavailable, never counted passed. The clean suite
contains 82 runtime cases; 15 build-only cases run separately on the SDK host.
Inspection/cross-directory/native budgets passed. A no-.NET Linux chroot native
smoke passed; it is not full clean-target qualification. No comparable external
agent/token-accounting driver was available. The combined token-benefit criterion
above therefore remains incomplete. scripts/qualify-clean-target.ps1 and
scripts/token-experiment.ps1 describe/validate the missing evidence and required
external drivers. Release assembly/publication rejects absent proof.

Native Windows/Linux: 5.006/5.234 MiB; warm status P95 33.368/4.732 ms. Input audits,
official Mermaid checks and current official SHA pins passed. Standards and Spec
reviews were fixed/re-reviewed with no remaining findings. See docs/STATUS.md,
docs/RELEASING.md, ADR 0007 and docs/STATUS.md (summary; raw logs are not versioned) for actual identities, red/green
results, reproduction, tooling provenance and limitations. Changes are uncommitted
for coordinator acceptance. P08 was not implemented; no push/real tag/release/deployment
was performed. Hosting/origin/main/authorized tag rules remain external prerequisites.

Coordinator acceptance follow-up: seven new public Package fixtures use disposable
clean Git repositories, copied public scripts/real notices and synthetic candidate/
gate evidence. Against the pre-sync Linux script they exposed five stale-source/
evidence acceptances (2 passed / 5 failed). After the coordinator's full Linux run
ended and the missing guard was synchronized, all 15 focused Package/release tests
passed on each platform. Valid archives are inspected independently; changed HEAD,
worktree, TRX, distribution, benchmark or binary must refuse before staging or
archive creation. Matching affected source hashes and raw results are recorded in
docs/STATUS.md (summary; raw logs are not versioned) Both review axes found no follow-up issues.
Current test inventory is 259; no full 259-test run is claimed. Clean-target/native
driver/token criteria above remain open. Changes remain uncommitted.
