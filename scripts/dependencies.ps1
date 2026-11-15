param([string]$Repository = "$PSScriptRoot/..", [string]$Inventory = "$PSScriptRoot/../docs/dependency-inventory.json")
$ErrorActionPreference = 'Stop'
try {
    $reviews = Get-Content -Raw -LiteralPath $Inventory | ConvertFrom-Json -AsHashtable
    $locks = @(Get-ChildItem "$Repository/src","$Repository/tests" -Recurse -Filter 'packages*.lock.json' -ErrorAction SilentlyContinue)
    if (!$locks.Count) { throw 'No dependency lock files found.' }
    foreach ($lock in $locks) {
        $graph = (Get-Content -Raw -LiteralPath $lock.FullName | ConvertFrom-Json -AsHashtable).dependencies
        foreach ($framework in $graph.Values) {
            foreach ($id in $framework.Keys) {
                $package = $framework[$id]
                $key = "$id/$($package.resolved)"
                $review = $reviews[$key]
                foreach ($field in @('upstream','license','purpose','classification','maintenanceEvidence','reviewedOn','provenance','aotEvidence')) {
                    if (!$review -or !$review[$field]) { throw "Missing dependency review evidence: $key ($field)" }
                }
                if ($review.contentHash -cne $package.contentHash) { throw "Dependency hash differs from reviewed provenance: $key" }
                if ($review.license -ne 'MIT' -and !($id -eq 'NsDepCop' -and $review.license -eq 'GPL-2.0-only' -and $review.classification -eq 'build-only')) { throw "Unapproved dependency license: $key" }
                if ($id -eq 'NsDepCop' -and $lock.FullName -match '[\\/]src[\\/]') {
                    [xml]$project = Get-Content -Raw (Join-Path $lock.DirectoryName 'Merstride.csproj')
                    $reference = $project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'NsDepCop'
                    if ($reference.PrivateAssets -ne 'all' -or $reference.ExcludeAssets -ne 'compile;runtime') { throw 'NsDepCop must remain build-only.' }
                }
            }
        }
    }
    Write-Output 'DEPENDENCIES: all locked direct and transitive packages have reviewed provenance and licenses.'
} catch { [Console]::Error.WriteLine("DEPENDENCIES: $($_.Exception.Message)"); exit 1 }
