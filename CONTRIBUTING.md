# Contributing

Install .NET SDK 10.0.404, PowerShell 7, and Visual Studio 2022 or newer Build Tools
with Desktop development with C++ and a Windows SDK for win-x64 Native AOT.
Run `pwsh -NoProfile -File scripts/build.ps1` from the repository.
Tests invoke the published executable. Results are under artifacts/tests; benchmarks
and SDK information are under artifacts. Build-time fixtures require the SDK.

Follow AGENTS.md and the approved slice order. Use English and NUnit 4; no FluentAssertions.
Review dependency provenance, license, lock-file changes, and analyzer separation.
Update dependency-inventory.json deliberately when adopting versions; the gate never
automatically approves unknown dependencies. Update verified action pins from official releases.

Keep commits local only when requested. Do not publish, deploy, or begin the next slice
without the agreed acceptance. The workflow's `gate` job is the intended future required check.
