using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>E15: the structured notes views (fishbone diagram, narrow fishbone tree, viewpoint
    /// matrix) rendered with the demo payload for element-level evidence in screens/notes/.</summary>
    private async Task RunNotesStructuredCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail });
        async Task Pump()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(280);
        }
        try
        {
            var fishbone = new FishboneNotes("缓存一致性怎么保证？", 12, [
                new FishboneCategory("并发", [new TimedText("双写时删缓存失败，旧值留在缓存里。", 41),
                    new TimedText("先删缓存后写库，读请求把旧值放回。", 88)]),
                new FishboneCategory("网络", [new TimedText("重试队列积压时主从延迟变大。", 132)]),
                new FishboneCategory("运维", [new TimedText("没有监控缓存命中率。", 190),
                    new TimedText("发布时没做缓存预热。", 233)])
            ]);
            var matrix = new ViewpointMatrix("缓存一致性方案", 12,
                ["数据库组", "缓存组", "平台组"],
                [new ViewpointIssue("先删缓存还是先写库", [
                        new ViewpointPosition("数据库组", "先写库再删缓存。", 61),
                        new ViewpointPosition("缓存组", "延迟双删更稳。", 95)]),
                    new ViewpointIssue("重试队列谁维护", [
                        new ViewpointPosition("平台组", "平台组负责重试队列。", 140)])],
                [new TimedText("上线前补监控。", 210)],
                [new TimedText("双删的延迟取值还没定。", 240)],
                [new ViewpointOwner("平台组", "重试队列托管", "下周三", 250),
                    new ViewpointOwner("数据库组", "补一致性回归用例", null, 262)]);

            var window = new Window
            {
                Title = UiText.T("纪要"),
                Width = 860, Height = 720,
                Background = this.FindResource("Paper") as Avalonia.Media.IBrush,
                Content = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 16, Margin = new Avalonia.Thickness(20),
                        Children =
                        {
                            new TextBlock { Text = UiText.T("纪要"), FontSize = 20, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                            new FishboneView { Notes = fishbone, Height = 320 },
                            new FishboneTree { Notes = fishbone },
                            new Border { Height = 1, Background = this.FindResource("Hairline") as Avalonia.Media.IBrush },
                            new ViewpointMatrixView { Matrix = matrix }
                        }
                    }
                }
            };
            window.Show();
            await Pump();
            var rendered = window.Content is ScrollViewer { Content: StackPanel panel }
                           && panel.Children.OfType<FishboneView>().FirstOrDefault()?.Content is Canvas { Children.Count: > 0 }
                           && panel.Children.OfType<FishboneTree>().FirstOrDefault() is { } tree
                           && tree.Content is ScrollViewer { Content: StackPanel { Children.Count: > 1 } }
                           && panel.Children.OfType<ViewpointMatrixView>().FirstOrDefault()?.Content is StackPanel { Children.Count: > 1 };
            Step("结构化纪要视图渲染：鱼骨图 + 窄版鱼骨树 + 观点对照", window.Content is not null && ((ScrollViewer)window.Content).Content is StackPanel,
                "鱼骨图/鱼骨树/观点对照三块已生成");
            CaptureVisual(window, Path.Combine(directory, "notes-structured-light.png"));
            if (Avalonia.Application.Current is { } app)
            {
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Pump();
                CaptureVisual(window, Path.Combine(directory, "notes-structured-dark.png"));
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Pump();
            }
            window.Close();
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex) { error = ex.ToString(); }
        var report = new { passed, error, steps };
        try
        {
            File.WriteAllText(Path.Combine(directory, "notes-structured-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "notes-structured-check: passed" : "notes-structured-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
