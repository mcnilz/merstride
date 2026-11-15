param([string]$Defect = 'none')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
. "$root/scripts/release-functions.ps1"
$temp = Join-Path ([IO.Path]::GetTempPath()) ('merstride-release-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temp | Out-Null
$savedGh = $env:MERSTRIDE_GH_FIXTURE
try {
    $version = if ($Defect -eq 'prerelease draft') { '0.1.0-rc.1' } else { '0.1.0' }
    $releaseTag = 'v' + $version
    $git = Join-Path $temp 'git'; New-Item -ItemType Directory $git | Out-Null
    & git -C $git init --initial-branch main -q
    & git -C $git -c user.name=Fixture -c user.email=fixture@example.invalid commit --allow-empty -qm fixture
    if ($LASTEXITCODE -ne 0) { throw 'Local Git fixture setup failed.' }
    $commit = & git -C $git rev-parse HEAD
    & git -C $git update-ref refs/remotes/origin/main $commit
    & git -C $git tag $releaseTag
    $output = Join-Path $temp 'assets'; New-Item -ItemType Directory $output | Out-Null
    $platforms = [ordered]@{}
    foreach ($rid in @('win-x64','linux-x64')) {
        $stage = Join-Path $temp $rid; New-Item -ItemType Directory $stage | Out-Null
        $binary = if ($rid -eq 'win-x64') { 'merstride.exe' } else { 'merstride' }
        # Controlled release-boundary fixture, never claimed to be native behavior evidence.
        "release fixture $rid" | Set-Content "$stage/$binary"
        $record = @{result='passed';commit=$commit;runId='fixture-run';rid=$rid;version=$version;dirty=$false;sdk='11.0.100';binarySha256=(Get-ReleaseHash "$stage/$binary")}
        if ($Defect -eq 'an artifact from another commit' -and $rid -eq 'linux-x64') { $record.commit = 'a' * 40 }
        if ($Defect -eq 'a failed required test' -and $rid -eq 'win-x64') { $record.result='failed' }
        $record | ConvertTo-Json | Set-Content "$stage/platform.json"
        'Fixture instructions' | Set-Content "$stage/README.txt"
        Copy-ReleaseNotices $stage
        $archive = Join-Path $output (Get-ArchiveName $version $rid)
        if ($rid -eq 'win-x64') { [IO.Compression.ZipFile]::CreateFromDirectory($stage,$archive) }
        else {
            $stream = [IO.File]::Create($archive)
            $gzip = [IO.Compression.GZipStream]::new($stream,[IO.Compression.CompressionLevel]::Optimal)
            $writer = [System.Formats.Tar.TarWriter]::new($gzip)
            try {
                foreach ($file in Get-ChildItem $stage -File) {
                    $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile,$file.Name)
                    $entry.Mode = [IO.UnixFileMode]493
                    $entry.DataStream = [IO.File]::OpenRead($file.FullName)
                    try { $writer.WriteEntry($entry) } finally { $entry.DataStream.Dispose() }
                }
            } finally { $writer.Dispose(); $gzip.Dispose(); $stream.Dispose() }
        }
        $inventory = Read-ReleaseJson "$root/docs/release-scenario-inventory.json"
        $cases = @($inventory.cleanScenarios | ForEach-Object {
            if ($_ -notmatch '^(.*):(\d+) (.*)$') { throw 'Invalid controlled scenario identity.' }
            @{feature=$Matches[1];line=[int]$Matches[2];scenario=$Matches[3];result=@{status='passed'}}
        })
        $platforms[$rid] = @{commit=$commit;cleanNoRuntime=$true;allApplicableScenarios=$true;processTree=$true;atomicSnapshots=$true;referenceBudgets=$true;binarySha256=$record.binarySha256;environment=$(if ($rid -eq 'win-x64') {'fixture Windows 11'} else {'fixture Linux'});evidence='controlled fixture only';scenarios=$cases;featureHashes=$inventory.featureHashes}
    }
    if ($Defect -eq 'a missing advertised platform archive') { Remove-Item -LiteralPath "$output/merstride-0.1.0-linux-x64.tar.gz" }
    $qualification = Join-Path $temp 'qualification.json'
    $samples = @(foreach ($task in @('deterministic-chain','test-fix-loop')) { foreach ($variant in @('manual','merstride')) { foreach ($i in 1..5) {
        @{task=$task;variant=$variant;iteration=$i;elapsedMilliseconds=1;actual=@{agent='fixture';model='fixture';taskIdentity=$task;equivalentResult=$true;modelCalls=0;cacheEffects='fixture';failures=@();inputTokens=$null;outputTokens=$null}}
    } } })
    @{schemaVersion=1;commit=$commit;platforms=$platforms;tokenExperiment=@{result='measured';evidence='controlled fixture only';samples=$samples}} | ConvertTo-Json -Depth 15 | Set-Content $qualification
    if ($Defect -eq 'missing qualification') { Copy-Item "$root/docs/release-qualification.json" $qualification -Force }
    $tag = if ($Defect -eq 'invalid tag') { 'v01.2.3' } else { $releaseTag }
    if ($Defect -eq 'incomplete experiment') {
        $q = Read-ReleaseJson $qualification; $q.tokenExperiment.samples=$q.tokenExperiment.samples[0..18]
        $q | ConvertTo-Json -Depth 15 | Set-Content $qualification
    }
    if ($Defect -eq 'failed clean scenario') {
        $q = Read-ReleaseJson $qualification; $q.platforms['linux-x64'].scenarios[0].result.status='failed'
        $q | ConvertTo-Json -Depth 15 | Set-Content $qualification
    }
    if ($Defect -eq 'external qualification') {
        $q = Read-ReleaseJson $qualification
        $env:GITHUB_SHA = $commit
        $env:WINDOWS_QUALIFICATION_JSON = $q.platforms['win-x64'] | ConvertTo-Json -Depth 20 -Compress
        $env:LINUX_QUALIFICATION_JSON = $q.platforms['linux-x64'] | ConvertTo-Json -Depth 20 -Compress
        $env:TOKEN_EXPERIMENT_JSON = $q.tokenExperiment | ConvertTo-Json -Depth 20 -Compress
        $qualification = Join-Path $temp 'external.json'
        & pwsh -NoProfile -File "$root/scripts/collect-qualification.ps1" -Output $qualification
        if ($LASTEXITCODE -ne 0) { throw 'External qualification collection failed.' }
    }
    $arguments = @('-Directory',$output,'-Tag',$tag,'-Commit',$commit,'-RunId','fixture-run','-Qualification',$qualification)
    & pwsh -NoProfile -File "$root/scripts/release.ps1" -Mode Assemble @arguments
    $assembled = $LASTEXITCODE
    if ($Defect -eq 'manifest platform identity' -and $assembled -eq 0) {
        $manifest = Read-ReleaseJson "$output/manifest.json"; $manifest.platforms[0].binarySha256='a' * 64
        $manifest | ConvertTo-Json -Depth 20 | Set-Content "$output/manifest.json"
        @(Get-ReleaseAssetNames $version | Where-Object { $_ -ne 'SHA256SUMS' } | Sort-Object | ForEach-Object { "$(Get-ReleaseHash (Join-Path $output $_))  $_" }) | Set-Content "$output/SHA256SUMS"
    }
    $env:MERSTRIDE_GH_FIXTURE = Join-Path $temp 'gh.json'
    @{tag=$releaseTag;isPrerelease=$false;exists=($Defect -in @('an already published version','matching draft','wrong draft','corrupt draft','prerelease draft'));isDraft=($Defect -ne 'an already published version');commit=$(if ($Defect -eq 'wrong draft') { 'b' * 40 } else { $commit });assets=@{};calls=@();public=$false} | ConvertTo-Json -Depth 10 | Set-Content $env:MERSTRIDE_GH_FIXTURE
    if ($Defect -in @('matching draft','corrupt draft')) {
        $state = Read-ReleaseJson $env:MERSTRIDE_GH_FIXTURE
        $state.assets['LICENSE']=[Convert]::ToBase64String([IO.File]::ReadAllBytes("$output/LICENSE"))
        if ($Defect -eq 'corrupt draft') { $state.assets['LICENSE']=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('wrong')) }
        $state | ConvertTo-Json -Depth 10 | Set-Content $env:MERSTRIDE_GH_FIXTURE
    }
    if ($Defect -eq 'a tag outside main') { & git -C $git update-ref refs/remotes/origin/main ('c' * 40) 2>$null; & git -C $git update-ref -d refs/remotes/origin/main }
    if ($assembled -eq 0) {
        & pwsh -NoProfile -File "$root/scripts/release.ps1" -Mode Publish @arguments -GitDirectory $git -Repository fixture/merstride -Gh "$PSScriptRoot/gh.ps1"
        $result = $LASTEXITCODE
    } else { $result = $assembled }
    $state = Read-ReleaseJson $env:MERSTRIDE_GH_FIXTURE
    if ($Defect -in @('none','matching draft','prerelease draft','external qualification')) {
        if ($result -ne 0 -or !$state.public -or $state.assets.Count -ne @(Get-ReleaseAssetNames $version).Count -or $state.isPrerelease -ne $version.Contains('-')) { throw 'Expected complete fixture publication with correct prerelease status.' }
        foreach ($name in Get-ReleaseAssetNames $version) {
            if ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$output/$name")) -cne $state.assets[$name]) { throw 'Published fixture bytes differ.' }
        }
    } else {
        if ($result -eq 0 -or $state.public -or @($state.calls | Where-Object { $_ -match '^upload|^edit' }).Count) { throw 'Unsafe release was not blocked before publication/overwrite.' }
    }
    Write-Output "RELEASE FIXTURE PASSED: $Defect; no real remote operation."
} finally {
    $env:MERSTRIDE_GH_FIXTURE = $savedGh
    if ([IO.Path]::GetFullPath($temp).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) -and (Split-Path $temp -Leaf) -like 'merstride-release-test-*') { Remove-Item -LiteralPath $temp -Recurse -Force }
}
