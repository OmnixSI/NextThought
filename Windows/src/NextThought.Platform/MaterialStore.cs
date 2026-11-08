using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class MaterialStore(AppPaths paths, AppPaths? ocrComponents = null)
{
    public string Root => Path.Combine(paths.Root, "Materials");
    public string SourcePath(MaterialDocument document) => Path.Combine(Root, document.Id, "source" + document.Extension);
    public IReadOnlyList<MaterialDocument> List()
    {
        if (!Directory.Exists(Root)) return [];
        return Directory.EnumerateDirectories(Root).Where(folder => Guid.TryParse(Path.GetFileName(folder), out _))
            .Select(folder => Path.Combine(folder, "document.json")).Where(File.Exists)
            .Select(file => { try { return JsonSerializer.Deserialize<MaterialDocument>(File.ReadAllText(file), JsonDefaults.Options); } catch (JsonException) { return null; } })
            .Where(x => x is not null).Cast<MaterialDocument>().OrderBy(x => x.Name).ToArray();
    }
    public async Task<MaterialDocument> ImportAsync(string path, CancellationToken ct = default, bool enhancedOcr = false)
    {
        path = Path.GetFullPath(path); var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!DocumentReader.Extensions.Contains(ext)) throw new NotSupportedException("不支持该资料格式。");
        if (new FileInfo(path).Length > 200L * 1024 * 1024) throw new InvalidDataException("资料超过 200 MB。");
        Directory.CreateDirectory(Root);
        var staging = Path.Combine(Root, ".import-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            var source = Path.Combine(staging, "source" + ext);
            await using (var input = File.OpenRead(path)) await using (var output = File.Create(source)) await input.CopyToAsync(output, ct);
            string hash;
            await using (var hashInput = File.OpenRead(source)) hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashInput, ct));
            var existing = List().FirstOrDefault(x => x.Sha256 == hash);
            if (existing is not null && (!enhancedOcr || existing.Reader == "paddle-native-v1")) return existing;
            var (blocks, warnings) = await new DocumentReader(enhancedOcr ? new EnhancedOcrService(ocrComponents ?? paths) : null).ReadAsync(source, ct);
            var document = new MaterialDocument { Name = Path.GetFileName(path), Sha256 = hash, Extension = ext, Blocks = blocks, Warnings = warnings, Reader = enhancedOcr ? "paddle-native-v1" : "windows-basic-v1" };
            if (existing is not null)
            {
                // Upgrade derived text atomically while preserving the original ID, hash and source bytes.
                document = existing with { Blocks = blocks, Warnings = warnings, Reader = document.Reader };
                await AppPaths.AtomicWriteAsync(Path.Combine(Root, existing.Id, "document.json"), JsonSerializer.Serialize(document, JsonDefaults.Options), ct);
                return document;
            }
            await File.WriteAllTextAsync(Path.Combine(staging, "document.json"), JsonSerializer.Serialize(document, JsonDefaults.Options), ct);
            ct.ThrowIfCancellationRequested();
            Directory.Move(staging, Path.Combine(Root, document.Id));
            return document;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
