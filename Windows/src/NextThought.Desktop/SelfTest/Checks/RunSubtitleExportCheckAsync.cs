namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunSubtitleExportCheckAsync(string directory)
    {
        await Task.Delay(200);
        var error = "";
        var srtText = "";
        var vttText = "";
        var lines = 0;
        var estimated = 0;
        try
        {
            if (state.CurrentSession is null) error = "没有可导出的会议（用 --demo 或打开已有会议）。";
            else
            {
                var srt = state.ExportCurrentSubtitles(SessionSubtitleFormat.Srt);
                var vtt = state.ExportCurrentSubtitles(SessionSubtitleFormat.Vtt);
                srtText = srt.Text;
                vttText = vtt.Text;
                estimated = srt.EstimatedTimingCount;
                lines = state.CurrentLines.Count;
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "demo.srt"), srt.Text, new System.Text.UTF8Encoding(false));
                await File.WriteAllTextAsync(Path.Combine(directory, "demo.vtt"), vtt.Text, new System.Text.UTF8Encoding(false));
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var passed = error.Length == 0 && lines > 0
                     && srtText.Contains(" --> ", StringComparison.Ordinal)
                     && srtText.StartsWith("1\n", StringComparison.Ordinal)
                     && vttText.StartsWith("WEBVTT\n\n", StringComparison.Ordinal);
        var report = new
        {
            passed,
            lines,
            estimated,
            srtFirstCue = string.Join("\n", srtText.Split('\n').Take(3)),
            vttHeader = vttText.Length >= 6 ? vttText[..6] : vttText,
            hasTranslation = srtText.Contains("译文：", StringComparison.Ordinal),
            files = new[] { Path.Combine(directory, "demo.srt"), Path.Combine(directory, "demo.vtt") },
            error
        };
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "export-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        }
        catch { }
        Console.WriteLine(passed ? "subtitle-export-check: passed" : "subtitle-export-check: failed");
        Close();
    }

    /// <summary>
    /// Imports a small text document, then checks that its technical terms reach the recognizer
    /// hotword list together with the glossary (MaterialTerms integration).
    /// </summary>
}
