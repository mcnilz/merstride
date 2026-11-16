param(
    [Parameter(Mandatory)][string]$Tag,
    [string]$ExpectedVersion,
    [ValidateSet('lightweight','annotated','wrong-commit','outside-main')]
    [string]$Context = 'lightweight'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$temp = Join-Path $root ('artifacts/tag-tests/' + [Guid]::NewGuid().ToString('N'))
$savedOutput = $env:GITHUB_OUTPUT
New-Item -ItemType Directory -Force $temp | Out-Null
try {
    & git -C $temp init --initial-branch main -q
    & git -C $temp -c user.name=Fixture -c user.email=fixture@example.invalid commit --allow-empty -qm fixture
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize local tag fixture.' }
    $commit = & git -C $temp rev-parse HEAD
    & git -C $temp update-ref refs/remotes/origin/main $commit
    # Invalid tag cases still exercise the public entry point in a real repository.
    if ($ExpectedVersion) {
        if ($Context -eq 'annotated') {
            & git -C $temp -c user.name=Fixture -c user.email=fixture@example.invalid tag -a $Tag -m fixture
        } else { & git -C $temp tag $Tag }
        if ($LASTEXITCODE -ne 0) { throw 'Cannot create local fixture tag.' }
    }
    if ($Context -eq 'wrong-commit') { $commit = 'a' * 40 }
    if ($Context -eq 'outside-main') { & git -C $temp update-ref -d refs/remotes/origin/main }
    $env:GITHUB_OUTPUT = Join-Path $temp 'output.txt'
    & pwsh -NoProfile -File "$root/scripts/release.ps1" -Mode Tag -Tag $Tag -Commit $commit -RunId fixture-run -GitDirectory $temp
    $result = $LASTEXITCODE
    if ($ExpectedVersion -and $Context -in @('lightweight','annotated')) {
        if ($result -ne 0 -or (Get-Content -Raw $env:GITHUB_OUTPUT).Trim() -cne "version=$ExpectedVersion") {
            throw 'Valid tag did not export the expected version.'
        }
    } elseif ($result -eq 0 -or (Test-Path $env:GITHUB_OUTPUT)) {
        throw 'Invalid release tag/context exported a version.'
    }
    Write-Output 'TAG FIXTURE PASSED: local repository only; no remote tag created.'
} finally {
    $env:GITHUB_OUTPUT = $savedOutput
    $parent = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/tag-tests')) + [IO.Path]::DirectorySeparatorChar
    if (![IO.Path]::GetFullPath($temp).StartsWith($parent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected tag fixture cleanup path.' }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
