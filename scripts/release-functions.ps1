function Read-ReleaseJson([string]$Path) { Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable }
function Get-ReleaseHash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Get-ReleaseVersion([string]$Tag) {
    if ($Tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-([0-9A-Za-z-]+\.)*[0-9A-Za-z-]+)?$') { throw 'Expected a SemVer vX.Y.Z or prerelease tag.' }
    $version = $Tag.Substring(1)
    if ($version.Contains('-')) {
        foreach ($part in ($version -split '-',2)[1].Split('.')) {
            if ($part -match '^0[0-9]+$') { throw 'Numeric SemVer prerelease identifiers cannot have leading zeros.' }
        }
    }
    return $version
}
function Assert-ReleaseTag([string]$GitDirectory, [string]$Tag, [string]$Commit) {
    $resolved = & git -C $GitDirectory rev-parse --verify "$Tag^{commit}"
    if ($LASTEXITCODE -ne 0 -or $resolved -cne $Commit) { throw 'Tag does not resolve to tested source commit.' }
    & git -C $GitDirectory merge-base --is-ancestor $Commit refs/remotes/origin/main
    if ($LASTEXITCODE -ne 0) { throw 'Release tag must be on origin/main.' }
}
function Get-ArchiveName([string]$Version, [string]$Rid) { "merstride-$Version-$Rid" + $(if ($Rid -eq 'win-x64') { '.zip' } else { '.tar.gz' }) }
function Get-NoticeNames { @('LICENSE','dotnet-LICENSE.txt','dotnet-THIRD-PARTY-NOTICES.txt','YamlDotNet-LICENSE.txt','System.CommandLine-LICENSE.txt','shipped-dependencies.json') }
function Get-ReleaseAssetNames([string]$Version) { @((Get-ArchiveName $Version 'win-x64'),(Get-ArchiveName $Version 'linux-x64')) + @(Get-NoticeNames) + @('RELEASE-NOTES.md','qualification.json','manifest.json','SHA256SUMS') }
function Copy-ReleaseNotices([string]$Destination) {
    Copy-Item -LiteralPath "$PSScriptRoot/../LICENSE" -Destination $Destination
    foreach ($file in @('dotnet-LICENSE.txt','dotnet-THIRD-PARTY-NOTICES.txt','YamlDotNet-LICENSE.txt','System.CommandLine-LICENSE.txt')) { Copy-Item -LiteralPath "$PSScriptRoot/../docs/licenses/$file" -Destination $Destination }
    Copy-Item -LiteralPath "$PSScriptRoot/../docs/shipped-dependencies.json" -Destination $Destination
}
function Assert-PlatformArchive([string]$Archive, [string]$Rid, [string]$Version, [string]$Commit, [string]$RunId) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('merstride-archive-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $temp | Out-Null
    try {
        $binary = if ($Rid -eq 'win-x64') { 'merstride.exe' } else { 'merstride' }
        $expected = @($binary,'platform.json','README.txt') + @(Get-NoticeNames)
        if ($Rid -eq 'win-x64') {
            $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
            try { $entries = @($zip.Entries.FullName) } finally { $zip.Dispose() }
        } else {
            $entries = @(& tar -tzf $Archive)
            if ($LASTEXITCODE -ne 0) { throw 'Cannot list Linux archive.' }
            $permissions = @(& tar -tvzf $Archive)
            if ($LASTEXITCODE -ne 0 -or @($permissions | Where-Object { $_ -notmatch '^-' }).Count -or !($permissions | Where-Object { $_ -match '^-..x..x..x.* merstride$' })) { throw 'Linux archive requires regular files and executable permissions.' }
        }
        if ($entries.Count -ne $expected.Count -or (Compare-Object ($entries | Sort-Object) ($expected | Sort-Object))) { throw 'Unexpected/missing archive contents.' }
        if ($Rid -eq 'win-x64') { [IO.Compression.ZipFile]::ExtractToDirectory($Archive, $temp) }
        else { & tar -xzf $Archive -C $temp; if ($LASTEXITCODE -ne 0) { throw 'Cannot extract Linux archive.' } }
        $gate = Read-ReleaseJson "$temp/platform.json"
        if ($gate.result -cne 'passed' -or $gate.commit -cne $Commit -or $gate.runId -cne $RunId -or $gate.version -cne $Version -or $gate.rid -cne $Rid -or $gate.dirty -or $gate.sdk -cne '11.0.100') { throw 'Platform identity or required local gates differ.' }
        if ((Get-ReleaseHash "$temp/$binary") -cne $gate.binarySha256) { throw 'Archive binary checksum differs from tested binary.' }
        foreach ($name in Get-NoticeNames) {
            $source = if ($name -eq 'LICENSE') { "$PSScriptRoot/../LICENSE" } elseif ($name -eq 'shipped-dependencies.json') { "$PSScriptRoot/../docs/$name" } else { "$PSScriptRoot/../docs/licenses/$name" }
            if ((Get-ReleaseHash "$temp/$name") -cne (Get-ReleaseHash $source)) { throw "Distribution notice differs: $name" }
        }
        return $gate
    } finally {
        # Only the exact unique directory created above can be removed.
        if ([IO.Path]::GetFullPath($temp).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) -and (Split-Path $temp -Leaf) -like 'merstride-archive-*') { Remove-Item -LiteralPath $temp -Recurse -Force }
    }
}
function Assert-Qualification([string]$Path, [string]$Commit, $Platforms) {
    $q = Read-ReleaseJson $Path
    if ($q.schemaVersion -ne 1 -or $q.commit -cne $Commit) { throw 'Release qualification is incomplete or belongs to another commit.' }
    foreach ($p in $Platforms) {
        $proof = $q.platforms[$p.rid]
        if (!$proof) { throw "Required platform qualification unavailable: $($p.rid)" }
        foreach ($field in @('cleanNoRuntime','allApplicableScenarios','processTree','atomicSnapshots','referenceBudgets')) {
            if ($proof[$field] -isnot [bool] -or !$proof[$field]) { throw "Required release qualification unavailable: $($p.rid) $field" }
        }
        if ($proof.binarySha256 -cne $p.binarySha256 -or !$proof.environment -or !$proof.evidence) { throw 'Qualification must identify the tested binary and actual environment/evidence.' }
        if ($proof.commit -cne $Commit) { throw 'Platform qualification source commit differs.' }
        if ($p.rid -eq 'win-x64' -and $proof.environment -notmatch 'Windows 11') { throw 'Windows Server is not Windows 11 qualification.' }
        $inventory = Read-ReleaseJson "$PSScriptRoot/../docs/release-scenario-inventory.json"
        foreach ($feature in $inventory.featureHashes.Keys) {
            if ($proof.featureHashes[$feature] -cne $inventory.featureHashes[$feature] -or $inventory.featureHashes[$feature] -cne (Get-ReleaseHash "$PSScriptRoot/../$feature")) { throw 'Qualification feature-source identity differs.' }
        }
        $expected = @($inventory.cleanScenarios | Sort-Object)
        $actual = @($proof.scenarios | ForEach-Object { "$($_.feature.Replace('\','/')):$($_.line) $($_.scenario)" } | Sort-Object)
        if ($actual.Count -ne $expected.Count -or (Compare-Object $actual $expected) -or @($proof.scenarios | Where-Object { $_.result.status -cne 'passed' }).Count) { throw 'Every actual release-applicable scenario/outline row must pass on the clean target.' }
    }
    if (!$q.tokenExperiment -or $q.tokenExperiment.result -cne 'measured' -or !$q.tokenExperiment.evidence) { throw 'Same-agent token-benefit experiment evidence is unavailable.' }
    $samples = @($q.tokenExperiment.samples)
    if ($samples.Count -ne 20 -or @($samples.actual.agent | Sort-Object -Unique).Count -ne 1 -or @($samples.actual.model | Sort-Object -Unique).Count -ne 1) { throw 'Token experiment requires twenty actual same-agent/model samples.' }
    foreach ($task in @('deterministic-chain','test-fix-loop')) {
        foreach ($variant in @('manual','merstride')) {
            $group = @($samples | Where-Object { $_.task -ceq $task -and $_.variant -ceq $variant })
            if ($group.Count -ne 5 -or (Compare-Object @($group.iteration | Sort-Object) @(1,2,3,4,5))) { throw 'Token experiment requires five independent runs per task and variant.' }
            foreach ($sample in $group) {
                foreach ($field in @('agent','model','taskIdentity','equivalentResult','modelCalls','cacheEffects','failures','inputTokens','outputTokens')) {
                    if (!$sample.actual.ContainsKey($field)) { throw "Missing actual token experiment field: $field" }
                }
                if ($sample.actual.equivalentResult -isnot [bool] -or !$sample.actual.equivalentResult -or $sample.elapsedMilliseconds -le 0) { throw 'Token experiment result equivalence or timing unavailable.' }
            }
        }
        if (@(($samples | Where-Object task -CEQ $task).actual.taskIdentity | Sort-Object -Unique).Count -ne 1) { throw 'Token experiment task changed across variants.' }
    }
}
function Assert-ReleaseSet([string]$Directory, [string]$Version, [string]$Commit, [string]$RunId, [string]$Qualification) {
    $expected = @(Get-ReleaseAssetNames $Version | Sort-Object)
    $actual = @((Get-ChildItem -LiteralPath $Directory -File).Name | Sort-Object)
    if (Compare-Object $expected $actual) { throw 'Release asset set is incomplete or contains unexpected files.' }
    $manifest = Read-ReleaseJson "$Directory/manifest.json"
    if ($manifest.schemaVersion -ne 1 -or $manifest.version -cne $Version -or $manifest.tag -cne "v$Version" -or $manifest.commit -cne $Commit -or $manifest.runId -cne $RunId -or $manifest.platforms.Count -ne 2) { throw 'Release manifest identity differs.' }
    $platforms = @('win-x64','linux-x64') | ForEach-Object { Assert-PlatformArchive (Join-Path $Directory (Get-ArchiveName $Version $_)) $_ $Version $Commit $RunId }
    foreach ($platform in $platforms) {
        $record = @($manifest.platforms | Where-Object { $_.rid -ceq $platform.rid })
        if ($record.Count -ne 1) { throw 'Manifest platform set differs from archives.' }
        foreach ($field in @('commit','runId','version','sdk','binarySha256','result','dirty')) {
            if ($record[0][$field] -cne $platform[$field]) { throw "Manifest platform identity differs: $($platform.rid) $field" }
        }
    }
    Assert-Qualification $Qualification $Commit $platforms
    if ($manifest.qualificationSha256 -cne (Get-ReleaseHash $Qualification)) { throw 'Qualification changed since assembly.' }
    $names = @($manifest.assets.name | Sort-Object)
    if (Compare-Object $names @($expected | Where-Object { $_ -notin @('manifest.json','SHA256SUMS') })) { throw 'Manifest asset set differs.' }
    foreach ($asset in $manifest.assets) { if ($asset.sha256 -cne (Get-ReleaseHash (Join-Path $Directory $asset.name))) { throw "Asset checksum differs: $($asset.name)" } }
    $sums = @(Get-Content "$Directory/SHA256SUMS")
    $expectedSums = @($expected | Where-Object { $_ -ne 'SHA256SUMS' } | ForEach-Object { "$(Get-ReleaseHash (Join-Path $Directory $_))  $_" })
    if ($sums.Count -ne $expectedSums.Count -or (Compare-Object $sums $expectedSums)) { throw 'SHA256SUMS does not match the exact release set.' }
}
function Invoke-ReleaseGh([string]$Gh, [string[]]$Arguments) {
    $text = & $Gh @Arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "GitHub CLI failed: $text" }
    return $text
}
function Publish-ReleaseSet([string]$Gh, [string]$Repository, [string]$Tag, [string]$Commit, [string]$Directory, [string]$Version) {
    # Listing distinguishes a missing version from authentication/network errors (which fail closed).
    $releases = Invoke-ReleaseGh $Gh @('release','list','--repo',$Repository,'--limit','1000','--json','tagName,isDraft') | ConvertFrom-Json
    $existing = @($releases | Where-Object tagName -CEQ $Tag)
    if ($existing.Count -gt 1 -or ($existing.Count -eq 1 -and !$existing[0].isDraft)) { throw 'Version is already published; assets will not be overwritten.' }
    if ($existing.Count -eq 0) {
        $args = @('release','create',$Tag,'--repo',$Repository,'--verify-tag','--target',$Commit,'--draft','--title',$Tag,'--notes-file',"$Directory/RELEASE-NOTES.md")
        if ($Version.Contains('-')) { $args += '--prerelease' }
        Invoke-ReleaseGh $Gh $args | Out-Null
    }
    $release = Invoke-ReleaseGh $Gh @('release','view',$Tag,'--repo',$Repository,'--json','isDraft,isPrerelease,tagName,targetCommitish,assets') | ConvertFrom-Json
    if (!$release.isDraft -or $release.tagName -cne $Tag -or $release.targetCommitish -cne $Commit) { throw 'Existing draft tag/commit identity differs.' }
    $expected = @(Get-ReleaseAssetNames $Version)
    if (@($release.assets | Where-Object name -CNotIn $expected).Count) { throw 'Existing draft contains unexpected assets.' }
    $download = Join-Path ([IO.Path]::GetTempPath()) ('merstride-draft-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $download | Out-Null
    try {
        if ($release.assets.Count) {
            Invoke-ReleaseGh $Gh @('release','download',$Tag,'--repo',$Repository,'--dir',$download) | Out-Null
            foreach ($asset in $release.assets) {
                if ((Get-ReleaseHash (Join-Path $download $asset.name)) -cne (Get-ReleaseHash (Join-Path $Directory $asset.name))) { throw 'Matching draft asset checksum differs; refusing overwrite.' }
            }
        }
        foreach ($name in $expected) {
            if ($name -cnotin $release.assets.name) { Invoke-ReleaseGh $Gh @('release','upload',$Tag,(Join-Path $Directory $name),'--repo',$Repository) | Out-Null }
        }
        # Verify uploaded bytes before making the draft public, including resumed drafts.
        $verify = Join-Path $download 'verify'; New-Item -ItemType Directory $verify | Out-Null
        Invoke-ReleaseGh $Gh @('release','download',$Tag,'--repo',$Repository,'--dir',$verify) | Out-Null
        if (Compare-Object @((Get-ChildItem $verify -File).Name | Sort-Object) @($expected | Sort-Object)) { throw 'Remote draft asset set differs.' }
        foreach ($name in $expected) { if ((Get-ReleaseHash "$verify/$name") -cne (Get-ReleaseHash "$Directory/$name")) { throw 'Uploaded draft checksum differs.' } }
        $prerelease = $Version.Contains('-').ToString().ToLowerInvariant()
        Invoke-ReleaseGh $Gh @('release','edit',$Tag,'--repo',$Repository,('--prerelease='+$prerelease),'--draft=false') | Out-Null
    } finally {
        if ([IO.Path]::GetFullPath($download).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) -and (Split-Path $download -Leaf) -like 'merstride-draft-*') { Remove-Item -LiteralPath $download -Recurse -Force }
    }
}
