# P03: Existing-agent task handoffs

**What to build:** Pause for an existing agent, accept results, and resume deterministic work across CLI processes.

**Blocked by:** P02.

**Status:** ready-for-agent

**Acceptance scope:** @P03, 03-agents; @A03 @A04 @A06 @A07 @A08 @A09 @A17.

- [ ] Return stable current requests, literal prompts, selected context, and sorted allowed outcomes.
- [ ] Validate completions without mutation on rejection; preserve idempotent receipts and detect conflicts.
- [ ] Give loop activations fresh request IDs and expose context availability and source identity.
- [ ] Match the agent guide to actual protocol behavior.
