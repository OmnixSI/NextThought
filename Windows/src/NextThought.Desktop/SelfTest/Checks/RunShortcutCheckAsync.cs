namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunShortcutCheckAsync(string path)
    {
        await Task.Delay(300);
        var key = state.ShortcutKey("record", 0x52);
        var passed = key == 0x78 && hotkeys is { Unregistered.Count: 0 };
        var report = new
        {
            passed, key = NextThought.Core.ShortcutKeys.Name(key), unregistered = hotkeys?.Unregistered ?? []
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
        Console.WriteLine(passed ? "shortcut-check: passed" : "shortcut-check: failed");
        Close();
    }
}
