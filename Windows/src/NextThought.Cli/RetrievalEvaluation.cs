using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;

internal sealed record RetrievalCase(string Id, string Query, string Category, string Split, string[] Relevant);
internal sealed record RetrievalBenchmark(MaterialDocument[] Documents, RetrievalCase[] Cases);
internal static class RetrievalEvaluation
{
    public static async Task<int> RunAsync(Func<string, string?> option, CancellationToken ct)
    {
        var input = option("--benchmark") ?? throw new ArgumentException("Missing --benchmark");
        var benchmark = JsonSerializer.Deserialize<RetrievalBenchmark>(await File.ReadAllTextAsync(input, ct), JsonDefaults.Options)!;
        var clock = Stopwatch.StartNew(); var index = new KeywordIndex(benchmark.Documents); var buildMs = clock.Elapsed.TotalMilliseconds;
        var results = benchmark.Cases.Select(c =>
        {
            clock.Restart(); var found = index.Search(c.Query, 5); var elapsedMs = clock.Elapsed.TotalMilliseconds;
            var locators = found.Select(s => s.DocumentId + ":" + s.Location).ToArray();
            return new { c.Id, c.Query, c.Category, c.Split, relevant = c.Relevant, found = locators, elapsedMs,
                hit = c.Relevant.Length > 0 && c.Relevant.Any(locators.Contains), falsePositive = c.Relevant.Length == 0 && found.Count > 0 };
        }).ToArray();
        var output = new { testedAt = DateTimeOffset.Now, engine = "keyword-current", chunker = DocumentChunks.Version, documents = benchmark.Documents.Length, cases = results.Length, buildMs,
            positiveCases = results.Count(x => x.relevant.Length > 0), hits = results.Count(x => x.hit), negativeCases = results.Count(x => x.relevant.Length == 0), falsePositives = results.Count(x => x.falsePositive), results };
        await File.WriteAllTextAsync(option("--json") ?? throw new ArgumentException("Missing --json"), JsonSerializer.Serialize(output, JsonDefaults.Options), ct);
        Console.WriteLine($"Keyword baseline: {output.hits}/{output.positiveCases} hit@5; false positives {output.falsePositives}/{output.negativeCases}; build {buildMs:F1} ms"); return 0;
    }
}
