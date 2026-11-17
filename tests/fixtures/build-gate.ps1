param(
    [Parameter(Mandatory)][string]$Windows,
    [Parameter(Mandatory)][string]$Linux,
    [Parameter(Mandatory)][int]$ExpectedExit
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../../scripts/read-yaml.ps1"
$workflow = Read-WorkflowYaml (Get-Content -Raw "$PSScriptRoot/../../.github/workflows/build.yml")
$step = $workflow['jobs']['gate']['steps'][0]
$env:BUILD_RESULT=$Windows
$env:LINUX_RESULT=$Linux
& pwsh -NoProfile -Command $step['run']
if ($LASTEXITCODE -ne $ExpectedExit) { throw "Build gate returned $LASTEXITCODE; expected $ExpectedExit." }
