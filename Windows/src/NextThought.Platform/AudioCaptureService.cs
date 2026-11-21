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
    private readonly List<CapturePipe> retired = [];
    private readonly object pipesGate = new();
    private CancellationTokenSource? lifetime;
    private Task? pump;
    private MMDeviceEnumerator? monitor;
    private DeviceNotifications? notifications;
    private DateTimeOffset origin;
    private long sample;
    private int failed, stopping;
    private double microphoneGain = 1;
    private string? systemDevicePreference;
    private string? microphoneDevicePreference;
    public string? LastError { get; private set; }
    public event Action<AudioFrame>? FrameReady;
    public event Action<string>? Failed;
    public event Action<AudioSource, double>? LevelChanged;
    public static IReadOnlyList<AudioDeviceInfo> Devices(bool system)
    {
        // Diagnostic switch for the Native AOT probe: NAudio's COM enumerator is not AOT-safe,
        // so the AOT build can start and report timings/memory without touching audio.
        if (Environment.GetEnvironmentVariable("NEXTTHOUGHT_DISABLE_AUDIO_DEVICES") is "1" or "true") return [];
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
        systemDevicePreference = settings.SystemDeviceId;
        microphoneDevicePreference = settings.MicrophoneDeviceId;
        try
        {
            if (settings.CaptureSystem)
            {
                if (settings.CaptureProcessId > 0) pipes.Add(new CapturePipe(AudioSource.System, new ProcessLoopbackCapture(settings.CaptureProcessId, settings.CaptureProcessStartTicks), null, ReportFailure));
                else Add(AudioSource.System, settings.SystemDeviceId);
            }
            if (settings.CaptureMicrophone) Add(AudioSource.Microphone, settings.MicrophoneDeviceId);
            monitor = new MMDeviceEnumerator();
            notifications = new DeviceNotifications(this, ReportFailure);
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
                        if (now - lastWake > TimeSpan.FromMilliseconds(1500))
                        {
                            // F2: a short stall (sleep, CPU load) may drop audio, but must not end the meeting.
                            LastError = "检测到短暂卡顿，已丢弃卡顿期间的音频并继续记录。";
                        }
                        lastWake = now;
                        var due = timeline.Advance(clock.Elapsed);
                        // A long sleep must not create minutes of backlog; keep at most a second.
                        for (var count = Math.Min(due, 10); count > 0; count--) EmitFrame();
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
        CapturePipe[] snapshot;
        lock (pipesGate) snapshot = pipes.ToArray();
        foreach (var pipe in snapshot)
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
        var pipe = CreatePipe(source, id);
        lock (pipesGate) pipes.Add(pipe);
    }

    private CapturePipe CreatePipe(AudioSource source, string? id)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = string.IsNullOrWhiteSpace(id)
            ? enumerator.GetDefaultAudioEndpoint(source == AudioSource.System ? DataFlow.Render : DataFlow.Capture, Role.Multimedia)
            : enumerator.GetDevice(id);
        try { return new CapturePipe(source, device, ReportFailure); }
        catch { device.Dispose(); throw; }
    }

    private bool IsTracked(string id)
    {
        lock (pipesGate) return pipes.Any(pipe => pipe.DeviceId == id);
    }

    /// <summary>Device currently feeding a source; used by the hardware switch check.</summary>
    public string? ActiveDeviceId(AudioSource source)
    {
        lock (pipesGate) return pipes.FirstOrDefault(pipe => pipe.Source == source)?.DeviceId;
    }

    /// <summary>QA hook: force the same path the default-device notification takes.</summary>
    public void SwitchForCheck(AudioSource source, string deviceId) =>
        SwitchDefaultDevice(source == AudioSource.System ? DataFlow.Render : DataFlow.Capture, deviceId);

    /// <summary>COM notification thread: move the switch off the callback and keep the old pipe alive.</summary>
    private void ScheduleDefaultDeviceChange(DataFlow flow, string id)
    {
        if (lifetime is null || Volatile.Read(ref stopping) != 0) return;
        _ = Task.Run(() => SwitchDefaultDevice(flow, id));
    }

    private void SwitchDefaultDevice(DataFlow flow, string id)
    {
        var source = flow == DataFlow.Render ? AudioSource.System : AudioSource.Microphone;
        CapturePipe? current;
        lock (pipesGate) current = pipes.FirstOrDefault(pipe => pipe.Source == source);
        if (current is null || current.DeviceId == id || current.DeviceId == "process-loopback") return;
        // An explicitly chosen device is not overridden by a system-default change.
        if (!string.IsNullOrWhiteSpace(source == AudioSource.System ? systemDevicePreference : microphoneDevicePreference)) return;
        CapturePipe replacement;
        try { replacement = CreatePipe(source, id); replacement.Start(); }
        catch (Exception exception)
        {
            ReportFailure("默认音频设备切换失败，会议继续使用原设备：" + exception.Message);
            return;
        }
        CapturePipe? previous = null;
        lock (pipesGate)
        {
            var index = pipes.FindIndex(pipe => pipe.Source == source);
            if (index < 0) pipes.Add(replacement);
            else { previous = pipes[index]; pipes[index] = replacement; }
        }
        // Stop and release the old pipe immediately: its 3-second buffer and removed-device
        // callbacks must not be able to fail the meeting being recorded on the new device.
        previous?.Stop();
        previous?.Dispose();
    }
    public async ValueTask DisposeAsync()
    {
        if (lifetime == null) return;
        Interlocked.Exchange(ref stopping, 1);
        try
        {
            foreach (var pipe in pipes) pipe.Stop();
            lifetime.Cancel(); if (pump != null) await pump;
            // F3: drain until empty (bounded only by a deadline) so the last sentence is not lost.
            var deadline = Stopwatch.StartNew();
            while (pipes.Any(p => p.HasBufferedAudio) && deadline.Elapsed < TimeSpan.FromSeconds(5)) EmitFrame();
            EmitFrame();
        }
        finally
        {
            CapturePipe[] snapshot; lock (pipesGate) { snapshot = pipes.ToArray(); pipes.Clear(); snapshot = snapshot.Concat(retired).ToArray(); retired.Clear(); }
            foreach (var pipe in snapshot) pipe.Dispose();
            DisposeMonitor(); lifetime.Dispose(); lifetime = null;
        }
    }
    private void DisposeMonitor()
    {
        if (monitor == null) return;
        try { if (notifications != null) monitor.UnregisterEndpointNotificationCallback(notifications); }
        catch (System.Runtime.InteropServices.COMException) { }
        monitor.Dispose(); monitor = null; notifications = null;
    }
    private sealed class DeviceNotifications(AudioCaptureService owner, Action<string> failure) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string id, DeviceState state) { if (state != DeviceState.Active && owner.IsTracked(id)) failure("所选音频设备已断开或不可用，记录已停止，已识别内容将保留。"); }
        public void OnDeviceRemoved(string id) { if (owner.IsTracked(id)) failure("所选音频设备已移除，记录已停止，重新连接后可开始新会议。"); }
        public void OnDeviceAdded(string id) { }
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) { if (role == Role.Multimedia) owner.ScheduleDefaultDeviceChange(flow, id); }
        public void OnPropertyValueChanged(string id, PropertyKey key) { }
    }
    private sealed class CapturePipe : IDisposable
    {
        private readonly object sync = new();
        private readonly MMDevice? device;
        private readonly IWaveIn capture;
        private readonly BufferedWaveProvider buffer;
        private readonly ISampleProvider samples;
        private readonly float[] frame = new float[1600];
        private bool disposed, stopped;
        public AudioSource Source { get; }
        public string DeviceId => device?.ID ?? "process-loopback";
        public bool HasBufferedAudio { get { lock (sync) return buffer.BufferedBytes > 0; } }
        public CapturePipe(AudioSource source, MMDevice endpoint, Action<string> failure)
            : this(source, source == AudioSource.System ? new WasapiLoopbackCapture(endpoint) : new WasapiCapture(endpoint, true, 30), endpoint, failure) { }
        public CapturePipe(AudioSource source, IWaveIn input, MMDevice? endpoint, Action<string> failure)
        {
            Source = source; device = endpoint; capture = input;
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
        public void Dispose()
        {
            if (disposed) return;
            lock (sync) disposed = true;
            try { capture.StopRecording(); } catch (System.Runtime.InteropServices.COMException) { }
            try { capture.Dispose(); } catch (System.Runtime.InteropServices.COMException) { }
            device?.Dispose();
        }
    }
}
