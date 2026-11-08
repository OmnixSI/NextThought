param([string]$RunName = 'build-v2')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($RunName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid QA run name.' }
$qa = Join-Path $root ('artifacts\p4\installer-qa\' + $RunName)
if (Test-Path -LiteralPath $qa) { throw 'QA fixture directory already exists; preserve it and choose a new run directory.' }
New-Item -Path $qa -ItemType Directory | Out-Null
$baseline = Join-Path $qa 'legacy-payload'
Copy-Item -LiteralPath (Join-Path $root 'artifacts\NextThought-win-x64-0.3.1') -Destination $baseline -Recurse
# Keep the original desktop binary, but add the new deployment helper in its separate tools directory.
Copy-Item -Path (Join-Path $root 'artifacts\p4\deployment-tools\*') -Destination (Join-Path $baseline 'tools') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'scripts\Run-Validation.cmd') -Destination $baseline
Copy-Item -LiteralPath (Join-Path $root 'artifacts\NextThought-win-x64-0.4.0\tools\validation') -Destination (Join-Path $baseline 'tools') -Recurse
& py -3.12 (Join-Path $root 'scripts\write_release_manifest.py') --directory $baseline --version 0.3.1 --signing unsigned-preview
if ($LASTEXITCODE -ne 0) { throw 'Legacy QA manifest failed.' }
$candidate = Join-Path $qa 'candidate-payload'
Copy-Item -LiteralPath (Join-Path $root 'artifacts\NextThought-win-x64-0.4.0') -Destination $candidate -Recurse
Copy-Item -Path (Join-Path $root 'artifacts\p4\deployment-tools\*') -Destination (Join-Path $candidate 'tools') -Recurse -Force
& py -3.12 (Join-Path $root 'scripts\write_release_manifest.py') --directory $candidate --version 0.4.0 --signing unsigned-preview
if ($LASTEXITCODE -ne 0) { throw 'Candidate QA manifest failed.' }
$compiler = Join-Path $root 'scripts\build_installer.ps1'
foreach ($case in @(
    @{Payload=$baseline; Version='0.3.1'; Name='QA-baseline-0.3.1'; Fault=$false},
    @{Payload=$candidate; Version='0.4.0'; Name='QA-upgrade-0.4.0'; Fault=$false},
    @{Payload=$candidate; Version='0.4.0'; Name='QA-failure-0.4.0'; Fault=$true}
)) {
    & $compiler -Payload $case.Payload -Version $case.Version -Unsigned -InstallId NextThought.Windows.Preview.QA20261108 -ShortcutGroup 'NextThought P4 QA 20261108' -OutputDirectory $qa -OutputName $case.Name -FailBeforeActivation:$case.Fault
}
Write-Output 'QA installers prepared; these fixtures are not release packages.'
