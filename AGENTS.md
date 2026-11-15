# Merstride development

Implement the accepted baseline in docs/CONTRACT.md and docs/IMPLEMENTATION.md.
Work one approved slice at a time. The user delegated acceptance of P02–P07 to the
coordinating chat: it reviews each completed slice, commits locally, and starts the
next implementation chat sequentially. Implementation chats stop at their slice.
Use English for development artifacts and CLI messages.
Use the public CLI for behavior tests and the public build boundary for architecture/policy tests.
Keep required Gherkin steps strict: undefined, ambiguous, pending, or skipped steps fail.
Implementation chats leave changes uncommitted for coordinator acceptance. The user
authorized the coordinator to commit between slices. Do not push or publish.

## Agent skills

Use the installed to-tickets, implement, tdd, and code-review skills for implementation.
The baseline and P01–P07 breakdown are accepted; discovery and breakdown approval are complete.
Local tickets: one file per slice in `.scratch/merstride/issues/`; see docs/agents/issue-tracker.md.
Domain terminology: GLOSSARY.md. Read relevant decisions in docs/adr/ before changes.
Triage vocabulary: docs/agents/triage-labels.md.
