# P15: Native macOS releases verified entirely through GitHub Actions

**What to build:** Build, test and package Merstride for Apple Silicon
(`osx-arm64`) on GitHub-hosted macOS runners, then include the verified archive
in the existing SemVer tag release. Development and verification must not require
the maintainer to own a Mac or operate a self-hosted macOS runner.

**Blocked by:** None for implementation; P14 is complete. Actual publication
remains blocked by P07's outstanding qualification, required repository controls
and successful hosted macOS evidence. Reuse the independent native clean-target
suite driver required by P07, implementing its macOS support where needed;
do not replace it with the .NET-hosted acceptance runner.

**Status:** complete — hosted macOS build, managed/native acceptance, package,
separate no-system-.NET extracted-archive qualification and aggregate gate passed
in run 44024602627 for commit `39f864d`. P07 release prerequisites remain open.

**Approval and delivery boundary:** The user requested a new issue after
confirming that no personal Mac is available and discussing GitHub-hosted macOS
execution. This session records the scope only. Implementation and any local
commit require a separately assigned implementation session and its acceptance
instructions. No push, hosted workflow dispatch, remote tag, release publication
or deployment is authorized by issue creation. A later implementation must
prepare reviewable changes before seeking any still-required remote authorization.

**Current behavior:** P14 retains the Windows x64/Linux x64 release path and
records a macOS deferral. The build and benchmark scripts classify non-Windows
hosts as Linux; native acceptance observes processes through Linux strace and
/proc. Dependency locks, distribution audits, package/manifest validation and
qualification collection do not yet cover macOS. A missing personal Mac is not
the technical blocker: GitHub-hosted macOS runners can supply execution evidence.

**Acceptance scope:** Use the published native CLI for product behavior and the
public build/release boundary for policy and architecture. Preserve existing
scenario tags and add dedicated @P15 cases where appropriate. Required scenarios
must fail on undefined, ambiguous, pending or skipped steps.

- [x] Select and document a supported GitHub-hosted Apple Silicon macOS runner
      and minimum supported macOS version. Install the pinned SDK and required
      native tooling using reviewed setup mechanisms. Build both the actual
      Native AOT executable and native test fixture for osx-arm64; do not label
      cross-compiled or Linux results as macOS execution evidence.
- [x] Distinguish Windows, Linux and macOS explicitly in build, benchmark,
      fixture and evidence handling. Provide macOS-capable external process
      observation that detects short-lived child launches as required by the
      existing tests. Verify real interruption, attached child termination,
      timeout/output limits, concurrent snapshots, atomic replacement and explicit
      recovery on macOS. Preserve existing assertions rather than skip or weaken
      checks that currently depend on strace, /proc or Windows Job accounting.
- [x] Add and review macOS-specific dependency lock coverage and compiler/linker
      inputs. Adapt distribution auditing to actual macOS native outputs, including
      debug-symbol handling. Retain complete applicable runtime notices and keep
      build-only analyzer code out of shipped artifacts. Update inventories for
      any newly resolved target packages without disturbing Windows/Linux locks.
- [x] Add a required macOS build/test/package job to the existing tag release
      graph. Bind checkout, version, source commit and workflow run exactly as
      for the existing platforms. Extend the aggregate gate, workflow policy,
      artifact downloads, archive allowlist, manifest, checksums, qualification
      collection and publication validation together. A failed, cancelled,
      skipped or missing required macOS result prevents publication.
- [x] Produce a versioned osx-arm64 tar.gz archive with executable permissions,
      notices and instructions. Validate its contents and exact binary identity.
      Execute the extracted binary on macOS and verify its version and applicable
      native acceptance behavior. Existing public versions remain immutable;
      matching draft retries retain integrity checks and do not clobber assets.
- [x] Automate a separate macOS qualification stage without an installed .NET
      SDK/runtime. Standard hosted images contain .NET, so an ordinary successful
      build job is insufficient. Establish and verify actual runtime absence in
      the qualification environment, using an independently executable native
      suite driver; hiding dotnet on PATH is not sufficient. Collect exact
      commit/binary/scenario identities and platform evidence on GitHub Actions.
      Missing evidence must fail explicitly. Preserve existing reference-budget
      and experiment requirements; ordinary hosted timings do not waive them.
- [x] Add controlled public-boundary regressions for required macOS artifacts,
      source/version/run mismatches, failed platform jobs and absent/stale macOS
      qualification. Run the existing Windows/Linux regression gates and the
      actual macOS gate. Report unavailable hosted execution as unverified,
      never passed; local fixture results are not platform qualification.
- [x] Update the release procedure, supported-platform/download documentation,
      status and relevant decisions consistently, superseding P14's macOS
      deferral only after the required path is verified. Document hosted runner
      usage/cost implications and retain concise evidence without personal paths
      or machine identities. Keep raw logs and environment inventories in ignored
      artifacts. Actual publication still needs its existing prerequisites and
      separate authorization.

