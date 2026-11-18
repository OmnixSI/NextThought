using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NextThought.Core;

public sealed record RestorePoint
{
    public int Version { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; }
    public string Reason { get; init; } = "";
    public string Fingerprint { get; init; } = "";
    public Dictionary<string, string> Files { get; init; } = [];
}

/// <summary>
/// Content-addressed recovery storage (port of IncrementalBackupStore.swift). A published
/// manifest is a commit: objects are durable and verified before it appears, and pruning only
/// follows a successful write.
/// </summary>
public sealed class IncrementalBackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public IncrementalBackupStore(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }
    private string Objects => Path.Combine(Root, "Objects");
    private string Points => Path.Combine(Root, "RestorePoints");

    public static string Digest(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public static bool SafePath(string path)
    {
        if (path.Length == 0 || path.StartsWith('/') || path.Contains('\\')) return false;
        return path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));
    }

    /// <summary>Stores every file and directory snapshot, then publishes a verified manifest.</summary>
    public string Create(
        IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyDictionary<string, string>? directories = null,
        string reason = "",
        int keep = 10,
        long budget = 2L * 1024 * 1024 * 1024)
    {
        Directory.CreateDirectory(Objects);
        Directory.CreateDirectory(Points);
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        void Store(byte[] data, string name)
        {
            if (!SafePath(name)) throw new InvalidDataException("备份包含不安全的路径。");
            var hash = Digest(data);
            var target = Path.Combine(Objects, hash);
            var existingHash = File.Exists(target) ? Digest(File.ReadAllBytes(target)) : null;
            if (existingHash != hash) File.WriteAllBytes(target, data);
            references[name] = hash;
        }

        foreach (var (name, data) in files) Store(data, name);
        foreach (var (prefix, directory) in directories ?? new Dictionary<string, string>())
        {
            if (!SafePath(prefix)) throw new InvalidDataException("备份包含不安全的路径。");
            if (!Directory.Exists(directory)) continue; // optional data directories may not exist yet
            var root = Path.GetFullPath(directory);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new NotSupportedException("备份不能包含符号链接。");
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                Store(File.ReadAllBytes(file), prefix + "/" + relative);
            }
        }

        var fingerprint = Digest(Encoding.UTF8.GetBytes(string.Join("\n",
            references.Keys.OrderBy(name => name, StringComparer.Ordinal).Select(name => name + "\u0000" + references[name]))));
        var existing = Manifests();
        if (existing.Count > 0 && existing[0].Point.Fingerprint == fingerprint)
        {
            Verify(existing[0].Point);
            return existing[0].Path;
        }

        var point = new RestorePoint
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Reason = reason,
            Fingerprint = fingerprint,
            Files = references
        };
        Verify(point);
        var path = Path.Combine(Points, $"{Guid.NewGuid()}-{reason}.ntrestore");
        File.WriteAllText(path, JsonSerializer.Serialize(point, JsonOptions));
        Prune(Math.Max(1, keep), budget);
        return path;
    }

    public long StorageBytes() =>
        Directory.Exists(Objects)
            ? Directory.EnumerateFiles(Objects).Sum(file => new FileInfo(file).Length)
            : 0;

    /// <summary>Writes a restore point back; only points belonging to this store are accepted.</summary>
    public void Restore(string url, string destination)
    {
        var directory = Path.GetFullPath(Path.GetDirectoryName(url) ?? "");
        if (!string.Equals(directory, Path.GetFullPath(Points), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("恢复点不属于这个备份库。");
        var point = JsonSerializer.Deserialize<RestorePoint>(File.ReadAllText(url), JsonOptions)
                    ?? throw new InvalidDataException("恢复点损坏。");
        Verify(point);
        foreach (var (name, hash) in point.Files)
        {
            var file = Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, File.ReadAllBytes(Path.Combine(Objects, hash)));
        }
    }

    private void Verify(RestorePoint point)
    {
        if (point.Version != 1) throw new InvalidDataException("恢复点版本不支持。");
        foreach (var (name, hash) in point.Files)
        {
            var objectPath = Path.Combine(Objects, hash);
            if (!SafePath(name) || hash.Length != 64 || !hash.All(char.IsAsciiHexDigit) || !File.Exists(objectPath)
                || Digest(File.ReadAllBytes(objectPath)) != hash)
                throw new InvalidDataException("恢复点损坏。");
        }
        var expected = Digest(Encoding.UTF8.GetBytes(string.Join("\n",
            point.Files.Keys.OrderBy(name => name, StringComparer.Ordinal).Select(name => name + "\u0000" + point.Files[name]))));
        if (point.Fingerprint != expected) throw new InvalidDataException("恢复点校验失败。");
    }

    private List<(string Path, RestorePoint Point)> Manifests()
    {
        if (!Directory.Exists(Points)) return [];
        var result = new List<(string Path, RestorePoint Point)>();
        foreach (var file in Directory.EnumerateFiles(Points))
        {
            var extension = Path.GetExtension(file);
            if (extension is not (".ntrestore" or ".ihrestore")) continue;
            try
            {
                var point = JsonSerializer.Deserialize<RestorePoint>(File.ReadAllText(file), JsonOptions);
                if (point is not null) result.Add((file, point));
            }
            catch (JsonException)
            {
            }
        }
        return result.OrderByDescending(entry => entry.Point.CreatedAt).ToList();
    }

    private void Prune(int keep, long budget)
    {
        var remaining = Manifests();
        HashSet<string> Hashes() => remaining.SelectMany(entry => entry.Point.Files.Values).ToHashSet(StringComparer.Ordinal);
        long Size() => Hashes().Sum(hash =>
        {
            var path = Path.Combine(Objects, hash);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        });

        while (remaining.Count > 1 && (remaining.Count > keep || Size() > budget))
        {
            var old = remaining[^1];
            remaining.RemoveAt(remaining.Count - 1);
            File.Delete(old.Path);
        }

        var referenced = Hashes();
        foreach (var file in Directory.EnumerateFiles(Objects))
            if (!referenced.Contains(Path.GetFileName(file)))
                File.Delete(file);
    }
}
