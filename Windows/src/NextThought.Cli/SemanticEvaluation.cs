using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class SemanticEvaluation
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var benchmark = JsonSerializer.Deserialize<RetrievalBenchmark>(await File.ReadAllTextAsync(option("--benchmark")!, ct), JsonDefaults.Options)!;
        var minimum = double.Parse(option("--minimum") ?? ".55", System.Globalization.CultureInfo.InvariantCulture);
        using var service = new RetrievalService(paths, new EmbeddingModelStore(new AppPaths(option("--model-data-root"))));
        var results = new List<object>(); int hits = 0, falsePositives = 0; var times = new List<double>(); var all = Stopwatch.StartNew();
        foreach (var c in benchmark.Cases)
        {
            var result = await service.SearchAsync(c.Query, true, ct, benchmark.Documents, minimum); times.Add(result.Seconds);
            var found = result.Sources.Take(5).Select(s => s.DocumentId + ":" + s.Location).ToArray();
            var hit = c.Relevant.Length > 0 && c.Relevant.Any(found.Contains); if (hit) hits++;
            var falsePositive = c.Relevant.Length == 0 && found.Length > 0; if (falsePositive) falsePositives++;
            results.Add(new { c.Id, c.Query, c.Category, c.Split, relevant = c.Relevant, found, hit, falsePositive, result.Method, result.Warning, result.Seconds,
                scores = result.SemanticScores.Take(10).Select(s => new { locator = s.Chunk.DocumentId + ":" + s.Chunk.Location, s.Score }) });
            if (results.Count % 5 == 0) Console.WriteLine($"Semantic evaluation {results.Count}/{benchmark.Cases.Length}");
        }
        var repeat = await service.SearchAsync(benchmark.Cases[0].Query, true, ct, benchmark.Documents, minimum);
        var embedded = service.DocumentEmbeddingCount;
        using var reloaded = new RetrievalService(paths, new EmbeddingModelStore(new AppPaths(option("--model-data-root"))));
        await reloaded.SearchAsync(benchmark.Cases[0].Query, true, ct, benchmark.Documents, minimum);
        var p95 = times.Order().ElementAt(Math.Min(times.Count - 1, (int)Math.Ceiling(times.Count * .95) - 1));
        var report = new { testedAt = DateTimeOffset.Now, engine = "qwen3-onnx-q8-hybrid", identity = RetrievalService.Identity, minimumSimilarity = minimum, totalSeconds = all.Elapsed.TotalSeconds,
            positiveCases = benchmark.Cases.Count(c => c.Relevant.Length > 0), hits, negativeCases = benchmark.Cases.Count(c => c.Relevant.Length == 0), falsePositives, p95Seconds = p95,
            service.CorpusBuildCount, documentEmbeddings = embedded, loadedVectors = service.CachedVectorsLoaded, reloadDocumentEmbeddings = reloaded.DocumentEmbeddingCount, reloadCachedVectors = reloaded.CachedVectorsLoaded, results };
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(report, JsonDefaults.Options), ct);
        Console.WriteLine($"Semantic hit@5 {hits}/{report.positiveCases}; false positives {falsePositives}/{report.negativeCases}; p95 {p95:F3}s; reload embeds {reloaded.DocumentEmbeddingCount}");
        return results.Count == benchmark.Cases.Length && service.CorpusBuildCount == 1 && reloaded.DocumentEmbeddingCount == 0 ? 0 : 2;
    }
}
