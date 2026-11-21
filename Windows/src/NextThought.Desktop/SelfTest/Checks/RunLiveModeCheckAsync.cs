namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunLiveModeCheckAsync(string path)
    {
        var passed = false;
        var notes = false;
        var hidden = false;
        var subtitles = false;
        var restored = false;
        try
        {
            state.Section = AppSection.Live;
            await Task.Delay(300);
            notes = IsVisible;
            state.LiveMode = LiveWindowMode.Hidden;
            await Task.Delay(250);
            hidden = !IsVisible && subtitleBar?.IsVisible != true;
            state.LiveMode = LiveWindowMode.Subtitles;
            await Task.Delay(250);
            subtitles = !IsVisible && subtitleBar?.IsVisible == true;
            state.LiveMode = LiveWindowMode.Notes;
            await Task.Delay(250);
            restored = IsVisible && subtitleBar?.IsVisible != true;
            passed = notes && hidden && subtitles && restored;
        }
        catch (Exception ex) { Console.WriteLine("live-mode-check: " + ex.Message); }
        var report = new { passed, notes, hidden, subtitles, restored };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "live-mode-check: passed" : "live-mode-check: failed");
        Close();
    }

    /// <summary>QA: a note timestamp jumps back to the live transcript.</summary>
}
