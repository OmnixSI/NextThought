using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class MaterialStore(AppPaths paths)
{
    public string Root => Path.Combine(paths.Root, "Materials");
    public string SourcePath(MaterialDocument document) => Path.Combine(Root, document.Id, "source" + document.Extension);
    public IReadOnlyList<MaterialDocument> List()
    {
        if (!Directory.Exists(Root)) return [];
        return Directory.EnumerateFiles(Root, "document.json", SearchOption.AllDirectories)
            .Select(file => { try { return JsonSerializer.Deserialize<MaterialDocument>(File.ReadAllText(file), JsonDefaults.Options); } catch (JsonException) { return null; } })
            .Where(x => x is not null).Cast<MaterialDocument>().OrderBy(x => x.Name).ToArray();
    }
    public async Task<MaterialDocument> ImportAsync(string path, CancellationToken ct = default)
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
            var existing = List().FirstOrDefault(x => x.Sha256 == hash); if (existing is not null) return existing;
            var (blocks, warnings) = await new DocumentReader().ReadAsync(source, ct);
            var document = new MaterialDocument { Name = Path.GetFileName(path), Sha256 = hash, Extension = ext, Blocks = blocks, Warnings = warnings };
            await File.WriteAllTextAsync(Path.Combine(staging, "document.json"), JsonSerializer.Serialize(document, JsonDefaults.Options), ct);
            ct.ThrowIfCancellationRequested();
            Directory.Move(staging, Path.Combine(Root, document.Id));
            return document;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
