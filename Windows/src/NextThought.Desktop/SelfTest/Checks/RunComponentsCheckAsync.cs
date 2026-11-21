using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NextThought.Desktop.Controls;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// Stage 3 §6.1: renders the component sample page, checks every reusable control is present and
    /// that MarkdownText produced bold/bullet/inline-code runs, then captures the light and dark
    /// samples for the compare notes.
    /// </summary>
    private async Task RunComponentsCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
#if DEBUG
            var window = new ComponentSamplesWindow();
            window.Show();
            await Task.Delay(900);

            var tree = window.GetVisualDescendants().ToList();
            var expected = new (string Name, Type Type)[]
            {
                ("ControlButtonBody", typeof(ControlButtonBody)),
                ("BusyActionLabel", typeof(BusyActionLabel)),
                ("AIHeading", typeof(AIHeading)),
                ("InkSegmented", typeof(InkSegmented)),
                ("InteractiveRowBody", typeof(InteractiveRowBody)),
                ("ShortcutHint", typeof(ShortcutHint)),
                ("StatusDot", typeof(StatusDot)),
                ("InkSwitch", typeof(InkSwitch)),
                ("WideToggleRow", typeof(WideToggleRow)),
                ("AppBackdrop", typeof(AppBackdrop)),
                ("GlassGroup", typeof(GlassGroup)),
                ("MenuPicker", typeof(MenuPicker)),
                ("PillTabs", typeof(PillTabs)),
                ("ProminentFill", typeof(ProminentFill)),
                ("ScrollEdgeFade", typeof(ScrollEdgeFade)),
                ("OperationFeedback", typeof(OperationFeedback)),
                ("PaperFieldBody", typeof(PaperFieldBody)),
                ("Lift", typeof(Lift)),
                ("VoiceWave", typeof(VoiceWave)),
                ("MarkdownText", typeof(MarkdownText)),
                ("MetricPill", typeof(MetricPill)),
                ("RefinementBar", typeof(RefinementBar)),
                ("UnderlineTabs", typeof(UnderlineTabs))
            };
            var missing = expected.Where(entry => tree.All(visual => visual.GetType() != entry.Type)).Select(entry => entry.Name).ToList();
            Step("23 个通用组件出现在样张页", missing.Count == 0, missing.Count == 0 ? "全部存在" : "缺少：" + string.Join(",", missing));

            var markdown = tree.OfType<MarkdownText>().FirstOrDefault();
            Step("MarkdownText 渲染加粗/列表/行内代码",
                (markdown?.RenderedBlockCount ?? 0) >= 3 && (markdown?.InlineRunCount ?? 0) >= 3,
                "blocks=" + (markdown?.RenderedBlockCount ?? 0) + " inlineRuns=" + (markdown?.InlineRunCount ?? 0));

            CaptureVisual(window, Path.Combine(directory, "component-samples-light.png"));
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            await Task.Delay(700);
            CaptureVisual(window, Path.Combine(directory, "component-samples-dark.png"));
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            await Task.Delay(300);
            Step("浅色与深色样张截图", true, "component-samples-light.png / component-samples-dark.png");
            window.Close();
#else
            Step("样张页（仅调试构建）", true, "component sample page is debug-only");
#endif
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "components-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "components-check: passed" : "components-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
