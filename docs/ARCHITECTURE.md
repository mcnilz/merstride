# Architecture enforcement and licensing

Revision 0.5 — accepted tool and licensing decisions; namespace layout remains a proposed implementation detail.

## MIT project, separate build tooling

The project-owned code and documentation MUST be released under MIT. Add the actual MIT license file with the correct copyright holder during repository setup. Distributed runtime dependencies must retain their notices and use compatible permissive licenses. Do not describe third-party components as relicensed under MIT.

NsDepCop is approved as a GPL-2.0 build-only Roslyn analyzer. It checks C# while compiling and is not part of the engine runtime. Using a GPL tool does not by itself impose GPL on independent input/output; incorporating GPL-covered code into the output is a different situation. Therefore, do not copy analyzer implementation code into the product, link its libraries into the engine, or bundle the analyzer in product releases. Any future redistribution of the tool itself must preserve and satisfy its own license separately.

Use a pinned NsDepCop PackageReference with `PrivateAssets="all"` and exclude `compile;runtime` assets while retaining the analyzer and required build integration assets. PrivateAssets controls propagation, not whether code enters the current project's output; it is not sufficient evidence by itself. Verify resolved assets, compiler references, native publish inputs, and final release contents for the selected package version and transitive dependencies. Keep the analyzer in the build-tool inventory, separate from the shipped-product dependency inventory.

No GPL exception or legal conclusion is inferred merely from an absent DLL: Native AOT can embed linked code. Release evidence must establish that no analyzer code is a compiler/runtime/link input and that no analyzer sources or binaries are bundled. This document defines the intended separation; verification occurs during implementation.

## Namespace boundaries

Proposed layout, using the accepted namespace root `Merstride`:

| Source area | Allowed internal dependencies |
| --- | --- |
| Domain | Domain only |
| Application | Domain, Application |
| Compilation | Domain, Compilation |
| Infrastructure | Domain, Application, Infrastructure |
| Cli | Composition and invocation of the other areas |

Domain contains workflow definitions, outcomes, and execution invariants. It must not depend on concrete CLI, YAML parsing, persistence, process adapters, or external infrastructure frameworks. Application coordinates use cases through narrow ports. Compilation converts document input into domain definitions. Infrastructure implements process and persistence ports. Keep these as logical boundaries; do not create one project, interface, or abstraction per namespace without a concrete need.

Define rules in version-controlled config.nsdepcop files, covering root namespaces and descendants. Prefer an explicit internal allowlist so new namespaces are not silently unrestricted. Restrict external dependencies in Domain as well. If bounded contexts are introduced later, permit cross-context access only through explicitly designated contracts; do not invent multiple contexts for this small engine.

Configure illegal namespace and assembly references as errors (`NSDEPCOP01` and `NSDEPCOP07`). Required analyzer configurations must exist and analysis must not be disabled in CI. Missing/disabled analysis must fail the build gate rather than produce a misleading clean result. Do not permit blanket suppressions or broad allow-all rules that defeat the intended boundaries. Document narrow exceptions with a reason.

NsDepCop enforces static dependencies, not aggregate invariants, business correctness, reflection-based dependencies, or every DDD practice. Behavioral tests remain necessary.

## Acceptance and sources

P01 implements the positive and negative build fixtures under A21. P07 verifies release separation under A22, repeating it after dependency/build changes. These checks are development tests and require an SDK; they do not introduce a product runtime dependency.

- [NsDepCop project and license](https://github.com/realvizu/NsDepCop)
- [NsDepCop diagnostic reference](https://github.com/realvizu/NsDepCop/blob/master/doc/Diagnostics.md)
- [GNU FAQ on output of GPL tools](https://www.gnu.org/licenses/gpl-faq.en.html#GPLOutput)
- [NuGet asset controls](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
