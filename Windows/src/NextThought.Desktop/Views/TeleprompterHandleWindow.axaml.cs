using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NextThought.Platform;

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
        Opened += (_, _) =>
        {
            WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
            ApplyNonActivatingStyle();
        };
    }

    public event Action? RestoreRequested;

private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        const int gwlExstyle = -20;
        const long wsExNoActivate = 0x08000000L, wsExToolWindow = 0x00000080L;
        var style = Win32Native.GetWindowLongPtr(handle, gwlExstyle).ToInt64() | wsExNoActivate | wsExToolWindow;
        Win32Native.SetWindowLongPtr(handle, gwlExstyle, new IntPtr(style));
    }

    private void Restore_Click(object? sender, RoutedEventArgs e) => RestoreRequested?.Invoke();

    public void PlaceNear(PixelPoint teleprompterPosition) =>
        Position = new PixelPoint(teleprompterPosition.X + 16, teleprompterPosition.Y + 16);
}
