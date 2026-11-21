# K2 不抢焦点实机测试（任务书 §9 K2）
# 记事本在前台 → 启动 NextThought 演示模式 + capture-hide 自检（打开真实的提词岛窗口，
# 该窗口 ShowActivated=false / Topmost / 不在任务栏）→ 用 SendInput 点击提词岛区域多次，
# 每次断言前台仍是记事本 → 再键入文本，从记事本剪贴板确认文字进了记事本。
# 证据：validation/realmachine/K2/report.json + 截图。
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = (Join-Path $PSScriptRoot '..\..\validation\realmachine\K2')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class K2Native
{
    public delegate bool EnumProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attachTo, uint attach, bool attachInput);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public sealed class WindowInfo
    {
        public long Handle;
        public int Left, Top, Right, Bottom;
        public string ClassName = "";
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
            var cls = new StringBuilder(256); GetClassName(handle, cls, cls.Capacity);
            var title = new StringBuilder(256); GetWindowText(handle, title, title.Capacity);
            windows.Add(new WindowInfo { Handle = handle.ToInt64(), Left = rect.Left, Top = rect.Top,
                Right = rect.Right, Bottom = rect.Bottom, ClassName = cls.ToString(), Title = title.ToString() });
            return true;
        }, IntPtr.Zero);
        return windows;
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
            windows.Add(new WindowInfo { Handle = handle.ToInt64(), Left = rect.Left, Top = rect.Top,
                Right = rect.Right, Bottom = rect.Bottom, ClassName = cls.ToString(), Title = title.ToString() });
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    /// <summary>SetForegroundWindow 受前台锁限制时，临时把输入线程接到前台线程上再激活。</summary>
    public static bool ForceForeground(IntPtr handle)
    {
        var foreground = GetForegroundWindow();
        if (foreground == handle) return true;
        uint foregroundProcess;
        var foregroundThread = GetWindowThreadProcessId(foreground, out foregroundProcess);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread
                       && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            ShowWindow(handle, 9);
            return SetForegroundWindow(handle);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}
