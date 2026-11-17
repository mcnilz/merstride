param([string]$Runtime, [string]$Version = '0.1.0', [switch]$ReleaseQualification)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/platform.ps1"
function Set-BuildPhase([string]$Name) {
    $script:buildPhase = $Name
    if ($env:GITHUB_ACTIONS -ceq 'true') { Write-Host "::notice title=Merstride build phase::$Name" }
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
# Short-lived build hosts are required when strace follows the acceptance tree.
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:UseSharedCompilation = 'false'
Push-Location "$PSScriptRoot/.."
Set-BuildPhase 'host validation'
try {
    $hostRuntime = Get-NativeRuntime
    if (!$Runtime) { $Runtime = $hostRuntime }
    if ($Runtime -cne $hostRuntime) { throw 'Run native builds on the matching platform, in separate checkouts.' }
    New-Item -ItemType Directory -Force artifacts | Out-Null
    if (Test-Path artifacts/local-gate.json) { Remove-Item -LiteralPath artifacts/local-gate.json }
    foreach ($summary in @('artifacts/test-summary.json','artifacts/benchmark.json')) {
        if (Test-Path -LiteralPath $summary) { Remove-Item -LiteralPath $summary }
    }
    Set-BuildPhase 'repository privacy'
    & ./scripts/repository-privacy.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Repository privacy check failed.' }
    Set-BuildPhase 'privacy fixtures'
    & ./tests/fixtures/repository-privacy.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Repository privacy fixtures failed.' }
    Set-BuildPhase 'locked product restore'
    & dotnet restore src/Merstride -r $Runtime "-p:RuntimeIdentifier=$Runtime" --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked product restore failed.' }
    Set-BuildPhase 'locked managed test restore'
    & dotnet restore tests/Merstride.Acceptance --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked test restore failed.' }
    Set-BuildPhase 'locked native fixture restore'
    & dotnet restore tests/Merstride.TestProcess -r $Runtime "-p:RuntimeIdentifier=$Runtime" --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked native fixture restore failed.' }
    Set-BuildPhase 'locked native suite restore'
    & dotnet restore tests/Merstride.NativeSuite -r $Runtime "-p:RuntimeIdentifier=$Runtime" --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked native suite restore failed.' }
    Set-BuildPhase 'workflow policy'
    & ./scripts/workflow-policy.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Workflow policy failed.' }
    Set-BuildPhase 'architecture policy'
    & ./scripts/architecture.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Architecture configuration gate failed.' }
    Set-BuildPhase 'dependency inventory'
    & ./scripts/dependencies.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Dependency review failed.' }
    Set-BuildPhase 'managed product build'
    & dotnet build src/Merstride -c Release -r $Runtime --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Product build failed.' }
    $audit = Join-Path (Get-Location) 'artifacts/audit'
    Set-BuildPhase 'native product publish'
    & dotnet publish src/Merstride -c Release -r $Runtime --no-restore -o artifacts/native -p:Version=$Version -p:ReleaseAuditDirectory=$audit
    if ($LASTEXITCODE -ne 0) { throw 'Native AOT publish failed.' }
    Set-BuildPhase 'native fixture publish'
    & dotnet publish tests/Merstride.TestProcess -c Release -r $Runtime --no-restore -o artifacts/fixture
    if ($LASTEXITCODE -ne 0) { throw 'Native test fixture publish failed.' }
    Set-BuildPhase 'native suite publish'
    & dotnet publish tests/Merstride.NativeSuite -c Release -r $Runtime --no-restore -o artifacts/suite
    if ($LASTEXITCODE -ne 0) { throw 'Native suite publish failed.' }
    Copy-Item -LiteralPath LICENSE -Destination artifacts/suite/
    foreach ($notice in @('dotnet-LICENSE.txt','dotnet-THIRD-PARTY-NOTICES.txt','Gherkin-LICENSE.txt','Cucumber.Messages-LICENSE.txt')) {
        Copy-Item -LiteralPath "docs/licenses/$notice" -Destination artifacts/suite/
    }
    $env:MERSTRIDE_BINARY = Join-Path (Get-Location) $(if ($IsWindows) { 'artifacts/native/merstride.exe' } else { 'artifacts/native/merstride' })
    $env:MERSTRIDE_FIXTURE = Join-Path (Get-Location) $(if ($IsWindows) { 'artifacts/fixture/test-process.exe' } else { 'artifacts/fixture/test-process' })
    Set-BuildPhase 'native distribution audit'
    & ./scripts/distribution-audit.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Distribution audit failed.' }
    if ($IsMacOS) {
        Set-BuildPhase 'macOS short-lived process observation preflight'
        if (Test-Path artifacts/tests/observer-preflight.trx) { Remove-Item -LiteralPath artifacts/tests/observer-preflight.trx }
        $priorWatchdog = $env:MERSTRIDE_PROCESS_WATCHDOG_SECONDS
        try {
            $env:MERSTRIDE_PROCESS_WATCHDOG_SECONDS = '120'
            & python3 scripts/observe-macos.py artifacts/preflight-process.trace -- dotnet test tests/Merstride.Acceptance -c Release --no-restore --filter 'Name~15-macos-release' --logger 'trx;LogFileName=observer-preflight.trx' --results-directory artifacts/tests
            $preflightExit = $LASTEXITCODE
        } finally { $env:MERSTRIDE_PROCESS_WATCHDOG_SECONDS = $priorWatchdog }
        $preflightCases = @()
        if (Test-Path artifacts/tests/observer-preflight.trx) {
            [xml]$preflight = Get-Content -Raw artifacts/tests/observer-preflight.trx
            $preflightCases = @($preflight.TestRun.Results.UnitTestResult)
        }
        if ($preflightExit -ne 0 -or $preflightCases.Count -ne 1 -or $preflightCases[0].outcome -cne 'Passed') {
            $detail = 'No successful native short-lived process observation result.'
            $failure = @($preflightCases | Where-Object outcome -NE 'Passed' | Select-Object -First 1)
            if ($failure.Count) { $detail = [string]$failure[0].Output.ErrorInfo.Message }
            throw "macOS process observation preflight failed: $detail"
        }
    }
    foreach ($result in @('artifacts/tests/p07.trx','artifacts/tests/scenarios.json')) {
        if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
    }
    $testArgs = @('test','tests/Merstride.Acceptance','-c','Release','--no-restore','--filter','TestCategory!=ReleaseEnvironment','--logger','trx;LogFileName=p07.trx','--results-directory','artifacts/tests')
    Set-BuildPhase 'managed acceptance under external observation'
    if ($IsWindows) { & dotnet @testArgs }
    elseif ($IsMacOS) {
        & python3 scripts/observe-macos.py artifacts/process.trace -- dotnet @testArgs
    } else {
        $env:MERSTRIDE_PROCESS_TRACE = Join-Path (Get-Location) 'artifacts/process.trace'
        & strace --seccomp-bpf -f -qq -e trace=execve -o $env:MERSTRIDE_PROCESS_TRACE dotnet @testArgs
    }
    $testExit = $LASTEXITCODE
    # Retain outcomes on failure without raw errors, local paths or environment data.
    $testResults = @()
    $scenarios = @()
    $trx = $null
    if (Test-Path artifacts/tests/p07.trx) {
        [xml]$trx = Get-Content -Raw artifacts/tests/p07.trx
        $testResults = @($trx.TestRun.Results.UnitTestResult | ForEach-Object { [ordered]@{test=$_.testName;outcome=$_.outcome} })
    }
    if (Test-Path artifacts/tests/scenarios.json) {
        $scenarios = @(Get-Content -Raw artifacts/tests/scenarios.json | ConvertFrom-Json | ForEach-Object {
            [ordered]@{feature=$_.feature;line=$_.line;scenario=$_.scenario;status=$_.result.status}
        })
    }
    [ordered]@{schemaVersion=1;exitCode=$testExit;releaseReady=$false;tests=$testResults;scenarios=$scenarios} |
        ConvertTo-Json -Depth 6 | Set-Content artifacts/test-summary.json
    if ($testExit -ne 0) {
        if ($env:GITHUB_ACTIONS -ceq 'true' -and $trx) {
            # Assertion messages can embed raw process output; allow only numeric/
            # boolean comparisons and exception type names into public annotations.
            $failures = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -NE 'Passed')
            foreach ($failure in @($failures | Select-Object -First 8)) {
                $message = [string]$failure.Output.ErrorInfo.Message
                $comparison = @([regex]::Matches($message, '(?m)^\s*(?:Expected:|But was:)\s+(?:-?\d+(?:\.\d+)?|True|False|<null>|<empty>)\s*$') | ForEach-Object { $_.Value.Trim() })
                $exceptionType = [regex]::Match($message, '\A[A-Za-z_][A-Za-z0-9_.]*Exception\s*:').Value.TrimEnd(':').Trim()
                $detail = "$($failure.testName): $exceptionType $($comparison -join '; ')".TrimEnd()
                foreach ($privateRoot in @((Get-Location).Path, [IO.Path]::GetTempPath(), $env:HOME, $env:USERPROFILE)) {
                    if ($privateRoot) { $detail = $detail.Replace($privateRoot, '.') }
                }
                if ($detail.Length -gt 900) { $detail = $detail.Substring(0, 900) }
                $detail = $detail.Replace('%','%25').Replace("`r",'%0D').Replace("`n",'%0A')
                Write-Host "::error title=Merstride acceptance failure::$detail"
            }
        }
        throw 'P01-P09 local acceptance/build checks failed. Required release environment qualification is separate.'
    }
    $suite = Join-Path (Get-Location) $(if ($IsWindows) { 'artifacts/suite/native-suite.exe' } else { 'artifacts/suite/native-suite' })
    $suiteArgs = @('--binary',$env:MERSTRIDE_BINARY,'--fixture',$env:MERSTRIDE_FIXTURE,'--exclude-release-environment-orchestrator','--results','artifacts/tests/native-scenarios.json')
    Set-BuildPhase 'independent native acceptance'
    if ($IsWindows) { & $suite @suiteArgs }
    elseif ($IsMacOS) { & python3 scripts/observe-macos.py artifacts/native-process.trace -- $suite @suiteArgs }
    else {
        $env:MERSTRIDE_PROCESS_TRACE = Join-Path (Get-Location) 'artifacts/native-process.trace'
        & strace --seccomp-bpf -f -qq -e trace=execve -o $env:MERSTRIDE_PROCESS_TRACE $suite @suiteArgs
    }
    if ($LASTEXITCODE -ne 0) { throw 'Independent native runtime suite failed.' }
    # Measure native startup before installing/rendering the development browser tool.
    Set-BuildPhase 'native budgets'
    & ./scripts/benchmark.ps1 -Binary $env:MERSTRIDE_BINARY
    if ($LASTEXITCODE -ne 0) { throw 'Initial native budget check failed.' }
    Set-BuildPhase 'Mermaid dependency policy'
    & node tools/mermaid/policy.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Mermaid development dependency review failed.' }
    Set-BuildPhase 'locked Mermaid restore'
    & npm ci --prefix tools/mermaid --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Locked Mermaid development restore failed.' }
    Set-BuildPhase 'Mermaid rendering'
    & node tools/mermaid/check.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Official Mermaid compatibility check failed.' }
    Set-BuildPhase 'final source and binary identities'
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record tested source commit.' }
    $dirty = @(& git status --porcelain).Count -ne 0
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record source cleanliness.' }
    $binaryVersion = (& $env:MERSTRIDE_BINARY version --json | ConvertFrom-Json).version
    if ($LASTEXITCODE -ne 0 -or $binaryVersion -cne $Version) { throw 'Embedded release version differs.' }
    [ordered]@{schemaVersion=1;result='passed';releaseReady=$false;commit=$commit;dirty=$dirty;rid=$Runtime;version=$Version;
        runId=$(if ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID } else { 'local-uncommitted' });sdk=(& dotnet --version);
        binarySha256=(Get-FileHash $env:MERSTRIDE_BINARY).Hash.ToLowerInvariant();
        fixtureSha256=(Get-FileHash $env:MERSTRIDE_FIXTURE).Hash.ToLowerInvariant();
        nativeSuiteSha256=(Get-FileHash $suite).Hash.ToLowerInvariant();
        testsSha256=(Get-FileHash artifacts/tests/p07.trx).Hash.ToLowerInvariant();
        distributionSha256=(Get-FileHash artifacts/audit/distribution.json).Hash.ToLowerInvariant();
        benchmarkSha256=(Get-FileHash artifacts/benchmark.json).Hash.ToLowerInvariant();
        qualification='Clean no-runtime environments and token experiment remain mandatory release gates.'
    } | ConvertTo-Json -Depth 8 | Set-Content artifacts/local-gate.json
    if ($ReleaseQualification) {
        & dotnet test tests/Merstride.Acceptance -c Release --no-restore --filter 'TestCategory=ReleaseEnvironment' --logger 'trx;LogFileName=p07-qualification.trx' --results-directory artifacts/tests
        if ($LASTEXITCODE -ne 0) { throw 'Required clean-target qualification is incomplete; publication blocked.' }
    }
} catch {
    if ($env:GITHUB_ACTIONS -ceq 'true') {
        # Public CI annotations retain only the bounded phase/error summary.
        # Raw output and environment data remain in the ignored build artifacts.
        $summary = "${buildPhase}: $($_.Exception.Message)".Replace((Get-Location).Path, '.')
        if ($summary.Length -gt 1000) { $summary = $summary.Substring(0, 1000) }
        $summary = $summary.Replace('%','%25').Replace("`r",'%0D').Replace("`n",'%0A')
        Write-Host "::error title=Merstride build failed::$summary"
    }
    throw
} finally { Pop-Location }
