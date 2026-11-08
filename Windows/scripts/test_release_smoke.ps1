param([Parameter(Mandatory)][string]$Payload, [string]$OutputRoot, [switch]$Headless)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts\p4\release-smoke' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw 'Smoke output already exists; use a fresh output directory.' }
New-Item -Path $OutputRoot -ItemType Directory | Out-Null
$copy = Join-Path $OutputRoot '程序 中文路径'
Copy-Item -LiteralPath ([IO.Path]::GetFullPath($Payload)) -Destination $copy -Recurse
$data = Join-Path $OutputRoot '隔离 用户资料'
New-Item -Path $data -ItemType Directory | Out-Null
$doctorJson = Join-Path $OutputRoot '基础自检.json'
$startupJson = Join-Path $data 'startup.json'
function Run-Contained([string]$File, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($File)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.Environment['NEXTTHOUGHT_DATA_DIR'] = $data
    $start.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
    $start.Environment['DOTNET_ROOT'] = Join-Path $OutputRoot 'no-global-dotnet'
    $start.Environment['DOTNET_ROOT_X64'] = Join-Path $OutputRoot 'no-global-dotnet'
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) { $process.Kill($true); $process.WaitForExit(); throw 'Owned smoke process exceeded 30 seconds.' }
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $OutputRoot ([IO.Path]::GetFileName($File) + '.stdout.txt')) -Encoding utf8
    $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $OutputRoot ([IO.Path]::GetFileName($File) + '.stderr.txt')) -Encoding utf8
    return $process.ExitCode
}
$doctorArgs = @('doctor', '--audio', (Join-Path $copy 'tools\validation\public-sample.wav'), '--json', $doctorJson)
if ($Headless) { $doctorArgs += @('--headless','true') }
$doctorExit = Run-Contained (Join-Path $copy 'tools\NextThought.Cli.exe') $doctorArgs
if ($doctorExit -ne 0) { @{passed=$false; stage='doctor'; exitCode=$doctorExit} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'smoke.json'); throw ('Contained doctor failed: ' + $doctorExit) }
$startupExit = Run-Contained (Join-Path $copy 'NextThought.exe') @('--startup-check', $startupJson)
if ($startupExit -ne 0 -or -not (Test-Path -LiteralPath $startupJson)) { @{passed=$false; stage='WPF startup'; exitCode=$startupExit} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'smoke.json'); throw ('Contained WPF startup failed: ' + $startupExit) }
$doctor = Get-Content -LiteralPath $doctorJson -Raw | ConvertFrom-Json
$startup = Get-Content -LiteralPath $startupJson -Raw | ConvertFrom-Json
$passed = $doctor.passed -and $startup.passed -and $doctor.externalDotnetRoot -and -not $startup.recording
[pscustomobject]@{
    testedAt=[DateTimeOffset]::Now.ToString('o'); passed=$passed; isolatedPath=$true; sdkRemovedFromChildPath=$true; externalRuntimeRootsPointToMissingDirectory=$true
    scope='Self-contained process isolation on this host, not a clean OS or interactive desktop layout test'
    doctorExit=$doctorExit; startupExit=$startupExit; asrReady=$doctor.asrReady; asrPassed=$doctor.asrPassed; startup=$startup
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'smoke.json') -Encoding utf8
if (-not $passed) { throw 'Self-contained smoke checks failed.' }
Write-Output 'Self-contained CLI and off-screen WPF startup: PASS'
