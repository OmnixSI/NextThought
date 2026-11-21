using System.Text.Json;
using NextThought.Desktop;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// K5 (task book §9): shortcut, teleprompter opacity 0.5, context sidebar and text zoom must
    /// survive closing the app. Runs twice against one isolated data directory: the first process
    /// writes the four values through the same surface the settings page uses and leaves a marker;
    /// the second process reads them back from disk.
    /// </summary>
    private async Task RunK5SettingsCheckAsync(string path)
    {
        await Task.Delay(300);
        var marker = Path.Combine(state.Paths.Root, "k5-phase.txt");
        var steps = new List<object>();
        var passed = true;
        void Step(string name, bool ok, string detail)
        {
            steps.Add(new { name, ok, detail });
            passed &= ok;
        }

        var verifying = File.Exists(marker);
        try
        {
            if (!verifying)
            {
                // The settings page's shortcut editor writes ShortcutText and executes this command.
                state.ShortcutText = "record=Ctrl+Alt+F9";
                await state.SaveShortcutsCommand.ExecuteAsync(null);
                var binding = state.ShortcutBinding("record", 0x52);
                Step("shortcut-set", binding.Key == 0x78, $"key=0x{binding.Key:X} mods=0x{binding.Modifiers:X}");

                // Same property the window toggle writes; the settings page persists it in Build().
                state.ContextSidebarHidden = true;
                await state.Settings.SaveAsync();
                Step("sidebar-set", state.ContextSidebarHidden, state.ContextSidebarHidden.ToString());

                // Mac teleprompterOpacity key: WindowLayout mirrors it into settings.json.
                var layout = WindowLayout.Load(state.Paths);
                layout.TeleprompterOpacity = 0.5;
                layout.Save(state.Paths);
                await Task.Delay(250);
                var reloaded = WindowLayout.Load(state.Paths);
                Step("opacity-set", Math.Abs(reloaded.TeleprompterOpacity - 0.5) < 1e-9, reloaded.TeleprompterOpacity.ToString("0.##"));

                // J7: the text zoom save callback the zoom shortcuts use.
                TextZoomService.SetFactorForCheck(1.2);
                TextZoomService.SaveForCheck();
                Step("zoom-set", Math.Abs(TextZoomService.ReadPersisted() - 1.2) < 1e-9, TextZoomService.ReadPersisted().ToString("0.##"));

                // A meeting boundary: load the demo session and save once more, the way the settings
                // page would after a meeting ends. (A real recorded meeting needs audio hardware.)
                state.LoadDemo(DemoStages.Home);
                await Task.Delay(400);
                await state.Settings.SaveAsync();
                Step("after-meeting-save", true, "LoadDemo(Home) + Settings.SaveAsync");

                Directory.CreateDirectory(state.Paths.Root);
                await File.WriteAllTextAsync(marker, DateTimeOffset.UtcNow.ToString("o"));
            }
            else
            {
                var overrides = state.ShortcutOverrides;
                var binding = state.ShortcutBinding("record", 0);
                Step("shortcut-survives-restart",
                    overrides.TryGetValue("record", out var saved) && saved == "Ctrl+Alt+F9" && binding.Key == 0x78,
                    $"{overrides.GetValueOrDefault("record")} key=0x{binding.Key:X}");

                Step("sidebar-survives-restart", state.ContextSidebarHidden, state.ContextSidebarHidden.ToString());

                var layout = WindowLayout.Load(state.Paths);
                Step("opacity-survives-restart", Math.Abs(layout.TeleprompterOpacity - 0.5) < 1e-9, layout.TeleprompterOpacity.ToString("0.##"));

                var zoom = TextZoomService.ReadPersisted();
                Step("zoom-survives-restart", Math.Abs(zoom - 1.2) < 1e-9, zoom.ToString("0.##"));
            }
        }
        catch (Exception error)
        {
            steps.Add(new { name = "exception", ok = false, detail = error.ToString() });
            passed = false;
        }

        passed = steps.Count > 0 && steps.All(step => (bool)step.GetType().GetProperty("ok")!.GetValue(step)!);
        var report = new { passed, phase = verifying ? "verify" : "set", steps };
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
        Console.WriteLine(passed ? "k5-check: passed" : "k5-check: failed");
        Close();
    }
}
