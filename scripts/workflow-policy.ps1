param([string]$WorkflowDirectory = "$PSScriptRoot/../.github/workflows")
$ErrorActionPreference = 'Stop'
try {
    . "$PSScriptRoot/read-yaml.ps1"
    $pins = (Get-Content -Raw "$PSScriptRoot/../.github/action-pins.json" | ConvertFrom-Json -AsHashtable).actions
    $files = @(Get-ChildItem -LiteralPath $WorkflowDirectory -File | Where-Object Extension -in @('.yml','.yaml'))
    if (!$files.Count) { throw 'No workflow files found.' }
    foreach ($file in $files) {
        $text = Get-Content -Raw -LiteralPath $file.FullName
        $workflow = Read-WorkflowYaml $text
        $release = $file.Name -ceq 'release.yml'
        foreach ($entry in $workflow['jobs'].GetEnumerator()) {
            $job = $entry.Value
            if ($job.ContainsKey('continue-on-error') -and $job['continue-on-error'] -cne 'false') { throw 'Required jobs cannot ignore failures.' }
            if ($job.ContainsKey('uses')) { throw "Unapproved reusable workflow: $($job['uses'])" }
            foreach ($step in $job['steps']) {
                if ($step.ContainsKey('continue-on-error') -and $step['continue-on-error'] -cne 'false') { throw 'Required steps cannot ignore failures.' }
                if (!$step.ContainsKey('uses')) { continue }
                $reference = $step['uses']
                $parts = $reference.Split('@')
                if ($parts.Count -ne 2 -or !$pins.ContainsKey($parts[0]) -or $parts[1] -cnotmatch '^[a-f0-9]{40}$' -or $pins[$parts[0]].sha -cne $parts[1]) {
                    throw "Unapproved action reference: $reference"
                }
                if ($parts[0] -eq 'actions/checkout' -and $step['with']['persist-credentials'] -cne 'false') { throw 'Checkout credentials must not persist.' }
            }
            if ($job.ContainsKey('permissions')) {
                if ($job['permissions'] -isnot [Collections.Generic.Dictionary[string,object]]) { throw 'Job permissions must be an explicit mapping.' }
                foreach ($permission in $job['permissions'].GetEnumerator()) {
                    if ($permission.Value -cne 'read' -and $permission.Value -cne 'none' -and !($release -and $entry.Key -ceq 'publish' -and $permission.Key -ceq 'contents' -and $permission.Value -ceq 'write')) { throw 'Unapproved workflow permission.' }
                }
            }
            if ($job.ContainsKey('if') -and !($entry.Key -ceq 'gate' -and $job['if'] -ceq 'always()')) { throw 'Required job cannot be conditional.' }
            foreach ($step in $job['steps']) {
                if ($step.ContainsKey('if') -and !($step['if'] -ceq 'always()' -and $step['uses'] -like 'actions/upload-artifact@*')) { throw 'Required step cannot be conditional.' }
            }
        }
        $events = $workflow['on']
        $permissions = $workflow['permissions']
        $build = $workflow['jobs']['windows']
        $gate = $workflow['jobs']['gate']
        $gateCommand = @('if ($env:BUILD_RESULT -ne ''success'') { throw ''Required build did not succeed.'' }', 'if ($env:LINUX_RESULT -ne ''success'') { throw ''Required Linux build did not succeed.'' }') -join "`n"
        if ($release) { $gateCommand = 'if ($env:TAG_RESULT -ne ''success'') { throw ''Required tag validation did not succeed.'' }' + "`n" + $gateCommand }
        $linux = $workflow['jobs']['linux']
        if ($text -match 'secrets\s*[.\[]' -or $events.ContainsKey('pull_request_target') -or
            $permissions -isnot [Collections.Generic.Dictionary[string,object]] -or $permissions.Count -ne 1 -or $permissions['contents'] -cne 'read' -or
            !$build -or $build.ContainsKey('if') -or $build['runs-on'] -cne 'windows-2025' -or
            !$linux -or $linux['runs-on'] -cne 'ubuntu-24.04' -or
            !$gate -or $gate['if'] -cne 'always()' -or $gate['steps'].Count -ne 1 -or
            $gate['steps'][0]['run'].Trim().Replace("`r",'') -cne $gateCommand -or $gate['steps'][0].ContainsKey('if') -or
            $gate['steps'][0]['env']['BUILD_RESULT'] -cne '${{ needs.windows.result }}' -or
            $gate['steps'][0]['env']['LINUX_RESULT'] -cne '${{ needs.linux.result }}') {
            throw "Unsafe or incomplete required workflow: $($file.Name)"
        }
        $expectedNeeds = if ($release) { @('tag','windows','linux') } else { @('windows','linux') }
        if (Compare-Object @($gate['needs'] | Sort-Object) @($expectedNeeds | Sort-Object)) { throw 'Aggregate gate must require every platform and tag job.' }
        foreach ($job in @($build,$linux)) {
            $command = if ($release) { './scripts/build.ps1 -Version $env:RELEASE_VERSION' } else { './scripts/build.ps1' }
            if (@($job['steps'] | Where-Object { $_['run'] -ceq $command }).Count -ne 1) { throw 'Missing required full build command.' }
        }
        if (!$release) {
            if ($file.Name -cne 'build.yml' -or !$events.ContainsKey('pull_request') -or !$events.ContainsKey('workflow_dispatch') -or
                $events['push']['branches'].Count -ne 1 -or $events['push']['branches'][0] -cne 'main' -or
                $workflow['jobs'].Count -ne 3 -or $text -match 'GH_TOKEN|github.token|gh release') { throw 'Unsafe read-only contribution workflow.' }
        } else {
            $jobs = $workflow['jobs']
            $publish = $jobs['publish']; $verify = $jobs['verify']
            $verifyCommand = @('New-Item -ItemType Directory -Force artifacts/release | Out-Null',
                'Copy-Item artifacts/download/windows/release/*.zip artifacts/release/',
                'Copy-Item artifacts/download/linux/release/*.tar.gz artifacts/release/',
                './scripts/collect-qualification.ps1',
                './scripts/release.ps1 -Mode Assemble -Qualification artifacts/qualification/input.json') -join "`n"
            if ($jobs.Count -ne 6 -or $events.Count -ne 1 -or $events['push']['tags'].Count -ne 1 -or $events['push']['tags'][0] -cne 'v*' -or
                $workflow['concurrency']['cancel-in-progress'] -cne 'false' -or $workflow['concurrency']['group'] -cne 'release-${{ github.ref }}' -or
                $publish['permissions'].Count -ne 1 -or $publish['permissions']['contents'] -cne 'write' -or
                (Compare-Object @($publish['needs'] | Sort-Object) @('gate','verify')) -or
                $verify['needs'].Count -ne 1 -or $verify['needs'][0] -cne 'gate' -or
                $gate['steps'][0]['env']['TAG_RESULT'] -cne '${{ needs.tag.result }}') { throw 'Unsafe release publication graph or privileges.' }
            foreach ($platform in @('windows','linux')) {
                if ($jobs[$platform]['needs'].Count -ne 1 -or $jobs[$platform]['needs'][0] -cne 'tag' -or
                    $jobs[$platform]['env']['RELEASE_VERSION'] -cne '${{ needs.tag.outputs.version }}') { throw 'Platform must build the validated tag version.' }
                $rid = if ($platform -eq 'windows') { 'win-x64' } else { 'linux-x64' }
                if (@($jobs[$platform]['steps'] | Where-Object { $_['run'] -ceq "./scripts/release.ps1 -Mode Package -Runtime $rid" }).Count -ne 1) { throw 'Missing verified platform packaging.' }
            }
            if (@($jobs['tag']['steps'] | Where-Object { $_['run'] -ceq './scripts/release.ps1 -Mode Tag' -and $_['id'] -ceq 'tag' }).Count -ne 1 -or
                $jobs['tag']['outputs']['version'] -cne '${{ steps.tag.outputs.version }}' -or
                @($verify['steps'] | Where-Object { $_['run'] -and $_['run'].Trim().Replace("`r",'') -ceq $verifyCommand }).Count -ne 1 -or
                @($publish['steps'] | Where-Object { $_['run'] -ceq './scripts/release.ps1 -Mode Publish' -and $_['env']['GH_TOKEN'] -ceq '${{ github.token }}' }).Count -ne 1) { throw 'Missing release identity/verification/publication boundary.' }
            foreach ($entry in $jobs.GetEnumerator()) {
                if ($entry.Key -cne 'publish' -and $entry.Value['env'] -and $entry.Value['env']['GH_TOKEN']) { throw 'Publication token is restricted to final publish step.' }
                foreach ($step in $entry.Value['steps']) {
                    if ($entry.Key -cne 'publish' -and (($step['env'] -and $step['env']['GH_TOKEN']) -or ($step['run'] -match 'gh release|github.token'))) { throw 'Publication token is restricted to final publish step.' }
                    if ($step['uses'] -like 'actions/download-artifact@*' -and ($step['with']['run-id'] -or $step['with']['github-token'] -or $step['with']['repository'])) { throw 'Release downloads must come from the same workflow run.' }
                }
            }
        }
    }
    Write-Output 'WORKFLOW: official verified action references and read-only fork build policy passed.'
} catch { [Console]::Error.WriteLine("WORKFLOW: $($_.Exception.Message)"); exit 1 }
