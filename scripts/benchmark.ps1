param([Parameter(Mandatory)][string]$Binary, [string]$Output = "$PSScriptRoot/../artifacts/benchmark.json")
$ErrorActionPreference = 'Stop'
$binaryPath = (Resolve-Path -LiteralPath $Binary).Path
$repo = (Resolve-Path "$PSScriptRoot/..").Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ("merstride-benchmark-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temp | Out-Null
function Invoke-Cli([string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($binaryPath)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($arg in ($Arguments + @('--state-dir', (Join-Path $temp 'state'), '--json'))) { $start.ArgumentList.Add($arg) }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(15000)) { $process.Kill($true); throw 'Benchmark watchdog expired.' }
    $watch.Stop()
    if ($process.ExitCode -ne 0) { throw "Benchmark CLI failed ($($start.ArgumentList -join ' ')): $($stderr.Result) $($stdout.Result)" }
    $result = @{ milliseconds = $watch.Elapsed.TotalMilliseconds; response = ($stdout.Result | ConvertFrom-Json) }
    $process.Dispose()
    return $result
}
try {
    # Setup launches the product; this first status is not claimed to be cold-cache latency.
    $initial = Invoke-Cli -Arguments @('start', "$repo/examples/single-end.md", '--workspace', $temp)
    $run = $initial.response
    $first = Invoke-Cli -Arguments @('status', $run.runId)
    $samples = @(1..30 | ForEach-Object { (Invoke-Cli -Arguments @('status', $run.runId)).milliseconds })
    $sorted = @($samples | Sort-Object)
    $helper = $env:MERSTRIDE_FIXTURE
    if (!$helper) { $helper = Join-Path $repo $(if ($IsWindows) { 'artifacts/fixture/test-process.exe' } else { 'artifacts/fixture/test-process' }) }
    if (!(Test-Path -LiteralPath $helper)) { throw 'Native process fixture is required for process feasibility measurement.' }
    $processWorkflow = (Get-Content -Raw "$repo/examples/process-chain.md").Replace('command: git', "command: $($helper | ConvertTo-Json -Compress)").Replace('args: [status, --short]', 'args: [effect, first]').Replace('args: [diff, --stat]', 'args: [effect, second]')
    $processFile = Join-Path $temp 'process.md'
    Set-Content -LiteralPath $processFile -Value $processWorkflow
    $processSamples = @(1..10 | ForEach-Object {
        $started = Invoke-Cli -Arguments @('start', $processFile, '--workspace', $temp)
        $advanced = Invoke-Cli -Arguments @('advance', $started.response.runId)
        if ($advanced.response.status -ne 'completed' -or $advanced.response.transitionCount -ne 2 -or $advanced.response.lastActivation.outcome -ne 'exit_0') { throw 'Native process chain feasibility failed.' }
        $advanced.milliseconds
    })
    $expectedEffects = (1..10 | ForEach-Object { "first`nsecond`n" }) -join ''
    if ([IO.File]::ReadAllText((Join-Path $temp 'effects.txt')) -cne $expectedEffects) { throw 'Process effects did not occur once in graph order.' }
    $processSorted = @($processSamples | Sort-Object)
    $bytes = (Get-Item -LiteralPath $binaryPath).Length
    $report = [ordered]@{
        measuredOn = [DateTime]::UtcNow.ToString('yyyy-MM-dd'); binary = [IO.Path]::GetFileName($binaryPath); sha256 = (Get-FileHash $binaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        version = (Invoke-Cli -Arguments @('version')).response.version; rid = $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' }); bytes = $bytes; mib = $bytes / 1MB
        sizeTarget15MiB = $bytes -le 15MB; sizeGate25MiB = $bytes -le 25MB
        firstStartMilliseconds = $initial.milliseconds; firstStatusMilliseconds = $first.milliseconds; warmStatusMilliseconds = $samples
        p95Milliseconds = $sorted[[Math]::Ceiling(0.95 * $samples.Count) - 1]; latencyGate150ms = $sorted[28] -lt 150
        methodology = '30 sequential fresh status processes; Stopwatch includes process creation, JSON stdout/stderr drain and exit. First status reported separately after start; not a cold boot or clean-runtime test.'
        processFeasibility = [ordered]@{
            fixtureSha256 = (Get-FileHash $helper -Algorithm SHA256).Hash.ToLowerInvariant()
            fixtureBytes = (Get-Item -LiteralPath $helper).Length
            chains = 10; processesPerChain = 2; orderedEffects = 20
            advanceMilliseconds = $processSamples
            p95Milliseconds = $processSorted[[Math]::Ceiling(0.95 * $processSamples.Count) - 1]
            methodology = 'Ten fresh native CLI advances, each executing two sequential native helpers and atomically persisting their results. All twenty file effects checked in order. Timings include CLI startup and process/log/state IO; no chain-latency gate is specified. External Windows descendant counting is in the acceptance suite.'
        }
    }
    New-Item -ItemType Directory -Force "$repo/artifacts" | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content $Output
    $report | ConvertTo-Json -Depth 6
    if (!$report.sizeGate25MiB -or !$report.latencyGate150ms) { throw 'Initial native budget exceeded; review measurements.' }
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    if ($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like 'merstride-benchmark-*') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
