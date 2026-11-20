using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Desktop.Localization;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// D1: capture the named pages in light and dark into a directory.
    /// G8/G9: while capturing, also dump the full text inventory of every page (including tooltips
    /// that are not open) into <c>text/</c>, so the English batch can be scanned for Chinese that
    /// does not belong to user data. The audit never writes text back.
    /// </summary>
    private async Task RunScreenshotsAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var textDirectory = Path.Combine(directory, "text");
        Directory.CreateDirectory(textDirectory);
        var source = UiTextSource.Instance;
        var previousPreference = source.Preference;
        var englishInventory = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var pages = new[]
        {
            (AppSection.Home, "home"), (AppSection.Prepare, "prepare"), (AppSection.Live, "live"), (AppSection.Notes, "notes"),
            (AppSection.History, "history"), (AppSection.Series, "series"),
            (AppSection.Library, "library"), (AppSection.Settings, "settings")
        };
        try
        {
            foreach (var english in new[] { false, true })
            {
                state.ApplyUiLanguage(english);
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
                        await CaptureWindowAsync(directory, textDirectory, name + suffix, english, englishInventory);
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
                                await CaptureWindowAsync(directory, textDirectory, tabNames[index] + suffix, english, englishInventory);
                            }
                            // E17: 校正原话 (CorrectedTranscript) — corrected/original transcript with per-line undo.
                            state.RefreshCorrectedTranscript();
                            var correctedWindow = new CorrectedTranscript { DataContext = state };
                            correctedWindow.Show();
                            await Task.Delay(200);
                            var correctedName = "corrected-transcript" + suffix;
                            CaptureVisual(correctedWindow, Path.Combine(directory, correctedName));
                            var correctedRows = await WriteInventoryAsync(correctedWindow, textDirectory, correctedName);
                            if (english) englishInventory[correctedName] = correctedRows;
                            correctedWindow.Close();
                        }
                        if (section == AppSection.Settings && this.FindControl<SettingsView>("SettingsOverlay") is { } settings)
                        {
                            foreach (var page in new[] { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" })
                            {
                                settings.SelectPage(page);
                                await Dispatcher.UIThread.InvokeAsync(settings.UpdateLayout, DispatcherPriority.Background);
                                await Task.Delay(120);
                                await CaptureWindowAsync(directory, textDirectory, "settings-" + page.ToLowerInvariant() + suffix, english, englishInventory);
                            }
                        }
                    }
                }
            }
            state.ApplyUiLanguage(false);
            foreach (var dark in new[] { false, true })
            {
                if (Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                var sample = new ControlSampleWindow();
                sample.Show();
                await Task.Delay(300);
                var controlsPath = Path.Combine(directory, dark ? "controls-dark.png" : "controls-light.png");
                CaptureVisual(sample, controlsPath);
                await WriteInventoryAsync(sample, textDirectory, dark ? "controls-dark" : "controls-light");
                sample.Close();
            }
            // First-run guide (E3): first and last step.
            state.OnboardingStep = 0;
            state.OnboardingVisible = true;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            CaptureWindow(Path.Combine(directory, "onboarding-first.png"));
            await WriteInventoryAsync(this, textDirectory, "onboarding-first");
            state.OnboardingStep = 3;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            CaptureWindow(Path.Combine(directory, "onboarding-last.png"));
            await WriteInventoryAsync(this, textDirectory, "onboarding-last");
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
            await WriteInventoryAsync(quick, textDirectory, "quick-settings-light");
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
                await WriteInventoryAsync(island, textDirectory, "island-" + islandState);
            }
            // G9: the island is user facing too; audit it in English without adding screenshot files.
            state.ApplyUiLanguage(true);
            foreach (var islandState in new[] { "launcher", "cue", "deck", "bilingual" })
            {
                island.ApplyStateForCheck(islandState);
                await Task.Delay(120);
                await WriteInventoryAsync(island, textDirectory, "island-" + islandState + "-en");
            }
            state.ApplyUiLanguage(false);
            island.Close();
            state.Recording = false;
            await File.WriteAllTextAsync(Path.Combine(textDirectory, "english-audit.json"),
                System.Text.Json.JsonSerializer.Serialize(englishInventory,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }));
        }
        finally
        {
            source.SetPreference(previousPreference);
            Close();
        }
    }

    private async Task CaptureWindowAsync(string directory, string textDirectory, string name, bool english,
        IDictionary<string, string[]> englishInventory)
    {
        CaptureWindow(Path.Combine(directory, name));
        var rows = await WriteInventoryAsync(this, textDirectory, name);
        if (english) englishInventory[name] = rows;
    }

    private static async Task<string[]> WriteInventoryAsync(Visual root, string textDirectory, string name)
    {
        var rows = LocalizationAudit.Collect(root).Select(row => row.Replace('\u0001', '\t')).ToArray();
        Directory.CreateDirectory(textDirectory);
        await File.WriteAllLinesAsync(Path.Combine(textDirectory, name + ".txt"), rows);
        return rows;
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
