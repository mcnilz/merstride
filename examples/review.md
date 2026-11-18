# Review changes

Requires Git in PATH and a Git repository as the workspace. This example captures status, delegates review to the existing agent, and supports rechecking after changes.
Use `merstride advance --save-stdout` on each visit to retain the status output
for the review request's context.
The console names the paused review step and prints its prompt, allowed outcomes
and request ID. After completing the request, call `advance` to resume. Use
`--details` to inspect the execution route. See [flow output](../docs/FLOW-OUTPUT.md).

```yaml merstride
schemaVersion: 1
id: review_changes
entry: capture
limits:
  maxTransitions: 12
nodes:
  capture:
    kind: process
    command: git
    args: [status, --porcelain]
    timeoutSeconds: 30
  review:
    kind: agent
    prompt: |
      Review the changes in the workspace using your existing tools.
      Inspect actual diffs and relevant files; status alone is insufficient.
      Return recheck after authorized fixes, approved when review is complete,
      or blocked when you cannot continue. Summarize findings in data.
    context:
      - nodes.capture.exitCode
      - nodes.capture.stdoutTail
  done:
    kind: end
    result: success
  failed:
    kind: end
    result: failure
```

```mermaid
flowchart TD
    %% merstride: review_changes
    capture["Capture changes"]
    review["Review changes"]
    done["Complete"]
    failed["Stop"]
    capture -->|exit_0| review
    capture -->|default| failed
    review -->|recheck| capture
    review -->|approved| done
    review -->|blocked| failed
```
