param(
    [ValidateSet('valid','changed-head','changed-worktree','changed-tests','changed-distribution','changed-benchmark','changed-binary')]
    [string]$Defect = 'valid',
    [string]$ResultFile
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$temp = Join-Path ([IO.Path]::GetTempPath()) ('merstride-package-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temp | Out-Null
function Get-FixtureHash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
try {
    # Copy the public scripts and their real notices into a disposable clean repository.
    # Candidate bytes and gate evidence below are synthetic control-flow fixtures only.
    foreach ($directory in @('scripts','docs/licenses','artifacts/native','artifacts/tests','artifacts/audit')) {
        New-Item -ItemType Directory -Force (Join-Path $temp $directory) | Out-Null
    }
    foreach ($file in @('scripts/release.ps1','scripts/release-functions.ps1','LICENSE','docs/shipped-dependencies.json',
        'docs/licenses/dotnet-LICENSE.txt','docs/licenses/dotnet-THIRD-PARTY-NOTICES.txt','docs/licenses/YamlDotNet-LICENSE.txt','docs/licenses/System.CommandLine-LICENSE.txt')) {
        Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $temp $file)
    }
    '/artifacts/' | Set-Content "$temp/.gitignore"
    '* text=auto eol=lf' | Set-Content "$temp/.gitattributes"
    'original source' | Set-Content "$temp/source.txt"
    & git -C $temp init --initial-branch main -q
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize local package fixture.' }
    & git -C $temp config core.autocrlf false
    & git -C $temp add --all
    if ($LASTEXITCODE -ne 0) { throw 'Cannot stage local package fixture.' }
    & git -C $temp -c user.name=Fixture -c user.email=fixture@example.invalid commit -qm 'Package fixture baseline'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot commit disposable package fixture.' }
    $commit = & git -C $temp rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify local package fixture commit.' }
    $runtime = if ($IsWindows) { 'win-x64' } else { 'linux-x64' }
    $binaryName = if ($IsWindows) { 'merstride.exe' } else { 'merstride' }
    $binary = Join-Path $temp "artifacts/native/$binaryName"
    $candidateBytes = [Text.Encoding]::UTF8.GetBytes('Synthetic package candidate; not a native executable or qualification evidence.')
    [IO.File]::WriteAllBytes($binary, $candidateBytes)
    if (!$IsWindows) { [IO.File]::SetUnixFileMode($binary, [IO.UnixFileMode]493) }
    $evidenceFiles = [ordered]@{
        testsSha256 = 'artifacts/tests/p07.trx'
        distributionSha256 = 'artifacts/audit/distribution.json'
        benchmarkSha256 = 'artifacts/benchmark.json'
    }
    $gate = [ordered]@{schemaVersion=1;result='passed';commit=$commit;runId='package-fixture';rid=$runtime;
        version='0.1.0';dirty=$false;sdk='11.0.100';binarySha256=(Get-FixtureHash $binary)}
    foreach ($field in $evidenceFiles.Keys) {
        "Synthetic $field gate evidence only." | Set-Content (Join-Path $temp $evidenceFiles[$field])
        $gate[$field] = Get-FixtureHash (Join-Path $temp $evidenceFiles[$field])
    }
    $gate | ConvertTo-Json -Depth 8 | Set-Content "$temp/artifacts/local-gate.json"
    $expectedError = switch ($Defect) {
        'changed-head' {
            & git -C $temp -c user.name=Fixture -c user.email=fixture@example.invalid commit --allow-empty -qm 'After gate'
            if ($LASTEXITCODE -ne 0) { throw 'Cannot advance disposable fixture HEAD.' }
            'Package source changed since verification.'
        }
        'changed-worktree' { 'changed source after gate' | Set-Content "$temp/source.txt"; 'Package source is no longer clean.' }
        'changed-tests' { 'changed TRX after gate' | Set-Content "$temp/artifacts/tests/p07.trx"; 'Required gate evidence changed since verification.' }
        'changed-distribution' { 'changed distribution proof after gate' | Set-Content "$temp/artifacts/audit/distribution.json"; 'Required gate evidence changed since verification.' }
        'changed-benchmark' { 'changed benchmark after gate' | Set-Content "$temp/artifacts/benchmark.json"; 'Required gate evidence changed since verification.' }
        'changed-binary' { 'changed candidate after gate' | Set-Content $binary; 'Tested binary changed.' }
        default { $null }
    }
    $output = & pwsh -NoProfile -File "$temp/scripts/release.ps1" -Mode Package -Tag v0.1.0 -Commit $commit `
        -RunId package-fixture -Runtime $runtime 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $release = Join-Path $temp 'artifacts/release'
    $archiveName = "merstride-0.1.0-$runtime" + $(if ($IsWindows) { '.zip' } else { '.tar.gz' })
    $archive = Join-Path $release $archiveName
    $entries = @()
    if ($Defect -eq 'valid') {
        if ($exitCode -ne 0 -or !(Test-Path -LiteralPath $archive)) { throw "Clean public Package failed: $output" }
        $extracted = Join-Path $temp 'artifacts/extracted'
        New-Item -ItemType Directory $extracted | Out-Null
        if ($IsWindows) { [IO.Compression.ZipFile]::ExtractToDirectory($archive, $extracted) }
        else {
            & tar -xzf $archive -C $extracted
            if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect packaged fixture archive.' }
        }
        $entries = @((Get-ChildItem -LiteralPath $extracted -File).Name | Sort-Object)
        $expected = @($binaryName,'platform.json','README.txt','LICENSE','dotnet-LICENSE.txt',
            'dotnet-THIRD-PARTY-NOTICES.txt','YamlDotNet-LICENSE.txt','System.CommandLine-LICENSE.txt','shipped-dependencies.json') | Sort-Object
        if ($entries.Count -ne $expected.Count -or (Compare-Object $entries $expected)) { throw 'Public Package archive has an unexpected file set.' }
        if ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $extracted $binaryName))) -cne [Convert]::ToBase64String($candidateBytes)) { throw 'Packaged candidate bytes differ.' }
        $packaged = Get-Content -Raw "$extracted/platform.json" | ConvertFrom-Json -AsHashtable
        foreach ($field in $gate.Keys) { if ($packaged[$field] -cne $gate[$field]) { throw "Packaged gate field differs: $field" } }
        if (!$IsWindows -and ([IO.File]::GetUnixFileMode((Join-Path $extracted $binaryName)) -band [IO.UnixFileMode]73) -ne [IO.UnixFileMode]73) { throw 'Linux package lost executable permission.' }
    } else {
        if ($exitCode -eq 0 -or !$output.Contains($expectedError)) { throw "Package mutation did not produce its expected refusal: $Defect; $output" }
        if (Test-Path -LiteralPath $archive) { throw 'Refused mutation created an archive.' }
        if (Test-Path -LiteralPath (Join-Path $release "stage-$runtime")) { throw 'Refused mutation entered staging.' }
    }
    $report = [ordered]@{schemaVersion=1;result='passed';case=$Defect;rid=$runtime;scope='Public Package control-flow fixture only; synthetic candidate and gate evidence; no native or release qualification.';
        scriptSha256=(Get-FixtureHash "$temp/scripts/release.ps1");functionsSha256=(Get-FixtureHash "$temp/scripts/release-functions.ps1");
        originalCommit=$commit;exitCode=$exitCode;expectedRefusal=$expectedError;archiveCreated=(Test-Path -LiteralPath $archive);archiveEntries=$entries}
    if ($ResultFile) {
        New-Item -ItemType Directory -Force (Split-Path ([IO.Path]::GetFullPath($ResultFile))) | Out-Null
        $report | ConvertTo-Json -Depth 8 | Set-Content $ResultFile
    }
    Write-Output $output
    Write-Output "PACKAGE FIXTURE PASSED: $Defect ($runtime); synthetic control-flow proof only."
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    if ($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) -and (Split-Path $resolved -Leaf) -like 'merstride-package-test-*') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
