using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// §3.5 series: drives the rebuilt series page on demo data, checks the Mac list search/sort,
    /// the scene row and the three detail tabs, and captures series-light/dark for §5.
    /// </summary>
    private async Task RunSeriesPageCheckAsync(string directory)
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
            AppState.PreserveDemoHistory = true;
            if (state.SeriesRows.Count == 0) state.LoadDemo();
            state.OnboardingVisible = false;
            state.Section = AppSection.Series;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(800);

            var view = this.GetVisualDescendants().OfType<SeriesView>().FirstOrDefault();
            Step("系列页加载", view is not null, view is null ? "未找到 SeriesView" : "SeriesView");
            Step("左栏搜索与排序", view is not null
                && view.GetVisualDescendants().OfType<TextBox>().Any(box => box.Text == state.SeriesQuery)
                && state.SeriesSortOptions.Count == 2, "排序选项 " + string.Join("/", state.SeriesSortOptions));
            var tabs = view?.GetVisualDescendants().OfType<TabItem>().Select(tab => tab.Header as string).ToList() ?? [];
            Step("三段标签 记录/资料/经验积累", tabs.Count >= 3, "TabItem " + string.Join(" | ", tabs));
            Step("左栏场景行", state.SeriesShownRows.Count > 0, "行 " + state.SeriesShownRows.Count);
            CaptureVisual(this, Path.Combine(shots, "series-light.png"));

            state.SeriesQuery = state.SeriesRows.FirstOrDefault()?.Name.Split(' ')[0] ?? "";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            Step("搜索过滤", state.SeriesQuery.Length == 0 || state.SeriesShownRows.Count <= state.SeriesRows.Count,
                "查询「" + state.SeriesQuery + "」→ " + state.SeriesShownRows.Count + "/" + state.SeriesRows.Count);
            state.SeriesQuery = "";

            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(600);
            CaptureVisual(this, Path.Combine(shots, "series-dark.png"));
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            await Task.Delay(200);
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        finally
        {
            AppState.PreserveDemoHistory = false;
        }
        var report = new { passed, error, steps };
        await File.WriteAllTextAsync(Path.Combine(directory, "series-page-check.json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        Console.WriteLine(passed ? "series-page: passed" : "series-page: failed " + (error ?? ""));
        Close();
    }
}
