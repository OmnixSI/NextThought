using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class OcrDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var cancel = int.TryParse(option("--cancel-after-ms"), out var ms); if (cancel) cancellation.CancelAfter(ms);
        var store = new MaterialStore(paths, new AppPaths()); var clock = Stopwatch.StartNew(); bool canceled = false; MaterialDocument? result = null;
        try { result = await store.ImportAsync(option("--image")!, cancellation.Token, enhancedOcr: true); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { canceled = true; }
        var documents = store.List(); var staged = Directory.Exists(store.Root) && Directory.EnumerateDirectories(store.Root, ".import-*").Any();
        var passed = cancel ? canceled && documents.Count == 0 && !staged : result is not null && result.Blocks.Any(b => b.Kind == "paddle-table") && result.Blocks.All(b => b.Page == 1 && b.Box?.Count == 4) && !staged;
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, canceled, elapsedSeconds = clock.Elapsed.TotalSeconds, documents = documents.Count, stagingRemains = staged, document = result }, JsonDefaults.Options), ct);
        Console.WriteLine("Enhanced OCR import/cancel/atomic archive: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
    }
}
