param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
Push-Location "$PSScriptRoot/.."
try {
    New-Item -ItemType Directory -Force artifacts | Out-Null
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
    & dotnet publish src/Merstride -c Release -r $Runtime --no-restore -o artifacts/native
    if ($LASTEXITCODE -ne 0) { throw 'Native AOT publish failed.' }
    & dotnet publish tests/Merstride.TestProcess -c Release -r $Runtime --no-restore -o artifacts/fixture
    if ($LASTEXITCODE -ne 0) { throw 'Native test fixture publish failed.' }
    $env:MERSTRIDE_BINARY = Join-Path (Get-Location) 'artifacts/native/merstride.exe'
    & dotnet test tests/Merstride.Acceptance -c Release --no-restore --logger 'trx;LogFileName=p03.trx' --results-directory artifacts/tests
    if ($LASTEXITCODE -ne 0) { throw 'P01/P02/P03 acceptance/build checks failed.' }
    & ./scripts/benchmark.ps1 -Binary $env:MERSTRIDE_BINARY
    if ($LASTEXITCODE -ne 0) { throw 'Initial native budget check failed.' }
} finally { Pop-Location }
