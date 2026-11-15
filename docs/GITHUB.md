# GitHub project, build, and release

Revision 0.5 — required implementation deliverables. No remote repository or working pipeline has been created by this specification package.

## Repository

Host the project publicly on GitHub under MIT. The repository name is `merstride`; its GitHub owner remains to be selected. Keep source, English docs, examples, feature files, dependency lock files, and build scripts versioned together. Provide CONTRIBUTING.md, SECURITY.md, issue/PR templates, and a correct MIT LICENSE at setup. Repository publishing is a separate action once the owner and source are available.

Implement `.github/workflows/build.yml`, `.github/workflows/release.yml`, and `.github/dependabot.yml`. Keep build commands in repository-owned PowerShell 7 scripts reusable locally. Do not add a third-party build framework or release action.

## Official actions policy

Every external `uses:` action MUST be GitHub-maintained and appear on the explicit project allowlist. A Marketplace verified-creator badge alone is insufficient. Start with only these official actions:

| Action | Stable release observed on 2026-11-15 | Purpose |
| --- | --- | --- |
| actions/checkout | v7.0.1 | Checkout |
| actions/setup-dotnet | v6.1.0 | Install pinned SDK |
| actions/upload-artifact | v7.0.2 | Store build/test artifacts |
| actions/download-artifact | v8.0.2 | Retrieve same-run release artifacts |

These are research observations, not perpetual version requirements. At workflow implementation, verify each upstream latest stable release, resolve its full commit SHA from the official repository, and use that SHA with a release-version comment. Never invent a SHA, use an unreviewed branch ref, or copy stale action versions from examples. Check action/runner compatibility. Configure weekly Dependabot updates for `github-actions` and `nuget`; review update PRs and do not enable blind automerge.

Allowed repository-owned scripts/reusable workflows must also be reviewed. Do not hide external actions inside local wrappers. Adding another official action requires a demonstrated task need and an allowlist update. Official GitHub CLI (`gh`) handles GitHub Releases; no community release action is needed.

## Build workflow

Run on pull requests, pushes to main, and manual dispatch. Use pull_request rather than pull_request_target to execute contribution code. Default token permissions are contents:read; no secrets or write token in untrusted build jobs. Set checkout persist-credentials:false. Cancel superseded PR builds, but never cancel an active release midway through publication.

Build Windows x64 on a pinned supported Windows hosted-runner label and Linux x64 on a pinned supported Ubuntu label. Select concrete labels after checking Native AOT prerequisites; record image/version evidence. Do not claim Windows Server hosted tests prove Windows 11 specifically: retain the Windows 11 release acceptance check. Do not claim a hosted SDK machine is a no-runtime test environment.

For each matrix target:

1. Install the exact SDK from global.json; restore locked NuGet dependencies, including target RID lock coverage.
2. Compile with required NsDepCop diagnostics as errors; run positive/negative architecture fixtures.
3. Run Gherkin acceptance scenarios for the implemented scope. Pending/undefined required steps fail.
4. Publish the target Native AOT binary with warnings reviewed.
5. Run acceptance/smoke checks against that actual binary; record version, size, and SHA-256.
6. Check dependency licensing and build-only/runtime separation; upload test results even after test failure, but never label a failed binary release-ready.

Use an always-running aggregate gate that fails for failed, cancelled, or unexpectedly skipped required jobs, so branch protection can require one stable check name. Configure branch rules when the repository is created. Begin with Windows in P01; add Linux before the first release claiming Linux support.

## Release workflow

A pushed SemVer tag `vX.Y.Z` or prerelease tag triggers release validation. Restrict release tags to authorized maintainers through repository rules. Reject malformed versions and tags whose commit is not on main. Resolve the exact tag commit; check that embedded version and intended release version match.

Build and test that commit for all advertised platforms using the same build scripts. Release only artifacts produced by that successful run, not the latest artifacts from another branch/run. Require architecture, Gherkin, license, package-content, size, and applicable platform gates. Performance thresholds run on a documented stable reference environment; hosted-runner timings are informative and do not substitute for that gate.

Create versioned ZIP (Windows) and tar.gz (Linux) archives, preserving executable permissions, plus SHA256SUMS, MIT license, required third-party notices, and release notes. Include source commit, RID, SDK/toolchain versions, test evidence, and clearly stated platform limitations. Verify archive contents and checksums before publication.

Only the final publish job receives contents:write and GH_TOKEN, after all build jobs pass. Use the official `gh release create` with `--verify-tag` and a draft release, upload the verified assets, then publish the complete draft. Use prerelease status for prerelease SemVer. Do not overwrite an existing published release or clobber assets. A retry may resume a matching draft after verifying tag, commit, expected asset set, and hashes. Serialize releases for the same tag.

No publication on PR builds. No community release action, curl-piped installer, or arbitrary fetched script. Use official hosted tooling/SDK setup and reviewed repository scripts.

## Sources

- [Checkout latest release](https://github.com/actions/checkout/releases/latest)
- [Setup-dotnet releases](https://github.com/actions/setup-dotnet/releases)
- [Upload-artifact latest release](https://github.com/actions/upload-artifact/releases/latest)
- [Download-artifact latest release](https://github.com/actions/download-artifact/releases/latest)
- [GitHub workflow action references](https://docs.github.com/en/enterprise-cloud@latest/actions/reference/workflows-and-actions/workflow-syntax)
- [Official GitHub CLI release creation](https://cli.github.com/manual/gh_release_create)
