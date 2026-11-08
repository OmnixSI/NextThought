using System.Diagnostics;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class ServiceDiagnostics
{
    public static async Task<int> TestAsync(AppPaths paths, Func<string, string?> option, IReadOnlyList<string> args, CancellationToken ct)
    {
        var store = new SettingsStore(paths); var settings = store.Load();
        settings.BaseUrl = option("--base-url") ?? settings.BaseUrl;
        settings.Model = option("--model") ?? settings.Model;
        settings.ReasoningEffort = option("--effort") ?? settings.ReasoningEffort;
        if (option("--system-name") is { } outputName) settings.SystemDeviceId = AudioCaptureService.Devices(true).First(x => x.Name.Contains(outputName, StringComparison.OrdinalIgnoreCase)).Id;
        if (option("--mic-name") is { } inputName) settings.MicrophoneDeviceId = AudioCaptureService.Devices(false).First(x => x.Name.Contains(inputName, StringComparison.OrdinalIgnoreCase)).Id;
        _ = ChatCompletionClient.Endpoint(settings.BaseUrl);
        if (args.Contains("--save-settings")) await store.SaveAsync(settings, ct);
        var options = new AnswerOptions(settings.BaseUrl, settings.Model, store.ReadApiKey(), settings.ReasoningEffort, 400);
        var client = new ChatCompletionClient(); var results = new List<object>(); var allPassed = true;
        async Task Check(string name, string prompt, string expected)
        {
            var clock = Stopwatch.StartNew(); double? first = null; var updates = 0;
            try
            {
                var answer = await client.AnswerAsync(options, "你正在执行软件验收。只依据提供的内容，用一句简洁中文回答。", prompt,
                    _ => { first ??= clock.Elapsed.TotalSeconds; updates++; }, ct);
                var passed = answer.Contains(expected, StringComparison.OrdinalIgnoreCase); allPassed &= passed;
                results.Add(new { name, passed, answer, firstContentSeconds = first, elapsedSeconds = clock.Elapsed.TotalSeconds, updates });
                Console.WriteLine($"{name}: {(passed ? "PASS" : "FAIL")} · {answer}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { allPassed = false; results.Add(new { name, passed = false, error = ex.Message }); Console.Error.WriteLine(name + ": " + ex.Message); }
        }
        await Check("connection", "请只回复：连接成功", "连接成功");
        if (allPassed)
        {
            await Check("manual-answer", "【合成测试会议】会议明确决定：由张三负责 Windows 移植验收，周五交付。问题：谁负责验收？", "张三");
            var tracker = new QuestionCandidateTracker();
            const string question = "为什么要先验证录音，再生成会议纪要？";
            var decision = tracker.Ingest(question, true, Guid.NewGuid(), DateTimeOffset.UtcNow);
            if (decision.Kind == CandidateKind.Trigger)
                await Check("automatic-answer", "【合成测试会议】录音是纪要的输入，需要先确认转写完整。问题：" + question, "转写");
            else { allPassed = false; results.Add(new { name = "automatic-answer", passed = false, error = "Question did not trigger" }); }
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var fragments = 0; var cancelled = false;
            try { await client.AnswerAsync(options, "软件取消验收。", "请列出 50 个会议记录测试项目。", _ => { fragments++; cancel.Cancel(); }, cancel.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { cancelled = true; }
            allPassed &= cancelled && fragments > 0;
            results.Add(new { name = "cancel-live-stream", passed = cancelled && fragments > 0, fragments });
            Console.WriteLine("cancel-live-stream: " + (cancelled ? "PASS" : "FAIL"));
        }
        var report = new { testedAt = DateTimeOffset.Now, passed = allPassed, endpoint = ChatCompletionClient.Endpoint(options.BaseUrl).AbsoluteUri,
            model = options.Model, reasoningEffort = options.ReasoningEffort, inputKind = "public synthetic acceptance fixtures only", results };
        var file = option("--json") ?? throw new ArgumentException("请指定 --json 验证报告路径。");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(report, JsonDefaults.Options), ct);
        return allPassed ? 0 : 2;
    }
}
