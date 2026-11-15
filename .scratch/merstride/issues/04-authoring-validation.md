# P04: Unambiguous workflow authoring

**What to build:** Compile the complete supported Markdown, Mermaid, and constrained YAML format with precise validation.

**Blocked by:** P03.

**Status:** ready-for-agent

**Acceptance scope:** @P04, 01-authoring; @A01 @A05 @A14 @A19.

- [ ] Select marked workflow pairs while respecting enclosing fences.
- [ ] Validate the full graph and YAML restrictions and preserve multiline prompts and string arguments.
- [ ] Reject oversized documents and graphs before execution.
- [ ] Preserve frozen definitions after source edits; check supported Mermaid rendering in development only.
