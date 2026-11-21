# K1 防截屏实机测试（GDI）：主窗口 + 提词岛/字幕条/提词浮窗
# 任务书 §9 K1。用 GDI 截整个屏幕，断言「共享时隐藏窗口」开启时窗口区域显示的是它后面的内容，
# 关闭时窗口出现在截图里。开/关两组截图都保存为证据。
#
# 运行：pwsh -File Windows/scripts/realmachine/k1-capture-hide.ps1 -Exe <NextThought.exe> [-Out <dir>]
# 说明：主窗口用 `--demo` 单独跑（窗口稳定、亲和性会由 StartTracking 应用）；提词岛/字幕条/提词浮窗
# 用 `--self-test=capture-hide` 打开探针窗口时抓取。GDI 行为已先用一个独立 magenta 探针验证：
# WDA_EXCLUDEFROMCAPTURE 的窗口在 BitBlt 截图里不出现（见 report.json 的 osProbe）。
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = (Join-Path $PSScriptRoot '..\..\validation\realmachine\K1')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class K1Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint affinity);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public sealed class WindowInfo
    {
        public long Handle;
        public int Left, Top, Right, Bottom;
        public string ClassName = "";
        public string Title = "";
        public uint Affinity;
    }

    public static List<WindowInfo> ForProcess(uint processId)
    {
        var windows = new List<WindowInfo>();
        EnumWindows((handle, _) =>
        {
            uint pid; GetWindowThreadProcessId(handle, out pid);
            if (pid != processId || !IsWindowVisible(handle)) return true;
            RECT rect; if (!GetWindowRect(handle, out rect)) return true;
            if (rect.Right - rect.Left <= 1 || rect.Bottom - rect.Top <= 1) return true;
            var cls = new StringBuilder(256); GetClassName(handle, cls, cls.Capacity);
            var title = new StringBuilder(256); GetWindowText(handle, title, title.Capacity);
            uint affinity; GetWindowDisplayAffinity(handle, out affinity);
            windows.Add(new WindowInfo
            {
                Handle = handle.ToInt64(), Left = rect.Left, Top = rect.Top,
                Right = rect.Right, Bottom = rect.Bottom, ClassName = cls.ToString(),
                Title = title.ToString(), Affinity = affinity
            });
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}
'@

function Capture-Screen {
    param([string]$Path)
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size) }
    finally { $graphics.Dispose() }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    return $bitmap
}

function Measure-Region {
    param([System.Drawing.Bitmap]$Bitmap, $Window)
    $left = [Math]::Max(0, $Window.Left); $top = [Math]::Max(0, $Window.Top)
    $right = [Math]::Min($Bitmap.Width, $Window.Right); $bottom = [Math]::Min($Bitmap.Height, $Window.Bottom)
    if ($right - $left -lt 8 -or $bottom - $top -lt 8) { return $null }
    $sum = 0.0; $sumSq = 0.0; $count = 0; $bright = 0
    for ($y = $top; $y -lt $bottom; $y += 4) {
        for ($x = $left; $x -lt $right; $x += 4) {
            $c = $Bitmap.GetPixel($x, $y)
            $v = 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B
            $sum += $v; $sumSq += $v * $v; $count++
            if ($v -gt 24) { $bright++ }
        }
    }
    if ($count -eq 0) { return $null }
    $mean = $sum / $count
    [pscustomobject]@{
        mean = [Math]::Round($mean, 2)
        stdDev = [Math]::Round([Math]::Sqrt([Math]::Max(0, $sumSq / $count - $mean * $mean)), 2)
        brightRatio = [Math]::Round($bright / $count, 3)
        samples = $count
    }
}

