using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// §6.7 E25/E30: opens the redesigned series editor and captures its primary sections
    /// (记录/资料/经验积累) plus the 配置 sections (技能/基本信息/回答要求) in light and dark.
    /// </summary>
    private async Task RunSeriesEditorTabsCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var shots = Path.Combine(directory, "screenshots");
        Directory.CreateDirectory(shots);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        SeriesEditorWindow? window = null;
        try
        {
            AppState.PreserveDemoHistory = true;
            if (state.SeriesRows.Count == 0) state.LoadDemo();
            state.SelectedSeries = state.SeriesRows.FirstOrDefault();
            if (state.SelectedSeries is null) throw new InvalidOperationException("演示数据没有系列");
            state.LoadSeriesEditor();
            state.RefreshSeriesLessonsForCheck();
            state.RefreshSeriesSkillsForCheck();
            window = new SeriesEditorWindow { DataContext = state };
            window.Show();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(700);

            Step("编辑器打开（记录为默认分区）", window.IsVisible, state.SelectedSeriesName);
            CaptureVisual(window, Path.Combine(shots, "series-editor-records-light.png"));

            window.ShowSectionForCheck("documents");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            CaptureVisual(window, Path.Combine(shots, "series-editor-documents-light.png"));

            window.ShowSectionForCheck("knowledge");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            CaptureVisual(window, Path.Combine(shots, "series-editor-knowledge-light.png"));

            window.ShowSectionForCheck("skills");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            CaptureVisual(window, Path.Combine(shots, "series-editor-skills-light.png"));

            window.ShowSectionForCheck("basic");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            Step("基本信息草稿操作条", window.FindControl<Avalonia.Controls.Border>("EditActionsBar")?.IsVisible == true,
                "保存/取消");
            CaptureVisual(window, Path.Combine(shots, "series-editor-basic-light.png"));

            window.ShowSectionForCheck("requirements");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(300);
            CaptureVisual(window, Path.Combine(shots, "series-editor-requirements-light.png"));

            window.ShowSectionForCheck("records");
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(600);
            CaptureVisual(window, Path.Combine(shots, "series-editor-records-dark.png"));
            window.ShowSectionForCheck("knowledge");
            await Task.Delay(300);
            CaptureVisual(window, Path.Combine(shots, "series-editor-knowledge-dark.png"));
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
            try { window?.Close(); } catch { }
        }
        var report = new { passed, error, steps };
        await File.WriteAllTextAsync(Path.Combine(directory, "series-editor-tabs-check.json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        Console.WriteLine(passed ? "series-editor-tabs: passed" : "series-editor-tabs: failed " + (error ?? ""));
        Close();
    }
}
