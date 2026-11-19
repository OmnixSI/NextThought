using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace NextThought.Desktop.Views;

/// <summary>
/// Keeps floating assistant windows out of screen shares and recordings
/// (WDA_EXCLUDEFROMCAPTURE, Windows 10 2004+). Returns false on older systems so
/// callers can keep the window visible rather than failing.
/// </summary>
public static class WindowCaptureHiding
{
    private const uint WdaExcludeFromCapture = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);

    /// <summary>Excludes the window from capture; true when the system accepted it.</summary>
    public static bool Hide(Window window)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return false;
        return SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
    }

    /// <summary>Reads the current affinity, for the self-check and diagnostics.</summary>
    public static uint Affinity(Window window)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return 0;
        return GetWindowDisplayAffinity(handle, out var affinity) ? affinity : 0;
    }

    public static bool IsHidden(Window window) => Affinity(window) == WdaExcludeFromCapture;
}
