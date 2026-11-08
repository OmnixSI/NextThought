using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class P3StabilityDiagnostics
{
    // Diagnostic-only bounded retries simulate explicitly retrying a failed request.
    // This does not change the product's request policy.
    private sealed class RetryingProbeClient(IAnswerClient inner, ConcurrentQueue<string> retries) : IAnswerClient
    {
        public async Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null, CancellationToken ct = default, int timeoutSeconds = 90)
        {
            for (var attempt = 0; ; attempt++)
            {
                try { return await inner.AnswerAsync(options, system, user, update, ct, timeoutSeconds); }
                catch (Exception ex) when (attempt < 2 && !ct.IsCancellationRequested && ex is HttpRequestException or IOException or TimeoutException)
                {
                    var detail = ex.GetType().Name + ": " + ex.GetBaseException().Message;
                    if (options.ApiKey.Length > 0) detail = detail.Replace(options.ApiKey, "[key]");
                    retries.Enqueue(detail); await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct);
                }
            }
        }
    }
    public static async Task<int> RunAsync(AppPaths components, Func<string, string?> option, CancellationToken ct)
    {
        var seconds = int.Parse(option("--seconds") ?? "3600", CultureInfo.InvariantCulture);
        if (seconds is < 60 or > 7200) throw new ArgumentOutOfRangeException("--seconds");
        var output = Path.GetFullPath(option("--json")!); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var paths = new AppPaths(option("--session-root")!); var store = new SessionStore(paths);
        var fixture = JsonSerializer.Deserialize<RetrievalBenchmark>(await File.ReadAllTextAsync(option("--benchmark")!, ct), JsonDefaults.Options)!;
        var waves = new[] { MeetingReplayDiagnostics.ReadMono(option("--system-wav")!), MeetingReplayDiagnostics.ReadMono(option("--mic-wav")!) };
        var configStore = new SettingsStore(components); var config = configStore.Load();
        var answerOptions = new AnswerOptions(config.BaseUrl, config.Model, configStore.ReadApiKey(), config.ReasoningEffort, 800);
        var retries = new ConcurrentQueue<string>();
        IAnswerClient client = new RetryingProbeClient(new ChatCompletionClient(), retries); using var translator = new MeetingTranslator(client, answerOptions);
        using var retrieval = new RetrievalService(paths, new EmbeddingModelStore(components));
        using var voice = new MeetingVoiceTagger(components, AudioSource.System);
        await retrieval.SearchAsync("谁负责确认曙光桥能安全使用？", true, ct, fixture.Documents);
        var meta = new SessionMeta { Title = $"P3 · {seconds / 60} 分钟受控双路稳定性验收", AudioSource = "controlled-public-pcm-replay", Recognizer = "xasr480+refine", AnswerModel = config.Model };
        await store.CreateAsync(meta, ct);
        var errors = new ConcurrentQueue<string>(); var tasks = new ConcurrentBag<Task>();
        var rows = new ConcurrentQueue<TranscriptLine>(); int remote = 0, voices = 0, translations = 0, queries = 0;
        bool retrievalCanceled = false, translationCanceled = false, retrievalRecovered = false, translationRecovered = false;
        bool ocrPassed = option("--ocr-image") is null; double? ocrSeconds = null;
        async Task Guard(Func<Task> work, string name)
        {
            try { await work(); }
            catch (Exception ex)
            {
                var detail = name + ": " + ex.GetType().Name + ": " + ex.Message + " | " + ex.GetBaseException().Message;
                if (answerOptions.ApiKey.Length > 0) detail = detail.Replace(answerOptions.ApiKey, "[key]");
                errors.Enqueue(detail);
            }
        }
        async Task SaveLine(TranscriptLine line)
        {
            await store.AppendTranscriptAsync(meta, line);
            if (line.Speaker == "remote")
            {
                var group = await voice.LabelAsync(line.StartedAt!.Value, line.EndedAt!.Value, ct);
                if (group.HasValue) { await store.AppendVoiceTagAsync(meta.Id, line.Id, group.Value); Interlocked.Increment(ref voices); }
                if (Interlocked.Increment(ref remote) % 3 == 1)
                {
                    var translated = await translator.TranslateAsync(line.Id, line.Text, ct);
                    if (translated is null) throw new InvalidDataException("Unexpected missing translation");
                    await store.AppendTranslationAsync(meta.Id, translated); Interlocked.Increment(ref translations);
                }
            }
        }
        async Task Search()
        {
            var found = await retrieval.SearchAsync("谁负责确认曙光桥能安全使用？", true, ct, fixture.Documents);
            if (found.Sources.Count == 0) throw new InvalidDataException("Retrieval lost its indexed source");
            Interlocked.Increment(ref queries);
        }
        async Task Answer()
        {
            const string question = "谁负责确认曙光桥能安全使用？";
            var found = await retrieval.SearchAsync(question, true, ct, fixture.Documents);
            var answer = await client.AnswerAsync(answerOptions, "严格根据参考摘录回答，并标注实际提供的 [D数字] 引用。", KeywordIndex.Context(found.Sources) + "\n" + question, ct: ct);
            var cited = KeywordIndex.Cited(answer, found.Sources);
            if (cited.Count == 0) throw new InvalidDataException("Missing source citation");
            await store.AppendAnswerAsync(meta, new AnswerRecord { Question = question, Answer = answer, Sources = cited, Model = answerOptions.Model });
        }
        async Task CancelRetrieval()
        {
            using var cancel = new CancellationTokenSource(1);
            try { await retrieval.SearchAsync("公开验收：谁负责桥梁安全确认和桥面检查？", true, cancel.Token, fixture.Documents); }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { retrievalCanceled = true; }
            await Search(); retrievalRecovered = true;
        }
        async Task CancelTranslation()
        {
            using var cancel = new CancellationTokenSource(10);
            translationCanceled = await translator.TranslateAsync(Guid.NewGuid(), "这是一次公开合成的翻译取消验收。", cancel.Token) is null && cancel.IsCancellationRequested;
            translationRecovered = await translator.TranslateAsync(Guid.NewGuid(), "连接恢复，继续测试。", ct) is not null;
        }
        var engine = new LiveSpeechEngine(new ModelStore(components), new AppSettings { LocalModel = "xasr480", SecondPass = true });
        engine.Failed += errors.Enqueue; engine.Warning += errors.Enqueue;
        engine.Updated += update =>
        {
            if (!update.IsFinal) return;
            var line = new TranscriptLine { Id = update.Id, Date = update.Start, StartedAt = update.Start, EndedAt = update.End, Text = update.Text,
                Speaker = update.Source == AudioSource.System ? "remote" : "me" };
            rows.Enqueue(line); tasks.Add(Guard(() => SaveLine(line), "line"));
        };
        var process = Process.GetCurrentProcess(); var watch = Stopwatch.StartNew(); var origin = DateTimeOffset.UtcNow;
        var samples = new List<object>(); var memory = new List<(double Time, long Private)>();
        int step = 0, minute = -1; bool firstAnswer = false, secondAnswer = false, cancelR = false, cancelT = false, ocrStarted = false;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        try
        {
            while (step < seconds * 10 && await timer.WaitForNextTickAsync(ct))
            {
                var target = Math.Min(seconds * 10, Math.Max(step + 1, (int)(watch.Elapsed.TotalSeconds * 10)));
                while (step < target)
                {
                    // Two ten-second public clips every 30 s; microphone clip is offset by 5 s.
                    var position = step % 300 * 1600;
                    var system = new AudioFrame(AudioSource.System, MeetingReplayDiagnostics.Slice(waves[0], position), step * 1600L, origin);
                    voice.Add(system); engine.Accept(system);
                    engine.Accept(new(AudioSource.Microphone, MeetingReplayDiagnostics.Slice(waves[1], position - 80000), step * 1600L, origin)); step++;
                }
                if (!firstAnswer && step >= 200) { firstAnswer = true; tasks.Add(Guard(Answer, "answer-1")); }
                if (!secondAnswer && step >= seconds * 7.5) { secondAnswer = true; tasks.Add(Guard(Answer, "answer-2")); }
                if (!cancelR && step >= seconds * 2.5) { cancelR = true; tasks.Add(Guard(CancelRetrieval, "retrieval-cancel")); }
                if (!cancelT && step >= seconds * 5) { cancelT = true; tasks.Add(Guard(CancelTranslation, "translation-cancel")); }
                if (!ocrStarted && option("--ocr-image") is { } image && step >= Math.Min(3000, seconds * 5))
                {
                    ocrStarted = true; tasks.Add(Guard(async () =>
                    {
                        var clock = Stopwatch.StartNew(); var blocks = await new EnhancedOcrService(components).ReadAsync(image, 1, ct);
                        ocrPassed = blocks.Any(b => b.Kind == "paddle-table" && b.Text.Contains("90")); ocrSeconds = clock.Elapsed.TotalSeconds;
                    }, "enhanced-ocr"));
                }
                if (step / 600 > minute)
                {
                    minute = step / 600; tasks.Add(Guard(Search, "retrieval")); process.Refresh();
                    var time = watch.Elapsed.TotalSeconds; memory.Add((time, process.PrivateMemorySize64));
                    var sample = new { seconds = time, framesPerChannel = step, lines = rows.Count, voices, translations, queries,
                        pendingTasks = tasks.Count(t => !t.IsCompleted), workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64, cpuSeconds = process.TotalProcessorTime.TotalSeconds, errors = errors.Count };
                    samples.Add(sample); await File.WriteAllTextAsync(Path.Combine(paths.Root, "heartbeat.json"), JsonSerializer.Serialize(sample, JsonDefaults.Options), ct);
                    Console.WriteLine($"P3 {time:F0}/{seconds}s | lines={rows.Count} voices={voices} translations={translations} queries={queries} RAM={process.WorkingSet64 / 1048576}MiB errors={errors.Count}");
                }
                if (!errors.IsEmpty) break;
            }
        }
        finally
        {
            await engine.DisposeAsync(); await Task.WhenAll(tasks); await store.EndAsync(meta);
        }
        var data = store.Load(meta.Id); var summaryPassed = false; var backupPassed = false;
        await Guard(async () => { await new SummaryService(store, client).GenerateAsync(meta.Id, answerOptions with { MaxOutputTokens = 2500 }, ct: ct); summaryPassed = store.Load(meta.Id).Meta.SummaryState == "ready"; }, "summary");
        await Guard(async () =>
        {
            var backup = Path.ChangeExtension(output, ".ntbackup"); await new BackupService(paths).ExportAsync(backup, ct);
            var restored = new AppPaths(Path.Combine(paths.Root, "RoundTrip")); await new BackupService(restored).ImportAsync(backup, ct);
            var copy = new SessionStore(restored).Load(meta.Id);
            backupPassed = copy.Lines.Select(l => (l.Id, l.Text, l.Voice, l.Translation?.Text)).SequenceEqual(data.Lines.Select(l => (l.Id, l.Text, l.Voice, l.Translation?.Text)));
        }, "backup");
        await store.ExportMarkdownAsync(meta.Id, Path.ChangeExtension(output, ".md"), ct);
        var timeline = data.Lines.GroupBy(l => l.Speaker).All(g => g.Zip(g.Skip(1)).All(p => p.First.Date <= p.Second.Date));
        var late = memory.Where(s => s.Time >= (seconds >= 600 ? 600 : 0)).ToArray();
        var growth = late.Length < 2 ? 0 : (late[^1].Private - late[0].Private) / 1048576.0;
        var passed = step == seconds * 10 && errors.IsEmpty && timeline && data.Meta.TranscriptCount == data.Lines.Count
            && data.Lines.Select(l => l.Id).Distinct().Count() == data.Lines.Count && voices > 0 && translations > 0 && queries > 0
            && retrievalCanceled && retrievalRecovered && translationCanceled && translationRecovered && ocrPassed && summaryPassed && backupPassed
            && data.Answers.Count >= 2 && (seconds < 600 || growth <= 256);
        var report = new { testedAt = DateTimeOffset.Now, passed, kind = "real-time public PCM replay; not hardware capture or a long desktop session", seconds,
            elapsedSeconds = watch.Elapsed.TotalSeconds, framesPerChannel = step, lines = data.Lines.Count, voices, translations, queries, answers = data.Answers.Count,
            timeline, retrievalCanceled, retrievalRecovered, translationCanceled, translationRecovered, ocrRequested = option("--ocr-image") is not null, ocrPassed, ocrSeconds,
            summaryPassed, backupPassed, parentPrivateGrowthAfterWarmupMiB = growth, memoryScope = "Parent process; separate OCR worker is not included",
            retryPolicy = "Diagnostic-only: at most 3 attempts; simulates explicit retry, not product auto-retry", transientNetworkRetries = retries.ToArray(), meta.Id, failures = errors.ToArray(), samples };
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, JsonDefaults.Options), ct);
        Console.WriteLine("P3 stability: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
    }
}
