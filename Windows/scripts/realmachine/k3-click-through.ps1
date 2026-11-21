# K3 鼠标穿透实机测试（任务书 §9 K3 + 裁决 §6）
# 演示模式能显示提词岛（capture-hide 自检打开真实的 CueIslandWindow）。岛窗口 Topmost，默认
# click-through；指针在岛面板/按钮上时才接管点击。
#   场景 A「收起/透明区域」：把光标放到岛窗口四角（不在面板上），模拟点击 → 下方 underlay 窗口
#   必须收到（k3-underlay.ps1 记录 WM_LBUTTONDOWN）。
#   场景 B「面板收到」：把光标移到岛面板上，等悬停展开（0.22s）后点击 → underlay 不应收到，
#   岛区域像素应发生变化（面板动作/展开）。
# 每次岛窗口生命周期很短（capture-hide 自检约 1–2 s），所以脚本按场景重试最多 8 次。
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = (Join-Path $PSScriptRoot '..\..\validation\realmachine\K3')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class K3Native
{
    public delegate bool EnumProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public sealed class WindowInfo
    {
        public long Handle;
        public int Left, Top, Right, Bottom;
        public string Title = "";
    }

    public static List<WindowInfo> AllWindows()
    {
        var windows = new List<WindowInfo>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            RECT rect; if (!GetWindowRect(handle, out rect)) return true;
            if (rect.Right - rect.Left <= 1 || rect.Bottom - rect.Top <= 1) return true;
            var title = new StringBuilder(256); GetWindowText(handle, title, 256);
            windows.Add(new WindowInfo { Handle = handle.ToInt64(), Left = rect.Left, Top = rect.Top,
                Right = rect.Right, Bottom = rect.Bottom, Title = title.ToString() });
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

function Shot([string]$Path) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size) } finally { $graphics.Dispose() }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    return $bitmap
}
function RegionDiff([System.Drawing.Bitmap]$A, [System.Drawing.Bitmap]$B, $Rect) {
    $sum = 0.0; $count = 0
    $w = [Math]::Min($A.Width, $B.Width); $h = [Math]::Min($A.Height, $B.Height)
    for ($y = [Math]::Max(0, $Rect.Top); $y -lt [Math]::Min($h, $Rect.Bottom); $y += 3) {
        for ($x = [Math]::Max(0, $Rect.Left); $x -lt [Math]::Min($w, $Rect.Right); $x += 3) {
            $ca = $A.GetPixel($x, $y); $cb = $B.GetPixel($x, $y)
            $sum += [Math]::Abs($ca.R - $cb.R) + [Math]::Abs($ca.G - $cb.G) + [Math]::Abs($ca.B - $cb.B)
            $count++
        }
    }
    if ($count -eq 0) { return 0 }
    return [Math]::Round($sum / (3 * $count), 2)
}
function Find-Window([string]$TitleLike, [int]$TimeoutMs = 8000) {
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    while ((Get-Date) -lt $deadline) {
        $match = [K3Native]::AllWindows() | Where-Object { $_.Title -like "*$TitleLike*" } | Select-Object -First 1
        if ($match) { return $match }
        Start-Sleep -Milliseconds 60
    }
    return $null
}
function Click-LogCount([string]$Path) {
    if (-not (Test-Path $Path)) { return 0 }
    return @(Get-Content $Path -ErrorAction SilentlyContinue).Count
}

