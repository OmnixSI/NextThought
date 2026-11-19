using System.Security.Cryptography;
using System.Text;

namespace NextThought.Core;

public enum BackupImportStatus
{
    New,
    Identical,
    Conflict
}

/// <summary>
/// Content fingerprints for portable backups (port of BackupContent.swift): ZIP timestamps are
/// not revisions, so compare the relative file paths and their bytes.
/// </summary>
public static class BackupContent
{
    /// <summary>SHA-256 of "path\0digest" lines, sorted; throws for missing or linked entries.</summary>
    public static string Fingerprint(string directory)
    {
        if (!Directory.Exists(directory)) throw new FileNotFoundException("备份目录不存在。", directory);
        var root = Path.GetFullPath(directory);
        var entries = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("._", StringComparison.Ordinal) || name == ".DS_Store") continue;
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("备份不能包含符号链接。");
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            entries.Add(relative + "\u0000" + Digest(File.ReadAllBytes(file)));
        }
        return Digest(Encoding.UTF8.GetBytes(string.Join("\n", entries.OrderBy(entry => entry, StringComparer.Ordinal))));
    }

    public static BackupImportStatus Status(string? local, string incoming) =>
        local is null ? BackupImportStatus.New : local == incoming ? BackupImportStatus.Identical : BackupImportStatus.Conflict;

    public static string Digest(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}

/// <summary>Preflight for a portable backup archive (port of BackupArchivePolicy.swift).</summary>
public sealed class BackupArchiveException(string message) : Exception(message)
{
    public static readonly BackupArchiveException UnsafePath = new("备份包含不安全的路径。");
    public static readonly BackupArchiveException Symlink = new("备份不能包含符号链接。");
    public static readonly BackupArchiveException ResourceLimit = new("备份展开后超出资源限制或可用磁盘空间。");
    public static readonly BackupArchiveException InvalidArchive = new("无法完整读取备份压缩目录。");
}

public static class BackupArchivePolicy
{
    private const long MaxUncompressed = 64L * 1024 * 1024 * 1024;
    private const long FreeReserve = 512L * 1024 * 1024;

    /// <summary>Validates zip entry names and the system listing before extraction.</summary>
    public static void Validate(string names, string listing, long freeBytes)
    {
        var paths = names.Split('\n').Select(path => path.TrimEnd('\r')).Where(path => path.Length > 0).ToList();
        if (paths.Count == 0 || paths.Count > 100_000) throw BackupArchiveException.ResourceLimit;
        foreach (var path in paths)
        {
            var normalized = path.EndsWith('/') ? path[..^1] : path;
            if (!SafePath(normalized) || path.Contains('\r') || path.Contains('\u0000')) throw BackupArchiveException.UnsafePath;
        }
        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Count) throw BackupArchiveException.InvalidArchive;
        if (listing.Split('\n').Any(line => line.StartsWith('l'))) throw BackupArchiveException.Symlink;

        var entries = System.Text.RegularExpressions.Regex.Matches(listing, @"(\d+) files?, (\d+) bytes uncompressed");
        if (entries.Count == 0) throw BackupArchiveException.InvalidArchive;
        var match = entries[^1];
        if (!int.TryParse(match.Groups[1].Value, out var count) || count != paths.Count) throw BackupArchiveException.InvalidArchive;
        if (!long.TryParse(match.Groups[2].Value, out var size)) throw BackupArchiveException.InvalidArchive;
        if (size > MaxUncompressed || freeBytes < FreeReserve || size > (freeBytes - FreeReserve) / 2) throw BackupArchiveException.ResourceLimit;
    }

    /// <summary>Relative, inside the archive, with no traversal or Windows drive syntax.</summary>
    public static bool SafePath(string path)
    {
        if (path.Length == 0 || path.Length > 1024) return false;
        if (path.StartsWith('/') || path.StartsWith('\\') || Path.IsPathRooted(path)) return false;
        if (path.Contains(':') || path.Contains('~')) return false;
        return path.Split('/', '\\').All(segment => segment is not (".." or ".") && segment.Length > 0);
    }
}
