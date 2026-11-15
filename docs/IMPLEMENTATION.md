# Implementation with coding agents

Revision 0.5. All development artifacts and communication recorded in the repository are English.

## Technology decision

.NET 11 Native AOT with stable C# 15 is accepted by the post-P04 human technology decision (ADR 0004). It fits the maintainer's C# experience. Prove native publish, constrained YAML parsing, source-generated System.Text.Json, locks, process invocation, size, and startup cost early. Avoid reflection-dependent production libraries, dynamic code generation, renderer dependencies, and framework-heavy CLI plumbing. Use NUnit 4 without FluentAssertions. Lock dependencies and review transitive licenses.

Rust and Go remain alternatives, not parallel implementations. If .NET misses a measured constraint, first remove avoidable dependencies; then document a single alternative decision.

## Matt Pocock workflow

Skill names were checked in the official repository at commit `5863806dd4e15c63174bee4e7515a4256792ecfa`. Read the actually installed versions before use; this package does not bundle them.

1. `setup-matt-pocock-skills`: configure the repository and local issue tracker.
2. `grill-me`: clarify only remaining decisions, not the entire already-specified product.
3. `to-spec`: reconcile approved decisions with this product spec, technical contract, and Gherkin requirements.
4. `to-tickets`: review the vertical slices, then create one local issue per approved slice with blockers and scenario tags.
5. `implement`: implement one approved ticket, using `tdd` and `code-review` under their current instructions.
6. Demonstrate each completed slice with its native binary, scenario results, known limitations, and next proposed slice; obtain acceptance before proceeding to the next package.

The baseline, P01–P07 tickets and public native CLI/build-policy test seams are accepted. The coordinating chat accepts and commits completed slices under user delegation; implementation chats stop uncommitted at their assigned slice. M01 adds the explicitly requested .NET 11 migration before P05. Do not push, publish issues, or deploy solely because a skill suggests a local commit.

## Vertical slices

| Slice | User-visible result | Dependencies | Acceptance scope |
| --- | --- | --- | --- |
| P01 | Native binary starts, persists, reads, and completes a single-end YAML workflow; guide works | None | End-to-end smoke, invalid YAML, A20; initial native budgets; NsDepCop rules and A21 build fixtures; feature runner and strict undefined-step handling; GitHub build workflow and A23/A24 policies |
| P02 | A deterministic process chain runs without model calls | P01 | 02-processes; process feasibility measurements; literal args and real logs |
| P03 | Existing agent receives, completes, and resumes tasks | P02 | 03-agents; agent guide matches actual protocol |
| P04 | Full supported embedding and graph validation are unambiguous | P03 | 01-authoring; complete YAML restrictions; development-only Mermaid compatibility check |
| M01 | Migrate the toolchain to .NET 11 / C# 15 without product changes | P04 | Existing CLI/build regression gates; ADR 0004 |
| P05 | Loops, time, output, and context stay bounded | M01 | 04-limits including exact boundaries |
| P06 | Crashes and concurrent access cannot silently replay effects | P05 | 05-recovery; OS-specific interruption and process-tree evidence |
| P07 | Native release is portable and its benefit measured | P06 | 06-interface plus all applicable features; Windows/Linux; A22 distribution checks; license and benchmark reports; release workflow and A25 |
| P08 | Discoverable English help and predictable argument handling preserve protocol JSON | Accepted P07 local implementation | 09-cli-usability; established parser review; Cli-only build boundaries; native Windows/Linux regression and budget gates |

Feature-level tags identify the main owning slice; individual tags may identify earlier ownership. P01 implements the single-end smoke scenario in 00-bootstrap.feature. It MUST use the real YAML path, not a temporary JSON-only format. Later unimplemented syntax must fail explicitly. Initial locks belong in P01; full concurrency/crash acceptance belongs in P06.

## Definition of done

- Implement behavior through the agreed public boundary, one red-green cycle at a time.
- Bind the actual Gherkin scenarios; no undefined/pending steps in the slice's gate.
- Run relevant acceptance tests against the published native binary.
- Keep guide, example, contract, and implemented subset consistent.
- No unjustified AOT/trimming warning suppression; record size and startup evidence as applicable.
- Review findings are fixed or explicitly recorded. Do not invent platform checks.
- Update active state with completed slice, test evidence, limitations, and next slice.
- No unrelated functionality, remote publication, or deployment.

## Original setup decisions (accepted; retained for context)

1. .NET AOT first, and the stated size/startup budgets.
2. CLI-only MVP and sequential execution; MCP/parallelism later.
3. Public CLI test seam, proposed slices, and development-only Gherkin runner choice.
4. Local branch/commit rules. The Merstride name and MIT licensing are already decided.

MIT licensing, NsDepCop for the C# build, English development, an agent guide, YAML metadata, and Gherkin acceptance scenarios are requested requirements, not remaining questions.

## Required architecture checks

Use NsDepCop for C# namespace dependency rules from P01 onward. Follow [ARCHITECTURE.md](ARCHITECTURE.md). Its build-fixture tests are a separate public build boundary, not private runtime-unit tests. Enforce violations as build errors and include a deliberately invalid fixture to prove the analyzer actually runs. A later language change requires an equivalent enforcement decision because NsDepCop analyzes C# only.

P01 also implements GitHub CI and dependency policy; P07 implements tested release automation. See GITHUB.md and DEPENDENCIES.md. Public GitHub hosting, official-only current Actions, and established libraries are decided requirements. The GitHub owner remains to be supplied when creating the `merstride` repository.
