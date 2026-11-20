using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// A small separate HWND shown while the teleprompter is click-through: the transparent
/// teleprompter cannot receive clicks, so this strip offers the way back.
/// </summary>
public partial class TeleprompterHandleWindow : Window
{
    public TeleprompterHandleWindow()
    {
        InitializeComponent();
        Opened += (_, _) => WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
    }

    public event Action? RestoreRequested;

    private void Restore_Click(object? sender, RoutedEventArgs e) => RestoreRequested?.Invoke();

    public void PlaceNear(PixelPoint teleprompterPosition) =>
        Position = new PixelPoint(teleprompterPosition.X + 16, teleprompterPosition.Y + 16);
}
