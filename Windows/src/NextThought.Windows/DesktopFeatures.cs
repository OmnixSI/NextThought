using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NextThought.Windows;

internal sealed class DesktopFeatures : IDisposable
{
    private readonly Window owner;
    private readonly Func<string> content;
    private readonly Action record;
    private HwndSource? source;
    private IconData icon;
    private Window? overlay;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    public DesktopFeatures(Window owner, Func<string> content, Action record)
    {
        this.owner = owner; this.content = content; this.record = record;
        var handle = new WindowInteropHelper(owner).EnsureHandle(); source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
        icon = new() { Size = (uint)Marshal.SizeOf<IconData>(), Window = handle, Id = 1, Flags = 7, Message = 0x8001, Icon = LoadIcon(IntPtr.Zero, new IntPtr(32512)), Tip = "NextThought · 双击恢复 · 右键菜单", Info = "", InfoTitle = "" };
        if (!Shell_NotifyIcon(0, ref icon)) throw new InvalidOperationException("无法创建通知区域图标。");
        foreach (var (id, key) in new[] { (1, 0x4E), (2, 0x4F), (3, 0x52) })
            if (!RegisterHotKey(handle, id, 0x4003, (uint)key)) HotkeyWarning = "部分 Ctrl+Alt+N/O/R 快捷键已被其他应用占用；请使用界面按钮。";
        timer.Tick += (_, _) => { if (overlay?.Content is ScrollViewer { Content: TextBox box }) box.Text = content(); }; timer.Start();
    }
    public string? HotkeyWarning { get; }
    public void Restore() { owner.Show(); if (owner.WindowState == WindowState.Minimized) owner.WindowState = WindowState.Normal; owner.Activate(); }
    public void ToggleOverlay()
    {
        if (overlay is not null) { overlay.Close(); return; }
        var window = new Window { Title = "NextThought 悬浮记录", Width = 460, Height = 350, MinWidth = 300, MinHeight = 150, Topmost = true, ShowInTaskbar = false,
            Content = new ScrollViewer { Content = new TextBox { Text = content(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(14), BorderThickness = new Thickness(0) } } };
        overlay = window; window.Closed += (_, _) => { if (ReferenceEquals(overlay, window)) overlay = null; }; window.Show();
    }
    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312) { handled = true; if (wParam.ToInt32() == 1) Restore(); else if (wParam.ToInt32() == 2) ToggleOverlay(); else if (wParam.ToInt32() == 3) record(); }
        if (message == 0x8001)
        {
            if (lParam.ToInt32() == 0x203) Restore();
            else if (lParam.ToInt32() == 0x205)
            {
                var menu = new ContextMenu();
                foreach (var (label, action) in new (string, Action)[] { ("打开工作台", Restore), ("切换悬浮窗", ToggleOverlay), ("退出", owner.Close) })
                { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
                menu.IsOpen = true;
            }
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        timer.Stop(); overlay?.Close(); Shell_NotifyIcon(2, ref icon);
        for (int i = 1; i <= 3; i++) UnregisterHotKey(icon.Window, i);
        source?.RemoveHook(Hook); source = null;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct IconData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, Message; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint operation, ref IconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
