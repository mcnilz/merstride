# Inspect uncertain effects before retry

After starting a process workflow such as [process-chain.md](process-chain.md),
retain its run ID and absolute state root. If its engine is terminated during
execution, a fresh status projects `interrupted` without changing the saved file.
The process may already have performed its external effect. Check that effect
before deciding whether another attempt is authorized and safe.

Use Bash with `merstride` on PATH, or replace `merstride` below with `"$binary"`
after the [README setup](../README.md#quick-start). Set `run_id` and `state_root` to the values
returned by start; keep the native absolute state path, including on Git Bash.

```bash
set -euo pipefail
# Use the run_id and state_root retained from start (see the README's jq extraction).
merstride status "$run_id" --state-dir "$state_root" --json
# Inspect the workspace and any external systems affected by the process.
# If a retry is appropriate, resolution alone launches nothing:
merstride resolve "$run_id" --retry --state-dir "$state_root" --json
merstride advance "$run_id" --state-dir "$state_root" --json
```

The next advance creates a fresh activation. The previous activation remains
recorded as interrupted. An alternative decision is to finish without any more
effects:

```bash
if merstride resolve "$run_id" --fail --state-dir "$state_root" --json; then
    printf 'Unexpected successful exit from resolve --fail.\n' >&2
    exit 1
else
    code=$?
    # Exit 5 is expected; preserve any other error for the caller.
    if [[ "$code" -ne 5 ]]; then exit "$code"; fi
fi
```

Choose one resolution for an interrupted run. Resolution is refused for ready,
waiting or terminal runs. A competing writer returns `RUN_LOCKED` immediately;
the retry example stops on any nonzero exit. Inspect the returned error and saved
status rather than retrying after an unexpected failure.
Different runs remain independent. Windows Ctrl+C or Ctrl+Break requests attached
process-tree termination and exit 130. Forced engine termination leaves uncertainty;
it does not promise to terminate children or undo their effects.