function Save-Crop {
    param([System.Drawing.Bitmap]$Bitmap, $Window, [string]$Path)
    $left = [Math]::Max(0, $Window.Left); $top = [Math]::Max(0, $Window.Top)
    $width = [Math]::Min($Bitmap.Width, $Window.Right) - $left
    $height = [Math]::Min($Bitmap.Height, $Window.Bottom) - $top
    if ($width -le 0 -or $height -le 0) { return }
    $crop = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($crop)
    try {
        $graphics.DrawImage($Bitmap, (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
            (New-Object System.Drawing.Rectangle $left, $top, $width, $height), [System.Drawing.GraphicsUnit]::Pixel)
    } finally { $graphics.Dispose() }
    $crop.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $crop.Dispose()
}

function Set-CapturePrivacy {
    param([string]$DataDir, [bool]$Enabled)
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
    $settingsPath = Join-Path $DataDir 'settings.json'
    $settings = @{}
    if (Test-Path $settingsPath) {
        try { $settings = Get-Content $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable } catch { $settings = @{} }
    }
    $settings['capturePrivacy'] = $Enabled
    $settings | ConvertTo-Json -Depth 6 | Set-Content -Path $settingsPath -Encoding UTF8
}

# 等窗口出现、亲和性符合预期且矩形稳定，然后整屏截图 + 存窗口裁切图。返回样本（无则 null）。
function Capture-WhenReady {
    param($Process, [string]$ClassFilter, [uint32]$ExpectedAffinity, [string]$Label, [string]$Directory,
          [string]$TitleFilter = '', [int]$TimeoutMs = 6000, [int]$MinDelayMs = 350)

    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    $appeared = $null; $lastRect = $null
    while ((Get-Date) -lt $deadline -and -not $Process.HasExited) {
        $windows = @([K1Native]::ForProcess([uint32]$Process.Id) | Where-Object {
            $_.ClassName -like $ClassFilter -and ($TitleFilter -eq '' -or $_.Title -like "*$TitleFilter*")
        })
        if ($windows.Count -gt 0) {
            $window = $windows[0]
            $rect = '{0},{1},{2},{3}' -f $window.Left, $window.Top, $window.Right, $window.Bottom
            if (-not $appeared) { $appeared = Get-Date }
            if ($window.Affinity -eq $ExpectedAffinity -and $lastRect -eq $rect -and
                ((Get-Date) - $appeared).TotalMilliseconds -ge $MinDelayMs) {
                $stamp = Get-Date -Format 'HHmmssfff'
                $fullPath = Join-Path $Directory ("screen-$Label-$stamp.png")
                $cropPath = Join-Path $Directory ("window-$Label-$stamp.png")
                $bitmap = Capture-Screen -Path $fullPath
                try {
                    $region = Measure-Region -Bitmap $bitmap -Window $window
                    Save-Crop -Bitmap $bitmap -Window $window -Path $cropPath
                } finally { $bitmap.Dispose() }
                return [pscustomobject]@{
                    label = $Label; className = $window.ClassName; title = $window.Title
                    handle = $window.Handle; affinity = ('0x{0:X}' -f $window.Affinity)
                    expectedAffinity = ('0x{0:X}' -f $ExpectedAffinity)
                    left = $window.Left; top = $window.Top; right = $window.Right; bottom = $window.Bottom
                    region = $region; screenShot = $fullPath; cropShot = $cropPath
                }
            }
            $lastRect = $rect
        }
        Start-Sleep -Milliseconds 120
    }
    return $null
}

function Invoke-Run {
    param([string]$Label, [bool]$Privacy, [string]$Directory)
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $samples = @()
    $expected = if ($Privacy) { 0x11 } else { 0 }
    $oldData = $env:NEXTTHOUGHT_DATA_DIR
    $dataDir = Join-Path $env:TEMP ('nt-k1-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    Set-CapturePrivacy -DataDir $dataDir -Enabled $Privacy
    $env:NEXTTHOUGHT_DATA_DIR = $dataDir
    $process = $null
    try {
        # 1) 主窗口：--demo 常驻，StartTracking 应用亲和性后再截图。
        $process = Start-Process -FilePath $Exe -ArgumentList @('--demo') -PassThru
        $main = Capture-WhenReady -Process $process -ClassFilter 'Avalonia-*' -ExpectedAffinity $expected `
            -Label 'main' -Directory $Directory -TimeoutMs 8000 -MinDelayMs 2000
        if ($main) { $samples += $main }
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        if ($process) { $process.WaitForExit(5000) | Out-Null }

        # 2) 提词岛/字幕条/提词浮窗：capture-hide self-test 依次打开探针窗口。探针生命周期只有
        # 几百毫秒，所以用同一个轮询同时盯着三个窗口：一旦出现且亲和性正确就立即截图（只截一次）。
        $process = Start-Process -FilePath $Exe -ArgumentList @('--demo', '--self-test=capture-hide') -PassThru
        $probes = [ordered]@{ 'island' = '提词岛'; 'subtitle' = '字幕条'; 'teleprompter' = '回答浮窗' }
        $captured = @{}
        $deadline = (Get-Date).AddSeconds(12)
        while ((Get-Date) -lt $deadline -and -not $process.HasExited) {
            $windows = @([K1Native]::ForProcess([uint32]$process.Id))
            foreach ($label in @($probes.Keys)) {
                if ($captured.ContainsKey($label)) { continue }
                $window = $windows | Where-Object { $_.Title -like "*$($probes[$label])*" } | Select-Object -First 1
                if (-not $window -or $window.Affinity -ne $expected) { continue }
                $stamp = Get-Date -Format 'HHmmssfff'
                $fullPath = Join-Path $Directory ("screen-$label-$stamp.png")
                $cropPath = Join-Path $Directory ("window-$label-$stamp.png")
                $bitmap = Capture-Screen -Path $fullPath
                try {
                    $region = Measure-Region -Bitmap $bitmap -Window $window
                    Save-Crop -Bitmap $bitmap -Window $window -Path $cropPath
                } finally { $bitmap.Dispose() }
                $captured[$label] = $true
                $samples += [pscustomobject]@{
                    label = $label; className = $window.ClassName; title = $window.Title
                    handle = $window.Handle; affinity = ('0x{0:X}' -f $window.Affinity)
                    expectedAffinity = ('0x{0:X}' -f $expected)
                    left = $window.Left; top = $window.Top; right = $window.Right; bottom = $window.Bottom
                    region = $region; screenShot = $fullPath; cropShot = $cropPath
                }
            }
            if ($captured.Count -eq $probes.Count) { break }
            Start-Sleep -Milliseconds 90
        }
        if ($process -and -not $process.HasExited) { $process.WaitForExit(20000) | Out-Null }
        return $samples
    } finally {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        $env:NEXTTHOUGHT_DATA_DIR = $oldData
        if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# 独立 magenta 探针：确认本机 GDI BitBlt 会排除 WDA_EXCLUDEFROMCAPTURE 的窗口。
function Test-OsGdiBehavior {
    $probeScript = Join-Path $env:TEMP 'k1-wda-probe.ps1'
    @'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class W { [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint a); }
"@
$form = New-Object System.Windows.Forms.Form
$form.StartPosition = 'Manual'
$form.Location = New-Object System.Drawing.Point 200, 200
$form.Size = New-Object System.Drawing.Size 320, 200
$form.BackColor = [System.Drawing.Color]::Magenta
$form.TopMost = $true
$form.Show()
$null = [W]::SetWindowDisplayAffinity($form.Handle, 0x11)
Start-Sleep -Seconds 10
$form.Close()
'@ | Set-Content -Path $probeScript -Encoding UTF8
    $process = Start-Process -FilePath 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
        -ArgumentList @('-ExecutionPolicy', 'Bypass', '-NoProfile', '-File', $probeScript) -PassThru
    try {
        Start-Sleep -Seconds 3
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size) }
        finally { $graphics.Dispose() }
        $magenta = 0; $count = 0
        for ($y = 190; $y -lt [Math]::Min(450, $bitmap.Height); $y += 5) {
            for ($x = 190; $x -lt [Math]::Min(550, $bitmap.Width); $x += 5) {
                $c = $bitmap.GetPixel($x, $y); $count++
                if ($c.R -gt 200 -and $c.G -lt 80 -and $c.B -gt 200) { $magenta++ }
            }
        }
        $bitmap.Dispose()
        [pscustomobject]@{
            samples = $count; magentaPixels = $magenta
            magentaRatio = if ($count -gt 0) { [Math]::Round($magenta / $count, 3) } else { 0 }
            excluded = ($magenta -eq 0)
        }
    } finally {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}


# ---- Windows.Graphics.Capture 证据 ------------------------------------------------------
# 应用自己的窗口用同一套 WDA 亲和性；WGC 是任务书要求的第二种截屏方式，会排除这些窗口。
function Resolve-WgcTool {
    param([string]$RepositoryRoot)
    $project = Join-Path $RepositoryRoot 'Windows/tools/realmachine/WgcCapture'
    $artifacts = Join-Path $env:TEMP 'nt-wgc-artifacts'
    if (-not (Test-Path $artifacts)) {
        & (Join-Path $RepositoryRoot 'Windows/.tools/dotnet/dotnet.exe') build $project -c Release `
            --artifacts-path $artifacts --verbosity quiet | Out-Null
    }
    $exe = Get-ChildItem $artifacts -Recurse -Filter 'WgcCapture.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*Release*' -or $_.FullName -like '*release*' } |
        Select-Object -First 1
    return $exe
}

function Invoke-WgcRun {
    param([string]$Label, [bool]$Privacy, [string]$Directory, $WgcExe)
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $expected = if ($Privacy) { 0x11 } else { 0 }
    $oldData = $env:NEXTTHOUGHT_DATA_DIR
    $dataDir = Join-Path $env:TEMP ('nt-k1w-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    Set-CapturePrivacy -DataDir $dataDir -Enabled $Privacy
    $env:NEXTTHOUGHT_DATA_DIR = $dataDir
    $process = $null
    try {
        $process = Start-Process -FilePath $Exe -ArgumentList @('--demo') -PassThru
        # 等主窗口和它的亲和性就绪。
        $deadline = (Get-Date).AddSeconds(12)
        $main = $null
        while ((Get-Date) -lt $deadline -and -not $process.HasExited) {
            $candidate = @([K1Native]::ForProcess([uint32]$process.Id) |
                Where-Object { $_.Title -eq 'NextThought' -and $_.Affinity -eq $expected }) | Select-Object -First 1
            if ($candidate) { $main = $candidate; break }
            Start-Sleep -Milliseconds 150
        }
        if (-not $main) { return $null }
        Start-Sleep -Milliseconds 600
        $png = Join-Path $Directory ("wgc-$Label-{0}.png" -f (Get-Date -Format 'HHmmssfff'))
        $capture = Start-Process -FilePath $WgcExe -ArgumentList @($png, '6000') -PassThru -Wait
        if ($capture.ExitCode -ne 0 -or -not (Test-Path $png)) { return [pscustomobject]@{ label = $Label; error = 'WGC capture failed' } }
        $bitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $png))
        try {
            $region = Measure-Region -Bitmap $bitmap -Window $main
            $cropPath = Join-Path $Directory ("wgc-window-$Label.png")
            Save-Crop -Bitmap $bitmap -Window $main -Path $cropPath
        } finally { $bitmap.Dispose() }
        return [pscustomobject]@{
            label = $Label; affinity = ('0x{0:X}' -f $main.Affinity)
            left = $main.Left; top = $main.Top; right = $main.Right; bottom = $main.Bottom
            region = $region; screenShot = $png; cropShot = $cropPath
        }
    } finally {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        $env:NEXTTHOUGHT_DATA_DIR = $oldData
        if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

New-Item -ItemType Directory -Path $Out -Force | Out-Null
$osProbe = Test-OsGdiBehavior
$onSamples = Invoke-Run -Label 'capturePrivacy=true' -Privacy $true -Directory (Join-Path $Out 'on')
$offSamples = Invoke-Run -Label 'capturePrivacy=false' -Privacy $false -Directory (Join-Path $Out 'off')

# Windows.Graphics.Capture：同一条「开/关」对照，主窗口区域应当分别显示后方内容/窗口本身。
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$wgcExe = Resolve-WgcTool -RepositoryRoot $repositoryRoot
$wgcSamples = @()
if ($wgcExe) {
    $wgcSamples += Invoke-WgcRun -Label 'capturePrivacy=true' -Privacy $true -Directory (Join-Path $Out 'wgc-on') -WgcExe $wgcExe.FullName
    $wgcSamples += Invoke-WgcRun -Label 'capturePrivacy=false' -Privacy $false -Directory (Join-Path $Out 'wgc-off') -WgcExe $wgcExe.FullName
} else {
    $wgcSamples += [pscustomobject]@{ label = 'tool'; error = 'WgcCapture.exe 未找到/未构建' }
}

$failures = @()
if (-not $osProbe.excluded) { $failures += 'OS 探针：GDI 截图没有排除 WDA_EXCLUDEFROMCAPTURE 窗口（本机 GDI 行为与预期不同）' }
if ($onSamples.Count -eq 0) { $failures += 'privacy=on 没有抓到窗口样本' }
if ($offSamples.Count -eq 0) { $failures += 'privacy=off 没有抓到窗口样本' }
$required = @('main', 'island', 'subtitle', 'teleprompter')
foreach ($label in $required) {
    if (-not ($onSamples | Where-Object { $_.label -eq $label })) { $failures += "privacy=on 缺少窗口样本：$label" }
    if (-not ($offSamples | Where-Object { $_.label -eq $label })) { $failures += "privacy=off 缺少窗口样本：$label" }
}
$onBad = @($onSamples | Where-Object { $_.affinity -ne '0x11' })
if ($onBad.Count -gt 0) { $failures += "privacy=on 亲和性不是 0x11：$($onBad.Count) 个样本" }
$offBad = @($offSamples | Where-Object { $_.affinity -ne '0x0' })
if ($offBad.Count -gt 0) { $failures += "privacy=off 亲和性不是 0x0：$($offBad.Count) 个样本" }

# 主窗口在屏幕上的矩形固定（同一次运行的窗口位置一致），比较开/关两次截图的同一区域：
# 开启时应是「后面的内容」（主窗口下方的页面/桌面），关闭时是主窗口自身内容 → 两者差异明显。
$onMain = @($onSamples | Where-Object { $_.label -eq 'main' }) | Select-Object -First 1
$offMain = @($offSamples | Where-Object { $_.label -eq 'main' }) | Select-Object -First 1
if ($onMain -and $offMain) {
    Add-Type -AssemblyName System.Drawing
    $onBitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $onMain.cropShot))
    $offBitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $offMain.cropShot))
    try {
        $width = [Math]::Min($onBitmap.Width, $offBitmap.Width)
        $height = [Math]::Min($onBitmap.Height, $offBitmap.Height)
        $sum = 0.0; $count = 0
        for ($y = 0; $y -lt $height; $y += 4) {
            for ($x = 0; $x -lt $width; $x += 4) {
                $a = $onBitmap.GetPixel($x, $y); $b = $offBitmap.GetPixel($x, $y)
                $sum += [Math]::Abs($a.R - $b.R) + [Math]::Abs($a.G - $b.G) + [Math]::Abs($a.B - $b.B)
                $count++
            }
        }
        $meanDiff = if ($count -gt 0) { [Math]::Round($sum / (3 * $count), 2) } else { 0 }
        if ($meanDiff -lt 12) { $failures += "主窗口开/关截图差异过小（meanAbsDiff=$meanDiff）：防截屏没有生效" }
    } finally { $onBitmap.Dispose(); $offBitmap.Dispose() }
} else {
    $failures += '主窗口开/关样本不足，无法比较'
}

