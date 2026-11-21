using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>E13 / §6.7: 共享可见性测试 — shows the full-screen capture with this app's own windows.</summary>
public partial class ShareTestWindow : Window
{
    public ShareTestWindow() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.ClearShareTestImage();
        Close();
    }
}
