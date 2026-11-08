using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record EmbeddingAsset(string Name, long Size, string Sha256, string? Remote = null);
public sealed record EmbeddingManifest(string Id, string Repository, string Revision, string OriginalModel, int Dimension, int MaxTokens, string QueryInstruction, EmbeddingAsset[] Files);
public sealed class EmbeddingModelStore(AppPaths paths)
{
    private static readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    public static EmbeddingManifest Manifest { get; } = ReadManifest();
    private readonly SemaphoreSlim gate = new(1, 1);
    public string Root => Path.Combine(paths.Models, Manifest.Id);
    public bool IsInstalled => File.Exists(Path.Combine(Root, "installed.json")) && Manifest.Files.All(f => File.Exists(Path.Combine(Root, f.Name)) && new FileInfo(Path.Combine(Root, f.Name)).Length == f.Size);
    public async Task VerifyAsync(CancellationToken ct = default)
    {
        if (!IsInstalled) throw new FileNotFoundException("尚未准备语义检索模型，请先点击准备检索组件。");
        foreach (var asset in Manifest.Files)
        { await using var stream = File.OpenRead(Path.Combine(Root, asset.Name)); if (Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)) != asset.Sha256) throw new InvalidDataException("语义模型校验失败：" + asset.Name); }
    }
    public async Task InstallAsync(IProgress<ModelProgress>? progress = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Root);
            foreach (var asset in Manifest.Files)
            {
                var target = Path.Combine(Root, asset.Name);
                if (File.Exists(target))
                { await using var old = File.OpenRead(target); if (old.Length == asset.Size && Convert.ToHexStringLower(await SHA256.HashDataAsync(old, ct).ConfigureAwait(false)) == asset.Sha256) continue; }
                var temporary = target + ".partial";
                try
                {
                    using var response = await http.GetAsync($"https://huggingface.co/{Manifest.Repository}/resolve/{Manifest.Revision}/{asset.Remote ?? asset.Name}", HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); response.EnsureSuccessStatusCode();
                    await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long total = 0;
                    await using (var output = File.Create(temporary))
                    {
                        var buffer = new byte[1024 * 1024]; int read;
                        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                        { total += read; if (total > asset.Size) throw new InvalidDataException("模型下载长度异常。"); await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false); hash.AppendData(buffer, 0, read); progress?.Report(new((double)total / asset.Size, $"准备语义模型 {asset.Name} · {total / 1024 / 1024} MB")); }
                    }
                    if (total != asset.Size || Convert.ToHexStringLower(hash.GetHashAndReset()) != asset.Sha256) throw new InvalidDataException("语义模型 SHA-256 校验失败。");
                    File.Move(temporary, target, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            await AppPaths.AtomicWriteAsync(Path.Combine(Root, "installed.json"), JsonSerializer.Serialize(Manifest, JsonDefaults.Options), ct).ConfigureAwait(false);
            progress?.Report(new(1, "语义模型已校验，可离线检索。"));
        }
        finally { gate.Release(); }
    }
    private static EmbeddingManifest ReadManifest()
    { using var stream = typeof(EmbeddingModelStore).Assembly.GetManifestResourceStream("NextThought.Platform.Resources.embedding-model.json")!; return JsonSerializer.Deserialize<EmbeddingManifest>(stream, JsonDefaults.Options)!; }
}
