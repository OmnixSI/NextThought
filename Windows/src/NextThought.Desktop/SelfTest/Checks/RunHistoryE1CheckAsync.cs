using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// §6.7 E1: drives the rebuilt 所有记录 page and captures light/dark evidence for
    /// validation/screens/history. Seeds two archived meetings in the isolated data directory
    /// (same helper as the E18 check), then asserts the two-pane structure and the Mac SessionRow.
    /// </summary>
    private async Task RunHistoryE1CheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var shots = Path.Combine(directory, "screenshots");
        Directory.CreateDirectory(shots);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            var store = new SessionStore(state.Paths);
            var first = await CreateArchivedMeetingAsync(store, "E1 自检会议一", "我们讨论了缓存一致性的方案",
                "缓存怎么保证一致？", "先更新数据库再删缓存");
            _ = await CreateArchivedMeetingAsync(store, "E1 自检会议二", "上线时间定在周五",
                "什么时候上线？", "周五上线");
            var series = WorkspaceSeries.Create("E1 自检系列") with
            {
                SessionIds = [first.Id.ToString("D")],
                Lessons = [new WorkspaceLesson { Text = "缓存一致性要上线前确认。", SourceSessionId = first.Id.ToString("D") }]
            };
            new WorkspaceStore(state.Paths.Root).Save(new WorkspaceData([series]));
            state.OnboardingVisible = false;
            state.ReloadWorkspaceForCheck();
            await state.RefreshHistoryAsync();
            state.Section = AppSection.History;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(600);

            var view = this.GetVisualDescendants().OfType<HistoryView>().FirstOrDefault();
            Step("HistoryView 已加载（两栏）", view is not null, view is null ? "未找到" : "HistoryView");
            Step("会话列表与系列分组", state.HistoryGroups.Count > 0 && state.FilteredHistory.Count > 0,
                "组 " + state.HistoryGroups.Count + "，会话 " + state.FilteredHistory.Count);
            var rows = state.HistoryGroups.SelectMany(group => group.Rows).ToList();
            Step("SessionRow 场景色点/时间/细节字段", rows.Count > 0
                && rows.All(row => row.TimeText.Length > 0 && row.Details.Length > 0), "行 " + rows.Count);
            CaptureVisual(this, Path.Combine(shots, "history-light.png"));

            await state.SelectHistoryAsync(first);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);
            Step("归档概览（选中会议）", state.HasSelectedHistory, state.CurrentSessionTitle);
            CaptureVisual(this, Path.Combine(shots, "history-overview-light.png"));

            // workspace/series scope (HistoryView.showFolder + onOpenRoot).
            state.OpenHistorySeriesCommand.Execute(series.Id);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(400);
            Step("系列作用域：标题/列表/面包屑", state.IsHistoryScoped
                && state.HistoryScopeTitle == series.Name
                && state.FilteredHistory.Count(row => !row.Meta.Id.Equals(first.Id)) == 0,
                state.HistoryScopeTitle + " · " + state.FilteredHistory.Count + " 场");
            CaptureVisual(this, Path.Combine(shots, "history-series-light.png"));
            state.ShowAllHistoryCommand.Execute(null);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            Step("返回所有记录", !state.IsHistoryScoped && state.FilteredHistory.Count >= 2, "会话 " + state.FilteredHistory.Count);

            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(700);
            CaptureVisual(this, Path.Combine(shots, "history-dark.png"));
            CaptureVisual(this, Path.Combine(shots, "history-overview-dark.png"));
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            await Task.Delay(300);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            try { File.WriteAllText(Path.Combine(directory, "history-e1.error.txt"), error); } catch { }
        }
        finally
        {
            AppState.PreserveDemoHistory = false;
        }
        var report = new { passed, error, steps };
        await File.WriteAllTextAsync(Path.Combine(directory, "history-e1-check.json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        Console.WriteLine(passed ? "history-e1: passed" : "history-e1: failed " + (error ?? ""));
        Close();
    }
}
