param(
    [string]$Matrix = "$PSScriptRoot/../docs/usability-scenario-review.tsv",
    [switch]$InventoryOnly,
    [string]$ManagedResults = "$PSScriptRoot/../artifacts/tests/scenarios.json",
    [string]$NativeResults = "$PSScriptRoot/../artifacts/tests/native-scenarios.json",
    [string]$Output = "$PSScriptRoot/../artifacts/tests/usability-audit.json"
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/..")
$inventory = Get-Content -Raw "$root/docs/release-scenario-inventory.json" | ConvertFrom-Json
$review = @(Import-Csv -LiteralPath $Matrix -Delimiter "`t")
function Assert-SameSet($Actual, $Expected, [string]$Label) {
    if (@($Actual).Count -ne @($Expected).Count -or @($Actual | Sort-Object -Unique).Count -ne @($Actual).Count -or
        @(Compare-Object @($Actual | Sort-Object) @($Expected | Sort-Object) -CaseSensitive).Count) {
        throw "$Label differs: missing, duplicate or unexpected scenario identities."
    }
}
function Get-Identity($Row) { "$($Row.feature.Replace('\','/')):$($Row.line) $($Row.scenario)" }
function Assert-PrimaryModes($Assessment, $Operations) {
    if ($Assessment.boundary -ne 'native-cli') { return }
    if (!$Assessment.primary -or !$Assessment.modes) { throw "Missing declared primary operations/modes: $($Assessment.identity)" }
    foreach ($declaration in $Assessment.primary.Split(' ')) {
        $parts = $declaration.Split('@', 2)
        $command = $parts[0]
        $variant = if ($parts.Count -eq 2) { $parts[1] } else { 'direct' }
        foreach ($mode in $Assessment.modes.Split(' ')) {
            if (!@($Operations | Where-Object { $_.purpose -ceq 'action' -and $_.command -ceq $command -and $_.mode -ceq $mode -and $_.variant -ceq $variant }).Count) {
                throw "Missing primary $declaration/$mode action (observations do not qualify): $($Assessment.identity)"
            }
        }
    }
}
$local = @($review | Where-Object boundary -NE 'release-environment')
$runtime = @($review | Where-Object boundary -In @('native-cli','native-observer','native-budget'))
Assert-SameSet @($local.identity) @($inventory.scenarios) 'Reviewed local inventory'
Assert-SameSet @($runtime.identity) @($inventory.cleanScenarios) 'Reviewed native inventory'
Assert-SameSet @(Get-ChildItem -LiteralPath "$root/features" -Filter '*.feature' | ForEach-Object { 'features/' + $_.Name }) @($inventory.featureHashes.PSObject.Properties.Name) 'Reviewed feature files'
if (@($review | Where-Object boundary -EQ 'release-environment').Count -ne 3) { throw 'All three recursive release-environment rows must remain accounted for.' }
foreach ($feature in $inventory.featureHashes.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath "$root/$($feature.Name)").Hash.ToLowerInvariant() -cne $feature.Value) {
        throw "Reviewed feature hash is stale: $($feature.Name)"
    }
}
foreach ($row in $review) {
    if (!$row.human -or !$row.ai -or !$row.owner -or !$row.tags -or !$row.evidence -or !$row.disposition) {
        throw "Incomplete human/AI assessment: $($row.identity)"
    }
    if ($row.boundary -notin @('native-cli','public-build','native-observer','native-budget','release-environment')) {
        throw "Unknown execution boundary: $($row.identity)"
    }
    if ($row.owner -cne (($row.tags.Split(' ') | Where-Object { $_ -cmatch '^@P[0-9]+$' }) -join ' ')) { throw "Owner tags differ: $($row.identity)" }
    if ($row.boundary -eq 'native-cli') {
        if (!$row.primary -or $row.primary -match '\bpending\b' -or $row.modes -notin @('text json','json')) { throw "Incomplete declared action/mode review: $($row.identity)" }
        if ($row.modes -ceq 'json' -and !$row.human.Contains('Deliberate malformed format syntax')) { throw "Unexplained readable-mode exception: $($row.identity)" }
    }
}
if ($InventoryOnly) { Write-Host "Usability review inventory: $($review.Count) independently identified rows; no execution claim."; exit 0 }
$managed = @(Get-Content -Raw $ManagedResults | ConvertFrom-Json)
$native = @(Get-Content -Raw $NativeResults | ConvertFrom-Json)
Assert-SameSet @($managed | ForEach-Object { Get-Identity $_ }) @($review.identity) 'Actual Gherkin parser inventory'
Assert-SameSet @($native | ForEach-Object { Get-Identity $_ }) @($runtime.identity) 'Executed native inventory'
$managedByIdentity = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
$reviewByIdentity = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($row in $managed) { $managedByIdentity.Add((Get-Identity $row), $row) }
foreach ($row in $review) { $reviewByIdentity.Add($row.identity, $row) }
foreach ($row in $review) {
    $actual = $managedByIdentity[$row.identity]
    if (($actual.tags -join ' ') -cne $row.tags) { throw "Requirement/owner tags changed: $($row.identity)" }
    if ($row.boundary -ne 'release-environment' -and $actual.result.status -cne 'passed') { throw "Required local scenario did not pass: $($row.identity)" }
    if ($row.boundary -eq 'native-cli' -and $actual.result.complementaryCalls -lt 1) { throw "Missing readable execution: $($row.identity)" }
    Assert-PrimaryModes $row $actual.result.operations
}
foreach ($row in $native) {
    if ($row.result.status -cne 'passed') { throw "Required native scenario did not pass: $(Get-Identity $row)" }
    $assessment = $reviewByIdentity[(Get-Identity $row)]
    if (($row.tags -join ' ') -cne $assessment.tags) { throw "Native requirement/owner tags changed: $(Get-Identity $row)" }
    if ($assessment.boundary -eq 'native-cli' -and $row.complementaryCalls -lt 1) { throw "Missing independent readable native execution: $(Get-Identity $row)" }
    Assert-PrimaryModes $assessment $row.operations
}
$inputs = @(& git -C $root ls-files --cached --others --exclude-standard -- src tests features scripts examples tools .github Directory.Build.props Directory.Build.targets global.json docs/CONTRACT.md docs/adr docs/release-scenario-inventory.json docs/usability-scenario-review.tsv)
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify reviewed source inputs.' }
$identities = @($inputs | Sort-Object -CaseSensitive | ForEach-Object { $_.Replace('\','/') + ' ' + (Get-FileHash -LiteralPath "$root/$_").Hash.ToLowerInvariant() })
$sourceHash = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($identities -join "`n") + "`n")))
[ordered]@{schemaVersion=1;result='passed';reviewedBase='910b7e7';reviewRows=$review.Count;
    localCases=$local.Count;nativeCases=$runtime.Count;releaseEnvironmentCases=3;releaseReady=$false;
    sourceInputsSha256=$sourceHash;sourceInputCount=$inputs.Count;
    matrixSha256=(Get-FileHash -LiteralPath $Matrix).Hash.ToLowerInvariant();
    managedSha256=(Get-FileHash -LiteralPath $ManagedResults).Hash.ToLowerInvariant();
    nativeSha256=(Get-FileHash -LiteralPath $NativeResults).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content -LiteralPath $Output
Write-Host "Usability audit: $($review.Count) reviewed rows, $($local.Count) local and $($runtime.Count) native cases passed. Three external release environments remain separate."
