using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E18: drives the real 所有会议 page on demo data — content search with hit navigator, archive
    /// overview, rename, delete with the Mac "keep accepted content" option, series selection and
    /// the meeting menu actions. Screenshots and a JSON report are written to the output directory.
    /// </summary>
    private async Task RunHistoryUiCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var screenshotDirectory = Path.Combine(directory, "screenshots");
        Directory.CreateDirectory(screenshotDirectory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            // Two real archived meetings in the isolated data directory, one in a series with an
            // accepted lesson so the "keep accepted content" delete variant is offered.
            var store = new SessionStore(state.Paths);
            var first = await CreateArchivedMeetingAsync(store, "E18 自检会议甲", "我们讨论了缓存一致性的方案", "缓存怎么保证一致？", "先更新数据库再删缓存");
            var second = await CreateArchivedMeetingAsync(store, "E18 自检会议乙", "上线时间定在周五", "什么时候上线？", "周五上线");
            var series = WorkspaceSeries.Create("E18 自检系列") with
            {
                SessionIds = [first.Id.ToString("D")],
                Lessons = [new WorkspaceLesson { Text = "缓存一致性要在上线前确认。", SourceSessionId = first.Id.ToString("D") }]
            };
            new WorkspaceStore(state.Paths.Root).Save(new WorkspaceData([series]));
            state.ReloadWorkspaceForCheck();
            await state.RefreshHistoryAsync();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            state.Section = AppSection.History;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);
            Step("所有会议页加载", state.History.Count >= 2, "会议 " + state.History.Count + " 场");
            CaptureVisual(this, Path.Combine(screenshotDirectory, "history-light.png"));

            // E18 content search: typing runs the projection-backed search and rows show the hit.
            state.HistoryQuery = "缓存一致性";
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && state.HistoryContentHits.Count == 0) await Task.Delay(200);
            var row = state.History.FirstOrDefault(item => item.Meta.Id == first.Id);
            Step("内容搜索：命中转写并标在行上", state.HistoryContentHits.Count > 0 && row is { HasSearchHit: true },
                state.HistorySearchStatus + "；" + (row?.SearchHitLine ?? ""));
            if (row is not null)
            {
                await state.SelectHistoryAsync(row.Meta);
                state.RefreshHistoryHitsForSelection();
                await Task.Delay(200);
                var before = state.HistoryHitIndex;
                state.NextHistoryHitCommand.Execute(null);
                Step("搜索命中导航：搜索结果有位置/摘要且可上下移动", state.HistoryHits.Count > 0,
                    "命中 " + state.HistoryHits.Count + " 条；" + state.HistoryHitPosition + "；" + state.HistoryHitLine);
                state.PreviousHistoryHitCommand.Execute(null);
                Step("命中导航回到上一个", state.HistoryHitIndex == before, "索引 " + state.HistoryHitIndex);
                CaptureVisual(this, Path.Combine(screenshotDirectory, "history-search-light.png"));
            }
            state.HistoryQuery = "";
            await Task.Delay(300);
            Step("清空搜索：命中与导航条一起清空", state.HistoryContentHits.Count == 0 && !state.HasHistoryHits, "命中 " + state.HistoryContentHits.Count);

            // E18 archive overview and answers list for the selected meeting.
            await state.SelectHistoryAsync(first);
            state.RefreshHistoryOverview();
            await Task.Delay(250);
            Step("归档概览与回答列表", state.HasSelectedHistory && state.HistoryAnswers.Count > 0,
                state.SelectedSessionFacts);
            CaptureVisual(this, Path.Combine(screenshotDirectory, "history-overview-light.png"));

            // E18 会议操作菜单: regenerate + rename + reveal + info + prepare-next wiring.
            state.SelectedSeries = state.SeriesRows.FirstOrDefault(item => item.Id == series.Id);
            state.PrepareNextMeetingAfter(state.History.First(item => item.Meta.Id == first.Id));
            var prepareDeadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < prepareDeadline && state.Working) await Task.Delay(100);
            Step("以此为上一场，准备下一场", state.Section == AppSection.Prepare && state.CarryTranscript,
                "Section=" + state.Section + "；带入上一场转写=" + state.CarryTranscript + "；Working=" + state.Working + "；" + state.Status);
            state.Section = AppSection.History;
            var idleDeadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < idleDeadline && (state.Working || state.Recording)) await Task.Delay(100);
            await state.SelectHistoryAsync(first);
            await state.RenameSessionAsync(state.History.First(item => item.Meta.Id == first.Id), "E18 自检会议甲（改名）");
            Step("重命名会议", new SessionStore(state.Paths).List().Any(item => item.Id == first.Id && item.Title.Contains("改名")),
                new SessionStore(state.Paths).List().First(item => item.Id == first.Id).Title);
            var info = state.SessionInfoLines(state.History.First(item => item.Meta.Id == first.Id));
            Step("详细信息字段", info.Count >= 8, info.Count + " 项");

            // E18 delete: the retain option appears because the series has an accepted lesson.
            var keep = WorkspaceSourceIntegrity.HasRetainableAcceptedContent(series, [first.Id.ToString("D")]);
            var deleteRow = state.History.First(item => item.Meta.Id == second.Id);
            var deleteIdle = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deleteIdle && (state.Working || state.Recording)) await Task.Delay(100);
            var countBefore = state.History.Count;
            await state.DeleteSessionAsync(deleteRow, retainAcceptedContent: keep);
            var countAfter = new SessionStore(state.Paths).List().Count;
            Step("删除会议（保留已确认系列内容选项）", countAfter == countBefore - 1,
                "删除前 " + countBefore + "，磁盘上剩 " + countAfter + "；保留选项=" + keep + "；" + state.Status);

            // E18 series management: series selection mode and batch delete of a series with records.
            state.BeginHistorySeriesSelectionCommand.Execute(null);
            state.SelectAllHistorySeriesCommand.Execute(null);
            Step("批量删除系列：选择当前结果", state.SelectedHistorySeries.Count >= 1, "已选 " + state.SelectedHistorySeries.Count + " 个系列");
            state.ExitHistorySeriesSelectionCommand.Execute(null);
            var keepSeries = state.SeriesRows.FirstOrDefault(item => item.Id == series.Id);
            if (keepSeries is not null)
            {
                var recordsBefore = new SessionStore(state.Paths).List().Count;
                await state.DeleteSeriesWithMeetingsAsync(series.Id);
                var recordsAfter = new SessionStore(state.Paths).List().Count;
                Step("删除系列连同会议记录", recordsAfter < recordsBefore || state.SeriesRows.All(item => item.Id != series.Id),
                    "会议 " + recordsBefore + "→" + recordsAfter + "；系列剩余 " + state.SeriesRows.Count);
            }
            var exitDeadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < exitDeadline && state.Working) await Task.Delay(100);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "history-ui-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "history-ui-check: passed" : "history-ui-check: failed " + (error ?? ""));
        Close();
    }

    private static async Task<SessionMeta> CreateArchivedMeetingAsync(SessionStore store, string title, string transcript,
        string question, string answer)
    {
        var meta = new SessionMeta { Title = title };
        await store.CreateAsync(meta);
        await store.AppendTranscriptAsync(meta, new TranscriptLine
        {
            Date = meta.StartedAt.AddSeconds(5), StartedAt = meta.StartedAt, Speaker = "remote", Text = transcript
        });
        await store.AppendAnswerAsync(meta, new AnswerRecord
        {
            Date = meta.StartedAt.AddSeconds(20), Question = question, Answer = answer, Model = "m", Automatic = true
        });
        await store.SaveSummaryAsync(meta, "# 标题\n## 摘要\n缓存一致性要在上线前确认。", "m");
        await store.EndAsync(meta);
        return meta;
    }
}
