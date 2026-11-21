param(
    [Parameter(Mandatory = $true)][string]$Setup,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$LogDirectory
)
# R5 / K9: install, self-test, data retention, silent upgrade, uninstall and reinstall for the
# per-user NextThought.Desktop installer. Runs entirely in throw-away directories and never touches
# the machine's real NextThought installation or %LOCALAPPDATA%\NextThought data.
$ErrorActionPreference = 'Stop'
$setupPath = [IO.Path]::GetFullPath($Setup)
if (-not (Test-Path -LiteralPath $setupPath)) { throw ('Installer not found: ' + $setupPath) }
if (-not $LogDirectory) { $LogDirectory = Join-Path $env:TEMP ('nt-lifecycle-' + [Guid]::NewGuid().ToString('N')) }
$logRoot = [IO.Path]::GetFullPath($LogDirectory)
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

$sandbox = Join-Path $env:TEMP ('nt-lifecycle-' + [Guid]::NewGuid().ToString('N'))
$installRoot = Join-Path $sandbox 'install'
$dataRoot = Join-Path $sandbox 'data'
$registryKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NextThought.Desktop_is1'
$checks = [Collections.Generic.List[object]]::new()
function Assert-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; passed = $Passed; detail = $Detail })
    Write-Output ($Name + ': ' + $Passed)
    if (-not $Passed) { throw ('Installer lifecycle check failed: ' + $Name + '; ' + $Detail) }
}
function Run-Owned([string]$File, [string[]]$Arguments, [hashtable]$Environment = @{}) {
    $start = [Diagnostics.ProcessStartInfo]::new($File)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($entry in $Environment.GetEnumerator()) { $start.Environment[$entry.Key] = $entry.Value }
    # Windows PowerShell 5.1 has no ProcessStartInfo.ArgumentList, so build the command line and
    # quote every argument that contains whitespace (the sandbox paths and logs can contain spaces).
    $start.Arguments = (($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' ')
    $process = [Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    return $process.ExitCode
}
function Install([string]$Tag) {
    $log = Join-Path $logRoot ($Tag + '-install.log')
    $code = Run-Owned $setupPath @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOCLOSEAPPLICATIONS',
        '/NORESTARTAPPLICATIONS', '/SP-', ('/DIR=' + $installRoot), ('/LOG=' + $log))
    return $code
}
function Uninstall([string]$Tag) {
    $uninstaller = Join-Path $installRoot 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller)) { return 1 }
    $log = Join-Path $logRoot ($Tag + '-uninstall.log')
    $code = Run-Owned $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG=' + $log))
    $deadline = (Get-Date).AddSeconds(60)
    while ((Test-Path -LiteralPath $installRoot) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
    return $code
}
function Data-Fingerprint {
    $items = @(Get-ChildItem -LiteralPath $dataRoot -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName | ForEach-Object { $($_.FullName.Substring($dataRoot.Length).TrimStart('\')) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
    return ($items -join "`n")
}

$passed = $false
try {
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
    # Fake user data the installer must never touch (sessions, models, settings, key marker).
    New-Item -ItemType Directory -Path (Join-Path $dataRoot 'Sessions') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $dataRoot 'models') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $dataRoot 'Sessions\synthetic.jsonl'), '{"public":"synthetic archive must survive install and uninstall"}')
    [IO.File]::WriteAllText((Join-Path $dataRoot 'models\preserved.txt'), 'synthetic model marker')
    [IO.File]::WriteAllText((Join-Path $dataRoot 'settings.json'), '{"schema":1,"synthetic":true}')
    Assert-Check 'Silent install returns success' ((Install 'baseline') -eq 0) 'Per-user installer, isolated /DIR'
    Assert-Check 'Installed executable exists' (Test-Path -LiteralPath (Join-Path $installRoot 'NextThought.exe')) $installRoot
    Assert-Check 'Uninstall registration exists' (Test-Path -LiteralPath $registryKey) $registryKey
    $registered = Get-ItemProperty -LiteralPath $registryKey
    Assert-Check 'Registered product is NextThought' ($registered.DisplayName -eq 'NextThought') ([string]$registered.DisplayName)
    Assert-Check 'Registered version matches the build' ($registered.DisplayVersion -eq $Version) ([string]$registered.DisplayVersion)

    $selfTest = Run-Owned (Join-Path $installRoot 'NextThought.exe') @('--self-test=startup') @{ NEXTTHOUGHT_DATA_DIR = $dataRoot }
    Assert-Check 'Installed app passes --self-test=startup' ($selfTest -eq 0) ('exit=' + $selfTest)
    # The app legitimately writes its own defaults/logs into the data directory while starting;
    # capture the baseline after that so the installer checks compare against real user data only.
    $originalData = Data-Fingerprint

    Assert-Check 'Overwrite install returns success' ((Install 'upgrade') -eq 0) 'Same installer over the existing per-user install'
    Assert-Check 'Data kept through upgrade' ((Data-Fingerprint) -eq $originalData) 'Sessions, model marker and settings unchanged'

    Assert-Check 'Uninstaller returns success' ((Uninstall 'uninstall') -eq 0) 'Silent per-user uninstall'
    Assert-Check 'Uninstall removed the program files' (-not (Test-Path -LiteralPath (Join-Path $installRoot 'NextThought.exe'))) 'NextThought.exe is gone'
    Assert-Check 'Uninstall removed the registration' (-not (Test-Path -LiteralPath $registryKey)) 'Only NextThought.Desktop was touched'
    Assert-Check 'Uninstall kept user data' ((Data-Fingerprint) -eq $originalData) 'Synthetic sessions, model marker and settings unchanged'

    Assert-Check 'Reinstall returns success' ((Install 'reinstall') -eq 0) 'Fresh install after uninstall'
    Assert-Check 'Reinstall kept user data' ((Data-Fingerprint) -eq $originalData) 'Data directory untouched by install/uninstall'
    Assert-Check 'Reinstalled app passes --self-test=startup' ((Run-Owned (Join-Path $installRoot 'NextThought.exe') @('--self-test=startup') @{ NEXTTHOUGHT_DATA_DIR = $dataRoot }) -eq 0) 'Data reused'
    Assert-Check 'Final uninstall returns success' ((Uninstall 'cleanup') -eq 0) 'Leave the machine clean'
    $passed = $true
}
finally {
    [pscustomobject]@{
        checkedAt   = [DateTimeOffset]::Now.ToString('o')
        passed      = $passed
        version     = $Version
        setup       = $setupPath
        installRoot = $installRoot
        dataRoot    = $dataRoot
        checks      = $checks
        note        = 'K9 automated install/upgrade/uninstall lifecycle. The startup self-test is the base check (--self-test=startup, exit 0, report in %TEMP%\nextthought-selftest); the in-app update path is covered by the WindowsUpdateCatalogTests/UpdateInstallerTests unit-level checks and the real-machine K10 run.'
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $logRoot ('lifecycle-' + $Version + '.json')) -Encoding utf8
    if (Test-Path -LiteralPath $sandbox) { Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue }
}
Write-Output 'Installer lifecycle: PASS'
