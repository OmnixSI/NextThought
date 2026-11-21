using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E19: exports one archived meeting through every 格式 × 转写版本 × 语言 combination, checks the
    /// real files, and screenshots the export sheet. The window runs on an isolated data directory.
    /// </summary>
    private async Task RunSessionExportCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var exportDirectory = Path.Combine(directory, "exports");
        Directory.CreateDirectory(exportDirectory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var store = new SessionStore(state.Paths);
            var meta = new SessionMeta { Title = "E19 自检导出 / 缓存: 复盘" };
            await store.CreateAsync(meta);
            await store.AppendTranscriptAsync(meta, new TranscriptLine
            {
                Date = meta.StartedAt.AddSeconds(10),
                StartedAt = meta.StartedAt.AddSeconds(10),
                EndedAt = meta.StartedAt.AddSeconds(15),
                Speaker = "remote",
                Text = "我们讨论了缓存一致性的方案",
                Translation = new SessionTranslation(Guid.NewGuid(), 1, "hash", "zh-en",
                    "We discussed cache consistency.", "m", meta.StartedAt.AddSeconds(12))
            });
            await store.AppendAnswerAsync(meta, new AnswerRecord
            {
                Date = meta.StartedAt.AddSeconds(20), Question = "缓存怎么保证一致？",
                Answer = "先更新数据库再删缓存。", Model = "m", Automatic = true
            });
            await store.SaveSummaryAsync(meta, "## 摘要\n缓存一致性要在上线前确认。", "m");
            await store.EndAsync(meta);
            await state.RefreshHistoryAsync();
            var row = state.History.FirstOrDefault(item => item.Meta.Id == meta.Id);
            if (row is null) throw new InvalidOperationException("导出测试会议没有出现在历史列表里。");
            await state.SelectHistoryAsync(row.Meta);
            var prepared = state.PrepareSessionExport();
            Step("导出会话准备", prepared && state.SessionExportTitle.Length > 0, state.SessionExportTitle);

            var formats = new[]
            {
                SessionExportFormat.Markdown, SessionExportFormat.Docx, SessionExportFormat.Pdf,
                SessionExportFormat.Srt, SessionExportFormat.Vtt
            };
            var variants = new[] { SessionTranscriptVariant.Corrected, SessionTranscriptVariant.Original };
            var languages = new[] { SessionLanguageView.Bilingual, SessionLanguageView.Source, SessionLanguageView.Translated };
            var exported = 0;
            var failures = new List<string>();
            foreach (var format in formats)
            foreach (var variant in variants)
            foreach (var language in languages)
            {
                var path = Path.Combine(exportDirectory,
                    $"{format}-{variant}-{language}.{format.FileExtension()}");
                if (!await state.SaveSessionExportAsync(format, variant, language, path))
                {
                    failures.Add(format + "/" + variant + "/" + language + "：导出返回失败");
                    continue;
                }
                var bytes = await File.ReadAllBytesAsync(path);
                var text = format is SessionExportFormat.Markdown or SessionExportFormat.Srt or SessionExportFormat.Vtt
                    ? System.Text.Encoding.UTF8.GetString(bytes) : "";
                var ok = bytes.Length > 0 && format switch
                {
                    SessionExportFormat.Markdown => text.StartsWith("# ") && text.Contains("完整转录"),
                    SessionExportFormat.Docx => bytes.Length > 2 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K',
                    SessionExportFormat.Pdf => bytes.Length > 4 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF",
                    SessionExportFormat.Srt => text.Contains("-->") && text.Contains("00:00:10"),
                    _ => text.StartsWith("WEBVTT")
                };
                if (ok) exported++; else failures.Add(format + "/" + variant + "/" + language + "：文件头或内容不符");
            }
            Step("逐个导出的格式全部成功", exported == formats.Length * variants.Length * languages.Length,
                "成功 " + exported + "/" + formats.Length * variants.Length * languages.Length
                + (failures.Count > 0 ? "；" + string.Join("；", failures.Take(4)) : ""));

            var original = state.BuildSessionExportText(SessionExportFormat.Markdown,
                SessionTranscriptVariant.Original, SessionLanguageView.Source);
            var corrected = state.BuildSessionExportText(SessionExportFormat.Markdown,
                SessionTranscriptVariant.Corrected, SessionLanguageView.Bilingual);
            Step("转写版本与语言影响导出", original.Contains("我们讨论了缓存一致性的方案")
                && !original.Contains("We discussed cache consistency.")
                && corrected.Contains("We discussed cache consistency."),
                "原始记录无译文=" + !original.Contains("We discussed cache consistency.") + "；双语文有译文=" + corrected.Contains("We discussed cache consistency."));

            // Screenshot the sheet itself (its preview shows the same projection the files carry).
            var sheet = new SessionExportSheet { DataContext = state };
            sheet.Show();
            await Task.Delay(800);
            CaptureVisual(sheet, Path.Combine(directory, "session-export-sheet-light.png"));
            sheet.Close();
            await Task.Delay(300);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "session-export-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "session-export-check: passed" : "session-export-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