New-Item -ItemType Directory -Path $Out -Force | Out-Null
$Underlay = Join-Path $PSScriptRoot 'k3-underlay.ps1'
$underlayLog = Join-Path $Out 'underlay-clicks.log'
$steps = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[string]
foreach ($process in [System.Diagnostics.Process]::GetProcessesByName('NextThought')) { try { $process.Kill() } catch { } }
$oldData = $env:NEXTTHOUGHT_DATA_DIR
$underlayOut = Join-Path $Out 'underlay.out.txt'
$underlayErr = Join-Path $Out 'underlay.err.txt'
$underlay = Start-Process -FilePath 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Underlay, '-LogPath', $underlayLog, '-Seconds', '240') `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput $underlayOut -RedirectStandardError $underlayErr
Start-Sleep -Seconds 2
try {
    # ---------- 场景 A：透明区域点击穿透 ----------
    $scenarioA = $false
    for ($attempt = 1; $attempt -le 8 -and -not $scenarioA; $attempt++) {
        $dataDir = Join-Path $env:TEMP ('nt-k3-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
        $env:NEXTTHOUGHT_DATA_DIR = $dataDir
        $app = Start-Process -FilePath $Exe -ArgumentList @('--demo', '--self-test=capture-hide') -PassThru
        try {
            $island = Find-Window -TitleLike '提词岛' -TimeoutMs 6000
            if (-not $island) { continue }
            # 光标先移到远处，再逐个尝试岛窗口四角的透明区域（不在面板/按钮上）。
            [K3Native]::SetCursorPos(20, 700)
            Start-Sleep -Milliseconds 80
            $left = [int]$island.Left; $top = [int]$island.Top
            $right = [int]$island.Right; $bottom = [int]$island.Bottom
            $candidates = @(
                @(($left + 12), ($top + 12)),
                @(($right - 12), ($bottom - 12)),
                @(($left + 12), ($bottom - 12)),
                @(($right - 12), ($top + 12))
            )
            $hit = $null
            foreach ($point in $candidates) {
                $before = Click-LogCount $underlayLog
                [K3Native]::Click([int]$point[0], [int]$point[1])
                Start-Sleep -Milliseconds 140
                $after = Click-LogCount $underlayLog
                if ($after -gt $before) { $hit = $point; break }
            }
            $afterFinal = Click-LogCount $underlayLog
            $detail = "attempt=$attempt island=($left,$top)-$right,$bottom hitPoint=$($hit -join ',') underlayClicks=0->$afterFinal"
            $steps.Add([pscustomobject]@{ scenario = 'A 收起/透明区域点击穿透'; passed = ($null -ne $hit); detail = $detail })
            if ($null -ne $hit) { $scenarioA = $true; Shot (Join-Path $Out 'scenario-a-pass-through.png') | Out-Null }
        } finally {
            if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
            if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
        }
        Start-Sleep -Milliseconds 300
    }
    if (-not $scenarioA) { $failures.Add('场景 A：透明区域点击没有落到下层窗口') }

    # ---------- 场景 B：面板收到点击 ----------
    $scenarioB = $false
    for ($attempt = 1; $attempt -le 8 -and -not $scenarioB; $attempt++) {
        $dataDir = Join-Path $env:TEMP ('nt-k3-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
        $env:NEXTTHOUGHT_DATA_DIR = $dataDir
        $app = Start-Process -FilePath $Exe -ArgumentList @('--demo', '--self-test=capture-hide') -PassThru
        try {
            $island = Find-Window -TitleLike '提词岛' -TimeoutMs 6000
            if (-not $island) { continue }
            $rect = [pscustomobject]@{ Left = [int]$island.Left; Top = [int]$island.Top; Right = [int]$island.Right; Bottom = [int]$island.Bottom }
            $centerX = [int](($rect.Left + $rect.Right) / 2)
            $panelY = [int]$rect.Top + [Math]::Min(26, [int](($rect.Bottom - $rect.Top) / 2))
            # 悬停到面板上，等 0.22 s 的展开动画后再点击
            [K3Native]::SetCursorPos($centerX, $panelY)
            Start-Sleep -Milliseconds 420
            $beforeBitmap = Shot (Join-Path $Out ("scenario-b-before-a$attempt.png"))
            $beforeClicks = Click-LogCount $underlayLog
            [K3Native]::Click($centerX, $panelY)
            Start-Sleep -Milliseconds 260
            $afterBitmap = Shot (Join-Path $Out ("scenario-b-after-a$attempt.png"))
            $afterClicks = Click-LogCount $underlayLog
            $diff = RegionDiff $beforeBitmap $afterBitmap $rect
            $beforeBitmap.Dispose(); $afterBitmap.Dispose()
            $passed = ($afterClicks -eq $beforeClicks) -and ($diff -ge 1.0)
            $detail = "attempt=$attempt island=($($rect.Left),$($rect.Top))-$($rect.Right),$($rect.Bottom) click=($centerX,$panelY) underlayClicks=$beforeClicks->$afterClicks islandDiff=$diff"
            $steps.Add([pscustomobject]@{ scenario = 'B 展开/面板收到点击'; passed = $passed; detail = $detail })
            if ($passed) { $scenarioB = $true }
        } finally {
            if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
            if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
        }
        Start-Sleep -Milliseconds 300
    }
    if (-not $scenarioB) { $failures.Add('场景 B：面板点击没有被岛收到（或岛区域没有变化）') }

    $underlayInfo = [pscustomobject]@{
        processAlive = ($underlay -and -not $underlay.HasExited)
        logExists = (Test-Path $underlayLog)
        logLines = (Click-LogCount $underlayLog)
        stdout = if (Test-Path $underlayOut) { (Get-Content $underlayOut -Raw -ErrorAction SilentlyContinue) } else { '' }
        stderr = if (Test-Path $underlayErr) { (Get-Content $underlayErr -Raw -ErrorAction SilentlyContinue) } else { '' }
    }
} finally {
    if ($underlay -and -not $underlay.HasExited) { Stop-Process -Id $underlay.Id -Force -ErrorAction SilentlyContinue }
    $env:NEXTTHOUGHT_DATA_DIR = $oldData
}
$report = [pscustomobject]@{
    test = 'K3 鼠标穿透'
    date = (Get-Date).ToString('s')
    exe = $Exe
    os = [System.Environment]::OSVersion.VersionString
    underlayLog = $underlayLog
    underlay = $underlayInfo
    steps = $steps
    passed = ($failures.Count -eq 0)
    failures = $failures
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $Out 'report.json') -Encoding UTF8
Write-Host ("K3 鼠标穿透：steps={0} 通过={1}" -f $steps.Count, $report.passed)
$failures | ForEach-Object { Write-Host ("  FAIL: " + $_) }
if (-not $report.passed) { exit 1 }
