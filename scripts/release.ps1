param(
    [Parameter(Mandatory)][ValidateSet('Tag','Package','Assemble','Validate','Publish')][string]$Mode,
    [string]$Directory = "$PSScriptRoot/../artifacts/release",
    [string]$Tag = $env:GITHUB_REF_NAME,
    [string]$Commit = $env:GITHUB_SHA,
    [string]$RunId = $env:GITHUB_RUN_ID,
    [string]$Runtime,
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$GitDirectory = "$PSScriptRoot/..",
    [string]$Qualification = "$PSScriptRoot/../docs/release-qualification.json",
    [string]$Gh = 'gh'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/release-functions.ps1"
try {
    $version = Get-ReleaseVersion $Tag
    if ($Commit -cnotmatch '^[0-9a-f]{40}$' -or !$RunId) { throw 'Exact source commit and workflow run identity are required.' }
    if ($Mode -eq 'Tag') {
        Assert-ReleaseTag $GitDirectory $Tag $Commit
        "version=$version" >> $env:GITHUB_OUTPUT
        exit 0
    }
    New-Item -ItemType Directory -Force $Directory | Out-Null
    if ($Mode -in @('Validate','Publish') -and !$PSBoundParameters.ContainsKey('Qualification')) { $Qualification = Join-Path $Directory 'qualification.json' }
    if ($Mode -eq 'Package') {
        if ($Runtime -cnotin @(Get-ReleaseRuntimes)) { throw 'Unsupported release platform.' }
        $root = [IO.Path]::GetFullPath("$PSScriptRoot/..")
        $gate = Read-ReleaseJson "$root/artifacts/local-gate.json"
        if ($gate.result -cne 'passed' -or $gate.commit -cne $Commit -or $gate.runId -cne $RunId -or $gate.rid -cne $Runtime -or $gate.version -cne $version -or $gate.dirty) { throw 'Package requires a clean, successfully tested same-run/version/commit/platform candidate.' }
        $currentCommit = & git -C $root rev-parse HEAD
        if ($LASTEXITCODE -ne 0 -or $currentCommit -cne $Commit) { throw 'Package source changed since verification.' }
        $changes = @(& git -C $root status --porcelain)
        if ($LASTEXITCODE -ne 0 -or $changes.Count) { throw 'Package source is no longer clean.' }
        foreach ($evidence in @(
            @{path='artifacts/tests/p07.trx';field='testsSha256'},
            @{path='artifacts/audit/distribution.json';field='distributionSha256'},
            @{path='artifacts/benchmark.json';field='benchmarkSha256'}
        )) {
            if ((Get-ReleaseHash "$root/$($evidence.path)") -cne $gate[$evidence.field]) { throw 'Required gate evidence changed since verification.' }
        }
        $binary = "$root/artifacts/native/" + $(if ($Runtime -eq 'win-x64') { 'merstride.exe' } else { 'merstride' })
        if ((Get-ReleaseHash $binary) -cne $gate.binarySha256) { throw 'Tested binary changed.' }
        $stage = Join-Path $Directory ('stage-' + $Runtime)
        if (Test-Path $stage) { throw 'Package staging directory already exists.' }
        New-Item -ItemType Directory $stage | Out-Null
        Copy-Item -LiteralPath $binary -Destination $stage
        Copy-ReleaseNotices $stage
        $gate | ConvertTo-Json -Depth 10 | Set-Content "$stage/platform.json"
        'Merstride: run ./merstride guide --json (Windows: .\merstride.exe guide --json). Use an absolute --state-dir to resume from another directory. See release notes for qualification and system requirements.' | Set-Content "$stage/README.txt"
        $archive = Join-Path $Directory (Get-ArchiveName $version $Runtime)
        if (Test-Path $archive) { throw 'Refusing to overwrite a platform archive.' }
        if ($Runtime -eq 'win-x64') { [IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive) }
        else {
            $copyfile = $env:COPYFILE_DISABLE
            try {
                if ($IsMacOS) { $env:COPYFILE_DISABLE = '1' }
                & tar -czf $archive -C $stage -- @((Get-ChildItem $stage -File).Name | Sort-Object)
                if ($LASTEXITCODE -ne 0) { throw 'Platform tar creation failed.' }
            } finally { $env:COPYFILE_DISABLE = $copyfile }
        }
        Assert-PlatformArchive $archive $Runtime $version $Commit $RunId | Out-Null
        Write-Output "RELEASE: packaged $archive"
        exit 0
    }
    if ($Mode -eq 'Assemble') {
        $platforms = Get-ReleaseRuntimes | ForEach-Object {
            Assert-PlatformArchive (Join-Path $Directory (Get-ArchiveName $version $_)) $_ $version $Commit $RunId
        }
        Assert-Qualification $Qualification $Commit $platforms
        Copy-Item -LiteralPath $Qualification -Destination "$Directory/qualification.json"
        Copy-ReleaseNotices $Directory
        Copy-Item -LiteralPath "$PSScriptRoot/../docs/RELEASE-NOTES.md" -Destination "$Directory/RELEASE-NOTES.md"
        $assets = @(Get-ReleaseAssetNames $version | Where-Object { $_ -notin @('manifest.json','SHA256SUMS') })
        $manifest = [ordered]@{schemaVersion=1;version=$version;tag=$Tag;commit=$Commit;runId=$RunId;platforms=@($platforms);
            qualificationSha256=(Get-ReleaseHash $Qualification);assets=@($assets | ForEach-Object { @{ name=$_;sha256=(Get-ReleaseHash (Join-Path $Directory $_)) } })}
        $manifest | ConvertTo-Json -Depth 15 | Set-Content "$Directory/manifest.json"
        @($assets + 'manifest.json' | Sort-Object | ForEach-Object { "$(Get-ReleaseHash (Join-Path $Directory $_))  $_" }) | Set-Content "$Directory/SHA256SUMS"
    }
    Assert-ReleaseSet $Directory $version $Commit $RunId $Qualification
    if ($Mode -eq 'Publish') {
        Assert-ReleaseTag $GitDirectory $Tag $Commit
        if ($Repository -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Explicit GitHub repository required.' }
        Publish-ReleaseSet $Gh $Repository $Tag $Commit $Directory $version
    }
    Write-Output 'RELEASE: complete same-run artifact set verified.'
} catch { Write-Error "RELEASE: $($_.Exception.Message)"; exit 1 }
