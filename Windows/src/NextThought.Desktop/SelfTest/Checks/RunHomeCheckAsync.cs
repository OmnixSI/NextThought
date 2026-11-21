using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Desktop.Views.Controls;
using NextThought.Desktop.Views.Home;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E1/§6.4: drives the home page's polish items — InkWash bound to reducedEffects, the sliding
    /// scene capsule (matchedGeometryEffect), the glyph/promise transitions, the focused composer
    /// stroke in the scene tint, and the continue-series order (lastSessionDate ?? updatedAt).
    /// </summary>
    private async Task RunHomeCheckAsync(string path)
    {
        await Task.Delay(350);
        var failures = new List<string>();
        var state = this.state;
        var seriesOrder = "";
        try
        {
            state.LoadDemo();
            state.Section = AppSection.Home;
            await PumpAsync();
            await PumpAsync();
            if (PageHost.Content is not HomeView home)
            {
                failures.Add("home page not shown");
                await WriteReportAsync(path, new Dictionary<string, object?>
                {
                    ["passed"] = false,
                    ["failures"] = failures
                });
                return;
            }

            // 1) Mac HomeView: InkWash(tint:animated: !reducedEffects)
            var ink = home.GetVisualDescendants().OfType<InkWash>().FirstOrDefault();
            if (ink is null) failures.Add("home InkWash missing");
            else if (ink.Animated != state.HomeInkWashAnimated)
                failures.Add("InkWash Animated is not bound to reducedEffects");
            var reducedBefore = Motion.AppReducedEffects;
            Motion.AppReducedEffects = true;
            if (state.HomeInkWashAnimated) failures.Add("reducedEffects did not turn the wash animation off");
            Motion.AppReducedEffects = reducedBefore;

            // 2-4) the composer transitions
            var composer = home.GetVisualDescendants().OfType<StartComposer>().FirstOrDefault();
            if (composer is null) failures.Add("StartComposer missing");
            else
            {
                var chipHost = composer.FindControl<Grid>("ChipHost");
                var capsule = composer.FindControl<Canvas>("SelectionLayer")?.Children.OfType<Border>().FirstOrDefault();
                if (chipHost is null) failures.Add("ChipHost missing");
                if (capsule is null) failures.Add("selection capsule missing");
                else if (!capsule.IsVisible) failures.Add("selection capsule hidden");
                if (chipHost is not null && capsule is not null)
                {
                    var chip = SelectedSceneChip(composer, state.SelectedMode);
                    if (chip is null) failures.Add("selected scene chip not found");
                    else if (chip.TranslatePoint(new Point(0, 0), chipHost) is { } origin
                             && Math.Abs(Canvas.GetLeft(capsule) - origin.X) > 2)
                        failures.Add("capsule is not over the selected chip");
                }
                // Scene change: glyph scales in (ScaleTransform), capsule travels, promise repaints.
                var firstLeft = capsule is null ? 0 : Canvas.GetLeft(capsule);
                var labChip = SelectedSceneChip(composer, MeetingMode.LabMeeting);
                if (labChip is null) failures.Add("lab-meeting chip not found");
                // Click the chip the way a person does, so the Mac transitions run.
                labChip?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await PumpAsync();
                var glyph = composer.FindControl<Control>("SceneGlyphControl");
                if (glyph?.RenderTransform is not ScaleTransform) failures.Add("glyph scale transition not applied");
                await WaitAsync(1200);
                if (capsule is not null && chipHost is not null)
                {
                    var movedChip = SelectedSceneChip(composer, state.SelectedMode);
                    if (movedChip?.TranslatePoint(new Point(0, 0), chipHost) is { } moved
                        && Math.Abs(Canvas.GetLeft(capsule) - moved.X) > 2)
                        failures.Add("capsule did not settle over the new chip");
                    if (Math.Abs(Canvas.GetLeft(capsule) - firstLeft) < 0.5)
                        failures.Add("capsule did not move between chips");
                }
                // 5) focused composer stroke = scene tint at 45%, blurred stroke = hairline.
                var title = composer.FindControl<TextBox>("TitleBox");
                var card = composer.FindControl<Border>("ComposerCard");
                if (title is null || card is null) failures.Add("TitleBox/ComposerCard missing");
                else
                {
                    title.Focus();
                    // Motion.hover: 0.14 s brush transition on the stroke.
                    await WaitAsync(260);
                    if (card.BorderThickness.Left < 1) failures.Add("focused stroke is not 1 px");
                    // The 45% lives in the brush colour's alpha (Mac scene.tint.opacity(0.45)).
                    if (card.BorderBrush is not SolidColorBrush { Color.A: >= 100 }) failures.Add("focused stroke is not the 45% scene tint");
                    // Move focus to the start button: Mac stroke returns to Ink.hairline at 0.5.
                    composer.GetVisualDescendants().OfType<Button>()
                        .FirstOrDefault(button => button.Classes.Contains("primary"))?.Focus();
                    await PumpAsync();
                    if (card.BorderThickness.Left > 0.6) failures.Add("blurred stroke did not return to the hairline");
                }
            }

            // 6) ContinueSeries: lastSessionDate ?? updatedAt, standing rows excluded, three at most.
            state.RefreshHomeSeries();
            seriesOrder = string.Join(" | ", state.HomeSeriesRows.Select(row => row.Name));
            if (state.HomeSeriesRows.Count != 3) failures.Add("continue series rows: " + state.HomeSeriesRows.Count);
            else if (!state.HomeSeriesRows[0].Name.Contains("ACL", StringComparison.Ordinal))
                failures.Add("continue series order is not Mac's: " + seriesOrder);

            var report = new Dictionary<string, object?>
            {
                ["passed"] = failures.Count == 0,
                ["failures"] = failures,
                ["inkWashAnimated"] = ink?.Animated,
                ["reducedEffectsOff"] = !state.HomeInkWashAnimated,
                ["seriesOrder"] = seriesOrder
            };
            await WriteReportAsync(path, report);
            Console.WriteLine(failures.Count == 0
                ? "home-check: passed"
                : "home-check: failed " + string.Join("; ", failures));
        }
        finally
        {
            Close();
        }
    }

    private static Button? SelectedSceneChip(StartComposer composer, MeetingMode mode) =>
        composer.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            button.Classes.Contains("scene") && button.Tag is SceneChoice scene && scene.Mode == mode);

    private static async Task WaitAsync(int milliseconds)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(milliseconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(40);
        }
    }
}
