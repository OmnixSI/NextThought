namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunProviderPresetCheckAsync(string path)
    {
        await Task.Delay(200);
        var settings = state.Settings;
        var deepSeek = settings.ProviderPresets.FirstOrDefault(preset => preset.Name == "DeepSeek");
        var passed = false;
        if (deepSeek is not null)
        {
            settings.SelectedProviderPreset = deepSeek;
            passed = settings.BaseUrl == "https://api.deepseek.com/v1" && settings.Model == "deepseek-chat";
        }
        var report = new
        {
            passed,
            presetCount = settings.ProviderPresets.Count,
            baseUrl = settings.BaseUrl,
            model = settings.Model
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
        Console.WriteLine(passed ? "provider-preset-check: passed" : "provider-preset-check: failed");
        Close();
    }

    /// <summary>QA: the settings search filters the visible cards.</summary>
}
