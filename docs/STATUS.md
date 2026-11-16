# Implementation status

P09 is locally accepted for commit. It makes inline agent completion the preferred
path in the CLI, documentation and normal agent/limit/recovery tests.
P01–P08 and the M01 .NET 11 / C# 15 migration are locally accepted and committed.
P08 adds English command help and System.CommandLine 3.0.0 while preserving
protocol version 1; P09 retains that protocol and file compatibility.
**Release qualification remains incomplete and publication fails closed.**

## P09 inline completion

Use `complete <run-id> --request <id> --outcome <allowed-outcome>` and optional
`--data <json-object>`; a completion file is no longer necessary. File mode remains
an explicitly documented compatibility alternative. Exactly one mode is required;
mode/argument errors exit 2, rejected JSON/data/request/outcome exits 4, without
mutating authoritative state. Both modes reuse the existing domain validation,
completion transition and atomic receipt persistence. Protocol version stays 1;
there are no new dependencies or storage schemas.

The default agent, context, receipt, completion-limit and competing-writer test
helpers now use parameters. File format/UTF-8, legacy defaults and cross-mode
compatibility retain targeted file coverage. The Windows full run recorded 29
successful direct inline completion calls and three file-mode compatibility calls;
the separate PowerShell case also passes inline JSON. This count excludes help
and invalid submissions, rather than treating every CLI invocation as completion.

Test-first verification began with a failing native outcome-only scenario, then
added inline data, rejection/limit, cross-mode and offline-discovery coverage.
All 37 new actual feature cases pass on Windows and Linux. Final complete gates
each passed 361 tests / 192 local feature cases, with zero failed or skipped tests.
Native AOT publish had zero warnings/errors. All 93 compared product, test, feature,
build and policy sources match across checkouts after LF normalization.

| Final measurement | Windows x64 | Linux x64 / WSL2 |
| --- | ---: | ---: |
| Native executable excluding symbols | 6,124,032 bytes / 5.840 MiB | 6,247,008 bytes / 5.958 MiB |
| First measured start | 36.005 ms | 21.601 ms |
| First measured status | 26.446 ms | 8.502 ms |
| 30 fresh warm status processes, P95 | 28.440 ms | 11.435 ms |
| Ten two-helper advances, P95 | 190.485 ms | 44.120 ms |
| Ordered native effects checked | 20 / 20 | 20 / 20 |

Both platforms meet the 25 MiB size and 150 ms status P95 gates. These are
development-host measurements, not clean-target or cold-boot qualification.

The initial Linux full run passed 360/361 tests: the existing explicit PowerShell
case reached its unchanged five-second timeout while ordinarily traced. A bounded
PowerShell-to-CLI probe recorded the same two successful execve events in both
modes, taking 3.117 s ordinarily versus 0.786 s with kernel filtering. Requesting
seccomp-BPF keeps follow-forks/execve observation and every deadline/count assertion,
while avoiding stops on irrelevant syscalls when supported. Both focused PowerShell
cases then passed, followed by the final complete gates above. The initial failure
is retained locally; no failed run is presented as a pass.

### Standards review

Zero actionable findings, including the Linux observation follow-up. CLI-only
production changes, shared validation and proportional public tests follow the
documented boundaries; no dependencies or speculative abstractions were added.

### Spec review

Zero actionable findings, including the Linux observation follow-up. Inline input
dominates normal examples/tests, compatibility and receipt semantics are retained,
and required feature bindings remain strict.

Privacy checks and a redacted full-history/staged secret scan passed. Raw logs,
TRX, traces, input audits and detailed local reports remain ignored under artifacts.
The two external release cases were executed separately and both failed explicitly
with RELEASE QUALIFICATION UNAVAILABLE, not undefined or skipped steps. Full local
results remain separately retained in the privacy-safe local test summary.

Next work remains P07's native clean-target driver and actual release qualification;
no subsequent feature slice or publication is authorized by P09.

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
The local gate executes all 192 local feature cases and supplemental tests;
required undefined, ambiguous, pending or skipped steps fail. It also checks
architecture, locked dependencies, distribution/notices, workflow policies,
Mermaid compatibility and native budgets. Local reports stay under ignored
`artifacts/`. See [TESTING.md](TESTING.md) for the privacy check and report policy.

Run `scripts/build.ps1 -ReleaseQualification` for the separate release scenarios.
They fail explicitly with `RELEASE QUALIFICATION UNAVAILABLE` until actual
independent platform reports exist; their absence is not counted as a pass.
The full independent native clean-target driver is still missing. Both actual
clean Windows/Linux reports, all 177 clean runtime cases plus the separately
mandatory 15 build-only cases, and the real external-agent token experiment
remain required. A minimal Linux no-runtime smoke is not full qualification.

Hosted GitHub execution, fork behavior and branch/tag controls remain unverified.
SDK-host builds, static policy checks and simulated release fixtures do not
establish those outcomes. No release publication is authorized.
