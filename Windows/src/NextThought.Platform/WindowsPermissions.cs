using System.Diagnostics;
using Microsoft.Win32;

namespace NextThought.Platform;

/// <summary>
/// E29: Windows privacy permissions for the settings page (Mac PermissionSettingsSection). On
/// macOS the app asks for 录屏 / 麦克风 / 摄像头; on Windows the screen capture path needs no
/// per-app privacy grant in this port, so the settings page only shows microphone and camera,
/// and unknown states read as "the system will ask when a feature needs it".
/// </summary>
public static class WindowsPermissions
{
    public const string MicrophoneSettingsUri = "ms-settings:privacy-microphone";
    public const string CameraSettingsUri = "ms-settings:privacy-webcam";

    private const string ConsentRoot =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public enum PermissionState { Unknown, Allowed, Denied }

    public static PermissionState Microphone() => ReadConsent("microphone");

    public static PermissionState Camera() => ReadConsent("webcam");

    /// <summary>
    /// Root <c>Value</c> is the machine-wide choice; desktop apps also get a
    /// <c>NonPackaged</c> child per executable. Deny anywhere wins, then Allow.
    /// </summary>
    public static PermissionState ReadConsent(string capability)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(PathFor(capability));
            if (root is null) return PermissionState.Unknown;
            var values = new List<string?> { root.GetValue("Value") as string };
            using (var nonPackaged = root.OpenSubKey("NonPackaged"))
            {
                if (nonPackaged is not null)
                {
                    foreach (var name in nonPackaged.GetSubKeyNames())
                    {
                        using var entry = nonPackaged.OpenSubKey(name);
                        values.Add(entry?.GetValue("Value") as string);
                    }
                }
            }
            return Classify(values);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return PermissionState.Unknown;
        }
    }

    /// <summary>Pure classification so the consent policy is testable without touching the registry.</summary>
    public static PermissionState Classify(IEnumerable<string?> values)
    {
        var seen = PermissionState.Unknown;
        foreach (var value in values)
        {
            if (string.Equals(value, "Deny", StringComparison.OrdinalIgnoreCase)) return PermissionState.Denied;
            if (string.Equals(value, "Allow", StringComparison.OrdinalIgnoreCase)) seen = PermissionState.Allowed;
        }
        return seen;
    }

    public static bool OpenMicrophoneSettings() => Open(MicrophoneSettingsUri);

    public static bool OpenCameraSettings() => Open(CameraSettingsUri);

    /// <summary>Opens a Windows Settings deep link (Mac opens x-apple.systempreferences anchors).</summary>
    public static bool Open(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string PathFor(string capability) => ConsentRoot + "\\" + capability;
}
