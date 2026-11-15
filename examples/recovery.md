# Inspect uncertain effects before retry

After starting a process workflow such as [process-chain.md](process-chain.md),
retain its run ID and absolute state root. If its engine is terminated during
execution, a fresh status projects `interrupted` without changing the saved file.
The process may already have performed its external effect. Check that effect
before deciding whether another attempt is authorized and safe.

```powershell
# Use the run ID and state root returned by start.
merstride status $runId --state-dir $stateRoot --json
# Inspect the workspace and any external systems affected by the process.
# If a retry is appropriate, resolution alone launches nothing:
merstride resolve $runId --retry --state-dir $stateRoot --json
merstride advance $runId --state-dir $stateRoot --json
```

The next advance creates a fresh activation. The previous activation remains
recorded as interrupted. An alternative decision is to finish without any more
effects:

```powershell
merstride resolve $runId --fail --state-dir $stateRoot --json
# Exit code 5 means the run is now failed.
```

Choose one resolution for an interrupted run. Resolution is refused for ready,
waiting or terminal runs. A competing writer returns `RUN_LOCKED` immediately;
different runs remain independent. Windows Ctrl+C or Ctrl+Break requests attached
process-tree termination and exit 130. Forced engine termination leaves uncertainty;
it does not promise to terminate children or undo their effects.
