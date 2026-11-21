namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunBackupManifestCheckAsync(string path)
    {
        await Task.Delay(100);
        var manifest = new NextThought.Core.BackupManifest
        {
            Kind = NextThought.Core.BackupManifest.BackupKind.Full,
            AppVersion = "check",
            AppBuild = "check",
            WorkspaceCount = 2,
            SessionCount = 3
        };
        manifest.Validate();
        var template = new NextThought.Core.NotesTemplate("custom", "模板", []);
        var merge = NextThought.Core.BackupMerge.NotesTemplates([], [template]);
        var restored = NextThought.Core.SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoApplyLessons"] = true,
            ["notExported"] = 1
        });
        var rejectedNewer = false;
        try
        {
            (manifest with { Version = 2 }).Validate();
        }
        catch (NextThought.Core.BackupManifest.ValidationException)
        {
            rejectedNewer = true;
        }
        var passed = merge.Added == 1 && merge.Templates.Count == 1
                     && restored.Count == 1 && restored["autoCollectKnowledgeCandidates"] is true
                     && !restored.ContainsKey("notExported") && rejectedNewer
                     && NextThought.Core.SettingsSnapshot.IsSecretAccount("answer.api.x")
                     && NextThought.Core.BackupMerge.SessionsToImport(new HashSet<Guid>(), []).Count == 0;
        var report = new { passed, added = merge.Added, restoredKeys = restored.Keys, rejectedNewer };
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
        Console.WriteLine(passed ? "backup-manifest-check: passed" : "backup-manifest-check: failed");
        Close();
    }

    /// <summary>Builds a meeting plan and checks the carry snapshot and effective mode.</summary>
}
