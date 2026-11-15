# P04: Unambiguous workflow authoring

**What to build:** Compile the complete supported Markdown, Mermaid, and constrained YAML format with precise validation.

**Blocked by:** P03.

**Status:** complete; accepted by coordinator; local commit authorized

**Implementation:** complete; uncommitted for coordinator acceptance against P03
`P03 implementation`.

**Acceptance scope:** @P04, 01-authoring; @A01 @A05 @A14 @A19.

- [x] Select marked workflow pairs while respecting enclosing fences.
- [x] Validate the full graph and YAML restrictions and preserve multiline prompts and string arguments.
- [x] Reject oversized documents and graphs before execution.
- [x] Preserve frozen definitions after source edits; check supported Mermaid rendering in development only.

Evidence: full build gate passed 162 tests, 61 actual P01–P04 feature cases (30 P04),
0 failed/skipped; native 4.902 MiB, warm status P95 27.172 ms; actual official
Mermaid 12.3.0 rendered five fixtures and reproduced nine expected rejections.
Separate Standards/Spec review findings were fixed; both follow-ups have zero
residual findings. docs/STATUS.md records exact results, retained failed startup
measurement, limitations and reproduction. ADR 0003 records the coordinator-accepted
contiguous-token/reserved-ID interpretation without changing the baseline.
No later slice or migration was started. Independent coordinator verification passed;
subsequent .NET 11 migration belongs to its own chat before P05.
