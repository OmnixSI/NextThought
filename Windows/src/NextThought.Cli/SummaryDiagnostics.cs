using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class SummaryDiagnostics
{
    public static async Task<int> TestAsync(AppPaths modelPaths, Func<string, string?> option, CancellationToken ct)
    {
        var sessionRoot = new AppPaths(option("--session-root") ?? throw new ArgumentException("Missing --session-root"));
        var sessions = new SessionStore(sessionRoot); var settingsStore = new SettingsStore(modelPaths); var settings = settingsStore.Load();
        var options = new AnswerOptions(settings.BaseUrl, settings.NotesModel.Length > 0 ? settings.NotesModel : settings.Model,
            settingsStore.ReadApiKey(), settings.ReasoningEffort, 1800);
        var results = new List<object>();
        var shortMeta = new SessionMeta { Title = "P1 合成纪要验收", AudioSource = "synthetic-text-fixture" };
        await sessions.CreateAsync(shortMeta, ct);
        var facts = new[] { "今天讨论 Windows 移植。", "决定先验证 HECATE 录音，再验收问答和会议纪要。",
            "张三负责周五前完成录音验收。", "李四负责整理纪要，交付时间尚未确定。", "GPU 加速尚未决定采用。" };
        for (var i = 0; i < facts.Length; i++) await sessions.AppendTranscriptAsync(shortMeta,
            new TranscriptLine { Date = shortMeta.StartedAt.AddSeconds(i * 10), Text = facts[i], Speaker = i % 2 == 0 ? "remote" : "me" }, ct);
        await sessions.EndAsync(shortMeta, ct);
        var client = new ChatCompletionClient();
        var shortSummary = await new SummaryService(sessions, client).GenerateAsync(shortMeta.Id, options, ct: ct);
        var shortPassed = shortSummary.Contains("张三") && shortSummary.Contains("李四") && shortSummary.Contains("周五") && sessions.Load(shortMeta.Id).Meta.SummaryState == "ready";
        results.Add(new { name = "short-live-summary", passed = shortPassed, sessionId = shortMeta.Id, markdown = shortSummary });
        Console.WriteLine("short-live-summary: " + (shortPassed ? "PASS" : "FAIL"));
        var longMeta = new SessionMeta { Title = "P1 分段纪要重试验收", AudioSource = "synthetic-text-fixture" };
        await sessions.CreateAsync(longMeta, ct);
        for (var i = 0; i < 70; i++) await sessions.AppendTranscriptAsync(longMeta,
            new TranscriptLine { Date = longMeta.StartedAt.AddSeconds(i * 10), Speaker = "remote",
                Text = $"议题 {i + 1}：本轮只验证 Windows 采音、回答、归档和纪要。" + facts[i % facts.Length] + "未明确的信息应标为待确认。" }, ct);
        await sessions.EndAsync(longMeta, ct);
        var outage = new OutageClient(client); var service = new SummaryService(sessions, outage); var interrupted = false;
        try { await service.GenerateAsync(longMeta.Id, options, ct: ct, chunkLimit: 2400); }
        catch (HttpRequestException) when (outage.Injected) { interrupted = true; }
        var progress = new List<SummaryProgress>();
        var longSummary = await service.GenerateAsync(longMeta.Id, options, progress.Add, ct, 2400);
        var longPassed = interrupted && progress.Any(p => p.Reused) && progress.Any(p => p.Total > 1) && sessions.Load(longMeta.Id).Meta.SummaryState == "ready";
        results.Add(new { name = "chunked-live-summary-with-controlled-outage", passed = longPassed, sessionId = longMeta.Id,
            outageInjected = outage.Injected, reusedChunks = progress.Count(p => p.Reused), chunks = progress.Max(p => p.Total), markdown = longSummary });
        Console.WriteLine("chunked-live-summary-with-controlled-outage: " + (longPassed ? "PASS" : "FAIL"));
        var file = option("--json") ?? throw new ArgumentException("Missing --json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed = shortPassed && longPassed,
            endpoint = ChatCompletionClient.Endpoint(options.BaseUrl).AbsoluteUri, model = options.Model, inputKind = "synthetic public test facts only", results }, JsonDefaults.Options), ct);
        await sessions.ExportMarkdownAsync(shortMeta.Id, Path.Combine(sessionRoot.Root, "short-summary.md"), ct);
        await sessions.ExportMarkdownAsync(longMeta.Id, Path.Combine(sessionRoot.Root, "chunked-summary.md"), ct);
        return shortPassed && longPassed ? 0 : 2;
    }
    private sealed class OutageClient(IAnswerClient inner) : IAnswerClient
    {
        private int calls;
        public bool Injected { get; private set; }
        public Task<string> AnswerAsync(AnswerOptions options, string system, string user, Action<string>? update = null, CancellationToken ct = default, int timeoutSeconds = 90)
        {
            if (++calls == 2) { Injected = true; throw new HttpRequestException("Controlled interruption before the second segment request."); }
            return inner.AnswerAsync(options, system, user, update, ct, timeoutSeconds);
        }
    }
}
