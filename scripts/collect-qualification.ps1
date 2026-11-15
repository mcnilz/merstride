param([string]$Output = "$PSScriptRoot/../artifacts/qualification/input.json")
$ErrorActionPreference = 'Stop'
# Reports are external data, never script text. Keeping them outside the commit
# avoids a circular requirement to embed a commit's own SHA in a tracked file.
foreach ($name in @('WINDOWS_QUALIFICATION_JSON','LINUX_QUALIFICATION_JSON','TOKEN_EXPERIMENT_JSON')) {
    if (![Environment]::GetEnvironmentVariable($name)) { throw "Required external release evidence unavailable: $name" }
}
$report = [ordered]@{
    schemaVersion=1;commit=$env:GITHUB_SHA
    platforms=@{
        'win-x64'=($env:WINDOWS_QUALIFICATION_JSON | ConvertFrom-Json -AsHashtable)
        'linux-x64'=($env:LINUX_QUALIFICATION_JSON | ConvertFrom-Json -AsHashtable)
    }
    tokenExperiment=($env:TOKEN_EXPERIMENT_JSON | ConvertFrom-Json -AsHashtable)
}
New-Item -ItemType Directory -Force (Split-Path ([IO.Path]::GetFullPath($Output))) | Out-Null
$report | ConvertTo-Json -Depth 25 | Set-Content $Output