$probeDiffs = @{}
Add-Type -AssemblyName System.Drawing
foreach ($label in @('island', 'subtitle', 'teleprompter')) {
    $onShot = ($onSamples | Where-Object { $_.label -eq $label } | Select-Object -First 1)
    $offShot = ($offSamples | Where-Object { $_.label -eq $label } | Select-Object -First 1)
    if (-not $onShot -or -not $offShot) { continue }
    $a = [System.Drawing.Bitmap]::FromFile((Resolve-Path $onShot.cropShot))
    $b = [System.Drawing.Bitmap]::FromFile((Resolve-Path $offShot.cropShot))
    try {
        $w = [Math]::Min($a.Width, $b.Width); $h = [Math]::Min($a.Height, $b.Height)
        $sum = 0.0; $count = 0
        for ($y = 0; $y -lt $h; $y += 4) {
            for ($x = 0; $x -lt $w; $x += 4) {
                $ca = $a.GetPixel($x, $y); $cb = $b.GetPixel($x, $y)
                $sum += [Math]::Abs($ca.R - $cb.R) + [Math]::Abs($ca.G - $cb.G) + [Math]::Abs($ca.B - $cb.B)
                $count++
            }
        }
        $probeDiffs[$label] = if ($count -gt 0) { [Math]::Round($sum / (3 * $count), 2) } else { 0 }
        if ($probeDiffs[$label] -lt 12) { $failures += "$label 开/关截图差异过小（meanAbsDiff=$($probeDiffs[$label])）" }
    } finally { $a.Dispose(); $b.Dispose() }
}

