# P10: Consolidate the repository around current use and continued development

**What to build:** Present Merstride as a coherent current project when someone
opens it on GitHub. Make the README a concise entry point explaining the product,
how people and existing agents use it, and how contributors continue development.
Remove obsolete onboarding material and excessive implementation chronology from
the current tree instead of requiring readers to reconstruct the development story.

**Blocked by:** P09 preferred inline completion (complete). No implementation
blocker. Outstanding P07 release qualification remains required for publication.

**Status:** complete — implemented and reviewed in the separately requested chat.

**Acceptance scope:** Documentation and repository organization. Retain all
normative behavior and existing acceptance-scenario tags. No feature redesign.

- [x] Lead the README with what Merstride does, its intended use, Mermaid control
      flow/YAML behavior and the deterministic existing-agent handoff model. Explain
      the sequential native CLI and lack of built-in LLM integration plainly.
- [x] Give a short, internally consistent quick start using existing examples and
      actual supported commands. Explain the agent loop, returned request/outcomes,
      preferred parameter-based completion, optional inline JSON data and explicit
      subsequent advance. Link to details instead of duplicating whole contracts.
- [x] Make it easy to find workflow authoring, CLI help, agent operation,
      development prerequisites, verification and contribution instructions.
      Clearly distinguish using the application from developing it.
- [x] Remove revision narratives, obsolete draft/setup language, slice-by-slice
      implementation storytelling and statements that implemented behavior belongs
      to future work. Describe the current product and real limitations accurately.
- [x] Review Markdown files for current value. Remove the obsolete repository
      start prompt and consolidate or remove superseded onboarding, naming,
      implementation coordination and historical status material where redundant.
      Do not retain removed content in a new tracked history/archive directory.
- [x] Preserve useful architectural decisions, the normative execution contract,
      real feature files, dependency inventories, MIT and third-party notices,
      security/contribution guidance and concise actionable development rules.
      Preserve the local issue tracker and its history unless a separate user
      instruction authorizes changing it.
- [x] Keep a concise current-state summary and actionable outstanding work. Do not
      describe P07 as release-qualified: the independent native clean-target driver,
      full clean-platform reports, external-agent token experiment and hosted
      controls remain incomplete or unverified. Do not invent published releases
      or installation options that do not exist.
- [x] Update all references after consolidation, including links, development
      instructions and any build/test fixtures that depend on removed files.
      Continued development must require no old setup prompt or repeated discovery.
- [x] Check local Markdown links and example commands, and run affected public
      CLI/build checks where appropriate. Preserve strict feature execution,
      architecture/dependency/release gates and privacy protections. Keep raw
      logs/environment data ignored and retain only concise safe summaries.
- [x] Deliver the consolidation as ordinary new local commit(s). Do not rewrite,
      rebase, filter or otherwise alter existing Git history. Do not push, publish,
      create releases or deploy. Review the final changes before committing.

Creating this issue does not authorize implementation in this chat or dispatch a
new agent chat. The user will initiate the separate implementation chat.

## Completion evidence

The README now leads with the product and a checked CLI/agent quick start.
Contributor and agent rules describe continued development without setup or slice
chronology. Removed the obsolete start prompt and redundant implementation,
coordination, naming, package-validation and domain-pointer documents; retained
normative behavior, decisions, scenario tags, licenses, inventories and ticket
history. Current status lists unfinished release qualification explicitly.

Windows x64 full local gate: 361 passed tests / 192 local feature cases, zero
failures/skips; native publish, architecture/dependency/distribution/privacy and
Mermaid checks passed. README commands, four workflow validations, inline JSON,
process-chain/log checks and local Markdown links passed. Standards and spec
reviews reported zero actionable findings. Linux was not rerun for documentation
changes. Raw verification remains under ignored artifacts/.

Delivered as an ordinary local commit; no history rewrite, push or publication.
The pre-existing `.idea/` ignore edit remains outside this commit.
