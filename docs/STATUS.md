# Current status

Merstride implements sequential native Markdown workflows with `process`, `agent`
and `end` nodes. Supported behavior includes multi-workflow selection, constrained
YAML/Mermaid validation, frozen definitions, literal process arguments, bounded
execution and selected context, persistent runs, idempotent completion receipts,
writer exclusion, concurrent snapshots and explicit crash recovery. English help
and the offline guide describe protocol version 1. Inline outcome parameters are
the preferred completion path; file completion remains compatible. The embedded
`creation-guide` supplies offline authoring instructions, a complete copyable
workflow and validation checklist without external content files.
Process authoring also supports shebang-selected inline scripts over UTF-8 stdin
and disjoint Windows/macOS/Linux execution variants, with no script-file transport
or interpreter provisioning. Direct command/args execution remains available.

The product uses .NET 11 Native AOT and stable C# 15. Windows x64 and Linux x64
have local development-host verification. **Release qualification is incomplete
and publication fails closed.** No clean-target or token-saving claim follows
from a local build.

## Verification

On 2026-11-16, P12 passed the complete Windows x64 local gate: 434 tests /
265 local feature cases, no failures or skips, native publish with no
warnings/errors, architecture/dependency/distribution/privacy checks and Mermaid
compatibility including the new inline example. The 45 dedicated P12 cases cover
frozen scripts/variants, multiline Unicode, literal shebang arguments, absolute/
relative/PATH resolution, env forms, malformed definitions, overlap rejection,
missing interpreter/platform coverage, streams, exit routing, bounded input/output,
timeouts, early exit, interruption, explicit retry and retained activation history.
Actual installed interpreters were PowerShell 7.6.10, Python 3.12.10 and Git Bash
5.3.15 on Windows; Git Bash execution is not Linux evidence. The complete portable
example validated and executed on Windows. Both standards/spec reviews had no
outstanding findings. P11's 28 offline-guide cases and all prior local regressions
remain in the passing suite.
The executable measured 5.957 MiB; warm `status` P95 was 19.180 ms, within the
25 MiB / 150 ms gates.

The most recent prior full Linux x64 (WSL2) verification passed 361 tests / 192
local feature cases with no failures/skips and met size/status gates. Linux was
not rerun for P11 or P12; neither slice has new Linux execution evidence. No macOS
environment was available and no macOS execution is claimed. P12's macOS/Linux
variant names and shared code do not qualify those targets. These are
development-host results, not clean-target or cold-boot qualification.

Reproduce the complete local gate with
`pwsh -NoProfile -File scripts/build.ps1` on each platform. Raw logs, TRX, traces,
audits and timing records stay under ignored `artifacts/`. See
[Testing](TESTING.md) for assertions and [Contributing](../CONTRIBUTING.md) for tools.

## Outstanding release work

- Implement the independent native clean-target suite driver. The NUnit host
  requires .NET and cannot serve as the no-runtime driver.
- Collect actual Windows 11 x64 and Linux x64 clean-platform reports for the exact
  candidate commit and binary hashes: all 250 runtime cases, plus the separately
  mandatory 15 build-only cases. A minimal Linux chroot smoke is insufficient.
- Run the real external-agent token experiment with the same agent/model/task
  and equivalent results. No token totals or savings percentage are established.
- Verify hosted GitHub execution, fork isolation and branch/tag controls. Static
  policy checks and simulated release fixtures do not prove hosted outcomes.

Use [release verification](RELEASING.md) for collection commands and provenance.
The two release-environment scenarios fail explicitly with
`RELEASE QUALIFICATION UNAVAILABLE` while independent evidence is absent. They
remain mandatory; local success does not waive them.
