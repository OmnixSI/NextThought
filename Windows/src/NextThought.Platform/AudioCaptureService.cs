using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
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
public sealed class AudioCaptureService(Func<CancellationToken, Task>? beforeFrame = null) : IAsyncDisposable
{
    private readonly List<CapturePipe> pipes = [];
    private CancellationTokenSource? lifetime;
    private Task? pump;
    private MMDeviceEnumerator? monitor;
    private DeviceNotifications? notifications;
    private DateTimeOffset origin;
    private long sample;
    private int failed, stopping;
    private double microphoneGain = 1;
    public string? LastError { get; private set; }
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
        if (!double.IsFinite(settings.MicrophoneGain) || settings.MicrophoneGain is < 1 or > 8) throw new ArgumentException("麦克风增益必须为 1 至 8 倍。");
        lifetime = new(); microphoneGain = settings.MicrophoneGain; sample = 0; failed = stopping = 0; LastError = null;
        try
        {
            if (settings.CaptureSystem) Add(AudioSource.System, settings.SystemDeviceId);
            if (settings.CaptureMicrophone) Add(AudioSource.Microphone, settings.MicrophoneDeviceId);
            monitor = new MMDeviceEnumerator();
            notifications = new DeviceNotifications(pipes.Select(p => p.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase), ReportFailure);
            monitor.RegisterEndpointNotificationCallback(notifications);
            foreach (var pipe in pipes) pipe.Start();
            origin = DateTimeOffset.UtcNow;
            pump = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                var clock = Stopwatch.StartNew(); var timeline = new AudioFrameClock(); var lastWake = DateTimeOffset.UtcNow;
                try
                {
                    while (await timer.WaitForNextTickAsync(lifetime.Token))
                    {
                        if (beforeFrame != null) await beforeFrame(lifetime.Token);
                        var now = DateTimeOffset.UtcNow;
                        if (now - lastWake > TimeSpan.FromMilliseconds(1500)) throw new IOException("检测到休眠或采音暂停，已停止本次记录，请重新开始。");
                        lastWake = now;
                        for (var due = timeline.Advance(clock.Elapsed); due > 0; due--) EmitFrame();
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                catch (Exception ex) { ReportFailure(ex.Message); }
            });
        }
        catch
        {
            Interlocked.Exchange(ref stopping, 1);
            foreach (var pipe in pipes) pipe.Dispose(); pipes.Clear(); DisposeMonitor(); lifetime.Dispose(); lifetime = null; throw;
        }
    }
    private void EmitFrame()
    {
        foreach (var pipe in pipes)
        {
            var pcm = pipe.Read(); if (pipe.Source == AudioSource.Microphone) PcmAudio.ApplyGain(pcm, microphoneGain);
            double power = 0;
            foreach (var value in pcm) power += value / 32768.0 * (value / 32768.0);
            LevelChanged?.Invoke(pipe.Source, Math.Sqrt(power / pcm.Length));
            FrameReady?.Invoke(new(pipe.Source, pcm, sample, origin));
        }
        sample += 1600;
    }
    private void ReportFailure(string message)
    {
        if (Volatile.Read(ref stopping) != 0 || Interlocked.Exchange(ref failed, 1) != 0) return;
        LastError = message; lifetime?.Cancel(); Failed?.Invoke(message);
    }
    private void Add(AudioSource source, string? id)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = string.IsNullOrWhiteSpace(id) ? enumerator.GetDefaultAudioEndpoint(source == AudioSource.System ? DataFlow.Render : DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(id);
        try { pipes.Add(new CapturePipe(source, device, ReportFailure)); }
        catch { device.Dispose(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (lifetime == null) return;
        Interlocked.Exchange(ref stopping, 1);
        try
        {
            foreach (var pipe in pipes) pipe.Stop();
            lifetime.Cancel(); if (pump != null) await pump;
            // Drain queued device samples and the resampler tail before the recognizer is finalized.
            var drain = 0;
            while (pipes.Any(p => p.HasBufferedAudio) && drain++ < 30) EmitFrame();
            EmitFrame();
        }
        finally
        {
            foreach (var pipe in pipes) pipe.Dispose();
            pipes.Clear(); DisposeMonitor(); lifetime.Dispose(); lifetime = null;
        }
    }
    private void DisposeMonitor()
    {
        if (monitor == null) return;
        try { if (notifications != null) monitor.UnregisterEndpointNotificationCallback(notifications); }
        catch (System.Runtime.InteropServices.COMException) { }
        monitor.Dispose(); monitor = null; notifications = null;
    }
    private sealed class DeviceNotifications(HashSet<string> ids, Action<string> failure) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string id, DeviceState state) { if (ids.Contains(id) && state != DeviceState.Active) failure("所选音频设备已断开或不可用，记录已停止，已识别内容将保留。"); }
        public void OnDeviceRemoved(string id) { if (ids.Contains(id)) failure("所选音频设备已移除，记录已停止，重新连接后可开始新会议。"); }
        public void OnDeviceAdded(string id) { }
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) { }
        public void OnPropertyValueChanged(string id, PropertyKey key) { }
    }
    private sealed class CapturePipe : IDisposable
    {
        private readonly object sync = new();
        private readonly MMDevice device;
        private readonly WasapiCapture capture;
        private readonly BufferedWaveProvider buffer;
        private readonly ISampleProvider samples;
        private readonly float[] frame = new float[1600];
        private bool disposed, stopped;
        public AudioSource Source { get; }
        public string DeviceId => device.ID;
        public bool HasBufferedAudio { get { lock (sync) return buffer.BufferedBytes > 0; } }
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
            capture.RecordingStopped += (_, e) => { if (!disposed && !stopped) failure("音频设备意外停止：" + (e.Exception?.Message ?? "请检查设备后重新开始。")); };
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
        public void Stop()
        {
            if (stopped || disposed) return; stopped = true;
            try { capture.StopRecording(); } catch (System.Runtime.InteropServices.COMException) { }
        }
        public void Dispose() { if (disposed) return; Stop(); lock (sync) disposed = true; capture.Dispose(); device.Dispose(); }
    }
}
