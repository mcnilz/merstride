# Implementation status

P01–P08 and the M01 .NET 11 / C# 15 migration are locally accepted and committed.
P08 adds English command help and System.CommandLine 3.0.0 while preserving
protocol version 1. No further feature slice is implemented here.
**Release qualification remains incomplete and publication fails closed.**

## Historical acceptance summary

These are historical reported results, not newly repeated checks or retained raw
logs. Reproduce current results using the scripts below.

| Slice | Local acceptance summary |
| --- | --- |
| P01 | Native workflow bootstrap, strict Gherkin runner and build policies |
| P02 | Deterministic process chains; 40 tests / 21 local feature cases |
| P03 | Existing-agent handoffs; 78 tests / 31 local feature cases |
| P04 | Authoring and graph validation; 162 tests / 61 local feature cases |
| M01 | .NET 11 / C# 15 migration; 165 tests / 61 local feature cases |
| P05 | Execution and context limits; 200 tests / 75 local feature cases |
| P06 | Recovery and concurrent snapshots; 217 tests / 82 local feature cases |
| P07 | Local native distribution and release controls; initial 252-test gates on both platforms, followed by 15 focused Package/release checks on both; no full final 259-test claim |
| P08 | Complete Windows/Linux gates: 324 tests / 155 local feature cases each; independent coordinator: full Windows gate and 80 affected Linux tests |

P08 historical native sizes were 6,121,984 bytes (Windows x64) and 6,246,992 bytes
(Linux x64). Both met the 25 MiB size gate and 150 ms warm status P95 gate.
These local measurements do not qualify clean target machines or token savings.
Both review axes reported no remaining actionable P08 findings. Raw local logs,
compiler/linker inputs and environment dumps are not versioned.

## Reproduction and remaining release work

Run `pwsh -NoProfile -File scripts/build.ps1` separately on Windows and Linux with
the pinned SDK and platform prerequisites in [RELEASING.md](RELEASING.md).
The local gate executes all 155 local feature cases and supplemental tests;
required undefined, ambiguous, pending or skipped steps fail. It also checks
architecture, locked dependencies, distribution/notices, workflow policies,
Mermaid compatibility and native budgets. Local reports stay under ignored
`artifacts/`. See [TESTING.md](TESTING.md) for the privacy check and report policy.

Run `scripts/build.ps1 -ReleaseQualification` for the separate release scenarios.
They fail explicitly with `RELEASE QUALIFICATION UNAVAILABLE` until actual
independent platform reports exist; their absence is not counted as a pass.
The full independent native clean-target driver is still missing. Both actual
clean Windows/Linux reports, all 140 clean runtime cases plus the separately
mandatory 15 build-only cases, and the real external-agent token experiment
remain required. A minimal Linux no-runtime smoke is not full qualification.

Hosted GitHub execution, fork behavior and branch/tag controls remain unverified.
SDK-host builds, static policy checks and simulated release fixtures do not
establish those outcomes. No release publication is authorized.
