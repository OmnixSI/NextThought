using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>D1: capture the named pages in light and dark into a directory.</summary>
    private async Task RunScreenshotsAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var pages = new[]
        {
            (AppSection.Home, "home"), (AppSection.Prepare, "prepare"), (AppSection.Live, "live"), (AppSection.Notes, "notes"),
            (AppSection.History, "history"), (AppSection.Series, "series"),
            (AppSection.Library, "library"), (AppSection.Settings, "settings")
        };
        foreach (var english in new[] { false, true })
        {
            NextThought.Core.UiText.EnglishUi = english;
            UiTranslation.Apply(this, english);
            foreach (var dark in new[] { false, true })
            {
                if (Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                foreach (var (section, name) in pages)
                {
                    state.Section = section;
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Task.Delay(250);
                    var suffix = (english ? "-en" : "") + (dark ? "-dark" : "-light") + ".png";
                    CaptureWindow(Path.Combine(directory, name + suffix));
                    if (section == AppSection.Notes
                        && PageHost.Content is Avalonia.Controls.Control notesPage
                        && notesPage.GetVisualDescendants().OfType<TabControl>().FirstOrDefault() is { } tabs)
                    {
                        var tabNames = new[] { "notes", "transcript", "answers", "personal", "evidence", "outline", "review", "work" };
                        for (var index = 0; index < tabs.ItemCount && index < tabNames.Length; index++)
                        {
                            tabs.SelectedIndex = index;
                            await Dispatcher.UIThread.InvokeAsync(notesPage.UpdateLayout, DispatcherPriority.Background);
                            await Task.Delay(120);
                            CaptureWindow(Path.Combine(directory, tabNames[index] + suffix));
                        }
                    }
                    if (section == AppSection.Settings && this.FindControl<SettingsView>("SettingsOverlay") is { } settings)
                    {
                        foreach (var page in new[] { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" })
                        {
                            settings.SelectPage(page);
                            await Dispatcher.UIThread.InvokeAsync(settings.UpdateLayout, DispatcherPriority.Background);
                            await Task.Delay(120);
                            CaptureWindow(Path.Combine(directory, "settings-" + page.ToLowerInvariant() + suffix));
                        }
                    }
                }
            }
        }
        NextThought.Core.UiText.EnglishUi = false;
        foreach (var dark in new[] { false, true })
        {
            if (Application.Current is { } app)
                app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            var sample = new ControlSampleWindow();
            sample.Show();
            await Task.Delay(300);
            CaptureVisual(sample, Path.Combine(directory, "controls" + (dark ? "-dark.png" : "-light.png")));
            sample.Close();
        }
        // First-run guide (E3): first and last step.
        state.OnboardingStep = 0;
        state.OnboardingVisible = true;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        CaptureWindow(Path.Combine(directory, "onboarding-first" + suffix));
        state.OnboardingStep = 3;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        CaptureWindow(Path.Combine(directory, "onboarding-last" + suffix));
        state.OnboardingVisible = false;

        // Quick settings panel (E26)
        var quick = new QuickSettingsPanel { DataContext = state };
        var quickWindow = new Avalonia.Controls.Window
        {
            Content = quick,
            SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight,
        };
        quickWindow.Show();
        await Task.Delay(250);
        CaptureVisual(quick, Path.Combine(directory, "quick-settings-light.png"));
        quickWindow.Close();

        // The current island has one implemented visual state; capture it as evidence until C5 adds five.
        state.Recording = true;
        state.DetectedQuestion = NextThought.Desktop.DemoContent.Question;
        var island = new CueIslandWindow(state);
        island.Show();
        foreach (var islandState in new[] { "collapsed", "hearing", "listening", "cue", "launcher", "deck", "bilingual" })
        {
            island.ApplyStateForCheck(islandState);
            await Task.Delay(250);
            CaptureVisual(island, Path.Combine(directory, "island-" + islandState + ".png"));
        }
        island.Close();
        state.Recording = false;
        Close();
    }

    private void CaptureWindow(string path) => CaptureVisual(this, path);

    private static void CaptureVisual(Avalonia.Visual visual, string path)
    {
        var bounds = visual.Bounds;
        var width = Math.Max(1, (int)Math.Round(bounds.Width));
        var height = Math.Max(1, (int)Math.Round(bounds.Height));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(visual);
        bitmap.Save(path);
    }
}
