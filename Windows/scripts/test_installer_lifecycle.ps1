param([string]$RunName = 'fixed-v2', [string]$PackageRun = 'build-v2')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qa = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\p4\installer-qa'))
if ($RunName -notmatch '^[A-Za-z0-9_-]+$' -or $PackageRun -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid QA run name.' }
$packageDirectory = Join-Path $qa $PackageRun
$installRoot = [IO.Path]::GetFullPath((Join-Path $qa ('安装 程序-' + $RunName)))
$dataRoot = [IO.Path]::GetFullPath((Join-Path $qa ('保留 用户资料-' + $RunName)))
if (-not $installRoot.StartsWith($qa + '\', [StringComparison]::OrdinalIgnoreCase) -or -not $dataRoot.StartsWith($qa + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe QA paths.' }
if (Test-Path -LiteralPath $installRoot) { throw 'QA installation already exists; do not overwrite evidence.' }
$keyName = 'NextThought.Windows.Preview.QA20261108_is1'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $keyName
if (Test-Path -LiteralPath $key) { throw 'QA installation registry identity already exists.' }
$group = 'NextThought P4 QA 20261108'
$shortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($group + '.lnk')
if (Test-Path -LiteralPath $shortcut) { throw 'QA shortcut already exists.' }
New-Item -Path (Join-Path $dataRoot 'Sessions') -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $dataRoot 'models') -ItemType Directory -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $dataRoot 'Sessions\synthetic.jsonl'), '{"public":"synthetic archive must survive installation"}')
[IO.File]::WriteAllText((Join-Path $dataRoot 'models\preserved.txt'), 'synthetic model marker')
[IO.File]::WriteAllText((Join-Path $dataRoot 'credentials.bin'), 'synthetic marker; NOT a real credential')
function Data-Fingerprint {
    $items = @(Get-ChildItem -LiteralPath $dataRoot -File -Recurse | Sort-Object FullName | ForEach-Object { [IO.Path]::GetRelativePath($dataRoot, $_.FullName) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
    return $items -join "`n"
}
$originalData = Data-Fingerprint
$events = [Collections.Generic.List[object]]::new()
function Assert-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $events.Add([pscustomobject]@{time=[DateTimeOffset]::Now.ToString('o'); name=$Name; passed=$Passed; detail=$Detail})
    Write-Output ($Name + ': ' + $Passed)
    if (-not $Passed) { throw ('Lifecycle check failed: ' + $Name + '; ' + $Detail) }
}
function Run-Owned([string]$File, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($File)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.Environment['NEXTTHOUGHT_DATA_DIR'] = $dataRoot
    foreach ($value in $Arguments) { $start.ArgumentList.Add($value) }
    $process = [Diagnostics.Process]::Start($start); $process.WaitForExit(); return $process.ExitCode
}
function Install-Package([string]$Name) {
    return Run-Owned (Join-Path $packageDirectory ($Name + '.exe')) @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOCLOSEAPPLICATIONS', '/NORESTARTAPPLICATIONS', '/SP-', ('/DIR=' + $installRoot), '/TASKS=desktopicon', ('/LOG=' + (Join-Path $qa ($RunName + '-' + $Name + '-install.log'))))
}
function State { return Get-Content -LiteralPath (Join-Path $installRoot 'installed-release.json') -Raw | ConvertFrom-Json }
function Link-Target {
    $shell = New-Object -ComObject WScript.Shell
    try { $link = $shell.CreateShortcut($shortcut); try {return $link.TargetPath} finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)} }
    finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
}
$passed = $false
try {
    Assert-Check 'Install baseline' ((Install-Package 'QA-baseline-0.3.1') -eq 0) 'Per-user QA installation only'
    Assert-Check 'Baseline selected' ((State).currentVersion -eq '0.3.1') 'Original desktop binary with isolated new deployment helper'
    $baselineState = Get-FileHash -LiteralPath (Join-Path $installRoot 'installed-release.json') -Algorithm SHA256
    $baselineLink = Link-Target
    $failureCode = Install-Package 'QA-failure-0.4.0'
    Assert-Check 'Controlled install failure observed' ($failureCode -ne 0) ('Exit=' + $failureCode)
    Assert-Check 'Failed upgrade preserved active state' ((Get-FileHash -LiteralPath (Join-Path $installRoot 'installed-release.json') -Algorithm SHA256).Hash -eq $baselineState.Hash) 'State bytes unchanged'
    Assert-Check 'Failed upgrade preserved shortcut' ((Link-Target) -eq $baselineLink) 'Original shortcut target unchanged'
    Assert-Check 'Failed upgrade preserved registry' ((Get-ItemProperty -LiteralPath $key).DisplayVersion -eq '0.3.1') 'HKCU QA identity unchanged'
    Assert-Check 'Upgrade' ((Install-Package 'QA-upgrade-0.4.0') -eq 0) 'Install candidate after controlled failure'
    Assert-Check 'Upgrade version selection' ((State).currentVersion -eq '0.4.0' -and (State).previousVersion -eq '0.3.1') 'Both versions retained'
    $cli = Join-Path $installRoot 'versions\0.4.0\tools\NextThought.Cli.exe'
    $oldBinary = Join-Path $installRoot 'versions\0.3.1\NextThought.exe'
    $oldBytes = [IO.File]::ReadAllBytes($oldBinary)
    $activeHash = (Get-FileHash -LiteralPath (Join-Path $installRoot 'installed-release.json') -Algorithm SHA256).Hash
    try {
        [IO.File]::AppendAllText($oldBinary, 'controlled QA corruption')
        $code = Run-Owned $cli @('deployment-rollback', '--install-root', $installRoot, '--uninstall-key', $keyName)
        Assert-Check 'Corrupt rollback rejected' ($code -ne 0) 'Previous version integrity required'
        Assert-Check 'Rejected rollback preserved selection' ((Get-FileHash -LiteralPath (Join-Path $installRoot 'installed-release.json') -Algorithm SHA256).Hash -eq $activeHash) 'Active version remains candidate'
    } finally { [IO.File]::WriteAllBytes($oldBinary, $oldBytes) }
    Assert-Check 'Rollback' ((Run-Owned $cli @('deployment-rollback', '--install-root', $installRoot, '--uninstall-key', $keyName)) -eq 0) 'Original binary restored and verified'
    Assert-Check 'Rollback version selection' ((State).currentVersion -eq '0.3.1' -and (State).previousVersion -eq '0.4.0') 'Can re-upgrade'
    Assert-Check 'Rollback shortcut' ((Link-Target) -eq $baselineLink) 'Shortcut points to baseline binary'
    Assert-Check 'Rollback registry' ((Get-ItemProperty -LiteralPath $key).DisplayVersion -eq '0.3.1') 'Registered display version matches selected binary'
    Assert-Check 'Re-upgrade' ((Install-Package 'QA-upgrade-0.4.0') -eq 0) 'Candidate selected again'
    Assert-Check 'Data retained through upgrades' ((Data-Fingerprint) -eq $originalData) 'All synthetic data bytes unchanged'
    $code = Run-Owned (Join-Path $installRoot 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG=' + (Join-Path $qa 'uninstall.log')))
    Assert-Check 'Uninstaller returned success' ($code -eq 0) ('Exit=' + $code)
    Assert-Check 'Uninstall removed registration' (-not (Test-Path -LiteralPath $key)) 'Only QA registration was touched'
    Assert-Check 'Uninstall removed shortcut' (-not (Test-Path -LiteralPath $shortcut)) 'Only QA shortcut was touched'
    Assert-Check 'Uninstall retained user data' ((Data-Fingerprint) -eq $originalData) 'Synthetic sessions, model marker and credential marker unchanged'
    $passed = $true
} finally {
    [pscustomobject]@{checkedAt=[DateTimeOffset]::Now.ToString('o'); passed=$passed; qaOnly=$true; installId=$keyName; installRoot=$installRoot; dataRoot=$dataRoot; events=$events} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $qa ('lifecycle-' + $RunName + '.json')) -Encoding utf8
}
Write-Output 'Installer lifecycle: PASS'
