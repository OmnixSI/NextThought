using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>E23: one downloadable document model (Mac ModelCatalog entry, pinned to the HF revision).</summary>
public sealed record DocumentModel(
    string Id,
    string Repository,
    string Revision,
    string Folder,
    IReadOnlyList<DocumentModelFile> Files)
{
    public long Bytes => Files.Sum(file => file.Size);
}

public sealed record DocumentModelFile(string Name, string Remote, long Size, string Sha256);

/// <summary>
/// E23: the models of the Windows document pipeline — the PaddleOCR-VL 1.6 reader and its
/// PP-DocLayoutV3 layout model. Both live under the app's model directory, are pinned by
/// SHA-256 and mirror the manifests the old Python preparation scripts used.
/// </summary>
public static class DocumentAssets
{
    // Pinned revisions from scripts/prepare_native_ocr_model.py and scripts/prepare_layout_model.py.
    public const string OcrRepository = "PaddlePaddle/PaddleOCR-VL-1.6";
    public const string OcrRevision = "132c45f52c0a26ed5a6b2f6bd8e3985794cc27dd";
    public const string LayoutRepository = "PaddlePaddle/PP-DocLayoutV3";
    public const string LayoutRevision = "241f8bdfc77a7c7bee915a5057aaee58c235a8d3";
    public const string OcrFolder = "paddleocr-vl-1.6-native";
    public const string LayoutFolder = "pp-doclayout-v3";

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static IReadOnlyList<DocumentModel> Models { get; } = Load();

    public static DocumentModel Ocr => Models.First(model => model.Id == "paddleocr");
    public static DocumentModel Layout => Models.First(model => model.Id == "layout");

    /// <summary>Fast check: installed marker plus file sizes (full SHA-256 runs from the settings page).</summary>
    public static bool IsInstalled(AppPaths paths, DocumentModel model) =>
        model.Files.All(file => new FileInfo(Path.Combine(paths.Models, model.Folder, file.Name)) is { Exists: true } info
                                && info.Length == file.Size);

    public static bool OcrInstalled(AppPaths paths) => IsInstalled(paths, Ocr) && IsInstalled(paths, Layout);

    /// <summary>
    /// E23: maps the saved reading method to the importer switch. A missing enhancement falls back
    /// to the system reader only when the fallback key allows it; Codex reading is not ported yet.
    /// </summary>
    public static bool UseEnhancedOcr(AppPaths paths, string provider, bool fallbackToSystem)
    {
        if (provider == "codex") throw new NotSupportedException("Codex 代读的文件读取尚未移植；请改用系统识别或本机增强。");
        if (provider != "paddle") return false;
        if (OcrInstalled(paths) && new DocumentRuntimeInstaller(paths).Installed) return true;
        if (fallbackToSystem) return false;
        throw new InvalidOperationException("已选择本机增强识别，但文档组件尚未安装；请先在设置中准备，或打开系统识别兜底。");
    }

    public static async Task VerifyAsync(AppPaths paths, DocumentModel model, CancellationToken ct = default)
    {
        foreach (var file in model.Files)
        {
            var path = Path.Combine(paths.Models, model.Folder, file.Name);
            if (!File.Exists(path)) throw new FileNotFoundException("文档模型缺失：" + file.Name);
            await using var stream = File.OpenRead(path);
            if (new FileInfo(path).Length != file.Size
                || Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)) != file.Sha256)
                throw new InvalidDataException("文档模型校验失败：" + file.Name);
        }
    }

    public static async Task InstallAsync(AppPaths paths, DocumentModel model, IProgress<ModelProgress>? progress = null, CancellationToken ct = default)
    {
        var root = Path.Combine(paths.Models, model.Folder);
        Directory.CreateDirectory(root);
        foreach (var file in model.Files)
        {
            var target = Path.Combine(root, file.Name);
            if (File.Exists(target) && new FileInfo(target).Length == file.Size && await HashMatchesAsync(target, file, ct)) continue;
            var partial = target + ".partial";
            try
            {
                using var response = await Http.GetAsync(
                    $"https://huggingface.co/{model.Repository}/resolve/{model.Revision}/{file.Remote}",
                    HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long total = 0;
                await using (var output = File.Create(partial))
                {
                    var buffer = new byte[1024 * 1024];
                    int read;
                    while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > file.Size) throw new InvalidDataException("文档模型下载长度异常。");
                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        progress?.Report(new ModelProgress((double)total / file.Size, $"下载 {file.Name} · {total / 1024 / 1024} MB"));
                    }
                }
                if (total != file.Size || Convert.ToHexStringLower(hash.GetHashAndReset()) != file.Sha256)
                    throw new InvalidDataException("文档模型 SHA-256 校验失败。");
                File.Move(partial, target, true);
            }
            finally { if (File.Exists(partial)) try { File.Delete(partial); } catch (IOException) { } }
        }
        progress?.Report(new ModelProgress(1, "文档模型已校验。"));
    }

    public static void Delete(AppPaths paths, DocumentModel model)
    {
        var root = Path.GetFullPath(Path.Combine(paths.Models, model.Folder));
        if (!root.StartsWith(Path.GetFullPath(paths.Models) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("文档模型路径无效。");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static async Task<bool> HashMatchesAsync(string path, DocumentModelFile file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)) == file.Sha256;
    }

    private static IReadOnlyList<DocumentModel> Load()
    {
        using var stream = typeof(DocumentAssets).Assembly.GetManifestResourceStream("NextThought.Platform.Resources.ocr-native-assets.json")
                           ?? throw new InvalidDataException("缺少文档模型清单。");
        using var document = JsonDocument.Parse(stream);
        var ocr = new List<DocumentModelFile>();
        var layout = new List<DocumentModelFile>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var size = entry.GetProperty("size").GetInt64();
            var sha = entry.GetProperty("sha256").GetString()!;
            var folder = path.Split('/')[0];
            var name = path[(path.IndexOf('/') + 1)..];
            if (folder == OcrFolder) ocr.Add(new DocumentModelFile(name, name, size, sha));
            else if (folder == LayoutFolder) layout.Add(new DocumentModelFile(name, name, size, sha));
        }
        return
        [
            new DocumentModel("paddleocr", OcrRepository, OcrRevision, OcrFolder, ocr),
            new DocumentModel("layout", LayoutRepository, LayoutRevision, LayoutFolder, layout)
        ];
    }
}
