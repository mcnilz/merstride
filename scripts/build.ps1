param([string]$Runtime = $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' }), [string]$Version = '0.1.0', [switch]$ReleaseQualification)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
# Short-lived build hosts are required when strace follows the acceptance tree.
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:UseSharedCompilation = 'false'
Push-Location "$PSScriptRoot/.."
try {
    if ($Runtime -cne $(if ($IsWindows) { 'win-x64' } else { 'linux-x64' })) { throw 'Run native builds on the matching platform, in separate checkouts.' }
    New-Item -ItemType Directory -Force artifacts | Out-Null
    if (Test-Path artifacts/local-gate.json) { Remove-Item -LiteralPath artifacts/local-gate.json }
    & dotnet --info | Set-Content artifacts/environment.txt
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect SDK environment.' }
    & dotnet restore src/Merstride -r $Runtime --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked product restore failed.' }
    & dotnet restore tests/Merstride.Acceptance --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked test restore failed.' }
    & dotnet restore tests/Merstride.TestProcess -r $Runtime --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked native fixture restore failed.' }
    & ./scripts/workflow-policy.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Workflow policy failed.' }
    & ./scripts/architecture.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Architecture configuration gate failed.' }
    & ./scripts/dependencies.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Dependency review failed.' }
    & dotnet build src/Merstride -c Release -r $Runtime --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Product build failed.' }
    $audit = Join-Path (Get-Location) 'artifacts/audit'
    & dotnet publish src/Merstride -c Release -r $Runtime --no-restore -o artifacts/native -p:Version=$Version -p:ReleaseAuditDirectory=$audit
    if ($LASTEXITCODE -ne 0) { throw 'Native AOT publish failed.' }
    & dotnet publish tests/Merstride.TestProcess -c Release -r $Runtime --no-restore -o artifacts/fixture
    if ($LASTEXITCODE -ne 0) { throw 'Native test fixture publish failed.' }
    $env:MERSTRIDE_BINARY = Join-Path (Get-Location) $(if ($IsWindows) { 'artifacts/native/merstride.exe' } else { 'artifacts/native/merstride' })
    $env:MERSTRIDE_FIXTURE = Join-Path (Get-Location) $(if ($IsWindows) { 'artifacts/fixture/test-process.exe' } else { 'artifacts/fixture/test-process' })
    & ./scripts/distribution-audit.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Distribution audit failed.' }
    $testArgs = @('test','tests/Merstride.Acceptance','-c','Release','--no-restore','--filter','TestCategory!=ReleaseEnvironment','--logger','trx;LogFileName=p07.trx','--results-directory','artifacts/tests')
    if ($IsWindows) { & dotnet @testArgs }
    else {
        $env:MERSTRIDE_PROCESS_TRACE = Join-Path (Get-Location) 'artifacts/process.trace'
        & strace -f -qq -e trace=execve -o $env:MERSTRIDE_PROCESS_TRACE dotnet @testArgs
    }
    if ($LASTEXITCODE -ne 0) { throw 'P01-P07 local acceptance/build checks failed. Required release environment qualification is separate.' }
    # Measure native startup before installing/rendering the development browser tool.
    & ./scripts/benchmark.ps1 -Binary $env:MERSTRIDE_BINARY
    if ($LASTEXITCODE -ne 0) { throw 'Initial native budget check failed.' }
    & node tools/mermaid/policy.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Mermaid development dependency review failed.' }
    & npm ci --prefix tools/mermaid --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Locked Mermaid development restore failed.' }
    & node tools/mermaid/check.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Official Mermaid compatibility check failed.' }
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record tested source commit.' }
    $dirty = @(& git status --porcelain).Count -ne 0
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record source cleanliness.' }
    $binaryVersion = (& $env:MERSTRIDE_BINARY version --json | ConvertFrom-Json).version
    if ($LASTEXITCODE -ne 0 -or $binaryVersion -cne $Version) { throw 'Embedded release version differs.' }
    [ordered]@{schemaVersion=1;result='passed';releaseReady=$false;commit=$commit;dirty=$dirty;rid=$Runtime;version=$Version;
        runId=$(if ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID } else { 'local-uncommitted' });sdk=(& dotnet --version);
        binarySha256=(Get-FileHash $env:MERSTRIDE_BINARY).Hash.ToLowerInvariant();
        testsSha256=(Get-FileHash artifacts/tests/p07.trx).Hash.ToLowerInvariant();
        distributionSha256=(Get-FileHash artifacts/audit/distribution.json).Hash.ToLowerInvariant();
        benchmarkSha256=(Get-FileHash artifacts/benchmark.json).Hash.ToLowerInvariant();
        environment=(Get-Content -Raw artifacts/environment.txt);
        qualification='Clean no-runtime environments and token experiment remain mandatory release gates.'
    } | ConvertTo-Json -Depth 8 | Set-Content artifacts/local-gate.json
    if ($ReleaseQualification) {
        & dotnet test tests/Merstride.Acceptance -c Release --no-restore --filter 'TestCategory=ReleaseEnvironment' --logger 'trx;LogFileName=p07-qualification.trx' --results-directory artifacts/tests
        if ($LASTEXITCODE -ne 0) { throw 'Required clean-target qualification is incomplete; publication blocked.' }
    }
} finally { Pop-Location }
