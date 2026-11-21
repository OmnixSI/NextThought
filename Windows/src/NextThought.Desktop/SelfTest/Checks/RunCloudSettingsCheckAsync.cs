namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunCloudSettingsCheckAsync(string path)
    {
        await Task.Delay(200);
        var settings = state.Settings;
        settings.SelectedCloudStt = settings.CloudSttChoices.First(choice => choice.Value == "soniox");
        settings.CloudSttModel = "stt-rt-v5";
        settings.CloudSttKey = "test-cloud-key";
        var built = settings.Build();
        state.SettingsStore.SaveCloudSttKey("test-cloud-key");
        var read = state.SettingsStore.ReadCloudSttKey();
        var passed = built.CloudStt == "soniox" && built.CloudSttModel == "stt-rt-v5" && read == "test-cloud-key";
        var report = new { passed, provider = built.CloudStt, model = built.CloudSttModel, keyRoundTrip = read.Length > 0 };
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
        Console.WriteLine(passed ? "cloud-settings-check: passed" : "cloud-settings-check: failed");
        Close();
    }

    /// <summary>QA: the saved appearance (dark) reaches the actual theme variant.</summary>
}
