using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>E23: Mac ManagedModelRow — one document component with 安装 / 校验／修复 / 删除.</summary>
public partial class ManagedModelRow : UserControl
{
    public ManagedModelRow() => InitializeComponent();

    private async void Install_Click(object? sender, RoutedEventArgs e) => await Run("install");

    private async void Verify_Click(object? sender, RoutedEventArgs e) => await Run("verify");

    private async void Delete_Click(object? sender, RoutedEventArgs e) => await Run("delete");

    private async Task Run(string action)
    {
        if (Tag is not AppState state || DataContext is not DocumentComponentRow row) return;
        await state.RunDocumentComponentActionAsync(row, action);
    }
}