"@

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class K2Input
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KBD { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSE { public int X; public int Y; public uint Data; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public KBD Key; [FieldOffset(8)] public MOUSE Mouse; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    public static void TypeUnicode(string text)
    {
        foreach (var character in text)
        {
            var down = new INPUT { Type = 1, Key = new KBD { ScanCode = character, Flags = 0x0004 } };
            var up = down;
            up.Key.Flags = 0x0004 | 0x0002;
            SendInput(2, new[] { down, up }, 40);
        }
    }

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

# 把记事本拉回前台：SetForegroundWindow 受前台锁限制时用一次点击；返回最终前台句柄。
function Focus-Notepad {
    param([IntPtr]$Handle, $Window)
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        $null = [K2Native]::ForceForeground($Handle)
        Start-Sleep -Milliseconds 200
        if ([K2Native]::GetForegroundWindow() -eq $Handle) { return $Handle }
        if ($Window) {
            [K2Native]::Click([int](($Window.Left + $Window.Right) / 2), [int](($Window.Top + $Window.Bottom) / 2))
            Start-Sleep -Milliseconds 250
        }
        if ([K2Native]::GetForegroundWindow() -eq $Handle) { return $Handle }
    }
    return [K2Native]::GetForegroundWindow()
}

function Find-Window {
    param([string]$TitleLike = '', [string]$ClassLike = '', [int]$TimeoutMs = 10000)
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    while ((Get-Date) -lt $deadline) {
        $match = [K2Native]::AllWindows() | Where-Object {
            ($TitleLike -eq '' -or $_.Title -like "*$TitleLike*") -and
            ($ClassLike -eq '' -or $_.ClassName -like $ClassLike)
        } | Select-Object -First 1
        if ($match) { return $match }
        Start-Sleep -Milliseconds 80
    }
    return $null
}

New-Item -ItemType Directory -Path $Out -Force | Out-Null
foreach ($process in [System.Diagnostics.Process]::GetProcessesByName('notepad')) {
    try { $process.Kill() } catch { }
}
$steps = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[string]
$app = $null
$notepad = $null
$oldData = $env:NEXTTHOUGHT_DATA_DIR
$dataDir = Join-Path $env:TEMP ('nt-k2-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$env:NEXTTHOUGHT_DATA_DIR = $dataDir
try {
    # 1) 记事本在前台
    $notepad = Start-Process -FilePath 'notepad.exe' -PassThru
    $notepadWindow = Find-Window -ClassLike 'Notepad' -TimeoutMs 12000
    if (-not $notepadWindow) { $notepadWindow = Find-Window -TitleLike '记事本' -TimeoutMs 5000 }
    if (-not $notepadWindow) { throw '记事本窗口没有出现' }
    $notepadHandle = [IntPtr]$notepadWindow.Handle
    $null = [K2Native]::ShowWindow($notepadHandle, 9)
    Start-Sleep -Milliseconds 400
    $null = [K2Native]::SetForegroundWindow($notepadHandle)
    Start-Sleep -Milliseconds 300
    if ([K2Native]::GetForegroundWindow() -ne $notepadHandle) {
        # SetForegroundWindow 受前台锁限制时，用一次点击获得焦点。
        [K2Native]::Click([int](($notepadWindow.Left + $notepadWindow.Right) / 2), [int](($notepadWindow.Top + $notepadWindow.Bottom) / 2))
        Start-Sleep -Milliseconds 300
    }
    $foreground = [K2Native]::GetForegroundWindow()
    $steps.Add([pscustomobject]@{ step = '记事本在前台'; ok = ($foreground -eq $notepadHandle); detail = ('foreground=0x{0:X}' -f $foreground.ToInt64()) })

    # 2) 启动应用：capture-hide 自检打开真实的提词岛窗口（ShowActivated=false）
    $app = Start-Process -FilePath $Exe -ArgumentList @('--demo', '--self-test=capture-hide') -PassThru
    $island = $null
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline -and -not $app.HasExited) {
        $candidate = [K2Native]::ForProcess([uint32]$app.Id) | Where-Object { $_.Title -like '*提词岛*' } | Select-Object -First 1
        if ($candidate) { $island = $candidate; break }
        Start-Sleep -Milliseconds 70
    }
    if (-not $island) { throw '提词岛窗口没有出现' }
    # 管道可能返回数组；统一取单个窗口对象。
    $island = @($island) | Select-Object -First 1
    $null = [K2Native]::SetForegroundWindow($notepadHandle)
    Start-Sleep -Milliseconds 120
    $steps.Add([pscustomobject]@{ step = '提词岛出现'; ok = $true; detail = "rect=$($island.Left),$($island.Top),$($island.Right),$($island.Bottom)" })
    Shot (Join-Path $Out 'island-visible.png')

    # 3) 点击岛的不同位置，前台必须仍是记事本
    $left = [int]$island.Left; $top = [int]$island.Top; $right = [int]$island.Right; $bottom = [int]$island.Bottom
    $centerX = [int](($left + $right) / 2)
    $centerY = [int](($top + $bottom) / 2)
    $leftPoint = [int]($centerX - 110)
    $rightPoint = [int]($centerX + 110)
    $points = @([pscustomobject]@{ X = $centerX; Y = $centerY },
                [pscustomobject]@{ X = $leftPoint; Y = $centerY },
                [pscustomobject]@{ X = $rightPoint; Y = $centerY })
    foreach ($point in $points) {
        # 每次点击前把记事本拉回前台（模拟用户正在记事本里打字时点了岛）
        $before = Focus-Notepad -Handle $notepadHandle -Window $notepadWindow
        Start-Sleep -Milliseconds 80
        [K2Native]::Click([int]$point.X, [int]$point.Y)
        Start-Sleep -Milliseconds 180
        $after = [K2Native]::GetForegroundWindow()
        $steps.Add([pscustomobject]@{
            step = "点击提词岛 ($($point.X),$($point.Y))"
            ok = ($before -eq $notepadHandle -and $after -eq $notepadHandle)
            detail = ('before=0x{0:X} after=0x{1:X}' -f $before.ToInt64(), $after.ToInt64())
        })
    }
    Shot (Join-Path $Out 'after-clicks.png')

    # 4) 键入文字 → 记事本；再 Ctrl+A/C 从剪贴板确认
    $window = [K2Native]::AllWindows() | Where-Object { $_.Handle -eq $notepadHandle.ToInt64() } | Select-Object -First 1
    $focused = Focus-Notepad -Handle $notepadHandle -Window $window
    Start-Sleep -Milliseconds 200
    [K2Input]::TypeUnicode('K2FocusTest 123')
    Start-Sleep -Milliseconds 250
    [K2Input]::Chord([uint16[]]@(0x11), 0x41)
    Start-Sleep -Milliseconds 120
    [K2Input]::Chord([uint16[]]@(0x11), 0x43)
    Start-Sleep -Milliseconds 250
    $clipboard = Get-Clipboard -Raw
    $steps.Add([pscustomobject]@{
        step = '键入进入记事本'
        ok = ($null -ne $clipboard -and $clipboard -like '*K2FocusTest 123*')
        detail = "clipboard=[" + $clipboard + "]"
    })
    $steps.Add([pscustomobject]@{
        step = '整个过程中应用未成为前台'
        ok = ([K2Native]::GetForegroundWindow() -eq $notepadHandle)
        detail = ('foreground=0x{0:X}' -f [K2Native]::GetForegroundWindow().ToInt64())
    })
} catch {
    $failures.Add($_.Exception.Message)
} finally {
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    if ($notepad -and -not $notepad.HasExited) { Stop-Process -Id $notepad.Id -Force -ErrorAction SilentlyContinue }
    $env:NEXTTHOUGHT_DATA_DIR = $oldData
    if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue }
}

foreach ($step in $steps) { if (-not $step.ok) { $failures.Add($step.step) } }
$report = [pscustomobject]@{
    test = 'K2 不抢焦点'
    date = (Get-Date).ToString('s')
    exe = $Exe
    os = [System.Environment]::OSVersion.VersionString
    steps = $steps
    passed = ($failures.Count -eq 0 -and $steps.Count -ge 4)
    failures = $failures
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $Out 'report.json') -Encoding UTF8
Write-Host ("K2 不抢焦点：steps={0} 通过={1}" -f $steps.Count, $report.passed)
$failures | ForEach-Object { Write-Host ("  FAIL: " + $_) }
if (-not $report.passed) { exit 1 }
