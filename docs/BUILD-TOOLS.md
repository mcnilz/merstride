# Build and test tool review

Reviewed 2026-11-15. The exact direct/transitive NuGet graph, hashes, provenance,
purposes, licenses, and classifications are in dependency-inventory.json and the
versioned Windows/Linux package lock files. The dependency gate requires evidence for
every locked version. All package license expressions were inspected from their
official NuGet packages; all are MIT except the explicitly approved NsDepCop tool.

## Separate architecture tool inventory

NsDepCop 3.3.0, GPL-2.0-only, is build-only. Its package carries Roslyn 4 and 5
analyzers and DotNet.Glob (assembly version 2.1.1.0, MIT; bundled analyzer support,
not a NuGet runtime dependency). Both remain analyzer inputs, excluded from compile
and runtime assets. Upstream: [NsDepCop](https://github.com/realvizu/NsDepCop),
[DotNet.Glob license](https://github.com/dazinator/DotNet.Glob/blob/master/LICENSE).
The architecture config enables namespace and assembly checks; the two illegal
dependency diagnostics are errors. SDK reference-pack assembly exceptions are
explicit; namespace rules still constrain actual use. Networking namespaces are
forbidden across production code. No production project references a test assembly.

YamlDotNet 18.1.0 and System.CommandLine 3.0.0 are the third-party runtime
packages. Their selected assets have no transitive runtime dependencies for
net11.0. Low-level YAML parsing and CLI help/parsing pass native publishing without
trimming/AOT warnings. Distribution audits inspect compiler/linker inputs and
notices; independent platform release qualification remains required.

NUnit 4.6.1 retains the selected major version; the latest upstream major was 5.
NUnit3TestAdapter 6.3.1 and Microsoft.NET.Test.Sdk 18.12.0 provide official test
execution. Gherkin 43.0.0 and Cucumber.Messages 35.0.0 are the maintained official
Cucumber parser/protocol, used only by tests. The adapter's Microsoft test-platform
dependencies are all test-only, including telemetry support. Development scripts
opt out of test-platform and SDK telemetry. None are production PackageReferences.
No FluentAssertions, LLM SDK, hosting framework or runtime Mermaid renderer is adopted.

## GitHub Actions and hosted image evidence

Official latest stable release API results and tag-to-commit resolution were checked
on 2026-11-15 and recorded in .github/action-pins.json. Each pinned action.yml was
also inspected: checkout, setup-dotnet, upload-artifact and download-artifact use Node 24. The documented
[windows-2025 image](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)
was image 20261111.331.1, Windows Server 2025 build 10.0.26100.33822, with Visual
Studio/C++ tooling and Node 24 support. The SDK is selected by global.json.
[Native AOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
require the Desktop development with C++ workload. Actual hosted image evidence
will be captured by the future GitHub run; no hosted run was performed locally.

The build workflow uses pull_request, read-only contents permission, disabled
credential persistence, and an always-running aggregate gate. It has no publication
job. The separate release workflow uses download-artifact for same-run assets. Dependabot
checks Actions and NuGet weekly; it does not automerge. A local policy check is not
proof of actual hosted fork execution or repository branch protection.

## Native toolchain

SDK 11.0.100 selects runtime/compiler/linker 11.0.0 and stable C# 15 for all
net11.0 projects. Official NuGet metadata records MIT and source commit
297acf930fb0f5c07fd4e51d78e521b4ad2e5d1f for the three updated compiler/linker
packages. Their direct/platform graph and restored content hashes are reviewed in
dependency-inventory.json; historical 10.0.15 records are retained. YamlDotNet
18.1.0 and System.CommandLine 3.0.0 use net10.0 assets compatible with net11.0. NUnit 4 remains the test host.

ADR 0004 explains the nine exact new SDK reference-pack assembly exceptions.
Microsoft.Extensions namespaces remain forbidden. The positive and negative
architecture fixtures compile under net11.0 and prove enforcement remains active.
Current native size/startup and analyzer-input inspection evidence is in STATUS.md.
The global C# language skill remains external. CI setup-dotnet consumes the same
global.json; existing verified official Action SHAs are unchanged. Hosted execution
remains unclaimed.
