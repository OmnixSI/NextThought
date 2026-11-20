using System.Runtime.InteropServices;
using NextThought.Core;
using NextThought.Platform;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NextThought.Desktop;

/// <summary>
/// Screen capture with a GDI DIB section (plan §7 IScreenCapture: 整屏/区域用屏幕 DC 复制).
/// Returns an Avalonia bitmap; the caller crops to the selected rectangle and hands the
/// PNG to the material library, where Windows.Media.Ocr reads the text.
/// </summary>
public static class ScreenCapture
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint Srccopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;

    public sealed record Capture(int X, int Y, int Width, int Height, WriteableBitmap Image);

    public static Capture VirtualScreen()
    {
        var x = Win32Native.GetSystemMetrics(SmXVirtualScreen);
        var y = Win32Native.GetSystemMetrics(SmYVirtualScreen);
        var width = Math.Max(1, Win32Native.GetSystemMetrics(SmCxVirtualScreen));
        var height = Math.Max(1, Win32Native.GetSystemMetrics(SmCyVirtualScreen));

        var screenDc = Win32Native.GetDC(IntPtr.Zero);
        var memoryDc = Win32Native.CreateCompatibleDC(screenDc);
        var info = new Win32Native.BitmapInfo
        {
            biSize = Marshal.SizeOf<Win32Native.BitmapInfo>(),
            biWidth = width,
            biHeight = -height, // top-down rows
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0
        };
        var dib = Win32Native.CreateDIBSection(memoryDc, ref info, 0, out var bits, IntPtr.Zero, 0);
        var previous = Win32Native.SelectObject(memoryDc, dib);
        try
        {
            Win32Native.BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, Srccopy | CaptureBlt);
            var stride = width * 4;
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var locked = bitmap.Lock())
            {
                for (var row = 0; row < height; row++)
                {
                    var source = IntPtr.Add(bits, row * stride);
                    var destination = IntPtr.Add(locked.Address, row * locked.RowBytes);
                    Win32Native.CopyMemory(destination, source, (nuint)stride);
                }
            }
            return new Capture(x, y, width, height, bitmap);
        }
        finally
        {
            Win32Native.SelectObject(memoryDc, previous);
            Win32Native.DeleteObject(dib);
            Win32Native.DeleteDC(memoryDc);
            Win32Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>The foreground window's rectangle in virtual-screen coordinates (window capture).</summary>
    public static PixelRect ForegroundWindowBounds()
    {
        var window = Win32Native.GetForegroundWindow();
        if (window == IntPtr.Zero || !Win32Native.GetWindowRect(window, out var rect)) return default;
        var left = Math.Max(rect.Left, Win32Native.GetSystemMetrics(SmXVirtualScreen));
        var top = Math.Max(rect.Top, Win32Native.GetSystemMetrics(SmYVirtualScreen));
        var right = Math.Min(rect.Right, left + Math.Max(1, Win32Native.GetSystemMetrics(SmCxVirtualScreen)));
        var bottom = Math.Min(rect.Bottom, top + Math.Max(1, Win32Native.GetSystemMetrics(SmCyVirtualScreen)));
        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    // MARK: E13 窗口选择器（WindowPickerSheet）

    /// <summary>One window the picker offers: title, owning process and its frame in screen pixels.</summary>
    public sealed record CaptureTarget(IntPtr Handle, string Title, string Owner, PixelRect Bounds)
    {
        public string DisplayTitle => Title.Length > 0 ? Title : UiText.T("未命名窗口");
        public string DisplayLine => Owner + " · " + Bounds.Width + "×" + Bounds.Height;
    }

    /// <summary>
    /// The visible top-level windows the picker can capture. NextThought's own windows are
    /// excluded (Mac captures with this app's applications excluded) and DWM-cloaked ghosts are
    /// skipped; tiny tool windows are hidden from the list.
    /// </summary>
    public static IReadOnlyList<CaptureTarget> AvailableWindows()
    {
        var own = Environment.ProcessId;
        var targets = new List<CaptureTarget>();
        Win32Native.EnumWindows((handle, _) =>
        {
            if (!Win32Native.IsWindowVisible(handle)) return true;
            var length = Win32Native.GetWindowTextLength(handle);
            if (length <= 0) return true;
            Win32Native.GetWindowThreadProcessId(handle, out var pid);
            if (pid == (uint)own) return true;
            // DWMWA_CLOAKED: on another virtual desktop or an invisible UWP shell host.
            if (Win32Native.DwmGetWindowAttribute(handle, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (!TryExtendedBounds(handle, out var bounds)) return true;
            if (bounds.Width < 60 || bounds.Height < 40) return true;
            var title = new System.Text.StringBuilder(length + 1);
            Win32Native.GetWindowText(handle, title, title.Capacity);
            var owner = "未知应用";
            try { using var process = System.Diagnostics.Process.GetProcessById((int)pid); owner = process.ProcessName; } catch { }
            targets.Add(new CaptureTarget(handle, title.ToString().Trim(), owner, bounds));
            return true;
        }, IntPtr.Zero);
        return targets;
    }

    /// <summary>The window's frame in screen pixels, cropped to the virtual screen.</summary>
    public static PixelRect WindowBounds(IntPtr handle) => TryExtendedBounds(handle, out var bounds) ? bounds : default;

    private static bool TryExtendedBounds(IntPtr handle, out PixelRect bounds)
    {
        bounds = default;
        // DWMWA_EXTENDED_FRAME_BOUNDS: the visible frame without the invisible resize border.
        if (Win32Native.DwmGetWindowAttribute(handle, 9, out Win32Native.NativeRect extended,
                Marshal.SizeOf<Win32Native.NativeRect>()) == 0)
        {
            if (TryClip(extended, out bounds)) return true;
        }
        if (Win32Native.GetWindowRect(handle, out var raw) && TryClip(raw, out bounds)) return true;
        return false;
    }

    private static bool TryClip(Win32Native.NativeRect rect, out PixelRect bounds)
    {
        bounds = default;
        var x = Win32Native.GetSystemMetrics(SmXVirtualScreen);
        var y = Win32Native.GetSystemMetrics(SmYVirtualScreen);
        var width = Math.Max(1, Win32Native.GetSystemMetrics(SmCxVirtualScreen));
        var height = Math.Max(1, Win32Native.GetSystemMetrics(SmCyVirtualScreen));
        var left = Math.Max(rect.Left, x);
        var top = Math.Max(rect.Top, y);
        var right = Math.Min(rect.Right, x + width);
        var bottom = Math.Min(rect.Bottom, y + height);
        if (right - left < 2 || bottom - top < 2) return false;
        bounds = new PixelRect(left, top, right - left, bottom - top);
        return true;
    }

    /// <summary>Grabs one window's frame: the whole desktop is copied and cropped to that frame.</summary>
    public static Capture? CaptureWindow(CaptureTarget target)
    {
        var bounds = target.Bounds.Width > 1 && target.Bounds.Height > 1 ? target.Bounds : WindowBounds(target.Handle);
        if (bounds.Width <= 1 || bounds.Height <= 1) return null;
        var capture = VirtualScreen();
        var cropped = Crop(capture, new PixelRect(bounds.X - capture.X, bounds.Y - capture.Y, bounds.Width, bounds.Height));
        return new Capture(bounds.X, bounds.Y, bounds.Width, bounds.Height, cropped);
    }

    /// <summary>Mean luma of a screenshot; the self-check uses it to prove own windows are excluded.</summary>
    public static double AverageBrightness(Avalonia.Media.Imaging.WriteableBitmap bitmap)
    {
        var total = 0.0;
        var count = 0L;
        using (var locked = bitmap.Lock())
        {
            var buffer = new byte[Math.Max(4, Math.Min(locked.RowBytes, 4 * 128))];
            for (var row = 0; row < Math.Min(locked.Size.Height, 128); row++)
            for (var column = 0; column < Math.Min(locked.Size.Width, 128); column += 4)
            {
                Marshal.Copy(IntPtr.Add(locked.Address, row * locked.RowBytes + column * 4), buffer, 0, 4);
                total += 0.114 * buffer[0] + 0.587 * buffer[1] + 0.299 * buffer[2];
                count++;
            }
        }
        return count == 0 ? 0 : total / count;
    }

    /// <summary>Crop the capture to a rectangle given in physical pixels relative to the image.</summary>
    public static WriteableBitmap Crop(Capture capture, PixelRect rectangle)
    {
        var left = Math.Clamp(rectangle.X, 0, capture.Width - 1);
        var top = Math.Clamp(rectangle.Y, 0, capture.Height - 1);
        var right = Math.Clamp(rectangle.X + rectangle.Width, left + 1, capture.Width);
        var bottom = Math.Clamp(rectangle.Y + rectangle.Height, top + 1, capture.Height);
        var width = right - left;
        var height = bottom - top;

        var source = capture.Image;
        var buffer = new byte[width * height * 4];
        using (var locked = source.Lock())
        {
            var rowBytes = width * 4;
            for (var row = 0; row < height; row++)
            {
                var pointer = IntPtr.Add(locked.Address, (top + row) * locked.RowBytes + left * 4);
                Marshal.Copy(pointer, buffer, row * rowBytes, rowBytes);
            }
        }
        var cropped = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var locked = cropped.Lock())
        {
            for (var row = 0; row < height; row++)
                Marshal.Copy(buffer, row * width * 4, IntPtr.Add(locked.Address, row * locked.RowBytes), width * 4);
        }
        return cropped;
    }











private const uint WdaExcludeFromCapture = 0x00000011;
/// <summary>E9/C18: keep NextThought windows out of the screen-read frame.</summary>
    public static void ExcludeOwnWindowsFromCapture()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;
        foreach (var window in desktop.Windows)
        {
            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle != IntPtr.Zero) Win32Native.SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
            }
            catch { }
        }
    }

    public static byte[] PngBytes(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

}
