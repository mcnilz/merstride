param([string]$Project = "$PSScriptRoot/../src/Merstride")
$ErrorActionPreference = 'Stop'
try {
    $projectDir = (Resolve-Path -LiteralPath $Project).Path
    $config = Join-Path $projectDir 'config.nsdepcop'
    if (!(Test-Path -LiteralPath $config)) { throw 'Missing NsDepCop configuration.' }
    [xml]$rules = Get-Content -Raw -LiteralPath $config
    if ($rules.NsDepCopConfig.IsEnabled -ne 'true' -or $rules.NsDepCopConfig.CheckAssemblyDependencies -ne 'true') { throw 'Architecture analysis disabled.' }
    if ($rules.NsDepCopConfig.ExcludedFiles -or $rules.NsDepCopConfig.InheritanceDepth -gt 0) { throw 'Unreviewed analyzer exclusions or inherited rules.' }
    foreach ($rule in $rules.NsDepCopConfig.Allowed) {
        if ($rule.From -eq '*' -or $rule.To -eq '*') { throw 'Broad allow-all namespace rule.' }
    }
    foreach ($area in @('Domain','Application','Compilation','Infrastructure')) {
        if (!($rules.NsDepCopConfig.Disallowed | Where-Object { $_.From -ceq "Merstride.$area.*" -and $_.To -ceq 'System.CommandLine.*' })) { throw "CLI parser boundary missing for $area." }
    }
    if ($env:DisableNsDepCop -in @('1','true','True')) { throw 'DisableNsDepCop environment variable disables analysis.' }
    $projectFile = Get-ChildItem -LiteralPath $projectDir -Filter *.csproj | Select-Object -First 1
    [xml]$projectXml = Get-Content -Raw -LiteralPath $projectFile.FullName
    $analyzer = $projectXml.Project.ItemGroup.PackageReference | Where-Object Include -eq 'NsDepCop'
    if (!$analyzer -or $analyzer.Version -ne '3.3.0' -or $analyzer.PrivateAssets -ne 'all' -or $analyzer.ExcludeAssets -ne 'compile;runtime') { throw 'Missing or ineffective build-only analyzer reference.' }
    $evaluated = & dotnet msbuild $projectFile.FullName -nologo '-getProperty:RunAnalyzers,RunAnalyzersDuringBuild,DisableNsDepCop,NoWarn,WarningsAsErrors,TreatWarningsAsErrors' | Out-String
    if ($LASTEXITCODE -ne 0) { throw 'Cannot evaluate analyzer properties.' }
    $properties = ($evaluated | ConvertFrom-Json).Properties
    if ($properties.RunAnalyzers -eq 'false' -or $properties.RunAnalyzersDuringBuild -eq 'false' -or $properties.DisableNsDepCop -in @('true','1')) { throw 'Architecture analysis disabled by build properties.' }
    if ($properties.NoWarn -match '(?i)NSDEPCOP|(^|;)all(;|$)') { throw 'Illegal dependencies suppressed globally.' }
    if ($properties.TreatWarningsAsErrors -ne 'true' -and ($properties.WarningsAsErrors -notmatch 'NSDEPCOP01' -or $properties.WarningsAsErrors -notmatch 'NSDEPCOP07')) { throw 'Architecture violations are not build errors.' }
    $dir = Get-Item -LiteralPath $projectDir
    while ($null -ne $dir) {
        foreach ($name in @('.editorconfig','Directory.Build.props','Directory.Build.targets')) {
            $path = Join-Path $dir.FullName $name
            if (Test-Path -LiteralPath $path) {
                $text = Get-Content -Raw -LiteralPath $path
                if ($text -match '(?im)NSDEPCOP\d+\.severity\s*=\s*(none|silent|suggestion|warning)' -or
                    $text -match '(?im)dotnet_analyzer_diagnostic.*severity\s*=\s*(none|silent)' -or
                    $text -match '(?is)<NoWarn>[^<]*NSDEPCOP') { throw "Illegal dependencies suppressed globally in $path." }
            }
        }
        $dir = $dir.Parent
    }
    Write-Output 'ARCHITECTURE: effective build-only NsDepCop configuration.'
} catch { Write-Error "ARCHITECTURE: $($_.Exception.Message)"; exit 1 }
