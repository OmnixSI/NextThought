using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Desktop.Localization;
using NextThought.Desktop.Views;

namespace NextThought.Desktop.Views;

/// <summary>
/// J7: end-to-end check of the global text zoom. Walks the six Mac steps down to the three the
/// task book names (0.9 / 1.2 / 1.45), proves the pages are rescaled through
/// <see cref="TextZoom.Scaled"/>, the factor survives a save + reload under the Mac key
/// <c>textZoom</c>, and the cue island / answer window keep their own size.
/// </summary>
public partial class MainWindow
{
    private sealed record ZoomCheckStep(string Name, bool Passed, string Detail);

    private async Task RunTextZoomCheckAsync(string path)
    {
        await Task.Delay(400);
        var steps = new List<ZoomCheckStep>();
        var restore = TextZoomService.Factor;
        try
        {
            state.Section = AppSection.Home;
            await PumpTextZoomAsync();

            void Check(string name, bool passed, string detail) => steps.Add(new ZoomCheckStep(name, passed, detail));

            Check("steps-are-the-mac-steps",
                TextZoom.Steps.SequenceEqual(new[] { 0.9, 1, 1.1, 1.2, 1.3, 1.45 })
                && TextZoom.PreferenceKey == "textZoom",
                string.Join(", ", TextZoom.Steps) + " · " + TextZoom.PreferenceKey);
            Check("island-and-answer-window-keep-their-own-size",
                TextZoomService.RequiresOwnSize(typeof(CueIslandWindow))
                && TextZoomService.RequiresOwnSize(typeof(TeleprompterWindow)),
                "cue island + answer window excluded from the page zoom");

            foreach (var factor in new[] { 0.9, 1.2, 1.45 })
            {
                TextZoomService.SetFactorForCheck(factor);
                await PumpTextZoomAsync();
                var probe = TextZoomService.Probe(this);
                var expected = probe is { } value ? Math.Round(value.Base * factor, MidpointRounding.AwayFromZero) : -1;
                Check("scaled-" + factor.ToString("0.##"),
                    probe is { } found && Math.Abs(found.Actual - expected) < 0.001 && Math.Abs(TextZoomService.Factor - factor) < 1e-9,
                    probe is { } p ? $"{p.Base:0.##} -> {p.Actual:0.##} (expected {expected:0.##})" : "no probe");
                var truncations = LocalizationAudit.CollectTruncations(this);
                Check("no-truncation-" + factor.ToString("0.##"), truncations.Count == 0,
                    truncations.Count == 0 ? "0 rows" : string.Join(" | ", truncations.Take(4)));
            }

            TextZoomService.SetFactorForCheck(1);
            await PumpTextZoomAsync();
            var restored = TextZoomService.Probe(this);
            Check("reset-restores-the-size",
                restored is { } pair && Math.Abs(pair.Actual - pair.Base) < 0.001,
                restored is { } r ? $"{r.Base:0.##} == {r.Actual:0.##}" : "no probe");

            // 决议 §3.1 zoom 2: content that appears after the zoom is already at the zoom. The
            // old 600 ms catch-up timer used to patch late content after it had painted at 100 %.
            TextZoomService.SetFactorForCheck(1.45);
            Console.WriteLine("zoom-check: before late window");
            var lateWindow = new Window
            {
                Width = 280,
                Height = 140,
                Content = new TextBlock { Text = "zoom probe", FontSize = 15 }
            };
            lateWindow.Show(this);
            Console.WriteLine("zoom-check: late window shown");
            await PumpTextZoomAsync();
            Console.WriteLine("zoom-check: late window pumped");
            var lateProbe = TextZoomService.Probe(lateWindow);
            var lateExpected = lateProbe is { } late ? Math.Round(late.Base * 1.45, MidpointRounding.AwayFromZero) : -1;
            Check("new-content-scales-immediately",
                lateProbe is { } foundLate && Math.Abs(foundLate.Actual - lateExpected) < 0.001,
                lateProbe is { } lp ? $"{lp.Base:0.##} -> {lp.Actual:0.##} (expected {lateExpected:0.##})" : "no probe");
            lateWindow.Close();
            Console.WriteLine("zoom-check: late window closed");

            // Persistence: write through the normal save callback, then reload the file.
            TextZoomService.SetFactorForCheck(1.2);
            TextZoomService.SaveForCheck();
            var persisted = TextZoomService.ReadPersisted();
            Check("persist-textZoom", Math.Abs(persisted - 1.2) < 1e-9, persisted.ToString("0.##"));
            TextZoomService.SetFactorForCheck(1);
            TextZoomService.Reload();
            Check("reload-after-restart", Math.Abs(TextZoomService.Factor - 1.2) < 1e-9, TextZoomService.Factor.ToString("0.##"));
        }
        catch (Exception error)
        {
            steps.Add(new ZoomCheckStep("exception", false, error.ToString()));
        }
        finally
        {
            // Leave the app at the size it started with and remember that size again.
            TextZoomService.SetFactorForCheck(1);
            TextZoomService.SaveForCheck();
            TextZoomService.Reload();
            state.Section = AppSection.Home;
        }
        var report = new
        {
            passed = steps.Count > 0 && steps.All(step => step.Passed),
            scope = "three zoom steps, page rescaling, persistence, island/answer-window exclusion",
            steps
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(report.passed ? "zoom-check: passed" : "zoom-check: failed");
        Close();
    }

    private static async Task PumpTextZoomAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(120);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
