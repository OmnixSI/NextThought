namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunUpdateBackupCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "update-backup");
        var app = Path.Combine(directory, "app");
        Directory.CreateDirectory(Path.Combine(app, "runtimes"));
        await File.WriteAllTextAsync(Path.Combine(app, "NextThought.exe"), "old");
        await File.WriteAllTextAsync(Path.Combine(app, "runtimes", "native.dll"), "native");
        var backup = NextThought.Core.UpdateBackup.Create(app, Path.Combine(directory, "backups"), "0.64.0");
        await File.WriteAllTextAsync(Path.Combine(app, "NextThought.exe"), "new");
        NextThought.Core.UpdateBackup.Restore(backup.Path, app);
        var passed = File.ReadAllText(Path.Combine(app, "NextThought.exe")) == "old"
                     && File.ReadAllText(Path.Combine(app, "runtimes", "native.dll")) == "native"
                     && NextThought.Core.UpdateBackup.List(Path.Combine(directory, "backups")).Count == 1;
        var report = new { passed, backup = Path.GetFileName(backup.Path), restored = "old" };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "update-backup-check: passed" : "update-backup-check: failed");
        Close();
    }


    /// <summary>Writes the post-exit restore script and checks its wait/copy/start steps.</summary>
}
