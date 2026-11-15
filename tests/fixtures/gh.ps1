$ErrorActionPreference = 'Stop'
$state = Get-Content -Raw $env:MERSTRIDE_GH_FIXTURE | ConvertFrom-Json -AsHashtable
$command = $args[1]
$state.calls += "$command " + ($args -join ' ')
switch ($command) {
    'list' { if ($state.exists) { @(@{tagName=$state.tag;isDraft=$state.isDraft}) | ConvertTo-Json -AsArray -Compress } else { '[]' } }
    'create' { if ($state.exists) { exit 1 }; $state.exists=$true; $state.isDraft=$true }
    'view' { @{isDraft=$state.isDraft;isPrerelease=$state.isPrerelease;tagName=$state.tag;targetCommitish=$state.commit;assets=@($state.assets.Keys | ForEach-Object { @{name=$_} })} | ConvertTo-Json -Depth 10 -Compress }
    'upload' {
        $name = Split-Path $args[3] -Leaf
        if ($state.assets.ContainsKey($name)) { exit 1 }
        $state.assets[$name]=[Convert]::ToBase64String([IO.File]::ReadAllBytes($args[3]))
    }
    'download' {
        $directory = $args[[Array]::IndexOf($args,'--dir')+1]
        foreach ($name in $state.assets.Keys) { [IO.File]::WriteAllBytes((Join-Path $directory $name),[Convert]::FromBase64String($state.assets[$name])) }
    }
    'edit' {
        $prerelease = @($args | Where-Object { $_ -like '--prerelease=*' })
        if ($prerelease.Count -ne 1) { exit 1 }
        $state.isPrerelease=$prerelease[0] -ceq '--prerelease=true'
        $state.public=$true; $state.isDraft=$false
    }
    default { exit 1 }
}
$state | ConvertTo-Json -Depth 10 | Set-Content $env:MERSTRIDE_GH_FIXTURE
exit 0
