namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunWindowCloseCheckAsync(string path)
    {
        var passed = false;
        var islandHidden = false;
        var islandReshow = false;
        var subtitleHidden = false;
        var subtitleReshow = false;
        try
        {
            ShowCueIsland();
            await Task.Delay(300);
            if (island is { } cue)
            {
                cue.Close();
                await Task.Delay(300);
                islandHidden = !cue.IsVisible;
                cue.Show();
                await Task.Delay(300);
                islandReshow = cue.IsVisible;
                cue.AllowClose = true;
                cue.Close();
            }
            var bar = new SubtitleBarWindow(state) { Persistent = true };
            bar.Show();
            await Task.Delay(300);
            bar.Close();
            await Task.Delay(300);
            subtitleHidden = !bar.IsVisible;
            bar.Show();
            bar.ResumeAfterUserShow();
            await Task.Delay(300);
            subtitleReshow = bar.IsVisible;
            bar.AllowClose = true;
            bar.Close();
            passed = islandHidden && islandReshow && subtitleHidden && subtitleReshow;
        }
        catch (Exception ex) { Console.WriteLine("window-close-check: " + ex.Message); }
        var report = new { passed, islandHidden, islandReshow, subtitleHidden, subtitleReshow };
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
        Console.WriteLine(passed ? "window-close-check: passed" : "window-close-check: failed");
        Close();
    }

    /// <summary>QA: a detected share retracts the cue island and restores it afterwards.</summary>
}
