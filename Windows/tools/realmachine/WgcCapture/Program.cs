using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace WgcCapture;

/// <summary>
/// K1 evidence tool: captures the primary monitor through Windows.Graphics.Capture (the modern
/// capture API that honours SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)) and writes one PNG.
/// Used by Windows/scripts/realmachine/k1-capture-hide.ps1 together with the GDI capture.
/// </summary>
internal static class Program
{
    private const int D3D11SdkVersion = 7;
    private const int D3D11CreateDeviceBgraSupport = 0x20;
    private const int DriverTypeHardware = 1;

    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IdxgiDeviceGuid = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel,
        out IntPtr context);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor, [In] ref Guid iid);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: WgcCapture <output.png> [milliseconds-to-wait-for-frame]");
            return 2;
        }
        var output = args[0];
        var timeout = args.Length > 1 ? int.Parse(args[1]) : 5000;
        try
        {
            Console.WriteLine("WGC supported: " + GraphicsCaptureSession.IsSupported());
            if (!GraphicsCaptureSession.IsSupported())
            {
                Console.Error.WriteLine("Windows.Graphics.Capture is not supported on this system");
                return 3;
            }
            Capture(output, timeout);
            Console.WriteLine("saved " + output);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Capture(string output, int timeout)
    {
        var device = CreateDirect3DDevice();
        var monitor = MonitorFromPoint(new POINT { X = 0, Y = 0 }, 2 /* MONITOR_DEFAULTTOPRIMARY */);
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var itemGuid = GraphicsCaptureItemGuid;
        var itemPointer = interop.CreateForMonitor(monitor, ref itemGuid);
        var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        Marshal.Release(itemPointer);

        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        using var session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;

        using var frameReady = new ManualResetEventSlim(false);
        SoftwareBitmap? captured = null;
        Exception? failure = null;
        framePool.FrameArrived += (pool, _) =>
        {
            try
            {
                using var frame = pool.TryGetNextFrame();
                captured = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().GetAwaiter().GetResult();
                frameReady.Set();
            }
            catch (Exception error)
            {
                failure = error;
                frameReady.Set();
            }
        };
        session.StartCapture();
        var gotFrame = frameReady.Wait(timeout);
        session.Dispose();
        framePool.Dispose();
        if (failure is not null) throw failure;
        if (!gotFrame || captured is null) throw new TimeoutException("no Windows.Graphics.Capture frame arrived");
        SavePng(captured, output).GetAwaiter().GetResult();
        captured.Dispose();
    }

    private static IDirect3DDevice CreateDirect3DDevice()
    {
        var result = D3D11CreateDevice(IntPtr.Zero, DriverTypeHardware, IntPtr.Zero,
            D3D11CreateDeviceBgraSupport, IntPtr.Zero, 0, D3D11SdkVersion, out var d3dDevice, out _, out _);
        if (result < 0)
        {
            // Machines without a hardware D3D11 device can fall back to WARP.
            result = D3D11CreateDevice(IntPtr.Zero, 5 /* D3D_DRIVER_TYPE_WARP */, IntPtr.Zero,
                D3D11CreateDeviceBgraSupport, IntPtr.Zero, 0, D3D11SdkVersion, out d3dDevice, out _, out _);
        }
        if (result < 0) Marshal.ThrowExceptionForHR(result);
        try
        {
            var unknown = Marshal.QueryInterface(d3dDevice, IdxgiDeviceGuid, out var dxgiDevice);
            if (unknown < 0) Marshal.ThrowExceptionForHR(unknown);
            try
            {
                var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var graphicsDevice);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                try { return MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice); }
                finally { Marshal.Release(graphicsDevice); }
            }
            finally { Marshal.Release(dxgiDevice); }
        }
        finally { Marshal.Release(d3dDevice); }
    }

    private static async Task SavePng(SoftwareBitmap bitmap, string path)
    {
        using var memory = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memory);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        memory.Seek(0);
        using var reader = new DataReader(memory.GetInputStreamAt(0));
        var size = (uint)memory.Size;
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(path, bytes);
    }
}
