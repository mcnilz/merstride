# Established dependencies only

This policy applies to product dependencies, development tools and tests.

Prefer .NET BCL APIs. A dependency must solve a concrete requirement, have an identifiable upstream, meaningful release/maintenance history, documented API and license, and evidence of established use. An unfamiliar tiny package is not acceptable merely because it saves a few lines. Star/download counts alone are not sufficient. Apply this to direct and transitive dependencies, build tools, and tests.

Record package ID, exact version, upstream, purpose, maintenance evidence/date, license, runtime versus development-only classification, transitive graph, and Native AOT evidence where relevant. Inspect lock-file changes in review. Unknown, abandoned, or inadequately documented dependencies block adoption until replaced or explicitly reassessed; do not silently add them. NsDepCop is already explicitly selected and retains its build-only licensing exception.

## Current choices

| Area | Choice | Policy |
| --- | --- | --- |
| JSON, process execution, IO, hashing | .NET BCL / System.Text.Json source generation | Preferred; no wrapper packages |
| YAML | YamlDotNet from aaubry/YamlDotNet | Low-level parsing with explicit constrained-profile validation; native verification required |
| Tests | NUnit 4 with official NUnit adapter and Microsoft test tooling as required | Selected family; pin reviewed versions |
| Gherkin syntax | Official Cucumber Gherkin parser | Development-only syntax parser |
| Gherkin execution | Explicit NUnit-backed bindings | Executes actual feature files; rejects unknown/ambiguous steps |
| Architecture | NsDepCop | Explicitly selected build-only analyzer |
| CLI | System.CommandLine 3.0.0 | Generated help and parsing in Cli only; exact review in dependency-inventory.json |
| Mermaid subset | Repository-owned parser for the documented small subset | No renderer or unknown Mermaid-engine package |

The Gherkin parser alone is not a runner. Keep the NUnit adapter small and explicit, execute actual feature files, and reject unknown/ambiguous steps. Do not create a general BDD framework. If that becomes substantial, assess an established framework rather than expanding custom infrastructure.

YamlDotNet is not automatically approved for a reflection-based deserializer. Prefer an appropriate low-level parsing path with explicit validation, then demonstrate AOT compatibility, size budget, duplicate-key handling, alias rejection, and YAML scalar semantics. Avoid replacing established YAML parsing with a custom general YAML implementation.

Exact versions, hashes, provenance, licenses and verification evidence are recorded in dependency-inventory.json, shipped-dependencies.json and the platform lock files. See [build tools](BUILD-TOOLS.md) and [Mermaid tooling](MERMAID-VALIDATION.md) for development-only reviews. All shipped licenses remain compatible with the MIT distribution policy in ARCHITECTURE.md.

Sources: [YamlDotNet](https://github.com/aaubry/YamlDotNet), [Cucumber Gherkin](https://github.com/cucumber/gherkin), [NUnit](https://github.com/nunit/nunit), [NsDepCop](https://github.com/realvizu/NsDepCop).
