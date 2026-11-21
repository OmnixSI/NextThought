using System.IO.Compression;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>
/// Writes a .ntbackup archive: manifest, filtered settings, one JSON per series and (optionally)
/// the session folders. The archive is built in a temporary file and moved into place.
/// </summary>
public static class BackupArchive
{
    public static readonly JsonSerializerOptions Options = new(JsonDefaults.ArchiveOptions)
    {
        WriteIndented = true
    };

    /// <summary>Writes the archive and returns the final path.</summary>
    public static string Write(string destination, BackupManifest manifest, WorkspaceData workspaces,
        IReadOnlyDictionary<string, object?> settings, string? sessionsDirectory = null)
    {
        manifest.Validate();
        var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "manifest.json", JsonSerializer.Serialize(manifest, Options));
                WriteEntry(archive, "settings.json", JsonSerializer.Serialize(SettingsSnapshot.Filter(settings), Options));
                foreach (var series in workspaces.Series)
                    WriteEntry(archive, $"Workspaces/{series.Id}.json", JsonSerializer.Serialize(series, Options));
                if (sessionsDirectory is { Length: > 0 } && Directory.Exists(sessionsDirectory))
                {
                    foreach (var session in Directory.EnumerateDirectories(sessionsDirectory))
                    {
                        var id = Path.GetFileName(session);
                        foreach (var file in Directory.EnumerateFiles(session, "*", SearchOption.AllDirectories))
                        {
                            var relative = Path.GetRelativePath(session, file).Replace('\\', '/');
                            var entry = archive.CreateEntry($"Sessions/{id}/{relative}", CompressionLevel.Optimal);
                            using var output = entry.Open();
                            using var input = File.OpenRead(file);
                            input.CopyTo(output);
                        }
                    }
                }
            }
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>Reads and validates the manifest without extracting the archive.</summary>
    public static BackupManifest? ReadManifest(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.GetEntry("manifest.json");
        if (entry is null) return null;
        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<BackupManifest>(stream,
            new JsonSerializerOptions(Options) { PropertyNameCaseInsensitive = true });
        manifest?.Validate();
        return manifest;
    }

    private static void WriteEntry(ZipArchive archive, string name, string json)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(json);
    }
}
