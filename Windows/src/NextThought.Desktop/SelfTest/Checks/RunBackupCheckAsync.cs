namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunBackupCheckAsync(string directory)
    {
        await Task.Delay(120);
        var error = "";
        var passed = false;
        long storage = 0;
        string restored = "";
        try
        {
            Directory.CreateDirectory(directory);
            var store = new NextThought.Core.IncrementalBackupStore(Path.Combine(directory, "store"));
            var files = new Dictionary<string, byte[]>
            {
                ["settings.json"] = System.Text.Encoding.UTF8.GetBytes("{\"theme\":\"light\"}"),
                ["Sessions/a/transcript.jsonl"] = System.Text.Encoding.UTF8.GetBytes("{\"text\":\"你好\"}\n")
            };
            var point = store.Create(files, reason: "check", keep: 2);
            var again = store.Create(files, reason: "check", keep: 2);
            var destination = Path.Combine(directory, "restored");
            store.Restore(point, destination);
            restored = File.ReadAllText(Path.Combine(destination, "settings.json"));
            storage = store.StorageBytes();
            var manifests = Directory.GetFiles(Path.Combine(store.Root, "RestorePoints"), "*.ntrestore").Length;
            passed = point == again && manifests == 1 && restored.Contains("light") && storage > 0
                     && File.Exists(Path.Combine(destination, "Sessions", "a", "transcript.jsonl"));
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        var report = new { passed, storageBytes = storage, restored, error };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "backup-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
        }
        catch { }
        Console.WriteLine(passed ? "backup-check: passed" : "backup-check: failed");
        Close();
    }

    /// <summary>
    /// Builds a real answer prompt through the ported builder and checks the system section,
    /// the user JSON and the budgets.
    /// </summary>
}
