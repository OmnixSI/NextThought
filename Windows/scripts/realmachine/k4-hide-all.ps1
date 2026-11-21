# K4 关闭与隐藏实机测试（任务书 §9 K4）— 本机可验证的部分
# 演示模式没有加载语音模型，无法开一场真实会议，所以「Alt+F4 关提词岛→提问→岛重现」这半
# 需要人在真实会议里做（RELEASE.md §9 K4 有步骤）。本脚本验证可验证的另一半：
#   1) 打开提词浮窗后按「隐藏所有窗口」（Ctrl+Alt+G）：主窗口/浮窗全部隐藏，应用不退出；
#   2) 再按一次：窗口全部恢复。
# 证据：validation/realmachine/K4/report.json + 截图。
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = (Join-Path $PSScriptRoot '..\..\validation\realmachine\K4')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class K4Native
{
    public delegate bool EnumProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr handle, StringBuilder name, int count);

    public static List<string> VisibleTitles(uint processId)
    {
        var titles = new List<string>();
        EnumWindows((handle, _) =>
        {
            uint pid; GetWindowThreadProcessId(handle, out pid);
            if (pid != processId || !IsWindowVisible(handle)) return true;
            var text = new StringBuilder(256); GetWindowText(handle, text, 256);
            if (text.Length > 0) titles.Add(text.ToString());
            return true;
        }, IntPtr.Zero);
        return titles;
    }
}
"@

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class K4Input
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KBD { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSE { public int X; public int Y; public uint Data; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public KBD Key; [FieldOffset(8)] public MOUSE Mouse; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    public static void Chord(ushort[] modifiers, ushort key)
    {
        var list = new System.Collections.Generic.List<INPUT>();
        foreach (var modifier in modifiers)
            list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = modifier } });
        list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = key } });
        list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = key, Flags = 0x0002 } });
        for (var index = modifiers.Length - 1; index >= 0; index--)
            list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = modifiers[index], Flags = 0x0002 } });
        var inputs = list.ToArray();
        SendInput((uint)inputs.Length, inputs, 40);
    }
}
"@

function Shot([string]$Path) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size) } finally { $graphics.Dispose() }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

New-Item -ItemType Directory -Path $Out -Force | Out-Null
$steps = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[string]
$app = $null
$oldData = $env:NEXTTHOUGHT_DATA_DIR
$dataDir = Join-Path $env:TEMP ('nt-k4-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$env:NEXTTHOUGHT_DATA_DIR = $dataDir
try {
    $app = Start-Process -FilePath $Exe -ArgumentList @('--demo') -PassThru
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline) {
        $titles = [K4Native]::VisibleTitles([uint32]$app.Id)
        if ($titles.Count -gt 0) { break }
        Start-Sleep -Milliseconds 150
    }
    $initial = [K4Native]::VisibleTitles([uint32]$app.Id)
    $steps.Add([pscustomobject]@{ step = '应用启动、主窗口可见'; ok = ($initial.Count -ge 1); detail = ($initial -join ' | ') })

    # 1) 打开提词浮窗（Ctrl+Alt+O）
    Start-Sleep -Milliseconds 800
    [K4Input]::Chord([uint16[]]@(0x11, 0x12), 0x4F)
    Start-Sleep -Milliseconds 1200
    $withTeleprompter = [K4Native]::VisibleTitles([uint32]$app.Id)
    $steps.Add([pscustomobject]@{ step = '提词浮窗打开'; ok = ($withTeleprompter -match '回答浮窗').Count -gt 0; detail = ($withTeleprompter -join ' | ') })
    Shot (Join-Path $Out 'before-hide.png')

    # 2) 隐藏所有窗口（Ctrl+Alt+G）
    [K4Input]::Chord([uint16[]]@(0x11, 0x12), 0x47)
    Start-Sleep -Milliseconds 1200
    $hidden = [K4Native]::VisibleTitles([uint32]$app.Id)
    $steps.Add([pscustomobject]@{ step = '隐藏所有窗口后无可见应用窗口'; ok = ($hidden.Count -eq 0); detail = ($hidden -join ' | ') })
    $steps.Add([pscustomobject]@{ step = '隐藏后应用进程仍在'; ok = (-not $app.HasExited); detail = "exited=$($app.HasExited)" })
    Shot (Join-Path $Out 'after-hide.png')

    # 3) 再按一次恢复
    [K4Input]::Chord([uint16[]]@(0x11, 0x12), 0x47)
    Start-Sleep -Milliseconds 1200
    $restored = [K4Native]::VisibleTitles([uint32]$app.Id)
    $steps.Add([pscustomobject]@{ step = '再次按键后窗口恢复'; ok = ($restored.Count -ge 1); detail = ($restored -join ' | ') })
    Shot (Join-Path $Out 'after-restore.png')
} catch {
    $failures.Add($_.Exception.Message)
} finally {
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    $env:NEXTTHOUGHT_DATA_DIR = $oldData
    if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
}

foreach ($step in $steps) { if (-not $step.ok) { $failures.Add($step.step) } }
$report = [pscustomobject]@{
    test = 'K4 关闭与隐藏（可验证部分：隐藏所有窗口）'
    date = (Get-Date).ToString('s')
    exe = $Exe
    os = [System.Environment]::OSVersion.VersionString
    steps = $steps
    partial = 'Alt+F4 关提词岛后提问重现需要真实会议（本机 demo 无语音模型，见 README/RELEASE.md §9 K4）'
    passed = ($failures.Count -eq 0 -and $steps.Count -ge 5)
    failures = $failures
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $Out 'report.json') -Encoding UTF8
Write-Host ("K4 隐藏所有窗口：steps={0} 通过={1}" -f $steps.Count, $report.passed)
$failures | ForEach-Object { Write-Host ("  FAIL: " + $_) }
if (-not $report.passed) { exit 1 }
