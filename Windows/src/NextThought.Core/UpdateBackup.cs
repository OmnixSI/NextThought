namespace NextThought.Core;

public sealed record UpdateBackupInfo(string Version, string Path, DateTimeOffset CreatedAt);

/// <summary>
/// Keeps a copy of the installed program files before an in-app update replaces them, so a
/// failed update can be rolled back by restoring the newest backup.
/// </summary>
public static class UpdateBackup
{
    /// <summary>Copies the program directory into <paramref name="backupRoot"/>; user data is never here.</summary>
    public static UpdateBackupInfo Create(string appDirectory, string backupRoot, string version)
    {
        if (!Directory.Exists(appDirectory)) throw new DirectoryNotFoundException(appDirectory);
        var safe = SafeName(version);
        var createdAt = DateTimeOffset.UtcNow;
        var directory = Path.Combine(backupRoot, safe + "-" + createdAt.ToString("yyyyMMdd-HHmmss"));
        CopyDirectory(appDirectory, directory);
        return new UpdateBackupInfo(version, directory, createdAt);
    }

    /// <summary>Newest first; unreadable folders are ignored.</summary>
    public static IReadOnlyList<UpdateBackupInfo> List(string backupRoot)
    {
        if (!Directory.Exists(backupRoot)) return [];
        return Directory.EnumerateDirectories(backupRoot)
            .Select(path =>
            {
                var name = Path.GetFileName(path);
                // Directory names are "<version>-yyyyMMdd-HHmmss"; the suffix is always 16 chars.
                var version = name.Length > 16 && name[^16] == '-' ? name[..^16] : name;
                var createdAt = Directory.GetCreationTimeUtc(path);
                return new UpdateBackupInfo(version, path, createdAt);
            })
            .OrderByDescending(backup => backup.CreatedAt)
            .ToList();
    }

    /// <summary>Deletes older backups, keeping the newest <paramref name="keep"/>.</summary>
    public static void Prune(string backupRoot, int keep = 2)
    {
        foreach (var backup in List(backupRoot).Skip(Math.Max(0, keep))) Directory.Delete(backup.Path, true);
    }

    /// <summary>Copies a backup over the program directory without deleting files it does not contain.</summary>
    public static void Restore(string backupPath, string targetDirectory)
    {
        if (!Directory.Exists(backupPath)) throw new DirectoryNotFoundException(backupPath);
        CopyDirectory(backupPath, targetDirectory);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static string SafeName(string value)
    {
        var name = value.Trim();
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains('/') || name.Contains('\\') || name is "." or "..")
            throw new ArgumentException("版本号不能用作目录名。", nameof(value));
        return name;
    }
}
