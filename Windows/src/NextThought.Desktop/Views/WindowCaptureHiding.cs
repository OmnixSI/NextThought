using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

/// <summary>
/// Keeps floating assistant windows out of screen shares and recordings
/// (WDA_EXCLUDEFROMCAPTURE, Windows 10 2004+). Returns false on older systems so
/// callers can keep the window visible rather than failing.
/// </summary>
public static class WindowCaptureHiding
{
    private const uint WdaExcludeFromCapture = 0x00000011;
    private const uint WdaNone = 0;

    /// <summary>C18: the setting all windows read when they open.</summary>
    public static bool PrivacyEnabled { get; set; } = true;

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

    /// <summary>C18: apply the capture-privacy setting; false restores normal visibility.</summary>
    public static bool Apply(Window window, bool enabled)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return false;
        return SetWindowDisplayAffinity(handle, enabled ? WdaExcludeFromCapture : WdaNone);
    }

    private static DispatcherTimer? tracker;

    /// <summary>C18: keep applying the current privacy setting to every window, including new ones.</summary>
    public static void StartTracking()
    {
        if (tracker is not null) return;
        tracker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        tracker.Tick += (_, _) => ApplyAll(PrivacyEnabled);
        tracker.Start();
        ApplyAll(PrivacyEnabled);
    }

    /// <summary>C18: apply to every open window of the app.</summary>
    public static void ApplyAll(bool enabled)
    {
        PrivacyEnabled = enabled;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime app) return;
        foreach (var window in app.Windows) Apply(window, enabled);
    }
}
