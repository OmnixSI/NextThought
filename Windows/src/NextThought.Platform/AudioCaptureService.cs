using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record AudioDeviceInfo(string Id, string Name) { public override string ToString() => Name; }

internal sealed class MonoSampleProvider(ISampleProvider source) : ISampleProvider
{
    private float[] input = [];
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    public int Read(float[] buffer, int offset, int count)
    {
        var channels = source.WaveFormat.Channels; var needed = count * channels;
        if (input.Length < needed) input = new float[needed];
        var read = source.Read(input, 0, needed); var frames = read/channels;
        for (var i = 0; i < frames; i++) { float sum = 0; for (var c = 0; c < channels; c++) sum += input[i*channels+c]; buffer[offset+i] = sum/channels; }
        return frames;
    }
}

/// <summary>WASAPI endpoint capture. The system endpoint is the selected device mix, not a per-process filter.</summary>
public sealed class AudioCaptureService : IAsyncDisposable
{
    private readonly List<CapturePipe> pipes = [];
    private CancellationTokenSource? lifetime;
    private Task? pump;
    public event Action<AudioFrame>? FrameReady;
    public event Action<string>? Failed;
    public event Action<AudioSource, double>? LevelChanged;
    public static IReadOnlyList<AudioDeviceInfo> Devices(bool system)
    {
        using var enumerator = new MMDeviceEnumerator();
        var flow = system ? DataFlow.Render : DataFlow.Capture;
        var collection = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        string? defaultId = null;
        try { using var endpoint = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia); defaultId = endpoint.ID; }
        catch (System.Runtime.InteropServices.COMException) { /* No default endpoint is a valid device state. */ }
        var list = new List<AudioDeviceInfo>();
        foreach (var device in collection) { list.Add(new(device.ID, device.FriendlyName)); device.Dispose(); }
        return list.OrderByDescending(x => x.Id == defaultId).ToArray();
    }
    public void Start(AppSettings settings)
    {
        if (lifetime != null) throw new InvalidOperationException("音频采集已经开始。");
        if (!settings.CaptureSystem && !settings.CaptureMicrophone) throw new InvalidOperationException("请至少选择一种声音来源。");
        var origin = DateTimeOffset.UtcNow; lifetime = new();
        try
        {
            if (settings.CaptureSystem) Add(AudioSource.System, settings.SystemDeviceId);
            if (settings.CaptureMicrophone) Add(AudioSource.Microphone, settings.MicrophoneDeviceId);
            foreach (var pipe in pipes) pipe.Start();
            pump = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                var clock = Stopwatch.StartNew(); long lastTick = 0, sample = 0;
                try
                {
                    while (await timer.WaitForNextTickAsync(lifetime.Token))
                    {
                        if (clock.ElapsedMilliseconds-lastTick > 1500 && lastTick > 0) throw new IOException("音频时间线中断，请结束后重新开始会议。");
                        lastTick = clock.ElapsedMilliseconds;
                        foreach (var pipe in pipes)
                        {
                            var pcm = pipe.Read(); double power = 0;
                            foreach (var value in pcm) power += value/32768.0 * (value/32768.0);
                            LevelChanged?.Invoke(pipe.Source, Math.Sqrt(power/pcm.Length));
                            FrameReady?.Invoke(new(pipe.Source, pcm, sample, origin));
                        }
                        sample += 1600;
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                catch (Exception ex) { Failed?.Invoke(ex.Message); lifetime.Cancel(); }
            });
        }
        catch { foreach (var pipe in pipes) pipe.Dispose(); pipes.Clear(); lifetime.Dispose(); lifetime = null; throw; }
    }
    private void Add(AudioSource source, string? id)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = string.IsNullOrWhiteSpace(id) ? enumerator.GetDefaultAudioEndpoint(source == AudioSource.System ? DataFlow.Render : DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(id);
        try { pipes.Add(new CapturePipe(source, device, message => Failed?.Invoke(message))); }
        catch { device.Dispose(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (lifetime == null) return;
        lifetime.Cancel();
        if (pump != null) await pump;
        foreach (var pipe in pipes) pipe.Dispose();
        pipes.Clear(); lifetime.Dispose(); lifetime = null;
    }
    private sealed class CapturePipe : IDisposable
    {
        private readonly object sync = new();
        private readonly MMDevice device;
        private readonly WasapiCapture capture;
        private readonly BufferedWaveProvider buffer;
        private readonly ISampleProvider samples;
        private readonly float[] frame = new float[1600];
        private bool disposed;
        public AudioSource Source { get; }
        public CapturePipe(AudioSource source, MMDevice endpoint, Action<string> failure)
        {
            Source = source; device = endpoint;
            capture = source == AudioSource.System ? new WasapiLoopbackCapture(endpoint) : new WasapiCapture(endpoint, true, 30);
            buffer = new(capture.WaveFormat) { BufferDuration = TimeSpan.FromSeconds(3), ReadFully = false, DiscardOnBufferOverflow = false };
            ISampleProvider mono = new MonoSampleProvider(buffer.ToSampleProvider());
            samples = mono.WaveFormat.SampleRate == 16000 ? mono : new WdlResamplingSampleProvider(mono, 16000);
            capture.DataAvailable += (_, e) =>
            {
                try { lock (sync) { if (!disposed) buffer.AddSamples(e.Buffer, 0, e.BytesRecorded); } }
                catch (Exception ex) { failure("音频缓冲异常：" + ex.Message); }
            };
            capture.RecordingStopped += (_, e) => { if (!disposed && e.Exception != null) failure("音频设备停止：" + e.Exception.Message); };
        }
        public void Start() => capture.StartRecording();
        public short[] Read()
        {
            Array.Clear(frame); int count;
            lock (sync) count = samples.Read(frame, 0, frame.Length);
            var pcm = new short[1600];
            for (var i = 0; i < count; i++) pcm[i] = (short)Math.Clamp((int)Math.Round(frame[i] * 32768), short.MinValue, short.MaxValue);
            return pcm; // Silence still advances the timeline and closes the VAD utterance.
        }
        public void Dispose() { lock (sync) disposed = true; capture.StopRecording(); capture.Dispose(); device.Dispose(); }
    }
}
