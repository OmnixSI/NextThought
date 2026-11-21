namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunDemoCheckAsync(string path)
    {
        await Task.Delay(300);
        // `--demo` alone no longer seeds the demo (the data lives only while a demo capture runs), so
        // the self-check loads it itself like the screenshot batch does.
        state.LoadDemo();
        await Task.Delay(150);
        var sessionFiles = Directory.Exists(state.Paths.Sessions)
            ? Directory.EnumerateFiles(state.Paths.Sessions, "*", SearchOption.AllDirectories).Count() : 0;
        var passed = state.CurrentSession is not null && state.CurrentLines.Count >= 10
                     && state.History.Count >= 3 && !string.IsNullOrWhiteSpace(state.AnswerText)
                     && sessionFiles == 0;
        var report = new
        {
            passed, lines = state.CurrentLines.Count, history = state.History.Count,
            hasAnswer = !string.IsNullOrWhiteSpace(state.AnswerText), sessionFiles,
            meeting = state.CurrentSession?.Title ?? ""
        };
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
        Console.WriteLine(passed ? "demo-check: passed" : "demo-check: failed");
        Close();
    }
}
