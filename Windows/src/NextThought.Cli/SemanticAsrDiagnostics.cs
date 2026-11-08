using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class SemanticAsrDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var wave = option("--wav") ?? throw new ArgumentException("Missing --wav");
        var fixture = JsonSerializer.Deserialize<RetrievalBenchmark>(await File.ReadAllTextAsync(option("--benchmark")!, ct), JsonDefaults.Options)!;
        var configuredPaths = new AppPaths(); var models = new ModelStore(configuredPaths);
        object? baseline = null, concurrent = null; string baselineText = "", concurrentText = ""; double baselineRtf = 0, concurrentRtf = 0;
        void Transcribe(bool parallel)
        {
            var lines = new List<string>(); var clock = Stopwatch.StartNew();
            using var recognizer = new SpeechRecognizerSession(models, new AppSettings { LocalModel = "xasr480", SecondPass = false });
            recognizer.Updated += u => { if (u.IsFinal) lines.Add(u.Text); }; recognizer.TranscribeWave(wave, AudioSource.System, ct);
            var text = string.Join("\n", lines); var rtf = recognizer.InferenceSeconds / Math.Max(.001, recognizer.AudioSeconds);
            var report = new { text, rtf, recognizer.AudioSeconds, recognizer.InferenceSeconds, totalSeconds = clock.Elapsed.TotalSeconds };
            if (parallel) { concurrent = report; concurrentText = text; concurrentRtf = rtf; } else { baseline = report; baselineText = text; baselineRtf = rtf; }
        }
        using var retrieval = new RetrievalService(paths, new EmbeddingModelStore(configuredPaths));
        await retrieval.SearchAsync("桥面巡检周期", true, ct, fixture.Documents); // Warm up the optional model and the document index.
        await Task.Run(() => Transcribe(false), ct);
        var queries = Task.Run(async () => { foreach (var item in fixture.Cases.Take(15)) await retrieval.SearchAsync(item.Query, true, ct, fixture.Documents); }, ct);
        await Task.WhenAll(queries, Task.Run(() => Transcribe(true), ct));
        var passed = baselineText.Length > 0 && baselineText == concurrentText && baselineRtf < 1 && concurrentRtf < 1;
        var report = new { testedAt = DateTimeOffset.Now, passed, baseline, concurrent, queries = 15, processWorkingSetMiB = Process.GetCurrentProcess().WorkingSet64 / 1048576.0,
            microphoneEnabled = false, hardwareCaptureEnabled = false, secondPass = false, runtime = "Shared ORT 1.28.2, CPU" };
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(report, JsonDefaults.Options), ct); Console.WriteLine(JsonSerializer.Serialize(report, JsonDefaults.Options)); return passed ? 0 : 2;
    }
}
