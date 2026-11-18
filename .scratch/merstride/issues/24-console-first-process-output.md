# P24: Show process output in the calling console by default

**What to build:** While Merstride runs a process node, forward the tool's
stdout and stderr to the calling console by default. The invoking agent must
receive the actual tool output in the same command invocation without a file
read or a follow-up `logs` command. Allow explicit file output for stdout only,
stderr only, or both streams.

**Blocked by:** None.

**Status:** complete

**Approval and delivery boundary:** Local issue capture only. Implementation and
a local commit require a separate assignment.

The follow-up request to implement the open issues assigns P24 for local
implementation, verification, review and a scoped commit.

## Context

P17 and P20 show Merstride's execution activity, outcomes and routes. The process
output itself currently goes to per-activation log files. An agent can see that
a command ran but may need a second call to learn what it printed.

## Acceptance criteria

- [x] In default text mode, process stdout reaches the caller's stdout and
      process stderr reaches the caller's stderr as the process writes. This
      also works through redirected streams, as used by agent tool runners.
      Output already emitted remains available when the process fails. Neither
      stream creates a per-activation output file by default.
- [x] Provide an explicit way to write each stream to a file independently.
      Cover stdout only, stderr only and both. A stream without a file option
      still goes to the console. Document whether a selected stream is also
      echoed to the console and how file destinations are resolved.
- [x] Explicit `--json` still emits exactly one protocol response on stdout.
      Define how the same invocation exposes bounded process stdout and stderr
      to a structured caller without requiring a file read or injecting raw
      process output into the JSON response stream.
- [x] Keep draining both streams concurrently and preserve timeout, output-limit,
      routing and interruption behavior. Define how `logs` and agent handoff
      tails behave with and without file output, including existing saved runs
      with retained logs.
- [x] Update the contract, relevant decisions and CLI guidance. Add `@P24`
      scenarios against the published native CLI that observe both streams
      before a controlled process exits, verify each file-output combination
      and absence of default output files, and cover failure output and JSON
      framing.

## Delivery notes

- `--save-stdout` and `--save-stderr` select run-owned activation files. A saved
  stream supplies the matching handoff tail. A missing stream is unavailable.
- Standards and ticket reviews identified a timeout blocked by an unread
  Windows console pipe and stale stderr-log guidance. Execution now abandons a
  stalled write after the drain deadline, and the native timeout scenario
  checks the saved route. Merstride starts its next message on a fresh line
  when tool output leaves the line open.
- Windows x64 focused CLI verification covers both text and JSON. Raw output
  and other machine-specific evidence remain under ignored artifacts.
