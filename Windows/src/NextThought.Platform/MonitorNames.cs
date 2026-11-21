using System.Runtime.InteropServices;

namespace NextThought.Platform;

/// <summary>
/// E13 / decisions §3.3: the display picker uses the system's friendly monitor name
/// (EnumDisplayDevices gives the monitor's device string, e.g. "DELL U2720Q"). The Mac picker
/// shows NSScreen.localizedName; Windows has no such API, so the device string is the equivalent.
/// </summary>
public static class MonitorNames
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint deviceNumber, ref DisplayDevice deviceInfo, uint flags);

    private const int AttachedToDesktop = 0x00000001;

    /// <summary>Adapter device name (\\.\DISPLAY1) → monitor friendly name, in adapter order.</summary>
    public static IReadOnlyList<(string Adapter, string Friendly)> FriendlyNames()
    {
        var names = new List<(string, string)>();
        try
        {
            for (uint index = 0; index < 16; index++)
            {
                var adapter = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
                if (!EnumDisplayDevices(null, index, ref adapter, 0)) break;
                if ((adapter.StateFlags & AttachedToDesktop) == 0) continue;
                var monitor = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
                var friendly = EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, 0)
                    ? monitor.DeviceString.Trim()
                    : "";
                if (friendly.Length == 0) friendly = adapter.DeviceString.Trim();
                names.Add((adapter.DeviceName, friendly.Length > 0 ? friendly : adapter.DeviceName));
            }
        }
        catch { }
        return names;
    }
}
