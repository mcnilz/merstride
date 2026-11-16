# Contributing

## Prerequisites

Build and test on the matching target platform with:

- .NET SDK 11.0.100 (pinned in `global.json`), using stable C# 15.
- PowerShell 7.3+ for scripts and native JSON argument examples.
- Windows x64: Visual Studio Build Tools with Desktop development with C++ and
  a Windows SDK for Native AOT.
- Linux x64: a Native AOT compiler/linker toolchain, ICU, OpenSSL and `strace`.
- Node >=22.12.0, npm and an installed Chromium browser for development-only
  Mermaid verification. Windows uses Edge by default; set `MERSTRIDE_BROWSER`
  to the browser executable on Linux or to override it.

These are development tools. The product is a native executable and does not
require .NET, Node or a browser on the target machine. Workflow commands have
their own prerequisites. Keep Windows and Linux checkouts and intermediates
separate; preserve both platform lock files. See [native verification](docs/RELEASING.md).

## Build and verify

From the repository root:

```powershell
pwsh -NoProfile -File scripts/build.ps1
```

This restores locked dependencies, builds and publishes the native executable
and test helper, runs the local acceptance suite, checks architecture and
dependency/release policies, audits distribution inputs/notices, measures native
budgets and renders the supported Mermaid examples. The executable is under
`artifacts/native/`; test results and raw verification data stay under ignored
`artifacts/`. The build fails on required undefined, ambiguous, pending or skipped
Gherkin steps. [Testing](docs/TESTING.md) explains the public seams and fixtures.

Local success does not qualify a release. The independent clean-target driver,
full platform reports, real external-agent experiment and hosted controls remain
outstanding; see [status](docs/STATUS.md) and [release verification](docs/RELEASING.md).

## Work on an approved ticket

Read [AGENTS.md](AGENTS.md), the assigned [local ticket](.scratch/merstride/issues/),
the [contract](docs/CONTRACT.md), relevant [decisions](docs/adr/) and
[vocabulary](GLOSSARY.md). Discovery and the baseline breakdown are complete;
continue from the current ticket rather than repeating repository setup.

Implement one approved scope at a time with the installed `implement`, `tdd` and
`code-review` skills; use `to-tickets` when breaking down newly approved work.
Skills are external development tools and are not bundled with Merstride.
Behavior tests invoke the published CLI and observe JSON, exit codes and process/
filesystem effects. Architecture and policy tests use the public build boundary.
Use English for development artifacts and CLI messages, NUnit 4 without
FluentAssertions, and preserve existing requirement/scenario tags.

Keep the offline guide, examples and normative contract consistent. Validate
affected examples through the CLI, run relevant checks and the full local gate,
then review against both repository standards and the ticket. Record concise
results and actual limitations in [status](docs/STATUS.md); keep raw logs local.
Follow the ticket/session's acceptance and local-commit instructions. Stop at
the assigned scope; pushing, publishing and deploying require separate authorization.

## Dependency and privacy rules

Review provenance, license, lock-file changes and analyzer separation under the
[dependency policy](docs/DEPENDENCIES.md). Deliberately update the inventories for
adopted versions; gates never approve unknown dependencies automatically. Verify
action pins against official releases when updating workflows. Preserve MIT and
all third-party notices.

Before committing, run:

```powershell
pwsh -NoProfile -File scripts/repository-privacy.ps1
```

Version concise reviewed results without personal paths or machine identities.
Keep raw logs, environment dumps and scanner reports under ignored `artifacts/`.
See [security reporting](SECURITY.md) for vulnerabilities.
