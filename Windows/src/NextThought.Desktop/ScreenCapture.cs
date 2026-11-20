using System.Runtime.InteropServices;
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
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var width = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        var height = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));

        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var info = new BitmapInfo
        {
            biSize = Marshal.SizeOf<BitmapInfo>(),
            biWidth = width,
            biHeight = -height, // top-down rows
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0
        };
        var dib = CreateDIBSection(memoryDc, ref info, 0, out var bits, IntPtr.Zero, 0);
        var previous = SelectObject(memoryDc, dib);
        try
        {
            BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, Srccopy | CaptureBlt);
            var stride = width * 4;
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var locked = bitmap.Lock())
            {
                for (var row = 0; row < height; row++)
                {
                    var source = IntPtr.Add(bits, row * stride);
                    var destination = IntPtr.Add(locked.Address, row * locked.RowBytes);
                    CopyMemory(destination, source, (nuint)stride);
                }
            }
            return new Capture(x, y, width, height, bitmap);
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(dib);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>The foreground window's rectangle in virtual-screen coordinates (window capture).</summary>
    public static PixelRect ForegroundWindowBounds()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return default;
        var left = Math.Max(rect.Left, GetSystemMetrics(SmXVirtualScreen));
        var top = Math.Max(rect.Top, GetSystemMetrics(SmYVirtualScreen));
        var right = Math.Min(rect.Right, left + Math.Max(1, GetSystemMetrics(SmCxVirtualScreen)));
        var bottom = Math.Min(rect.Bottom, top + Math.Max(1, GetSystemMetrics(SmCyVirtualScreen)));
        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
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

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out WindowRect rect);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint raster);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] private static extern void CopyMemory(IntPtr destination, IntPtr source, nuint length);

    private const uint WdaExcludeFromCapture = 0x00000011;

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    /// <summary>E9/C18: keep NextThought windows out of the screen-read frame.</summary>
    public static void ExcludeOwnWindowsFromCapture()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;
        foreach (var window in desktop.Windows)
        {
            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle != IntPtr.Zero) SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
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
