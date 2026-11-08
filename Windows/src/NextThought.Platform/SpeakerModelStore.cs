using System.Security.Cryptography;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class SpeakerModelStore(AppPaths paths)
{
    private static readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    public bool IsInstalled => File.Exists(SpeakerEmbedder.ModelFile(paths)) && new FileInfo(SpeakerEmbedder.ModelFile(paths)).Length == 26530309;
    public async Task InstallAsync(IProgress<ModelProgress>? progress = null, CancellationToken ct = default)
    {
        var file = SpeakerEmbedder.ModelFile(paths); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file)) { await using var current = File.OpenRead(file); if (Convert.ToHexStringLower(await SHA256.HashDataAsync(current, ct).ConfigureAwait(false)) == SpeakerEmbedder.Sha256) return; }
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var response = await http.GetAsync("https://huggingface.co/Wespeaker/wespeaker-resnet34-LM/resolve/f0c48c298fd835726c27956a5d617bad7115627e/voxceleb_resnet34_LM.onnx?download=true", HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false); long total = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[1024 * 1024]; int read;
                while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                { total += read; if (total > 26530309) throw new InvalidDataException("声纹模型长度异常。"); hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false); progress?.Report(new(total / 26530309.0, $"准备声纹模型 · {total / 1024 / 1024} MB")); }
            }
            if (total != 26530309 || Convert.ToHexStringLower(hash.GetHashAndReset()) != SpeakerEmbedder.Sha256) throw new InvalidDataException("声纹模型长度或 SHA-256 校验失败。");
            ct.ThrowIfCancellationRequested(); File.Move(temporary, file, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
