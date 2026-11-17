# Merstride development

Implement one approved local ticket at a time. The baseline discovery and breakdown
are complete. Read docs/CONTRACT.md and relevant docs/adr/ before changing behavior;
the contract and features/ are normative. Use GLOSSARY.md for domain terminology.

Follow CONTRIBUTING.md for prerequisites, verification and dependency/privacy rules.
Use the installed to-tickets skill for new breakdowns and implement, tdd and
code-review for implementation. Behavior tests use the published native CLI;
architecture/policy tests use the public build boundary. Required Gherkin steps
must fail if undefined, ambiguous, pending or skipped. Preserve scenario tags.
Use English for development artifacts and CLI messages.
Use Bash for general documentation examples; retain explicit platform/interpreter
examples. Agents may adapt commands to the available shell.
Human walkthroughs use readable default output; reserve --json and JSON tools
for examples that explicitly explain automated integration.

Local tickets and their history remain in `.scratch/merstride/issues/`; see
docs/agents/issue-tracker.md and docs/agents/triage-labels.md. Follow the assigned
ticket/session's acceptance and local-commit instructions and stop at its scope.
Do not push, publish or deploy without explicit authorization.

Keep raw verification logs and environment dumps under ignored artifacts/.
Version concise result summaries without personal paths or machine identities.
Run scripts/repository-privacy.ps1 before committing; preserve dependency and
license inventories. Consult docs/STATUS.md for current evidence and outstanding
release qualification; local gates do not establish release readiness.
