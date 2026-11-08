using System.Diagnostics;
using System.Net;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class P3PerformanceDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var fixture = JsonSerializer.Deserialize<RetrievalBenchmark>(await File.ReadAllTextAsync(option("--benchmark")!, ct), JsonDefaults.Options)!;
        var configured = new AppPaths(); var models = new ModelStore(configured); var wave = option("--wav")!; var reports = new List<object>();
        var baselineTexts = new List<string>(); var concurrentTexts = new List<string>(); var times = new List<double>(); bool passed = true;
        using var retrieval = new RetrievalService(paths, new EmbeddingModelStore(configured)); using var speaker = new SpeakerEmbedder(configured);
        var speakerClips = new[] { "fangjun-sr-1.wav", "leijun-sr-1.wav", "liudehua-sr-1.wav" }.Select(n => SpeakerDiagnostics.Read(Path.Combine(option("--fixtures")!, n))).ToArray();
        var first = Stopwatch.StartNew(); await retrieval.SearchAsync("巡检周期", true, ct, fixture.Documents); var cold = first.Elapsed.TotalSeconds;
        await using var companion = new CompanionServer(IPAddress.Loopback, _ => Task.FromResult(true)); companion.Publish(new("performance-fixture", "公开问题", "公开回答"));
        using var http = new HttpClient { BaseAddress = companion.BaseUri };
        for (int round = 0; round < 3; round++)
        {
            foreach (bool parallel in new[] { false, true })
            {
                var clock = Stopwatch.StartNew(); double rtf = 0, audio = 0, infer = 0; string text = "";
                var asr = Task.Run(() =>
                {
                    using var recognizer = new SpeechRecognizerSession(models, new AppSettings { LocalModel = "xasr480", SecondPass = false });
                    var lines = new List<string>(); recognizer.Updated += u => { if (u.IsFinal) lines.Add(u.Text); }; recognizer.TranscribeWave(wave, AudioSource.System, ct);
                    text = string.Join("\n", lines); audio = recognizer.AudioSeconds; infer = recognizer.InferenceSeconds; rtf = infer / audio;
                }, ct);
                var queryTimes = new List<double>();
                var work = parallel ? Task.Run(async () =>
                {
                    foreach (var item in fixture.Cases.Take(15)) { var q = Stopwatch.StartNew(); await retrieval.SearchAsync(item.Query, true, ct, fixture.Documents); queryTimes.Add(q.Elapsed.TotalSeconds); }
                }, ct) : Task.CompletedTask;
                var voices = parallel ? Task.Run(() => { foreach (var clip in speakerClips) { if (speaker.Embed(clip, ct) is null) throw new InvalidDataException("Speaker inference failed"); } }, ct) : Task.CompletedTask;
                var network = parallel ? Task.Run(async () => { for (int i = 0; i < 10; i++) await http.GetStringAsync("/", ct); }, ct) : Task.CompletedTask;
                await Task.WhenAll(asr, work, voices, network); passed &= rtf < 1 && text.Length > 0;
                if (parallel) concurrentTexts.Add(text); else baselineTexts.Add(text); times.Add(rtf);
                reports.Add(new { round, parallel, rtf, audioSeconds = audio, inferenceSeconds = infer, wallSeconds = clock.Elapsed.TotalSeconds, querySeconds = queryTimes, text, workingSetMiB = Process.GetCurrentProcess().WorkingSet64 / 1048576.0 });
            }
        }
        passed &= baselineTexts.SequenceEqual(concurrentTexts); var p = Process.GetCurrentProcess();
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, cpu = "Intel i9-14900HX", runtime = "Shared ORT 1.28.2 CPU; no GPU", coldRetrievalSeconds = cold, peakWorkingSetMiB = p.PeakWorkingSet64 / 1048576.0, medianBaselineRtf = times.Where((_,i)=>i%2==0).Order().ElementAt(1), medianConcurrentRtf = times.Where((_,i)=>i%2==1).Order().ElementAt(1), hardwareCapture = false, remoteTranslationLatencyIncluded = false, enhancedOcrIncluded = false, reports }, JsonDefaults.Options), ct);
        Console.WriteLine("P3 repeated ASR/semantic/speaker/LAN coexistence: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
    }
}
