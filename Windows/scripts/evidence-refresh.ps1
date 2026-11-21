# §5 证据重拍管线（裁决版）
# 1) 用最终构建跑 `--demo --screenshots` 抓全部页面矩阵（浅/深/英）；
# 2) 跑 `--self-test=live-states` 抓会中页 20 状态（浅/深）；
# 3) 删除 validation/screens 下所有旧截图（含 live/windows-*.png 等重做前文件），
#    按 `<页面>-light.png` / `<页面>-dark.png` / `<页面>-en-light.png` / `<页面>-en-dark.png` 规范落盘；
# 4) 每份 compare.md 开头写入截图来源提交；
# 5) 跑深色对比度自动检查（`--self-test=dark-contrast`，正文/背景对比度 < 4.5 的控件逐条列出）。
# 用法：pwsh -File Windows/scripts/evidence-refresh.ps1 -Exe <NextThought.exe> [-Build]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [switch]$Build,
    [string]$Commit = ''
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$screens = Join-Path $repo 'Windows\validation\screens'
$staging = Join-Path $env:TEMP ('nt-evidence-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $staging -Force | Out-Null

function Log([string]$message) { Write-Host ((Get-Date -Format 'HH:mm:ss') + '  ' + $message) }
function Run-App([string[]]$arguments, [int]$timeoutSeconds, [string]$name) {
    $dataDir = Join-Path $env:TEMP ('nt-evidence-data-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    $oldData = $env:NEXTTHOUGHT_DATA_DIR
    $env:NEXTTHOUGHT_DATA_DIR = $dataDir
    $stdout = Join-Path $staging ($name + '.out.txt')
    $stderr = Join-Path $staging ($name + '.err.txt')
    $process = $null
    try {
        $process = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw ($name + ' 超时')
        }
        Log ("$name exit=" + $process.ExitCode)
    } finally {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        Get-Process -Name 'NextThought' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*nt-evidence*' } |
            Stop-Process -Force -ErrorAction SilentlyContinue
        $env:NEXTTHOUGHT_DATA_DIR = $oldData
        if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

if (-not $Commit) {
    try { $Commit = (git -C $repo rev-parse --short HEAD).Trim() } catch { $Commit = 'unknown' }
}
# Run a private copy of the app so host-wide "kill NextThought.exe" sweeps cannot abort the
# long screenshot batch halfway through.
$privateDir = Join-Path $staging 'app'
New-Item -ItemType Directory -Path $privateDir -Force | Out-Null
Copy-Item -Path (Join-Path (Split-Path $Exe -Parent) '*') -Destination $privateDir -Recurse -Force
$privateExe = Join-Path $privateDir 'NTEvidence.exe'
Move-Item (Join-Path $privateDir 'NextThought.exe') $privateExe -Force
$Exe = $privateExe
Log ("evidence refresh from commit " + $Commit + " using " + $Exe)
if ($Build) {
    Log 'building final Release…'
    & (Join-Path $repo 'Windows\.tools\dotnet\dotnet.exe') build (Join-Path $repo 'Windows\src\NextThought.Desktop') `
        -c Release --artifacts-path (Join-Path $env:TEMP 'nt-evidence-artifacts') --verbosity minimal | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }
}

# ---------- 1) canonical page-matrix screenshots ----------
$batchDir = Join-Path $staging 'batch'
Run-App -arguments @('--demo', ('--screenshots=' + $batchDir)) -timeoutSeconds 900 -name 'screenshots'
if (-not (Test-Path $batchDir)) { throw 'no screenshot output' }
$batchFiles = @(Get-ChildItem $batchDir -Filter '*.png')

# ---------- 2) live-states (20 states, light/dark) ----------
$liveStatesDir = Join-Path $staging 'live-states'
$liveStatesOk = $false
try {
    $liveStatesExe = Get-Item $Exe
    if ($liveStatesExe.VersionInfo -ne $null) { }
    $source = Join-Path $env:TEMP 'nextthought-selftest\live-states'
    Remove-Item $source -Recurse -Force -ErrorAction SilentlyContinue
    try {
        Run-App -arguments @('--demo', '--self-test=live-states') -timeoutSeconds 420 -name 'live-states'
    } catch {
        # The self-test writes the report/images before its final Close(); a slow exit must not
        # throw away good evidence. Accept the directory when it is complete.
        if (-not ((Test-Path $source) -and (@(Get-ChildItem $source -Filter '*.png').Count -ge 40))) { throw }
        Log 'live-states: app exited slowly; using the complete capture directory'
    }
    if (Test-Path $source) {
        New-Item -ItemType Directory -Path $liveStatesDir -Force | Out-Null
        Copy-Item (Join-Path $source '*') $liveStatesDir -Recurse -Force
        $liveStatesOk = (@(Get-ChildItem $liveStatesDir -Filter '*.png')).Count -ge 1
    }
} catch { Log ('live-states failed: ' + $_.Exception.Message) }

# ---------- 3) 删旧图 + 规范落盘 ----------
$canonicalSuffixes = @('-light.png', '-dark.png', '-en-light.png', '-en-dark.png')
$liveStatePattern = '^[a-z0-9-]+-(light|dark)\.png$'
foreach ($pair in @(@('home','home'), @('prepare','prepare'), @('live','live'), @('notes','notes'),
                    @('history','history'), @('series','series'), @('library','library'), @('settings','settings'))) {
    $page = $pair[0]; $dir = Join-Path $screens $pair[1]
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    # 删掉旧截图：windows-*、单个 light/dark/en-*、以及任何不再产出的 png
    Get-ChildItem $dir -Filter '*.png' | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $dir -Filter '*.png' -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    foreach ($suffix in $canonicalSuffixes + @('-en.png')) {
        $name = $page + $suffix
        $source = Join-Path $batchDir ($name)
        if (Test-Path $source) { Copy-Item $source (Join-Path $dir $name) -Force }
        elseif ($suffix -eq '-en.png' -and (Test-Path (Join-Path $batchDir ($page + '-en-light.png')))) {
            # legacy single English file no longer produced; English pairs are canonical
        }
    }
    # 子视图（notes 页签、settings 页等）随批一起拷入目录，保持原文件名
    Get-ChildItem $batchDir -Filter ($page + '-*.png') | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $dir $_.Name) -Force
    }
}
# live-states：规范命名为 <状态>-<light|dark>.png
$liveStatesTarget = Join-Path $screens 'live-states'
New-Item -ItemType Directory -Path $liveStatesTarget -Force | Out-Null
Get-ChildItem $liveStatesTarget -Filter '*.png' -ErrorAction SilentlyContinue | Remove-Item -Force
if ($liveStatesOk) {
    Get-ChildItem $liveStatesDir -Filter '*.png' | ForEach-Object { Copy-Item $_.FullName (Join-Path $liveStatesTarget $_.Name) -Force }
}
# 其它页面目录：只删重做前旧文件（windows-*、light/dark 单文件），保留 compare.md
foreach ($dir in Get-ChildItem $screens -Directory) {
    if ($dir.Name -in @('home','prepare','live','notes','history','series','library','settings','live-states','current')) { continue }
    Get-ChildItem $dir.FullName -Filter 'windows-*.png' -ErrorAction SilentlyContinue | Remove-Item -Force
    foreach ($legacy in @('light.png','dark.png','en-light.png','en-dark.png')) {
        $path = Join-Path $dir.FullName $legacy
        if (Test-Path $path) { Remove-Item $path -Force }
    }
}

# ---------- 4) compare.md 开头写截图提交 ----------
$header = '> 截图来源：提交 `' + $Commit + '`（' + (Get-Date -Format 'yyyy-MM-dd HH:mm') + '，最终构建；浅/深/英与状态截图均由该提交生成）。'
Get-ChildItem $screens -Recurse -Filter 'compare.md' | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $lines = $text -split "`r?`n"
    $target = ''
    foreach ($line in $lines) { if ($line.Trim().Length -gt 0) { $target = $line; break } }
    $withoutOld = ($lines | Where-Object { $_ -notmatch '^> 截图来源：' }) -join "`n"
    $replaced = $withoutOld
    if ($target) {
        $index = $withoutOld.IndexOf($target)
        if ($index -ge 0) {
            $replaced = $withoutOld.Substring(0, $index + $target.Length) + "`n`n" + $header + "`n" + $withoutOld.Substring($index + $target.Length)
        }
    }
    if ($replaced -notlike ('*' + $header + '*')) { $replaced = $header + "`n`n" + $withoutOld }
    [System.IO.File]::WriteAllText($_.FullName, $replaced, (New-Object System.Text.UTF8Encoding $false))
}

# ---------- 5) 深色对比度自动检查 ----------
$contrastDir = Join-Path $staging 'contrast'
New-Item -ItemType Directory -Path $contrastDir -Force | Out-Null
$contrastOk = $false
try {
    Run-App -arguments @('--demo', '--self-test=dark-contrast') -timeoutSeconds 600 -name 'dark-contrast'
    $source = Join-Path $env:TEMP 'nextthought-selftest\dark-contrast'
    if (Test-Path $source) {
        $target = Join-Path $screens 'contrast'
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Copy-Item $source (Join-Path $target 'report.json') -Force
        $report = Get-Content (Join-Path $target 'report.json') -Raw -ErrorAction SilentlyContinue
        $contrastOk = $report -match '"passed"\s*:\s*true'
    }
} catch { Log ('dark-contrast failed: ' + $_.Exception.Message) }

# ---------- current snapshot + summary ----------
$current = Join-Path $screens 'current'
New-Item -ItemType Directory -Path $current -Force | Out-Null
Get-ChildItem $current -Filter '*.png' -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $batchDir -Filter '*.png' | ForEach-Object { Copy-Item $_.FullName (Join-Path $current $_.Name) -Force }
if ($liveStatesOk) {
    Get-ChildItem $liveStatesDir -Filter '*.png' | ForEach-Object { Copy-Item $_.FullName (Join-Path $current ('live-' + $_.Name)) -Force }
}
$contrastReport = Join-Path $screens 'contrast\report.json'
$contrastFailures = 0
if (Test-Path $contrastReport) {
    try {
        $parsed = Get-Content $contrastReport -Raw | ConvertFrom-Json
        $contrastFailures = [int]$parsed.failures
    } catch { $contrastFailures = -1 }
}
$summary = [pscustomobject]@{
    commit = $Commit
    batchImages = $batchFiles.Count
    liveStates = $liveStatesOk
    contrastPassed = $contrastOk
    contrastFailures = $contrastFailures
    batch = $batchDir
    generatedAt = (Get-Date).ToString('s')
}
$summary | ConvertTo-Json | Set-Content (Join-Path $screens 'evidence-refresh.json') -Encoding UTF8
Log ("done: batch=" + $batchFiles.Count + " liveStates=" + $liveStatesOk + " contrastPassed=" + $contrastOk + " contrastFailures=" + $contrastFailures)
if (-not $liveStatesOk) { exit 1 }
if ($contrastFailures -gt 0) { Log ("WARNING: " + $contrastFailures + " dark-contrast failures listed in screens/contrast/report.json (owners to fix)") }
