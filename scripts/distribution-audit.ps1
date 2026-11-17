param(
    [string]$NativeDirectory = "$PSScriptRoot/../artifacts/native",
    [string]$AuditDirectory = "$PSScriptRoot/../artifacts/audit",
    [string]$Assets = "$PSScriptRoot/../src/Merstride/obj/project.assets.json"
)
$ErrorActionPreference = 'Stop'
try {
    $graph = Get-Content -Raw $Assets | ConvertFrom-Json -AsHashtable
    $analyzer = $graph.libraries['NsDepCop/3.3.0']
    if (!$analyzer) { throw 'Expected pinned analyzer is missing from build assets.' }
    $forbidden = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending = [Collections.Generic.Queue[string]]::new(); $pending.Enqueue('NsDepCop')
    # Traverse every resolved target: analyzer-only transitive implementation is forbidden too.
    while ($pending.Count) {
        $id = $pending.Dequeue()
        if (!$forbidden.Add($id)) { continue }
        foreach ($target in $graph.targets.Values) {
            foreach ($key in @($target.Keys | Where-Object { $_.Split('/')[0] -eq $id })) {
                $asset = $target[$key]
                foreach ($kind in @('compile','runtime','native','runtimeTargets')) {
                    foreach ($path in $asset[$kind].Keys) {
                        if ($path -notmatch '(^|/)_\._$') { throw "Analyzer runtime/compile asset: $key $path" }
                    }
                }
                foreach ($dependency in $asset.dependencies.Keys) { $pending.Enqueue($dependency) }
            }
        }
    }
    $records = [ordered]@{}
    foreach ($kind in @('compiler','ilc','link','publish')) {
        $path = Join-Path $AuditDirectory "$kind.txt"
        $lines = @(Get-Content -LiteralPath $path)
        if (!$lines.Count) { throw "Missing actual $kind input evidence." }
        foreach ($line in $lines) {
            foreach ($id in $forbidden) { if ($line -match [regex]::Escape($id)) { throw "Analyzer entered $kind inputs: $line" } }
        }
        $records[$kind] = @{ count = $lines.Count; sha256 = (Get-FileHash $path).Hash.ToLowerInvariant() }
    }
    if ((Get-Content -Raw (Join-Path $AuditDirectory 'analyzers.txt')) -notmatch 'nsdepcop') { throw 'Analyzer was not observed in compiler tooling.' }
    $binaryName = if ($IsWindows) { 'merstride.exe' } else { 'merstride' }
    $files = @(Get-ChildItem -LiteralPath $NativeDirectory -Recurse -File)
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($NativeDirectory), $file.FullName).Replace('\','/')
        $allowed = @($binaryName,'merstride.pdb','merstride.dbg')
        $macSymbol = $IsMacOS -and $relative -cin @('merstride.dSYM/Contents/Info.plist',
            'merstride.dSYM/Contents/Resources/DWARF/merstride',
            'merstride.dSYM/Contents/Resources/Relocations/aarch64/merstride.yml')
        if ($relative -cnotin $allowed -and !$macSymbol) { throw "Unexpected native publish content: $relative" }
        $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($file.FullName))
        foreach ($id in $forbidden) { if ($text -match [regex]::Escape($id)) { throw "Analyzer identity in native content: $($file.Name)" } }
    }
    foreach ($file in @('LICENSE','docs/licenses/dotnet-LICENSE.txt','docs/licenses/dotnet-THIRD-PARTY-NOTICES.txt','docs/licenses/YamlDotNet-LICENSE.txt','docs/licenses/System.CommandLine-LICENSE.txt','docs/shipped-dependencies.json')) {
        if (!(Test-Path "$PSScriptRoot/../$file")) { throw "Missing distribution license/inventory: $file" }
    }
    $runtimeKeys = @($graph.libraries.Keys | Where-Object { $_ -match '^runtime\.(win-x64|linux-x64|osx-arm64)\.Microsoft\.DotNet\.ILCompiler/11\.0\.0$' })
    if ($runtimeKeys.Count -ne 1) { throw 'Expected exactly one native platform runtime notice source.' }
    $packageRoot = @($graph.packageFolders.Keys)[0]
    $runtimeNotice = Join-Path $packageRoot ($graph.libraries[$runtimeKeys[0]].path + '/THIRD-PARTY-NOTICES.TXT')
    $upstreamNotice = [IO.File]::ReadAllText($runtimeNotice).Replace("`r`n","`n")
    $shippedNotice = [IO.File]::ReadAllText("$PSScriptRoot/../docs/licenses/dotnet-THIRD-PARTY-NOTICES.txt").Replace("`r`n","`n")
    if ($upstreamNotice -cne $shippedNotice) { throw 'Shipped runtime notice is incomplete or differs from the resolved platform pack.' }
    [ordered]@{ schemaVersion=1; analyzer='NsDepCop/3.3.0'; analyzerOnlyDependencies=@($forbidden); inputs=$records;
        nativeFiles=@($files | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()} });
        result='passed'; runtimeNotice='Complete retained notice equals the resolved platform pack after line-ending normalization.';
        scope='Resolved assets, compiler/source/reference, ILC, native linker and publish item sets, and native contents. Separate build-tool inventory; notices retained.'
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $AuditDirectory 'distribution.json')
    Write-Output 'DISTRIBUTION: analyzer separation and permissive runtime notice inventory passed.'
} catch { Write-Error "DISTRIBUTION: $($_.Exception.Message)"; exit 1 }
