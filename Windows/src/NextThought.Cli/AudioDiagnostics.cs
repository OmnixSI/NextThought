using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NextThought.Core;
using NextThought.Platform;

internal static class AudioDiagnostics
{
    private sealed class Statistics
    {
        public long Frames { get; set; }
        public long Samples { get; set; }
        public long NonzeroSamples { get; set; }
        public int TimelineErrors { get; set; }
        public int Peak { get; set; }
        public double RmsSum { get; set; }
        public long NextSample { get; set; }
        public void Add(AudioFrame frame)
        {
            if (frame.StartSample != NextSample) TimelineErrors++;
            NextSample = frame.StartSample + frame.Samples.Length;
            Frames++; Samples += frame.Samples.Length;
            double power = 0;
            foreach (var value in frame.Samples)
            {
                if (value != 0) NonzeroSamples++;
                Peak = Math.Max(Peak, Math.Abs((int)value)); power += value / 32768.0 * (value / 32768.0);
            }
            RmsSum += Math.Sqrt(power / frame.Samples.Length);
        }
    }
    public static async Task<int> CaptureAsync(AppPaths modelPaths, Func<string, string?> option, IReadOnlyList<string> args, CancellationToken ct)
    {
        var source = option("--source") ?? "both";
        if (source is not ("system" or "microphone" or "both")) throw new ArgumentException("--source must be system, microphone or both");
        var seconds = double.Parse(option("--seconds") ?? "15", CultureInfo.InvariantCulture);
        if (seconds is < 1 or > 7200) throw new ArgumentOutOfRangeException("--seconds");
        var settings = new AppSettings { CaptureSystem = source is "system" or "both", CaptureMicrophone = source is "microphone" or "both", SecondPass = !args.Contains("--no-refine") };
        var outputs = AudioCaptureService.Devices(true); var inputs = AudioCaptureService.Devices(false);
        AudioDeviceInfo Choose(IReadOnlyList<AudioDeviceInfo> devices, string? name) =>
            devices.FirstOrDefault(d => name == null || d.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("找不到指定的声音设备：" + name);
        var output = settings.CaptureSystem || option("--play-wav") != null ? Choose(outputs, option("--system-name")) : null;
        var input = settings.CaptureMicrophone ? Choose(inputs, option("--mic-name")) : null;
        settings.SystemDeviceId = output?.Id; settings.MicrophoneDeviceId = input?.Id;
        settings.MicrophoneGain = double.Parse(option("--mic-gain") ?? "1", CultureInfo.InvariantCulture);
        var archivePaths = new AppPaths(option("--session-root") ?? throw new ArgumentException("采音诊断必须指定独立的 --session-root"));
        var store = new SessionStore(archivePaths);
        var meta = new SessionMeta { Title = "P1 采音验证 · " + source, AudioSource = source, Recognizer = "xasr480+refine", InPerson = source == "microphone" };
        await store.CreateAsync(meta, ct);
        var stats = new Dictionary<AudioSource, Statistics>();
        if (settings.CaptureSystem) stats[AudioSource.System] = new();
        if (settings.CaptureMicrophone) stats[AudioSource.Microphone] = new();
        var failures = new ConcurrentQueue<string>(); var warnings = new ConcurrentQueue<string>();
        var deviceFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectDisconnect = args.Contains("--expect-disconnect");
        var expectPause = args.Contains("--expect-pause");
        var writes = new ConcurrentQueue<Task>(); var finals = new ConcurrentQueue<TranscriptUpdate>();
        RecordingPrompt? prompt = null;
        var engine = args.Contains("--no-asr") ? null : new LiveSpeechEngine(new ModelStore(modelPaths), settings);
        if (engine != null)
        {
        engine.Failed += failures.Enqueue; engine.Warning += warnings.Enqueue;
        engine.Updated += update =>
        {
            prompt?.Update(update);
            if (!update.IsFinal) return;
            finals.Enqueue(update);
            writes.Enqueue(store.AppendTranscriptAsync(meta, new TranscriptLine { Id = update.Id, Date = update.Start, StartedAt = update.Start, EndedAt = update.End,
                Speaker = update.Source == AudioSource.System ? "remote" : settings.CaptureSystem ? "me" : "room", Text = update.Text }));
            Console.WriteLine($"[{update.Source}] {update.Text}");
        };
        }
        var ticks = 0; var pauseMilliseconds = int.Parse(option("--pause-ms") ?? "0", CultureInfo.InvariantCulture);
        if (pauseMilliseconds is < 0 or > 3000) throw new ArgumentOutOfRangeException("--pause-ms");
        var capture = new AudioCaptureService(async token => { if (++ticks == 10 && pauseMilliseconds > 0) await Task.Delay(pauseMilliseconds, token); });
        capture.Failed += message => { failures.Enqueue(message); deviceFailure.TrySetResult(); };
        capture.FrameReady += frame => { stats[frame.Source].Add(frame); engine?.Accept(frame); };
        var clock = new Stopwatch();
        try
        {
            if (args.Contains("--confirm"))
            {
                prompt = await RecordingPrompt.ShowAsync(seconds, input?.Name ?? "未启用麦克风", option("--play-wav") != null, ct, expectDisconnect);
                if (!await prompt.WaitForStartAsync(ct)) throw new OperationCanceledException("用户选择稍后测试。", ct);
            }
            if (option("--start-signal") is { } signal)
            {
                Console.WriteLine("READY: models loaded; waiting for start signal.");
                while (!File.Exists(signal)) await Task.Delay(50, ct);
            }
            clock.Start();
            capture.Start(settings);
            Console.WriteLine($"Capture started: {source}; output={output?.Name}; microphone={input?.Name}; seconds={seconds}");
            using var recordingCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, prompt?.StopToken ?? CancellationToken.None);
            try
            {
                if (expectDisconnect || expectPause) await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(seconds), recordingCancellation.Token), deviceFailure.Task);
                else if (option("--play-wav") is { } wav)
                    await Task.WhenAll(Task.Delay(TimeSpan.FromSeconds(seconds), recordingCancellation.Token), PlayAsync(wav, output!.Id, recordingCancellation.Token));
                else await Task.Delay(TimeSpan.FromSeconds(seconds), recordingCancellation.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && prompt?.StopToken.IsCancellationRequested == true) { }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { failures.Enqueue(ex.Message); }
        finally
        {
            await capture.DisposeAsync(); if (engine != null) await engine.DisposeAsync(); await Task.WhenAll(writes); await store.EndAsync(meta);
        }
        var data = store.Load(meta.Id);
        var microphoneText = string.Join(" ", finals.Where(x => x.Source == AudioSource.Microphone).Select(x => x.Text));
        var expectedMicrophone = option("--expect-mic");
        var microphoneVerified = expectedMicrophone == null || QuestionCandidateTracker.Normalize(microphoneText).Contains(QuestionCandidateTracker.Normalize(expectedMicrophone));
        var expectedDisconnectObserved = expectDisconnect && !failures.IsEmpty && failures.All(m => m.StartsWith("所选音频设备") || m.StartsWith("音频设备意外停止"));
        var expectedPauseObserved = expectPause && failures.Any(m => m.Contains("休眠或采音暂停") || m.Contains("音频时间线中断"));
        var passed = (expectDisconnect ? expectedDisconnectObserved : expectPause ? expectedPauseObserved : failures.IsEmpty)
            && stats.Values.All(s => s.Frames > 0 && s.TimelineErrors == 0) && microphoneVerified;
        if (passed && args.Contains("--save-settings"))
        {
            var settingsStore = new SettingsStore(modelPaths); var selected = settingsStore.Load();
            selected.MicrophoneGain = settings.MicrophoneGain; selected.MicrophoneDeviceId = input?.Id ?? selected.MicrophoneDeviceId;
            selected.SystemDeviceId = output?.Id ?? selected.SystemDeviceId;
            await settingsStore.SaveAsync(selected, ct);
        }
        var report = new { testedAt = DateTimeOffset.Now, passed, source, seconds, elapsedSeconds = clock.Elapsed.TotalSeconds,
            output = output?.Name, microphone = input?.Name, settings.MicrophoneGain, channels = stats.ToDictionary(x => x.Key.ToString(), x => x.Value),
            expectedMicrophone, microphoneVerified, expectedDisconnectObserved, expectedPauseObserved, pauseMilliseconds, finalLines = finals.ToArray(), failures = failures.ToArray(), warnings = warnings.ToArray(),
            sessionId = meta.Id, storedLines = data.Lines.Count, archive = archivePaths.Root, rawAudioSaved = false };
        var reportFile = option("--json") ?? Path.Combine(archivePaths.Root, "capture-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportFile))!);
        await File.WriteAllTextAsync(reportFile, JsonSerializer.Serialize(report, JsonDefaults.Options), ct);
        await store.ExportMarkdownAsync(meta.Id, Path.ChangeExtension(reportFile, ".md"), ct);
        Console.WriteLine($"Capture {(passed ? "PASS" : "FAIL")}: {data.Lines.Count} final lines; report={reportFile}");
        if (prompt != null)
        {
            prompt.Complete(passed ? "采音已完成，转写已保存" : "采音已结束，请检查转写结果");
            await prompt.WaitForCloseAsync(ct);
        }
        return passed ? 0 : 2;
    }
    private static async Task PlayAsync(string file, string deviceId, CancellationToken ct)
    {
        await Task.Delay(700, ct);
        using var devices = new MMDeviceEnumerator(); using var device = devices.GetDevice(deviceId);
        using var reader = new WaveFileReader(file);
        ISampleProvider samples = reader.ToSampleProvider(); var mix = device.AudioClient.MixFormat;
        if (samples.WaveFormat.SampleRate != mix.SampleRate) samples = new WdlResamplingSampleProvider(samples, mix.SampleRate);
        if (samples.WaveFormat.Channels != mix.Channels)
        {
            var mapped = new MultiplexingSampleProvider([samples], mix.Channels);
            for (var c = 0; c < mix.Channels; c++) mapped.ConnectInputToOutput(Math.Min(c, samples.WaveFormat.Channels - 1), c);
            samples = mapped;
        }
        using var player = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
        player.Init(new VolumeSampleProvider(samples) { Volume = .65f });
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.PlaybackStopped += (_, e) => { if (e.Exception == null) stopped.TrySetResult(); else stopped.TrySetException(e.Exception); };
        using var cancel = ct.Register(player.Stop);
        player.Play(); await stopped.Task.WaitAsync(ct);
    }
}
