param([Parameter(Mandatory)][string]$Driver, [string]$Output = 'artifacts/qualification/token-experiment.json')
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force (Split-Path ([IO.Path]::GetFullPath($Output))) | Out-Null
# Driver performs the same task with the same external agent/model and returns JSON.
# Product code never calls it. Real accounting is required; unavailable counts remain null.
$samples = @()
foreach ($task in @('deterministic-chain','test-fix-loop')) {
    foreach ($iteration in 1..5) {
        # Alternate order to expose rather than silently eliminate cache/order effects.
        $variants = if ($iteration % 2) { @('manual','merstride') } else { @('merstride','manual') }
        foreach ($variant in $variants) {
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $raw = & $Driver --task $task --variant $variant --iteration $iteration | Out-String
            $watch.Stop()
            if ($LASTEXITCODE -ne 0) { throw 'External experiment driver failed; retain its failure evidence.' }
            $value = $raw | ConvertFrom-Json -AsHashtable
            foreach ($field in @('agent','model','taskIdentity','equivalentResult','modelCalls','cacheEffects','failures','inputTokens','outputTokens')) {
                if (!$value.ContainsKey($field)) { throw "Missing actual experiment evidence: $field" }
            }
            if ($value.equivalentResult -isnot [bool] -or !$value.equivalentResult) { throw 'Experiment results are not equivalent.' }
            $samples += @{task=$task;variant=$variant;iteration=$iteration;elapsedMilliseconds=$watch.Elapsed.TotalMilliseconds;actual=$value}
            # Preserve every completed run if a later run fails.
            @{schemaVersion=1;result='incomplete';samples=$samples} | ConvertTo-Json -Depth 15 | Set-Content $Output
        }
    }
}
if (@($samples.actual.agent | Sort-Object -Unique).Count -ne 1 -or @($samples.actual.model | Sort-Object -Unique).Count -ne 1) { throw 'Agent/model identity changed.' }
foreach ($task in @('deterministic-chain','test-fix-loop')) {
    if (@(($samples | Where-Object task -CEQ $task).actual.taskIdentity | Sort-Object -Unique).Count -ne 1) { throw 'Task identity changed between variants.' }
}
@{schemaVersion=1;result='measured';samples=$samples;methodology='Two tasks, two variants, five runs each, alternating order. Actual external-agent calls/accounting only. Missing token accounting is null, never estimated.'} | ConvertTo-Json -Depth 15 | Set-Content $Output
