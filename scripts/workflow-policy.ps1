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
        foreach ($job in $workflow['jobs'].Values) {
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
                foreach ($value in $job['permissions'].Values) { if ($value -cne 'read' -and $value -cne 'none') { throw 'Unapproved workflow permission.' } }
            }
        }
        # P01 accepts only unprivileged build workflows. Release policy belongs to P07.
        $events = $workflow['on']
        $permissions = $workflow['permissions']
        $build = $workflow['jobs']['windows']
        $gate = $workflow['jobs']['gate']
        $gateCommand = 'if ($env:BUILD_RESULT -ne ''success'') { throw ''Required build did not succeed.'' }'
        if ($text -match 'secrets\s*[.\[]' -or $events.ContainsKey('pull_request_target') -or
            !$events.ContainsKey('pull_request') -or !$events.ContainsKey('workflow_dispatch') -or
            $events['push']['branches'].Count -ne 1 -or $events['push']['branches'][0] -cne 'main' -or
            $permissions -isnot [Collections.Generic.Dictionary[string,object]] -or $permissions.Count -ne 1 -or $permissions['contents'] -cne 'read' -or
            !$build -or $build.ContainsKey('if') -or $build['runs-on'] -cne 'windows-2025' -or
            @($build['steps'] | Where-Object { $_['run'] -ceq './scripts/build.ps1' -and !$_.ContainsKey('if') }).Count -ne 1 -or
            !$gate -or $gate['if'] -cne 'always()' -or $gate['needs'].Count -ne 1 -or $gate['needs'][0] -cne 'windows' -or
            $workflow['jobs'].Count -ne 2 -or $gate['steps'].Count -ne 1 -or
            $gate['steps'][0]['run'].Trim() -cne $gateCommand -or $gate['steps'][0].ContainsKey('if') -or
            $gate['steps'][0]['env']['BUILD_RESULT'] -cne '${{ needs.windows.result }}') {
            throw "Unsafe or incomplete P01 build workflow: $($file.Name)"
        }
    }
    Write-Output 'WORKFLOW: official verified action references and read-only fork build policy passed.'
} catch { Write-Error "WORKFLOW: $($_.Exception.Message)"; exit 1 }