**Out of scope:** Intel macOS (osx-x64) and universal binaries, installers,
Homebrew distribution, signing/notarization, purchasing hardware, self-hosted
runners, unrelated product behavior, and weakening P07 release qualification.

**References:** P14 and ADR 0011 describe the starting point and deferred work.
[GitHub-hosted runner specifications](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
and the [macOS Apple Silicon image inventory](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-arm64-Readme.md)
establish the available hosted environment. Reverify runner labels, architecture,
installed tooling and pricing during implementation rather than assume an image
or plan remains unchanged.

## Implementation session — 2026-11-16

The later assignment ("nächstes issue umsetzen") authorizes this implementation
and the implementation workflow's local commits. The creation-only boundary above
remains historical. No push, hosted workflow dispatch, remote tag or publication
was performed. The ticket remains open until actual macOS evidence is available;
unchecked combined acceptance items include those unexecuted platform assertions.

- Prepared macOS 15+ Apple Silicon on the standard macos-15 runner, Xcode 16.4,
  pinned SDK and explicit osx-arm64 build/benchmark/fixture identities. Reviewed
  official runner/image/pricing sources, macOS compiler package provenance/hash,
  MIT license and complete upstream notices. Existing Windows/Linux product lock
  contents are unchanged; no new product dependency or action was introduced.
- Added the independent Native AOT suite driver, parsing the real Gherkin files
  and sharing CLI fixture worlds. NUnit remains the managed host. Its reflection-
  dependent assertions cannot be published natively, so an explicit limited native
  adapter retains the required comparisons and rejects unsupported operations.
  Initial native assertions failed on TimeSpan comparisons, then passed after
  correction. Public driver tests verify identities/tags and failure for undefined
  steps, unimplemented scenarios and an incorrect executable. Additional MIT
  notices accompany the internal driver artifact, outside product archives.
- Prepared external DTrace fork/exec/exit observation with launch barrier,
  heartbeats and event-loss/error failures. The new runtime scenario requires
  one native parent and all 32 immediate-exit children to be observed after exit.
  Existing process-tree, interruption, limits, snapshots and recovery assertions
  remain unchanged. Actual macOS probe availability and execution are unverified.
- Both normal Build and tag Release require a separate macOS clean-target job.
  Normal builds use an internal 0.1.0 package for tag-free manual verification.
  The stage inventories/removes approved standalone runtime installations on the
  disposable hosted VM, verifies actual absence, native Mach-O/permissions and
  same-run archive/product/helper/driver identities, then executes the complete
  native runtime suite against the extracted binary. Missing proof fails closed.
  Separate exact-binary reference-machine budgets and the token experiment remain
  required for publication. Ordinary hosted timings cannot substitute for them.
- Windows full gate: 506 tests / 299 local feature cases plus 284 native runtime
  cases passed, no failures/skips or native build/publish warnings/errors. Linux
  full gate: 502 tests / 299 local feature cases plus 284 native runtime cases,
  also with no failures/skips or native build/publish warnings/errors. Architecture,
  dependency, privacy, distribution, budget and Mermaid gates passed on both.
- Final follow-ups: all 33 affected public driver/workflow/package checks passed
  on both platforms; Linux additionally republished the final native driver and
  reran all 284 runtime cases successfully. Final Windows full size/P95 were
  5.974 MiB / 30.334 ms; Linux size/P95 were 6.064 MiB / 13.349 ms. Raw logs,
  traces, reports, compiler inputs and package metadata remain in ignored artifacts.
- Independent Standards and Spec reviews found zero actionable findings for the
  main implementation and the tag-free/driver/license follow-up. P07 qualification,
  actual hosted macOS execution and release publication remain outstanding.

## Authorized hosted verification — 2026-11-16

The user approved pushing a separate verification branch and starting GitHub
tests ("ja"), then declined an additional browser sign-in. The existing SSH
connection pushed `codex/p15-macos-native-release`; a narrowly allowed push trigger
starts the existing read-only Build workflow without extra authentication or a
release tag. The trigger/policy change passed 23 public policy checks, privacy
and both independent review axes before push.

Hosted Build run 43895102356 for commit 380aec3 passed Windows and Linux.
GitHub cancelled macOS before execution because a hosted Apple Silicon runner
could not be acquired after repeated attempts, explicitly reporting capacity
constraints. The clean-target job was skipped; the aggregate gate correctly
failed. No macOS execution or qualification is claimed. A retry will use the same
approved branch and unchanged implementation. No release tag or publication was
created.

The later request ("in dem branch vielleicht erstmal nur für mac bauen?")
authorizes a macOS-only Build run on this exact verification branch. Windows and
Linux are intentionally omitted only for that branch reference; main, PR merge
refs and tag Release retain their full-platform gates. Both macOS build and
clean-target jobs remain required. Eight public gate cases first exposed the
prior behavior (two failed, six passed), then validate branch/result handling;
policy mutations reject a broadened skip guard or forged event reference.

Actual macOS-only runs 43900051213 and 43901641506 reached native distribution
auditing after successful Apple Silicon toolchain checks and native publishes.
A bounded public CI error annotation identified the exact failure: the SDK's
`merstride.dSYM/Contents/Resources/Relocations/aarch64/merstride.yml` was missing
from the macOS symbol allowlist. This direct, deterministic file-identity mismatch
does not require speculative competing hypotheses. Only that exact macOS path
is added; all file contents still undergo analyzer-identity scanning, unexpected
adjacent files are rejected, and product archives still exclude debug symbols.
Nine public distribution-audit cases passed locally on Windows with the proper
published-native fixture environment. The new positive macOS case remains for
the actual hosted run; Windows results are not macOS execution evidence.

Hosted run 43903812180 was cancelled at the unchanged 30-minute job limit without
completing tests. The next diagnosis adds public phase notices and a single
existing short-lived-process scenario before the full suites, guarded by a
120-second outer observer-harness watchdog. This narrows the failure loop without
relaxing product timeouts, skipping required scenarios or claiming qualification.
The full managed/native suites remain mandatory after the preflight succeeds.

Run 43938208167 reduced the macOS failure to one preflight case: the observer
reported missing fresh kernel heartbeats. The first of at most two additional
commits authorized by the user removes the kernel/managed wall-clock comparison:
freshness requires appended kernel heartbeat records and the same 100 ms local
monotonic drain interval. Exact launch counts, event-loss checks and timeouts
remain enforced. If the provider produces no records, bounded heartbeat/readiness
counts now distinguish that case without publishing raw traces. This tests the
clock-base hypothesis; an actual hosted result is still required.

Run 43941597866 falsified the clock-base hypothesis: the preflight saw ten valid
heartbeats initially and exactly ten after five seconds (12 records, READY true).
Apple's DTrace consumer flushes output after each work cycle, and its process
attachment can end tracing when the observed launcher becomes unobservable on
exec. The second and last additionally authorized commit removes that attachment:
the stopped root PID is a script argument, kernel lifecycle probes still track
its descendants, and its kernel exit ends tracing. The READY barrier, heartbeat
failure, exact 33-launch preflight and full suites remain required. This remains
a hypothesis pending the actual hosted rerun; no macOS qualification is claimed.

Run 43942604167 passed the exact short-lived-process preflight and reached the
full managed acceptance suite, which failed. The native suite and clean-target
qualification did not run. Public annotations exposed only the aggregate failure;
artifact download requires authentication. The next authorized diagnostic change
publishes at most eight bounded failing test names, exception types and numeric
or boolean assertion comparisons, with
checkout, temporary and home roots redacted and workflow-command escaping applied.
Captured stdout/stderr and stack traces remain private. No assertion, timeout or
release requirement changes.

Run 43948506691 exposed a native macOS lock classification defect: a conflicting
writer returned exit 6 instead of 4. .NET exposes macOS EWOULDBLOCK as raw errno
35; RunStore recognized only Windows sharing errors and Linux errno 11. Add the
macOS-specific mapping while preserving OS-backed exclusion and all error gates.
The Python fixture also observed three launches instead of two, consistent with
CPython's framework bin launcher execing Python.app; the observation harness now
selects the installed framework interpreter directly when present. Exact counts
remain unchanged. Normalize the test harness TMPDIR to its physical path to keep
.NET and PowerShell workspace comparisons independent of the /var symlink.
Recovery exceptions and PowerShell failures still require the hosted rerun; their
association with these defects is a hypothesis, not completed qualification.
The rebuilt native Windows binary passed all 97 affected process, recovery,
inline-script, readable-output and short-lived-observation cases (zero skipped).
Both incremental review axes reported no findings. These are regression checks,
not native macOS evidence.


## Hosted completion — 2026-11-17

Run [44024602627](https://github.com/mcnilz/merstride/actions/runs/44024602627)
completed successfully for commit `39f864d`: native build, all managed/native
gates, internal 0.1.0 packaging, separate extracted-archive qualification without
system .NET, and aggregate gate. The clean-target script requires all 284 exact
inventoried runtime cases and same-run native product/helper/driver identities.
This supersedes the historical pending-execution notes above. P15 is complete;
P07's other release prerequisites and publication authorization remain separate.
No additional code push was needed from the three newly authorized macOS pushes.


## Final resource policy and history — 2026-11-17

The user requested Windows/Linux builds restored on this branch, macOS jobs only
for releases, and the branch squashed after verification. Ordinary Build now has
only Windows, Linux and their mandatory aggregate gate; branch exceptions are
removed. Release retains all three platforms and the independent macOS clean
stage, which proves the package runs without system .NET. The earlier tag-free
workflow notes are historical and superseded by this resource policy.
The full final Windows gate passed 520 tests and all 284 native runtime cases,
with no failures/skips; policy, architecture, dependency, distribution, privacy,
budgets and Mermaid checks passed. Both review axes found no actionable issues.
The revised public aggregate test failed before the workflow change and passes
in the final suite. The final squash retains the already recorded macOS evidence
against its original tested source; the final CI policy itself schedules no Mac
job on this branch.
