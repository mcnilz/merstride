$ErrorActionPreference = 'Stop'
$gate = [IO.Path]::GetFullPath("$PSScriptRoot/../../scripts/repository-privacy.ps1")
$temp = Join-Path ([IO.Path]::GetTempPath()) ("merstride-privacy-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    & git -C $temp init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize privacy fixture.' }
    Set-Content -LiteralPath "$temp/README.md" -Value 'Public project summary.'
    & git -C $temp add README.md
    & pwsh -NoProfile -File $gate -Repository $temp *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Privacy gate rejected a public summary.' }
    $cases = @(
        @{ path='docs/evidence/raw.json'; content='{}' },
        @{ path='artifacts/raw.json'; content='{}' },
        @{ path='result.trx'; content='<test/>' },
        @{ path='environment.txt'; content='local environment' },
        @{ path='private.md'; content=('/ho' + 'me/fixture-user/work') },
        @{ path='private.md'; content=('C:' + '\Users\fixture-user\work') },
        @{ path='private.md'; content=('-' * 5 + 'BEGIN PRIVATE KEY' + '-' * 5) },
        @{ path='private.md'; content=('{' + '"host' + 'name":"fixture-host"}') }
    )
    foreach ($case in $cases) {
        $file = Join-Path $temp $case.path
        New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
        Set-Content -LiteralPath $file -Value $case.content
        & git -C $temp add --force -- $case.path
        if ($LASTEXITCODE -ne 0) { throw 'Cannot stage privacy fixture.' }
        & pwsh -NoProfile -File $gate -Repository $temp *> $null
        if ($LASTEXITCODE -eq 0) { throw "Privacy gate accepted forbidden fixture: $($case.path)" }
        & git -C $temp rm --force --quiet -- $case.path
        if ($LASTEXITCODE -ne 0) { throw 'Cannot remove privacy fixture.' }
    }
    Write-Output 'PRIVACY FIXTURES: public summary accepted; eight forbidden inputs rejected.'
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    if ((Split-Path $resolved -Parent) -ceq [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') -and (Split-Path $resolved -Leaf) -like 'merstride-privacy-*') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
