using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void RefreshDevices_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        await state.Settings.RefreshDevicesAsync();
        state.SetStatus("声音设备已刷新。");
    }

    private async void CopyCompanion_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || state.CompanionLink.Length == 0) return;
        if (state.CopyText is { } copy) await copy(state.CompanionLink);
        state.SetStatus("配对链接已复制。");
    }

    private void Notices_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        var path = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        if (File.Exists(path)) state.OpenExternal(path);
        else state.SetStatus("未找到第三方许可文件。");
    }
}
