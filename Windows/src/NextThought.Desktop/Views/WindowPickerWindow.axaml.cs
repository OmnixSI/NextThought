using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E13: 选择要截图的窗口 (ContentView.swift WindowPickerSheet). Lists the other applications'
/// visible windows; NextThought's own windows are excluded before the list is built.
/// </summary>
public partial class WindowPickerWindow : Window
{
    public WindowPickerWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Load();
    }

    public ScreenCapture.CaptureTarget? Selected { get; private set; }

    private void Load()
    {
        try
        {
            var rows = ScreenCapture.AvailableWindows().Select(target => new CaptureWindowRow(target)).ToList();
            WindowList.ItemsSource = rows;
            StatusText.Text = rows.Count == 0
                ? UiText.T("没有找到可截图的窗口。请先打开要截图的窗口，再点「刷新」。")
                : UiText.Format("%lld 个窗口", rows.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiText.T("读取窗口列表失败：") + ex.Message;
        }
    }

    private void Pick_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CaptureWindowRow row })
        {
            Selected = row.Target;
            Close(true);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}

/// <summary>E13: one row of the window picker.</summary>
public sealed record CaptureWindowRow(ScreenCapture.CaptureTarget Target)
{
    public string Title => Target.DisplayTitle;
    public string OwnerLine => Target.DisplayLine;
}
