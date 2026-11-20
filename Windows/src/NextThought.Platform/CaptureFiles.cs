namespace NextThought.Platform;

/// <summary>
/// E13: the temporary files screenshots leave behind. Mac writes them to the temp directory and
/// deletes them after the question; Windows keeps a bounded set and clears stale ones on start so
/// cancelled/failed captures cannot pile up.
/// </summary>
public static class CaptureFiles
{
    /// <summary>Prefixes the capture flow uses (region/window screenshots).</summary>
    public static readonly IReadOnlyList<string> Prefixes = ["nextthought-shot-", "nextthought-window-"];

    public static bool IsCaptureFile(string path)
    {
        var name = Path.GetFileName(path);
        return Prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Capture files older than <paramref name="cutoff"/>; also drops non-image siblings.</summary>
    public static IReadOnlyList<string> Stale(string directory, DateTimeOffset cutoff)
    {
        if (!Directory.Exists(directory)) return [];
        var result = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            if (!IsCaptureFile(path)) continue;
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff.UtcDateTime) result.Add(path);
            }
            catch { }
        }
        return result;
    }

    public static int DeleteStale(string directory, DateTimeOffset cutoff)
    {
        var deleted = 0;
        foreach (var path in Stale(directory, cutoff))
        {
            try { File.Delete(path); deleted++; } catch { }
        }
        return deleted;
    }
}
