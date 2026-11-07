using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using NextThought.Core;
using NextThought.Platform;

internal static class DocumentDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var fixtures = option("--fixtures") ?? throw new ArgumentException("Missing --fixtures");
        var expected = JsonSerializer.Deserialize<Dictionary<string, string[]>>(await File.ReadAllTextAsync(Path.Combine(fixtures, "expected.json"), ct))!;
        var store = new MaterialStore(paths); var results = new List<object>(); var passed = true;
        foreach (var (file, phrases) in expected)
        {
            var document = await store.ImportAsync(Path.Combine(fixtures, file), ct);
            var text = Regex.Replace(string.Join("\n", document.Blocks.Select(b => b.Text)), @"\s", "");
            var phrasesMatch = phrases.All(p => text.Contains(Regex.Replace(p, @"\s", ""), StringComparison.OrdinalIgnoreCase));
            var duplicate = await store.ImportAsync(Path.Combine(fixtures, file), ct);
            var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(store.SourcePath(document), ct)));
            var ok = phrasesMatch && duplicate.Id == document.Id && hash == document.Sha256 && document.Blocks.All(b => b.Location.Length > 0);
            passed &= ok;
            results.Add(new { file, passed = ok, phrasesMatch, document.Id, document.Sha256, blocks = document.Blocks, warnings = document.Warnings });
            Console.WriteLine($"{file}: {(ok ? "PASS" : "FAIL")} ({document.Blocks.Count} blocks)");
        }
        await File.WriteAllTextAsync(option("--json") ?? throw new ArgumentException("Missing --json"), JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, results }, JsonDefaults.Options), ct);
        return passed ? 0 : 2;
    }
}
