namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunAppearanceCheckAsync(string path)
    {
        await Task.Delay(300);
        var selected = state.Settings.SelectedAppearance?.Value ?? "";
        var actual = ActualThemeVariant;
        var passed = selected == "dark" && actual == Avalonia.Styling.ThemeVariant.Dark;
        var report = new { passed, selected, actual = actual.ToString() };
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
        Console.WriteLine(passed ? "appearance-check: passed" : "appearance-check: failed");
        Close();
    }

    /// <summary>QA: choosing a provider preset fills the API URL and model.</summary>
}
