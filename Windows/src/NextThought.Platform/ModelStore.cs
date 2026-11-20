using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

public sealed class ModelPackage
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Archive { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string VocabularySha256 { get; init; } = "";
    public Dictionary<string, long> Files { get; init; } = [];
}
[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(ModelPackage[]))]
internal sealed partial class ModelCatalogJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}

public sealed class ModelStore(AppPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    public static IReadOnlyList<ModelPackage> Catalog { get; } = LoadCatalog();
    public static ModelPackage Package(string id) => Catalog.First(x => x.Id == id);
    public string DirectoryFor(string id) => Path.Combine(paths.Models, Package(id).Archive);
    public bool IsInstalled(string id)
    {
        var package = Package(id); var directory = DirectoryFor(id); var manifest = Path.Combine(directory, "installed.json");
        if (!File.Exists(manifest)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            if (doc.RootElement.GetProperty("sha256").GetString() != package.Sha256) return false;
            foreach (var (name, size) in package.Files) if (!File.Exists(Path.Combine(directory, name)) || new FileInfo(Path.Combine(directory, name)).Length != size) return false;
            var vocabulary = Path.Combine(directory, "bpe.vocab");
            return File.Exists(vocabulary) && Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(vocabulary))) == package.VocabularySha256;
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException) { return false; }
    }
    public async Task InstallAsync(string id, IProgress<ModelProgress>? progress = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var package = Package(id);
            if (IsInstalled(id)) { progress?.Report(new(1, package.Title + " 已校验")); return; }
            Directory.CreateDirectory(paths.Downloads); Directory.CreateDirectory(paths.Models);
            var archive = Path.Combine(paths.Downloads, package.Archive + ".tar.bz2");
            if (!File.Exists(archive) || await HashAsync(archive, ct) != package.Sha256)
            {
                var partial = archive + ".partial";
                try
                {
                    using var response = await http.GetAsync("https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/" + package.Archive + ".tar.bz2", HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();
                    await using var input = await response.Content.ReadAsStreamAsync(ct);
                    await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1024*1024, true))
                    {
                        var buffer = new byte[1024*1024]; long total = 0; var length = response.Content.Headers.ContentLength;
                        while (await input.ReadAsync(buffer, ct) is var read && read > 0)
                        { await output.WriteAsync(buffer.AsMemory(0, read), ct); total += read; progress?.Report(new(length is > 0 ? total/(double)length*.88 : 0, $"下载 {package.Title} · {total/1024/1024} MB")); }
                    }
                    progress?.Report(new(.9, "校验 " + package.Title));
                    if (await HashAsync(partial, ct) != package.Sha256) throw new InvalidDataException("模型 SHA-256 校验失败，请重新下载。");
                    File.Move(partial, archive, true);
                }
                finally { if (File.Exists(partial)) File.Delete(partial); }
            }
            var staging = Path.Combine(paths.Models, ".install-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
            var keepRecovery = false;
            try
            {
                progress?.Report(new(.94, "解压 " + package.Title));
                var info = new ProcessStartInfo(TarExecutable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
                foreach (var argument in new[]{"-xjf", archive, "-C", staging}) info.ArgumentList.Add(argument);
                using var process = Process.Start(info) ?? throw new IOException("无法启动 Windows tar.exe。");
                var errors = process.StandardError.ReadToEndAsync(ct); var stdout = process.StandardOutput.ReadToEndAsync(ct);
                using var registration = ct.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } });
                await process.WaitForExitAsync(ct);
                if (process.ExitCode != 0) throw new IOException("模型解压失败：" + await errors);
                await stdout; ct.ThrowIfCancellationRequested();
                var extracted = Path.Combine(staging, package.Archive); var ready = Path.Combine(staging, "ready"); Directory.CreateDirectory(ready);
                foreach (var (name, size) in package.Files)
                {
                    var file = Path.Combine(extracted, name);
                    if (!File.Exists(file) || new FileInfo(file).Length != size) throw new InvalidDataException("模型文件不完整：" + name);
                    File.Move(file, Path.Combine(ready, name));
                }
                using (var vocabulary = Resource("xasr-bpe.vocab"))
                using (var output = File.Create(Path.Combine(ready, "bpe.vocab"))) await vocabulary.CopyToAsync(output, ct);
                await File.WriteAllTextAsync(Path.Combine(ready, "installed.json"), JsonSerializer.Serialize(new { archive = package.Archive, sha256 = package.Sha256, installedAt = DateTimeOffset.UtcNow }), ct);
                var target = DirectoryFor(id); var old = Path.Combine(staging, "old");
                if (Directory.Exists(target)) Directory.Move(target, old);
                try { Directory.Move(ready, target); }
                catch
                {
                    if (Directory.Exists(old)) { try { Directory.Move(old, target); } catch { keepRecovery = true; } }
                    throw;
                }
                if (!IsInstalled(id)) throw new InvalidDataException("模型安装校验未通过。");
                progress?.Report(new(1, package.Title + " 已就绪"));
            }
            finally
            {
                var resolved = Path.GetFullPath(staging);
                if (!keepRecovery && resolved.StartsWith(Path.GetFullPath(paths.Models) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(resolved, true);
            }
        }
        finally { gate.Release(); }
    }
    /// <summary>F21: Windows' bsdtar sits in System32; never resolve it through PATH.</summary>
    internal static string TarExecutable
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
            if (!File.Exists(path)) throw new FileNotFoundException("找不到系统 tar.exe。", path);
            return path;
        }
    }

    private static async Task<string> HashAsync(string file, CancellationToken ct)
    { await using var input = File.OpenRead(file); return Convert.ToHexStringLower(await SHA256.HashDataAsync(input, ct)); }
    private static Stream Resource(string suffix) => typeof(ModelStore).Assembly.GetManifestResourceStream("NextThought.Platform.Resources." + suffix) ?? throw new InvalidDataException("缺少模型目录资源。");
    private static IReadOnlyList<ModelPackage> LoadCatalog()
    { using var resource = Resource("models.json"); return JsonSerializer.Deserialize(resource, ModelCatalogJsonContext.Default.ModelPackageArray) ?? throw new InvalidDataException("模型目录无效。"); }
}
