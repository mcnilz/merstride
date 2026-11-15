# M01: Migrate the native toolchain to .NET 11 / C# 15

**What to build:** Preserve all accepted P01–P04 CLI behavior on stable .NET 11
Native AOT and C# 15, following the user's explicit post-P04 technology change.

**Blocked by:** P04, accepted at P04 implementation.
**Blocks:** P05.
**Status:** complete; accepted by coordinator; local commit authorized.
**Acceptance scope:** Existing P01–P04 scenario tags remain unchanged; public
native CLI and public build/policy seams. No P05 behavior or new product scope.

- [x] Pin the stable SDK and migrate production, tests, native helpers and build fixtures.
- [x] Review updated compiler/linker locks, provenance and licenses.
- [x] Keep the global language skill external, with no memory-safety preview opt-in.
- [x] Pass the complete native regression, architecture, dependency and Mermaid gates.
- [x] Record native size/startup/process evidence and actual limitations.
- [x] Complete independent Standards and Spec reviews; leave changes uncommitted.

Evidence and reproduction: docs/STATUS.md; technology decision: ADR 0004.

Full gate: 165 passed, 0 failed/skipped; 61 actual P01–P04 feature cases. Native
4.972 MiB, warm status P95 30.831 ms, process-chain P95 117.863 ms with 20/20 ordered
effects. Official Mermaid checks passed. Standards: zero residual findings after
correcting a stale evidence filename. Spec: zero findings. Platform, tracing and
P07 release limitations remain explicit. Reproduce with the reusable full build gate.