# WGC 断言：工具存在、两次都抓到主窗口、亲和性正确、开/关截图差异明显。
$wgcOn = @($wgcSamples | Where-Object { $_.label -eq 'capturePrivacy=true' -and -not $_.error }) | Select-Object -First 1
$wgcOff = @($wgcSamples | Where-Object { $_.label -eq 'capturePrivacy=false' -and -not $_.error }) | Select-Object -First 1
$wgcDiff = $null
if (-not $wgcOn -or -not $wgcOff) {
    $failures += 'Windows.Graphics.Capture 证据不足（工具或一次抓取失败）'
} else {
    if ($wgcOn.affinity -ne '0x11') { $failures += "WGC privacy=on 亲和性不是 0x11：$($wgcOn.affinity)" }
    if ($wgcOff.affinity -ne '0x0') { $failures += "WGC privacy=off 亲和性不是 0x0：$($wgcOff.affinity)" }
    $a = [System.Drawing.Bitmap]::FromFile((Resolve-Path $wgcOn.cropShot))
    $b = [System.Drawing.Bitmap]::FromFile((Resolve-Path $wgcOff.cropShot))
    try {
        $w = [Math]::Min($a.Width, $b.Width); $h = [Math]::Min($a.Height, $b.Height)
        $sum = 0.0; $count = 0
        for ($y = 0; $y -lt $h; $y += 4) {
            for ($x = 0; $x -lt $w; $x += 4) {
                $ca = $a.GetPixel($x, $y); $cb = $b.GetPixel($x, $y)
                $sum += [Math]::Abs($ca.R - $cb.R) + [Math]::Abs($ca.G - $cb.G) + [Math]::Abs($ca.B - $cb.B)
                $count++
            }
        }
        $wgcDiff = if ($count -gt 0) { [Math]::Round($sum / (3 * $count), 2) } else { 0 }
        if ($wgcDiff -lt 12) { $failures += "WGC 主窗口开/关截图差异过小（meanAbsDiff=$wgcDiff）" }
    } finally { $a.Dispose(); $b.Dispose() }
}

$report = [pscustomobject]@{
    test = 'K1 防截屏（GDI + Windows.Graphics.Capture）'
    date = (Get-Date).ToString('s')
    exe = $Exe
    os = [System.Environment]::OSVersion.VersionString
    screen = "$([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width)x$([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height)"
    osProbe = $osProbe
    wgc = $wgcSamples
    wgcDiffOnVsOff = $wgcDiff
    probeDiffOnVsOff = $probeDiffs
    privacyOn = $onSamples
    privacyOff = $offSamples
    passed = ($failures.Count -eq 0)
    failures = $failures
}
$report | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $Out 'report.json') -Encoding UTF8
Write-Host ("K1 GDI 防截屏：on={0} off={1} OS探针排除={2} 通过={3}" -f $onSamples.Count, $offSamples.Count, $osProbe.excluded, $report.passed)
if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Host ("  FAIL: " + $_) } }
if (-not $report.passed) { exit 1 }
