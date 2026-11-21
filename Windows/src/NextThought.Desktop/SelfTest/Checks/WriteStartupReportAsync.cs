namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task WriteStartupReportAsync(string path)
    {
        await Task.Delay(400);
        var passed = state.Ready && state.StartupErrorText is null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var presenterPlan = CurrentPresenterPlan();
            var report = new StartupReport(
                DateTimeOffset.Now,
                passed,
                "Avalonia 12 startup self-check; layout and interactive acceptance are measured separately",
                Math.Round(Program.Clock.Elapsed.TotalSeconds, 3),
                state.Section.ToString(),
                state.Ready,
                state.StartupErrorText,
                state.History.Count,
                state.Transcript.Count,
                state.InitTimings,
                [
                    "capture-dual", "asr-local", "sessions", "summary-markdown", "scenes-9",
                    "cue-island", "subtitles", "teleprompter", "region-capture", "hotkeys-19",
                    "tray", "hide-all", "auto-end", "notes-templates-9", "notes-fishbone",
                    "notes-outline", "notes-viewpoints", "proofread", "due-dates", "series",
                    "glossary", "update-check", "companion-lan",
                    "workspace-knowledge", "knowledge-merge", "question-bank", "live-translation", "translation-gate",
                    "presenter-mode", "companion-relay", "update-install", "material-hotwords"
                ],
                BuildDisplays().Select(display => new StartupScreen(display.Id, display.Name, display.Bounds.ToString(), display.IsPrimary)).ToList(),
                new StartupPresenter(
                    state.SettingsSnapshot.PresenterMode,
                    state.SettingsSnapshot.PresenterSlidesScreen,
                    presenterPlan.CanActivate,
                    presenterPlan.Camera?.Name,
                    presenterPlan.Slides?.Name,
                    presenterPlan.Summary,
                    presenterActive),
                typeof(AppState).Assembly.GetName().Version?.ToString(3),
                Environment.Version.ToString(),
                Math.Round(Environment.WorkingSet / 1048576.0, 1),
                Math.Round(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576.0, 1),
                Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                Math.Round(GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0, 1),
                System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
                Environment.ProcessPath,
                Width,
                Height,
                ClientSize.Width,
                ClientSize.Height,
                RenderScaling,
                Screens.Primary?.Scaling,
                Screens.Primary?.Bounds.ToString());
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, StartupJsonContext.Default.StartupReport));
        }
        catch (Exception exception)
        {
            passed = false;
            try
            {
                File.WriteAllText(path + ".error.txt",
                    "ready=" + state.Ready + "\nstartupError=" + (state.StartupErrorText ?? "") + "\n" + exception);
            }
            catch { }
        }
        Console.WriteLine(passed ? "startup-check: passed" : "startup-check: failed");
        Close();
    }

    /// <summary>
    /// Offline proof of the knowledge loop, with no answer service involved:
    /// template notes → candidates → review → workspace.json → prompt text.
    /// </summary>
}
