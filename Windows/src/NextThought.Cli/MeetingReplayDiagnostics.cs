using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NextThought.Core;
using NextThought.Platform;

internal static class MeetingReplayDiagnostics
{
    public static async Task<int> RunAsync(AppPaths modelPaths, Func<string, string?> option, CancellationToken ct)
    {
        var seconds = int.Parse(option("--seconds") ?? "1800", CultureInfo.InvariantCulture);
        if (seconds is < 10 or > 7200) throw new ArgumentOutOfRangeException("--seconds");
        var firstPath = option("--system-wav") ?? throw new ArgumentException("Missing --system-wav");
        var secondPath = option("--mic-wav") ?? throw new ArgumentException("Missing --mic-wav");
        var first = ReadMono(firstPath); var second = ReadMono(secondPath);
        var archivePaths = new AppPaths(option("--session-root") ?? throw new ArgumentException("Missing --session-root"));
        var store = new SessionStore(archivePaths); var meta = new SessionMeta { Title = "P1 · 30 分钟受控双通道回放", AudioSource = "controlled-pcm-replay", Recognizer = "xasr480+refine" };
        await store.CreateAsync(meta, ct);
        var settingsStore = new SettingsStore(modelPaths); var settings = settingsStore.Load();
        var options = new AnswerOptions(settings.BaseUrl, settings.Model, settingsStore.ReadApiKey(), settings.ReasoningEffort, 500);
        var client = new ChatCompletionClient(); var lines = new ConcurrentQueue<TranscriptLine>();
        var writes = new ConcurrentQueue<Task>(); var failures = new ConcurrentQueue<string>();
        var candidates = new ConcurrentQueue<QuestionCandidate>(); var tracker = new QuestionCandidateTracker();
        var engine = new LiveSpeechEngine(new ModelStore(modelPaths), new AppSettings());
        engine.Failed += failures.Enqueue; engine.Warning += failures.Enqueue;
        engine.Updated += update =>
        {
            if (!update.IsFinal) return;
            var line = new TranscriptLine { Id = update.Id, Date = update.Start, StartedAt = update.Start, EndedAt = update.End,
                Speaker = update.Source == AudioSource.System ? "remote" : "me", Text = update.Text };
            lines.Enqueue(line); writes.Enqueue(store.AppendTranscriptAsync(meta, line));
            if (update.Source == AudioSource.System && tracker.Ingest(update.Text, true, update.Id, DateTimeOffset.UtcNow) is { Kind: CandidateKind.Trigger, Candidate: { } candidate }) candidates.Enqueue(candidate);
        };
        var clock = Stopwatch.StartNew(); var origin = DateTimeOffset.UtcNow; var requests = new List<Task>();
        var samples = new List<object>(); var scheduler = new AutomaticAnswerScheduler(); var process = Process.GetCurrentProcess();
        var step = 0; var minute = -1; var automaticStarted = 0; var manualStarted = false;
        async Task AskAsync(string question, bool automatic)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                var context = string.Join("\n", lines.TakeLast(30).Select(l => l.Speaker + ": " + l.Text));
                var answer = await client.AnswerAsync(options, "你在验收会议助手。只依据给出的公开教学音频转写，用简短中文回答。", context + "\n问题：" + question, ct: ct);
                await store.AppendAnswerAsync(meta, new AnswerRecord { Question = question, Answer = answer, Automatic = automatic, Model = options.Model, Seconds = watch.Elapsed.TotalSeconds });
            }
            catch (Exception ex) { failures.Enqueue("回答失败：" + ex.Message); }
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        try
        {
            while (step < seconds * 10 && await timer.WaitForNextTickAsync(ct))
            {
                var target = Math.Min(seconds * 10, Math.Max(step + 1, (int)(clock.Elapsed.TotalSeconds * 10)));
                var microphoneActive = false;
                while (step < target)
                {
                    var position = step % 600 * 1600;
                    var a = Slice(first, position); var b = Slice(second, position - 80000);
                    microphoneActive |= b.Any(x => Math.Abs((int)x) > 1000);
                    engine.Accept(new(AudioSource.System, a, step * 1600L, origin));
                    engine.Accept(new(AudioSource.Microphone, b, step * 1600L, origin)); step++;
                }
                while (candidates.TryDequeue(out var candidate)) scheduler.Enqueue(candidate, DateTimeOffset.UtcNow);
                if (automaticStarted < 2 && scheduler.Tick(DateTimeOffset.UtcNow, microphoneActive) is { } pending)
                { automaticStarted++; requests.Add(AskAsync(pending.FullText, true)); }
                if (!manualStarted && step >= 200)
                { manualStarted = true; requests.Add(AskAsync("请用一句话概括最近转写的内容。", false)); }
                if (step / 600 > minute)
                {
                    minute = step / 600; process.Refresh();
                    var sample = new { seconds = clock.Elapsed.TotalSeconds, framesPerChannel = step, finalLines = lines.Count,
                        cpuSeconds = process.TotalProcessorTime.TotalSeconds, workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64, failures = failures.Count };
                    samples.Add(sample);
                    await File.WriteAllTextAsync(Path.Combine(archivePaths.Root, "heartbeat.json"), JsonSerializer.Serialize(sample, JsonDefaults.Options), ct);
                    Console.WriteLine($"Replay {clock.Elapsed.TotalSeconds:F0}/{seconds}s · lines={lines.Count} · RAM={process.WorkingSet64 / 1024 / 1024} MiB · errors={failures.Count}");
                }
                if (!failures.IsEmpty) break;
            }
        }
        finally
        {
            await engine.DisposeAsync(); await Task.WhenAll(writes); await Task.WhenAll(requests); await store.EndAsync(meta);
        }
        var data = store.Load(meta.Id);
        var timelineValid = data.Lines.GroupBy(l => l.Speaker).All(g => g.Zip(g.Skip(1)).All(p => p.First.Date <= p.Second.Date));
        var passed = step == seconds * 10 && failures.IsEmpty && data.Lines.Count > 0 && timelineValid
            && data.Lines.Select(l => l.Id).Distinct().Count() == data.Lines.Count && data.Meta.TranscriptCount == data.Lines.Count && data.Answers.Count > 0;
        var report = new { testedAt = DateTimeOffset.Now, passed, kind = "controlled real-time PCM replay; not continuous hardware capture", seconds,
            elapsedSeconds = clock.Elapsed.TotalSeconds, framesPerChannel = step, timelineValid, data.Meta, finalLines = data.Lines.Count,
            answers = data.Answers.Count, automaticAnswers = data.Answers.Count(x => x.Automatic), failures = failures.ToArray(), samples,
            inputs = new[] { new { file = Path.GetFileName(firstPath), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(firstPath))) },
                new { file = Path.GetFileName(secondPath), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(secondPath))) } } };
        var output = option("--json") ?? throw new ArgumentException("Missing --json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, JsonDefaults.Options));
        await store.ExportMarkdownAsync(meta.Id, Path.ChangeExtension(output, ".md"));
        Console.WriteLine($"Replay {(passed ? "PASS" : "FAIL")}: session {meta.Id}"); return passed ? 0 : 2;
    }
    private static short[] Slice(short[] samples, int position)
    {
        var output = new short[1600];
        if (position >= 0 && position < samples.Length) Array.Copy(samples, position, output, 0, Math.Min(1600, samples.Length - position));
        return output;
    }
    private static short[] ReadMono(string path)
    {
        using var file = new WaveFileReader(path); ISampleProvider samples = file.ToSampleProvider();
        if (samples.WaveFormat.Channels != 1) throw new InvalidDataException("Replay fixtures must be mono WAV.");
        if (samples.WaveFormat.SampleRate != 16000) samples = new WdlResamplingSampleProvider(samples, 16000);
        var data = new List<short>(); var buffer = new float[1600];
        while (samples.Read(buffer, 0, buffer.Length) is var count && count > 0)
            for (var i = 0; i < count; i++) data.Add((short)Math.Clamp((int)Math.Round(buffer[i] * 32768), short.MinValue, short.MaxValue));
        return data.ToArray();
    }
}
