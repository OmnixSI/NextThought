using System.Runtime.InteropServices;
using System.Text;

namespace NextThought.Platform;

/// <summary>
/// Best-effort Windows screen-sharing detector. macOS 15.4 cannot keep app windows out of
/// ScreenCaptureKit, so the Windows design also retracts the cue island while a known
/// meeting app is sharing the screen instead of relying on capture exclusion alone.
/// </summary>
public static class ScreenShareDetector
{
    private static readonly string[] TitleMarkers =
    [
        "screen sharing", "sharing screen", "screen share", "is sharing", "sharing your screen",
        "共享屏幕", "共享中", "正在共享", "屏幕共享"
    ];
    private static readonly string[] ClassMarkers = ["ZPShareWnd", "TeamsShareWindow", "WebexShare", "ShareWnd"];

    /// <summary>True when visible top-level windows look like a meeting app's share window.</summary>
    public static bool IsSharingActive()
    {
        var found = false;
        try
        {
            EnumWindows((handle, _) =>
            {
                if (!IsWindowVisible(handle)) return true;
                var title = Title(handle);
                if (IsSharingTitle(title, ClassName(handle)))
                {
                    found = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
        return found;
    }

    /// <summary>Pure predicate used by tests and by the window walker.</summary>
    public static bool IsSharingTitle(string? title, string? className)
    {
        var text = (title ?? "").Trim();
        if (text.Contains("NextThought", StringComparison.OrdinalIgnoreCase)) return false;
        var cls = className ?? "";
        foreach (var marker in ClassMarkers)
            if (cls.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
        if (cls.Contains("ScreenShare", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Length == 0) return false;
        var lower = text.ToLowerInvariant();
        foreach (var marker in TitleMarkers)
            if (lower.Contains(marker, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string Title(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0) return "";
        var buffer = new StringBuilder(length + 1);
        GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string ClassName(IntPtr handle)
    {
        var buffer = new StringBuilder(256);
        GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
}
