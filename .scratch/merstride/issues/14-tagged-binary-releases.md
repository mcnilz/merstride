# P14: Tag-triggered SemVer binary releases through GitHub Actions

**What to build:** A maintainer can release Merstride by pushing an authorized
semantic-version tag. GitHub Actions builds, verifies and publishes downloadable
native binaries for Windows and Linux, and macOS when feasible. Users can extract
the matching archive and run Merstride without an installed .NET runtime.

**Blocked by:** None for implementation of the workflow and macOS feasibility
assessment. Publication remains blocked by P07's outstanding release qualification
and the required hosted repository controls. This ticket does not waive them.

**Status:** complete — implemented and locally verified on 2026-11-16 after the
user's assignment ("umsetzen"). Standards and Spec reviews have no outstanding
findings. Hosted publication and P07 qualification remain open.

**Approval and delivery boundary:** The user requested one new local issue for
GitHub Actions releases, semantic versioning, release by tag, and Windows/Linux/
macOS binaries where possible. This session records that scope only.
Implementation and any local commit require a separately assigned implementation
session and its acceptance instructions. No tag creation, push, remote release,
publication or deployment is authorized by issue creation.

**Current behavior:** P07 already introduced a tag-triggered release workflow,
strict stable/prerelease tag validation, Windows x64 and Linux x64 native packages,
integrity verification and guarded GitHub Release publication. Existing controls
reject missing qualification and stale or mismatched evidence. Hosted execution
has not been established; macOS is not a supported release target. Extend and
complete this existing path rather than create a second release mechanism.

**Reference:** [DailyWingetNotify](https://github.com/mcnilz/DailyWingetNotify)
may supply reusable release patterns, as requested by the user. The repository
and workflow contents could not be retrieved during issue creation; no specific
behavior, compatibility or licensing is asserted. Inspect its actual workflows
and license during implementation, record the reviewed revision, and adapt useful
parts to Merstride's existing release and dependency policies.

**Acceptance scope:** Preserve P07's release scenarios and existing scenario tags.
Exercise release controls through the public build/release boundary and product
behavior through the published native CLI. Add dedicated @P14 scenarios where
appropriate; required steps must not be undefined, ambiguous, pending or skipped.

- [x] An authorized tag in the documented `vMAJOR.MINOR.PATCH` convention triggers
      the existing GitHub Actions release path. Document SemVer bump rules and
      supported prerelease tags, reject malformed tags, and mark prereleases
      appropriately on GitHub. Define whether SemVer build metadata is supported
      or deliberately excluded from release tags. Automatic version bumping is
      not required.
- [x] The validated tag determines the version reported by the native CLI,
      release name, archive names and manifest. All platform assets belong to
      the exact tagged commit and successful workflow run; mismatched versions,
      commits or evidence prevent publication.
- [x] Produce Windows x64 and Linux x64 native binary archives with clear OS/
      architecture/version names, checksums, release notes and required license
      notices. Extracted binaries run without an installed .NET runtime and
      report the expected version. Preserve all existing release qualification,
      integrity and no-overwrite requirements.
- [x] Assess native macOS builds on suitable GitHub-hosted runners, including
      Native AOT tooling, dependency locks/notices, executable permissions,
      runtime behavior and release verification. State the selected architecture
      explicitly. If feasible, include verified macOS archives in the same release
      path and update platform policies, manifests and qualification consistently.
      Otherwise record the concrete blocker and follow-up scope; Windows/Linux
      completion must not imply macOS support or silently omit a promised asset.
- [x] Required build, test, packaging or qualification failures prevent
      publication. Preserve minimal workflow permissions, pinned reviewed actions,
      same-run artifact integrity and safe handling of existing releases/drafts.
      Any adoption from the reference preserves applicable attribution/licenses.
- [x] Document the maintainer's version/tag/release procedure, repository setup
      prerequisites, supported platforms and download/run instructions. Keep the
      contract and relevant decisions consistent with any platform changes.
- [x] Verify valid stable/prerelease tags, invalid tags, asset/version identities,
      missing or failed required platforms, stale qualification and duplicate
      releases through controlled local fixtures. Record actual platform results
      and remaining hosted/clean-target limitations without claiming release
      readiness from local gates. Actual hosted publication requires separate
      authorization and the existing release prerequisites.

**Out of scope:** Package-manager distribution, installers, signing/notarization,
automatic version bumps, additional architectures beyond those explicitly chosen
for this scope, and unrelated CLI/workflow behavior changes.

## Implementation evidence — 2026-11-16

The later user assignment authorizes implementation and the implementation
workflow's local commit. The creation-only boundary above remains historical.
Checked acceptance items describe implemented controls and controlled local
verification, not an actual hosted release or completed P07 qualification.

- Retained the existing Windows/Linux tag pipeline and bound all release
  checkouts to the event commit. Added bounded job timeouts and public workflow
  policy enforcement. A moving-branch checkout mutation passed the old policy
  (red regression); both moving/missing-ref cases now fail as required.
- Added ten public Tag-mode cases covering stable/prerelease versions,
  annotated/lightweight tags, malformed syntax, excluded build metadata,
  mismatched commit and missing main ancestry. All 37 focused release/policy
  checks pass, including the existing guarded package/publication fixtures.
- Full Windows x64 gate with Version 0.2.0-rc.1: 480 tests / 298 local feature
  cases passed, zero failures/skips, no native build/publish warnings or errors.
  Native CLI reports exactly 0.2.0-rc.1 with JSON protocol 1. Privacy,
  architecture, dependency, distribution and official Mermaid gates pass.
  The two clean-target cases remain explicitly required and unrun, not passed.
- Verified all four unchanged action SHAs against their official stable release
  tags. No dependencies, dependency locks or license inventories changed.
- Added maintainer/version/tag and user download instructions, the explicit
  build-metadata exclusion, and ADR 0011. Reviewed the reference's actual
  workflow and MIT license at c8007fbffd9b9ea704e6c382afce5edb193ebeac; no source
  was copied. This supersedes the creation-time retrieval limitation above.
- macOS distribution is deferred: the required build/acceptance path currently
  assumes Linux strace and /proc, with no macOS locks/audits/qualification or
  execution environment. RELEASING.md records osx-arm64 as the first follow-up
  target and the work required before advertising its binary.
- No new Linux/macOS execution evidence, hosted workflow, remote tag, push or
  publication is claimed. Raw verification output remains under ignored artifacts.
- Native Windows binary: 5.973 MiB; warm status P95: 29.356 ms, within the
  existing 25 MiB / 150 ms gates. The tested prerelease is an example version,
  not a created release tag.
- Independent Standards and Spec reviews against the pre-implementation commit
  found zero actionable findings on either axis. The final follow-up only
  records review results and metrics; no implementation changed after the gate.
