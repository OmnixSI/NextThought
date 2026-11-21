using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using SherpaOnnx;

namespace NextThought.Platform;

/// <summary>
/// The 1.13.8 managed configuration uses LPStr. On Windows that is the active ANSI
/// codepage, while the C API expects UTF-8 filenames. Keep the official struct
/// layout, but allocate every nested string explicitly as UTF-8.
/// </summary>
internal sealed class Utf8NativeConfig : IDisposable
{
    public IntPtr Pointer { get; }
    private readonly List<IntPtr> strings = [];
    public Utf8NativeConfig(object config)
    {
        var size = Marshal.SizeOf(config.GetType()); Pointer = Marshal.AllocHGlobal(size);
        Marshal.Copy(new byte[size], 0, Pointer, size);
        try { Fill(config, Pointer); } catch { Dispose(); throw; }
    }
    private void Fill(object value, IntPtr location)
    {
        var type = value.GetType();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var address = location + checked((int)Marshal.OffsetOf(type, field.Name));
            var item = field.GetValue(value); if (item == null) continue;
            if (field.FieldType == typeof(string))
            { var pointer = Marshal.StringToCoTaskMemUTF8((string)item); strings.Add(pointer); Marshal.WriteIntPtr(address, pointer); }
            else if (field.FieldType == typeof(int)) Marshal.WriteInt32(address, (int)item);
            else if (field.FieldType == typeof(float)) Marshal.WriteInt32(address, BitConverter.SingleToInt32Bits((float)item));
            else if (field.FieldType == typeof(IntPtr)) Marshal.WriteIntPtr(address, (IntPtr)item);
            else if (field.FieldType.IsValueType) Fill(item, address);
            else throw new NotSupportedException("Unsupported Sherpa config field: " + field.Name);
        }
    }
    public void Dispose() { foreach (var pointer in strings) Marshal.FreeCoTaskMem(pointer); strings.Clear(); Marshal.FreeHGlobal(Pointer); }
}

internal sealed class Utf8OnlineRecognizer : IDisposable
{
    private IntPtr handle;
    public Utf8OnlineRecognizer(OnlineRecognizerConfig config)
    { using var native = new Utf8NativeConfig(config); handle = Native.CreateOnline(native.Pointer); if (handle == IntPtr.Zero) throw new InvalidDataException("Sherpa 无法创建流式识别器，请核对模型文件与配置。"); }
    public OnlineStream CreateStream()
    { var pointer = Native.CreateOnlineStream(handle); return pointer != IntPtr.Zero ? new(pointer) : throw new InvalidOperationException("无法创建识别流。"); }
    public bool IsReady(OnlineStream stream) => Native.IsReady(handle, stream.Handle) != 0;
    public void Decode(OnlineStream stream) => Native.DecodeOnline(handle, stream.Handle);
    public string GetText(OnlineStream stream)
    {
        var pointer = Native.OnlineJson(handle, stream.Handle);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("识别结果不可用。");
        try { using var json = JsonDocument.Parse(Marshal.PtrToStringUTF8(pointer)!); return json.RootElement.GetProperty("text").GetString() ?? ""; }
        finally { Native.DestroyOnlineJson(pointer); }
    }
    public void Dispose() { if (handle != IntPtr.Zero) { Native.DestroyOnline(handle); handle = IntPtr.Zero; } }
}
internal sealed class Utf8OfflineRecognizer : IDisposable
{
    private IntPtr handle;
    public Utf8OfflineRecognizer(OfflineRecognizerConfig config)
    { using var native = new Utf8NativeConfig(config); handle = Native.CreateOffline(native.Pointer); if (handle == IntPtr.Zero) throw new InvalidDataException("Sherpa 无法创建精修识别器，请核对模型文件与配置。"); }
    public OfflineStream CreateStream()
    { var pointer = Native.CreateOfflineStream(handle); return pointer != IntPtr.Zero ? new(pointer) : throw new InvalidOperationException("无法创建精修流。"); }
    public void Decode(OfflineStream stream) => Native.DecodeOffline(handle, stream.Handle);
    public string GetText(OfflineStream stream)
    {
        var pointer = Native.OfflineJson(stream.Handle);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("精修结果不可用。");
        try { using var json = JsonDocument.Parse(Marshal.PtrToStringUTF8(pointer)!); return json.RootElement.GetProperty("text").GetString() ?? ""; }
        finally { Native.DestroyOfflineJson(pointer); }
    }
    public void Dispose() { if (handle != IntPtr.Zero) { Native.DestroyOffline(handle); handle = IntPtr.Zero; } }
}
internal static class Native
{
    private const string Library = "sherpa-onnx-c-api";
    [DllImport("kernel32.dll", EntryPoint = "GetACP")] internal static extern uint ActiveCodePage();
    [DllImport(Library, EntryPoint = "SherpaOnnxCreateOnlineRecognizer", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr CreateOnline(IntPtr config);
    [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOnlineRecognizer", CallingConvention = CallingConvention.Cdecl)] internal static extern void DestroyOnline(IntPtr recognizer);
    [DllImport(Library, EntryPoint = "SherpaOnnxCreateOnlineStream", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr CreateOnlineStream(IntPtr recognizer);
    [DllImport(Library, EntryPoint = "SherpaOnnxIsOnlineStreamReady", CallingConvention = CallingConvention.Cdecl)] internal static extern int IsReady(IntPtr recognizer, IntPtr stream);
    [DllImport(Library, EntryPoint = "SherpaOnnxDecodeOnlineStream", CallingConvention = CallingConvention.Cdecl)] internal static extern void DecodeOnline(IntPtr recognizer, IntPtr stream);
    [DllImport(Library, EntryPoint = "SherpaOnnxGetOnlineStreamResultAsJson", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr OnlineJson(IntPtr recognizer, IntPtr stream);
    [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOnlineStreamResultJson", CallingConvention = CallingConvention.Cdecl)] internal static extern void DestroyOnlineJson(IntPtr result);
    [DllImport(Library, EntryPoint = "SherpaOnnxCreateOfflineRecognizer", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr CreateOffline(IntPtr config);
    [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOfflineRecognizer", CallingConvention = CallingConvention.Cdecl)] internal static extern void DestroyOffline(IntPtr recognizer);
    [DllImport(Library, EntryPoint = "SherpaOnnxCreateOfflineStream", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr CreateOfflineStream(IntPtr recognizer);
    [DllImport(Library, EntryPoint = "SherpaOnnxDecodeOfflineStream", CallingConvention = CallingConvention.Cdecl)] internal static extern void DecodeOffline(IntPtr recognizer, IntPtr stream);
    [DllImport(Library, EntryPoint = "SherpaOnnxGetOfflineStreamResultAsJson", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr OfflineJson(IntPtr stream);
    [DllImport(Library, EntryPoint = "SherpaOnnxDestroyOfflineStreamResultJson", CallingConvention = CallingConvention.Cdecl)] internal static extern void DestroyOfflineJson(IntPtr result);
}
