param(
    [Parameter(Mandatory)][string]$Binary,
    [Parameter(Mandatory)][string]$Fixture,
    [Parameter(Mandatory)][string]$SuiteDriver,
    [Parameter(Mandatory)][string]$Commit,
    [string]$Output = 'artifacts/qualification/clean-target.json'
)
$ErrorActionPreference = 'Stop'
# Run inside a newly provisioned Windows 11 / Linux VM, not on the SDK build host.
# SuiteDriver is the independently executable Merstride.NativeSuite output. Hosted
# macOS additionally uses qualify-macos.py for exhaustive removal/absence evidence.
$locations = if ($IsWindows) {
    @("$env:ProgramFiles/dotnet", "${env:ProgramFiles(x86)}/dotnet", "$env:LOCALAPPDATA/Microsoft/dotnet", "$env:USERPROFILE/.dotnet")
} elseif ($IsMacOS) { @('/usr/local/share/dotnet','/usr/local/lib/dotnet','/opt/homebrew/share/dotnet',"$env:HOME/.dotnet") }
else { @('/usr/share/dotnet','/usr/lib/dotnet','/usr/local/share/dotnet',"$env:HOME/.dotnet") }
if ((Get-Command dotnet -ErrorAction SilentlyContinue) -or @($locations | Where-Object { Test-Path $_ }).Count) { throw 'Target has a discoverable .NET installation; PATH hiding is insufficient.' }
$environment = [Runtime.InteropServices.RuntimeInformation]::OSDescription
if ($IsWindows) {
    $os = Get-CimInstance Win32_OperatingSystem
    if ($os.ProductType -ne 1 -or [int]$os.BuildNumber -lt 22000) { throw 'A clean Windows 11 workstation target is required.' }
    $environment = $os.Caption + '; build ' + $os.BuildNumber
}
foreach ($path in @($Binary,$Fixture,$SuiteDriver)) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing native target input: $path" } }
$scenarioOutput = [IO.Path]::GetFullPath($Output + '.scenarios.json')
New-Item -ItemType Directory -Force (Split-Path $scenarioOutput) | Out-Null
& $SuiteDriver --binary ([IO.Path]::GetFullPath($Binary)) --fixture ([IO.Path]::GetFullPath($Fixture)) --exclude-release-environment-orchestrator --results $scenarioOutput
if ($LASTEXITCODE -ne 0) { throw 'Clean-target native acceptance driver failed.' }
$results = Get-Content -Raw $scenarioOutput | ConvertFrom-Json
$inventory = Get-Content -Raw "$PSScriptRoot/../docs/release-scenario-inventory.json" | ConvertFrom-Json
$expected = @($inventory.cleanScenarios | Sort-Object)
$actual = @($results | ForEach-Object { "$($_.feature.Replace('\','/')):$($_.line) $($_.scenario)" } | Sort-Object)
if (!$expected.Count -or $actual.Count -ne $expected.Count -or (Compare-Object $actual $expected) -or @($results | Where-Object { $_.result.status -cne 'passed' }).Count) { throw "All $($expected.Count) inventoried nonrecursive runtime cases must pass independently. Build-only checks run on the build host." }
$featureHashes = [ordered]@{}
foreach ($feature in Get-ChildItem "$PSScriptRoot/../features/*.feature") { $featureHashes["features/"+$feature.Name]=(Get-FileHash $feature.FullName).Hash.ToLowerInvariant() }
[ordered]@{schemaVersion=1;commit=$Commit;binarySha256=(Get-FileHash $Binary).Hash.ToLowerInvariant();fixtureSha256=(Get-FileHash $Fixture).Hash.ToLowerInvariant();featureHashes=$featureHashes;
    cleanNoRuntime=$true;allApplicableScenarios=$true;environment=$environment;
    checkedRuntimeLocationsCount=$locations.Count;driverSha256=(Get-FileHash $SuiteDriver).Hash.ToLowerInvariant();
    scenariosSha256=(Get-FileHash $scenarioOutput).Hash.ToLowerInvariant();scenarios=$results;
    limitation='Installation-location checks supplement clean VM image provenance; retain the VM image identity and provisioning log. The native suite runs independently of the managed test host.'
} | ConvertTo-Json -Depth 15 | Set-Content $Output
