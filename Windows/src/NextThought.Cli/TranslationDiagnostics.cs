using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class TranslationDiagnostics
{
    public static async Task<int> RunAsync(AppPaths paths, Func<string, string?> option, CancellationToken ct)
    {
        var settingsStore = new SettingsStore(new AppPaths()); var config = settingsStore.Load();
        var options = new AnswerOptions(config.BaseUrl, config.Model, settingsStore.ReadApiKey(), config.ReasoningEffort);
        using var translator = new MeetingTranslator(new ChatCompletionClient(), options); var store = new SessionStore(paths);
        var meta = new SessionMeta { Title = "P3 公开合成翻译验收", AudioSource = "synthetic-text" }; await store.CreateAsync(meta, ct);
        var inputs = new[] { "今天先验证录音，再生成会议纪要。", "Please inspect bridge pier 7 before Friday. The budget is not approved.", "混凝土强度为 42 MPa，暂不允许通车。", "The bridge is 128 meters long. Do not change the original transcript." };
        var results = new List<object>(); bool passed = true;
        foreach (var text in inputs)
        {
            var line = new TranscriptLine { Text = text, Date = DateTimeOffset.UtcNow }; await store.AppendTranscriptAsync(meta, line, ct); var clock = Stopwatch.StartNew();
            var t = await translator.TranslateAsync(line.Id, text, ct); if (t is not null) await store.AppendTranslationAsync(meta.Id, t, ct);
            passed &= t is not null && t.Text.Length > 0 && t.Direction != MeetingTranslator.Direction(t.Text);
            results.Add(new { original = text, t?.Direction, translation = t?.Text, seconds = clock.Elapsed.TotalSeconds });
        }
        using var cancellation = new CancellationTokenSource(100); var canceled = await translator.TranslateAsync(Guid.NewGuid(), "请把这段公开测试文本翻译为英文，并保持数字 512 不变。", cancellation.Token);
        var recovery = await translator.TranslateAsync(Guid.NewGuid(), "连接恢复。", ct); passed &= canceled is null && recovery is not null;
        await store.EndAsync(meta, ct); var reloaded = store.Load(meta.Id); passed &= reloaded.Lines.Count == 4 && reloaded.Lines.All(l => l.Translation is not null);
        await store.ExportMarkdownAsync(meta.Id, Path.ChangeExtension(option("--json"), ".md")!, ct);
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, backend = config.BaseUrl, model = config.Model, sessionId = meta.Id, canceled = canceled is null, recovery = recovery?.Text, results }, JsonDefaults.Options), ct);
        Console.WriteLine("Translation directions/cancel/recovery/archive/export: " + (passed ? "PASS" : "FAIL")); return passed ? 0 : 2;
    }
}
