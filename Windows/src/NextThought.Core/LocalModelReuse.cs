using System.Security.Cryptography;

namespace NextThought.Core;

public sealed record LocalModelFile(string Path, long Bytes, string Sha256);

public sealed class LocalModelSearch
{
    public List<string> Directories { get; } = [];
    public bool Limited { get; set; }
    public int Inaccessible { get; set; }
}

public sealed class LocalModelReuseResult
{
    public int ReusedFiles { get; set; }
    public long ReusedBytes { get; set; }
}

/// <summary>
/// Finds already-downloaded model assets and copies verified ones into a new snapshot
/// (port of LocalModelReuse.swift). Filenames alone never establish compatibility: every
/// candidate is checked against the pinned size and SHA-256.
/// </summary>
public static class LocalModelReuse
{
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".Trash", "node_modules", ".build", ".venv", "venv", "site-packages", "DerivedData", "Library"
    };

    /// <summary>Enumerates names and metadata only; symlinked directories are not followed.</summary>
    public static LocalModelSearch Search(
        IReadOnlyList<string> roots,
        IReadOnlyList<string>? indexedFiles,
        IReadOnlySet<string> names,
        int maximumEntries = 300_000,
        double seconds = 20,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new LocalModelSearch();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        var count = 0;

        void Add(string directory)
        {
            var full = Path.GetFullPath(directory);
            if (seen.Add(full)) result.Directories.Add(full);
        }

        foreach (var file in indexedFiles ?? [])
        {
            if (!names.Contains(Path.GetFileName(file))) continue;
            Add(Directory.Exists(file) ? file : Path.GetDirectoryName(file) ?? Path.GetDirectoryName(Path.GetFullPath(file))!);
        }

        foreach (var root in roots)
        {
            if (!visited.Add(Path.GetFullPath(root))) continue;
            Add(root);
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline) { result.Limited = true; return result; }
                var directory = stack.Pop();
                IEnumerable<string> entries;
                try { entries = Directory.EnumerateFileSystemEntries(directory); }
                catch { result.Inaccessible++; continue; }
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    count++;
                    if (count > maximumEntries || DateTime.UtcNow > deadline) { result.Limited = true; return result; }
                    var name = Path.GetFileName(entry);
                    if (Directory.Exists(entry))
                    {
                        if (names.Contains(name)) Add(entry);
                        var attributes = File.GetAttributes(entry);
                        if (Skipped.Contains(name) || (attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if (visited.Add(Path.GetFullPath(entry))) stack.Push(entry);
                    }
                    else if (names.Contains(name))
                    {
                        Add(Path.GetDirectoryName(entry)!);
                    }
                }
            }
        }
        return result;
    }

    /// <summary>True when the file resolves to the pinned size and SHA-256.</summary>
    public static bool Matches(string path, LocalModelFile file)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return false;
            if (info.LinkTarget is not null && info.ResolveLinkTarget(true) is FileInfo resolved && resolved.Exists) info = resolved;
            if (!info.Exists || info.Length != file.Bytes) return false;
            using var stream = info.OpenRead();
            return Convert.ToHexStringLower(SHA256.HashData(stream)) == file.Sha256.ToLowerInvariant();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Copies matching files independently, verifying every staged copy before the atomic move.
    /// Originals are untouched; an incomplete snapshot still reuses its intact assets.
    /// </summary>
    public static LocalModelReuseResult Reuse(IReadOnlyList<LocalModelFile> files, IReadOnlyList<string> directories, string destination)
    {
        var result = new LocalModelReuseResult();
        foreach (var file in files)
        {
            if (Path.IsPathRooted(file.Path) || file.Path.Split('/', '\\').Any(segment => segment == ".."))
                throw new InvalidDataException("模型文件路径不安全。");
            var relative = file.Path.Replace('/', Path.DirectorySeparatorChar);
            var target = Path.Combine(destination, relative);
            if (Matches(target, file)) continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetFull = Path.GetFullPath(target);
            foreach (var directory in directories)
            {
                var candidate = Path.Combine(directory, relative);
                if (!seen.Add(candidate)) continue;
                if (string.Equals(Path.GetFullPath(candidate), targetFull, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Matches(candidate, file)) continue;

                var parent = Path.GetDirectoryName(target)!;
                Directory.CreateDirectory(parent);
                var stage = Path.Combine(parent, ".reuse-" + Guid.NewGuid().ToString("N"));
                try
                {
                    File.Copy(candidate, stage, true);
                    if (!Matches(stage, file)) throw new InvalidDataException("复制的模型文件校验失败。");
                    File.Move(stage, target, true);
                    result.ReusedFiles++;
                    result.ReusedBytes += file.Bytes;
                    break;
                }
                finally
                {
                    if (File.Exists(stage)) { try { File.Delete(stage); } catch { } }
                }
            }
        }
        return result;
    }
}
