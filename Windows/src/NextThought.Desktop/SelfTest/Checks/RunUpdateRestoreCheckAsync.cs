namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunUpdateRestoreCheckAsync(string path)
    {
        await Task.Delay(100);
        var backup = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "backup");
        var target = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "target");
        var executable = Path.Combine(target, "NextThought.exe");
        var script = NextThought.Core.UpdateRestore.Script(backup, target, executable, 1234);
        var passed = script.Contains("Wait-Process -Id 1234")
                     && script.Contains("Copy-Item -LiteralPath '" + backup + "'")
                     && script.Contains("Start-Process -FilePath '" + executable + "'");
        var report = new { passed, script = script.Replace("\r\n", "\n").Split('\n') };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "update-restore-check: passed" : "update-restore-check: failed");
        Close();
    }


    /// <summary>Reports the Authenticode status of a file and whether signatures are required.</summary>
}
