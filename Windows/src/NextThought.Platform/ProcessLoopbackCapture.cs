using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace NextThought.Platform;

/// <summary>Windows application loopback, including the selected process's descendants.</summary>
public sealed class ProcessLoopbackCapture(int processId, long startTicks) : IWaveIn
{
    private CancellationTokenSource? lifetime;
    private Task? worker;
    public WaveFormat WaveFormat { get; set; } = new(48000, 16, 2);
    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;
    public void StartRecording()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) throw new PlatformNotSupportedException("应用声音采集需要 Windows build 20348 或更高版本。");
        if (lifetime is not null) throw new InvalidOperationException("已经开始采集。");
        using (var target = Process.GetProcessById(processId)) if (target.StartTime.ToUniversalTime().Ticks != startTicks) throw new IOException("所选应用已经退出，请重新选择。");
        lifetime = new(); var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                using var target = Process.GetProcessById(processId);
                using var client = await ActivateAsync((uint)processId);
                client.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.Loopback | AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality, 1000000, 0, WaveFormat, Guid.Empty);
                var capture = client.AudioCaptureClient; client.Start(); initialized.TrySetResult();
                try
                {
                    while (!lifetime.IsCancellationRequested)
                    {
                        if (target.HasExited) throw new IOException("所选应用已经退出，应用采音已停止。");
                        while (capture.GetNextPacketSize() > 0)
                        {
                            var pointer = capture.GetBuffer(out var frames, out var flags); var bytes = new byte[frames * WaveFormat.BlockAlign];
                            try { if ((flags & AudioClientBufferFlags.Silent) == 0) Marshal.Copy(pointer, bytes, 0, bytes.Length); }
                            finally { capture.ReleaseBuffer(frames); }
                            DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
                        }
                        await Task.Delay(10, lifetime.Token);
                    }
                }
                finally { client.Stop(); }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception ex) { error = ex; initialized.TrySetException(ex); }
            finally { RecordingStopped?.Invoke(this, new StoppedEventArgs(error)); }
        });
        initialized.Task.GetAwaiter().GetResult();
    }
    public void StopRecording() { lifetime?.Cancel(); worker?.GetAwaiter().GetResult(); }
    public void Dispose() { StopRecording(); lifetime?.Dispose(); lifetime = null; }

    [StructLayout(LayoutKind.Sequential)] private struct Activation { public int Type; public uint Pid; public int Mode; }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public ushort Type, R1, R2, R3; public uint Size; public IntPtr Data; }
    [DllImport("Mmdevapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int ActivateAudioInterfaceAsync(string device, [In, MarshalAs(UnmanagedType.LPStruct)] Guid iid, IntPtr parameters, IActivateAudioInterfaceCompletionHandler handler, out IActivateAudioInterfaceAsyncOperation operation);
    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAgileObject { }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Handler : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public TaskCompletionSource<IAudioClient> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            try { operation.GetActivateResult(out var hr, out var result); Marshal.ThrowExceptionForHR(hr); Completion.TrySetResult((IAudioClient)result); }
            catch (Exception ex) { Completion.TrySetException(ex); }
        }
    }
    private static async Task<AudioClient> ActivateAsync(uint pid)
    {
        var activation = Marshal.AllocHGlobal(Marshal.SizeOf<Activation>()); var variant = Marshal.AllocHGlobal(Marshal.SizeOf<Blob>());
        try
        {
            Marshal.StructureToPtr(new Activation { Type = 1, Pid = pid, Mode = 0 }, activation, false);
            Marshal.StructureToPtr(new Blob { Type = 65, Size = (uint)Marshal.SizeOf<Activation>(), Data = activation }, variant, false);
            var handler = new Handler();
            Marshal.ThrowExceptionForHR(ActivateAudioInterfaceAsync("VAD\\Process_Loopback", typeof(IAudioClient).GUID, variant, handler, out var operation));
            try { return new AudioClient(await handler.Completion.Task.ConfigureAwait(false)); }
            finally { Marshal.ReleaseComObject(operation); GC.KeepAlive(handler); }
        }
        finally { Marshal.FreeHGlobal(variant); Marshal.FreeHGlobal(activation); }
    }
}
