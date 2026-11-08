using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class EmbeddingDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var store = new EmbeddingModelStore(paths); await store.VerifyAsync(ct);
        using var reference = JsonDocument.Parse(await File.ReadAllTextAsync(option("--reference")!, ct));
        var tokenizer = new QwenTokenizer(Path.Combine(store.Root, "tokenizer.json"));
        var mismatches = reference.RootElement.EnumerateArray().Where(c => !tokenizer.Encode(c.GetProperty("text").GetString()!).SequenceEqual(c.GetProperty("ids").EnumerateArray().Select(i => i.GetInt64()))).Select(c => c.GetProperty("text").GetString()).ToArray();
        Console.WriteLine("Tokenizer reference: " + (reference.RootElement.GetArrayLength() - mismatches.Length) + "/" + reference.RootElement.GetArrayLength());
        if (mismatches.Length > 0) { Console.WriteLine(JsonSerializer.Serialize(mismatches)); return 2; }
        var clock = Stopwatch.StartNew(); using var embedder = new QwenEmbedder(store); var loadSeconds = clock.Elapsed.TotalSeconds;
        clock.Restart(); var query = embedder.Embed("桥面多久巡查一次？", true, ct); var document = embedder.Embed("桥面日常巡检周期为七天。", false, ct);
        var similarity = query.Zip(document).Sum(p => (double)p.First * p.Second);
        var passed = query.Length == 1024 && similarity > .3;
        var report = new { testedAt = DateTimeOffset.Now, passed, tokenizerCases = reference.RootElement.GetArrayLength(), mismatches, runtime = embedder.Runtime, loadSeconds, inferenceSeconds = clock.Elapsed.TotalSeconds, dimension = query.Length, similarity };
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(report, JsonDefaults.Options), ct); Console.WriteLine(JsonSerializer.Serialize(report, JsonDefaults.Options)); return passed ? 0 : 2;
    }
}
