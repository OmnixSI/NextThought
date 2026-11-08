using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>Optional isolated Paddle worker. Cancellation kills the owned process tree.</summary>
public sealed class EnhancedOcrService(AppPaths paths)
{
    public string Python => Path.Combine(paths.Root, "runtimes", "paddleocr-vl-cpu", "Scripts", "python.exe");
    private async Task ValidateAsync(CancellationToken ct)
    {
        if (!File.Exists(Python)) throw new FileNotFoundException("增强 OCR 环境尚未准备，请按 P3_VALIDATION.md 安装独立 Python 组件，或取消勾选后用系统 OCR。");
        using var manifest = Assembly.GetExecutingAssembly().GetManifestResourceStream("NextThought.Platform.Resources.ocr-native-assets.json")!;
        using var json = await JsonDocument.ParseAsync(manifest, cancellationToken: ct);
        foreach (var entry in json.RootElement.EnumerateArray())
        {
            var file = Path.Combine(paths.Models, entry.GetProperty("path").GetString()!);
            if (!File.Exists(file) || new FileInfo(file).Length != entry.GetProperty("size").GetInt64()) throw new InvalidDataException("增强 OCR 组件缺失或大小不匹配。");
            await using var input = File.OpenRead(file);
            if (Convert.ToHexStringLower(await SHA256.HashDataAsync(input, ct)) != entry.GetProperty("sha256").GetString()) throw new InvalidDataException("增强 OCR 组件 SHA-256 不匹配。");
        }
    }
    public async Task<IReadOnlyList<DocumentBlock>> ReadAsync(string image, int page, CancellationToken ct = default)
    {
        await ValidateAsync(ct).ConfigureAwait(false);
        var work = Path.Combine(paths.Root, "Temp", "ocr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        try
        {
            var helper = Path.Combine(work, "worker.py"); var output = Path.Combine(work, "result.json");
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NextThought.Platform.Resources.ocr_native_worker.py")!)
            await using (var target = File.Create(helper)) await resource.CopyToAsync(target, ct);
            var start = new ProcessStartInfo(Python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "-u", helper, "--image", Path.GetFullPath(image), "--output", output, "--models", paths.Models }) start.ArgumentList.Add(value);
            start.Environment["PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK"] = "True"; start.Environment["HF_HUB_OFFLINE"] = "1"; start.Environment["PYTHONUTF8"] = "1";
            using var process = Process.Start(start) ?? throw new IOException("无法启动增强 OCR。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(10));
            // Drain bounded diagnostic output without putting model logs or source content into the UI.
            async Task Drain(StreamReader reader) { var buffer = new char[4096]; while (await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None) > 0) { } }
            var drains = Task.WhenAll(Drain(process.StandardOutput), Drain(process.StandardError));
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync().ConfigureAwait(false); await drains.ConfigureAwait(false);
                if (ct.IsCancellationRequested) throw; throw new TimeoutException("增强 OCR 单页超过 10 分钟，已停止。可改用系统 OCR。");
            }
            await drains.ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(output)) throw new IOException("增强 OCR 未完成，原文件未改动。请核对独立环境版本，或改用系统 OCR。");
            if (new FileInfo(output).Length > 8 * 1024 * 1024) throw new InvalidDataException("OCR 输出过大。");
            return Parse(await File.ReadAllTextAsync(output, ct), page);
        }
        finally { Directory.Delete(work, true); }
    }
    public static IReadOnlyList<DocumentBlock> Parse(string json, int page)
    {
        using var document = JsonDocument.Parse(json); var blocks = new List<DocumentBlock>();
        foreach (var result in document.RootElement.GetProperty("results").EnumerateArray())
        foreach (var block in result.GetProperty("json").GetProperty("res").GetProperty("parsing_res_list").EnumerateArray())
        {
            var text = block.GetProperty("block_content").GetString() ?? ""; if (text.Length == 0) continue;
            if (blocks.Count >= 1000 || text.Length > 500000) throw new InvalidDataException("OCR 区块超过上限。");
            var kind = block.GetProperty("block_label").GetString() ?? "text";
            var box = block.GetProperty("block_bbox").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            if (box.Length != 4 || box.Any(v => v < 0)) throw new InvalidDataException("OCR 区块位置无效。");
            blocks.Add(new($"第 {page} 页 · 区块 {blocks.Count + 1} · {kind} ({string.Join(",", box)})", text, page, "paddle-" + kind, box));
        }
        if (blocks.Count == 0) throw new InvalidDataException("增强 OCR 未找到文字，请对照原图或改用系统 OCR。");
        return LayoutReadingOrder.Order(blocks);
    }
}
