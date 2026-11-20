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
        Opened += (_, _) =>
        {
            WindowCaptureHiding.Apply(this, WindowCaptureHiding.PrivacyEnabled);
            ApplyNonActivatingStyle();
        };
    }

    public event Action? RestoreRequested;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    private void ApplyNonActivatingStyle()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return;
        const int gwlExstyle = -20;
        const long wsExNoActivate = 0x08000000L, wsExToolWindow = 0x00000080L;
        var style = GetWindowLongPtr(handle, gwlExstyle).ToInt64() | wsExNoActivate | wsExToolWindow;
        SetWindowLongPtr(handle, gwlExstyle, new IntPtr(style));
    }

    private void Restore_Click(object? sender, RoutedEventArgs e) => RestoreRequested?.Invoke();

    public void PlaceNear(PixelPoint teleprompterPosition) =>
        Position = new PixelPoint(teleprompterPosition.X + 16, teleprompterPosition.Y + 16);
}
