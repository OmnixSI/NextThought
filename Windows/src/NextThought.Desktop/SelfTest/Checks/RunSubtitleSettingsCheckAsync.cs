namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSubtitleSettingsCheckAsync(string path)
    {
        var passed = false;
        var width = 0d;
        var font = 0d;
        try
        {
            state.Settings.SubtitleWidth = 760;
            state.Settings.SubtitleFontSize = 28;
            state.Settings.SubtitleShowTranslation = false;
            state.LiveMode = LiveWindowMode.Subtitles;
            await Task.Delay(500);
            width = subtitleBar?.AppliedWidth ?? 0;
            font = subtitleBar?.AppliedFontSize ?? 0;
            passed = Math.Abs(width - 760) < 0.5 && Math.Abs(font - 28) < 0.5;
            state.LiveMode = LiveWindowMode.Notes;
            await Task.Delay(200);
        }
        catch (Exception ex) { Console.WriteLine("subtitle-settings-check: " + ex.Message); }
        var report = new { passed, width, font };
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
        Console.WriteLine(passed ? "subtitle-settings-check: passed" : "subtitle-settings-check: failed");
        Close();
    }

    /// <summary>QA: Notes/Subtitles/Hidden are three distinct states.</summary>
}
