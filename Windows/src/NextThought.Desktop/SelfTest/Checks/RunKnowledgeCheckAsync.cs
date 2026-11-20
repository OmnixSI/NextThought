namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunKnowledgeCheckAsync(string path)
    {
        await Task.Delay(300);
        var report = KnowledgeCheck.Run(state);
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch
        {
            report = report with { Passed = false };
        }
        Console.WriteLine(report.Passed ? "knowledge-check: passed" : "knowledge-check: failed");
        Close();
    }

    /// <summary>
    /// Dual-screen self-check on a machine with one display: a synthetic second display goes
    /// through the real layout code, then the window side effects are exercised and the real
    /// single-display topology must refuse to activate.
    /// </summary>
}
