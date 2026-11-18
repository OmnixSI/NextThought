using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NextThought.Desktop;

/// <summary>
/// System tray icon (plan §9 一键隐藏 / baseline WPF tray): restore, hide everything while a
/// meeting keeps recording, start or stop recording, and quit.
/// </summary>
internal sealed class TrayIntegration : IDisposable
{
    private readonly TrayIcon icon;
    private readonly NativeMenu menu = new();
    private readonly NativeMenuItem open = new("打开主窗口");
    private readonly NativeMenuItem hide = new("隐藏所有窗口");
    private readonly NativeMenuItem record = new("开始或停止记录");
    private readonly NativeMenuItem quit = new("退出 NextThought");
    private Action? actions;

    public TrayIntegration(Action show, Action hideAll, Action toggleRecording, Action exit)
    {
        open.Click += (_, _) => show();
        hide.Click += (_, _) => hideAll();
        record.Click += (_, _) => toggleRecording();
        quit.Click += (_, _) => exit();
        menu.Items.Add(open);
        menu.Items.Add(hide);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(record);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);
        icon = new TrayIcon
        {
            ToolTipText = "NextThought · 把讨论留下来",
            Menu = menu,
            IsVisible = true,
            Icon = LoadIcon()
        };
        icon.Clicked += (_, _) => show();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { icon });
        actions = show;
    }

    public void SetRecording(bool recording) => record.Header = recording ? "结束会议" : "开始记录";

    private static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://NextThought/Assets/NextThought.ico");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        icon.IsVisible = false;
        TrayIcon.SetIcons(Application.Current!, new TrayIcons());
        actions = null;
    }
}
