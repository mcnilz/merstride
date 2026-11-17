# P16: Prefer Bash for general tooling and documentation examples

**What to build:** Developers encounter readable Bash examples as the default
in the README and most general documentation, including CLI usage and routine
tooling instructions. Keep PowerShell examples where the documented task or
script depends on that platform or interpreter. General documentation should
not require readers to translate PowerShell before trying Merstride.

**Blocked by:** None (can start immediately).

**Status:** complete — implemented, locally verified and reviewed on 2026-11-17.

**Approval and delivery boundary:** The user requested one new local issue.
This session records the scope only; implementation and any local commit require
a separately assigned implementation session and its acceptance instructions.
Keep the current branch unchanged during issue creation. No push, publication
or deployment is authorized.

**Implementation session:** The user assigned the next ready issue on 2026-11-17.
P16 is the next unblocked implementation scope. Implement and verify on the
current branch, review both standards/spec axes, and make the local commit under
the installed implementation workflow. No remote operation is authorized.

**Current behavior:** The README quick start explicitly assumes PowerShell and
the Windows executable. General CLI, agent handoff and recovery examples also
use PowerShell syntax and JSON conversion. Repository build and verification
entry points include existing PowerShell scripts that must remain usable.

- [x] Use Bash as the primary shell for the README quick start and general
      command examples across the maintained documentation. Survey contributor,
      CLI, agent, authoring, recovery and tooling guidance for the same policy.
- [x] Translate examples into working Bash syntax, including executable paths,
      variables, quoting, JSON argument handling and extraction of run/request
      identifiers. Declare any required JSON utility and other prerequisites.
      Preserve the demonstrated CLI behavior and integration error handling.
- [x] Retain PowerShell examples for platform-dependent tasks and scripts that
      require PowerShell, with a clear explanation where needed. Existing
      PowerShell build and verification scripts remain valid entry points;
      this ticket does not require rewriting them or adding Bash wrappers.
- [x] Preserve deliberate interpreter-specific workflow examples and platform
      variants. Do not mechanically relabel PowerShell code fences as Bash or
      imply that Windows commands run unchanged on Unix systems.
- [x] Keep Bash examples canonical for general instructions, including guidance
      used by agents. Agents can adapt them to PowerShell when their environment
      requires it; do not duplicate every general example in both shells.
- [x] Verify changed runnable examples in Bash against the published native CLI
      and record the actual environment and any limitations. Keep the README,
      guides and prerequisite descriptions consistent. Preserve existing
      scenario tags and required checks; new product behavior is not intended.

**Out of scope:** CLI or workflow execution changes, removing Windows support,
replacing platform-dependent scripts, release qualification, and rewriting
historical evidence or local ticket history solely to change shell style.

**Implementation and evidence:**

- README, CLI, agent and recovery instructions now use quoted Bash commands and
  jq extraction; contributor/agent guidance establishes that convention. Caller
  prerequisites are explicit. Native Windows workspace paths use pwd -W or
  cygpath; Linux/macOS examples retain native Unix paths and binary names.
- Mermaid tooling examples use Bash. Existing PowerShell verification entry
  points remain unchanged and are invoked with pwsh, including release collection
  commands. Authoring examples retain explicit PowerShell and platform variants.
- Executed the README, CLI and agent-guide Bash blocks against the published
  native CLI on Windows x64 with Bash 5.3.15 (OSTYPE=cygwin), installed jq 1.7
  (reports jq-1.7-dirty), PowerShell 7 and SDK 11.0.100. Verified paths with spaces,
  IDs/state-root extraction, completion, dynamic multiline Unicode/quoted JSON,
  and exit-4 error preservation for invalid runs/data. Actual interrupted native
  fixture runs exercised both recovery retry and fail after effect inspection.
  The Bash Mermaid commands and privacy script invocation also passed.
- Complete Windows gate: 520 tests / 299 local feature cases, all 284 independent
  native runtime cases, zero failures/skips and no build/publish warnings/errors.
  Architecture/dependency/distribution/privacy/Mermaid checks passed. Native size
  5.974 MiB, warm status P95 29.477 ms. Raw evidence stays under ignored artifacts.
- Independent Standards review: zero actionable findings. Independent Spec review:
  zero actionable findings; its initially pending evidence summary is now recorded
  here and in docs/STATUS.md. No production or feature/tag changes, dependency
  additions, Linux/macOS execution, hosted run, release qualification, remote push
  or publication. P07's external qualification remains separately outstanding.
