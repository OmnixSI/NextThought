namespace NextThought.Platform;

public sealed class AppPaths
{
    public string Root { get; }
    public string Sessions => Path.Combine(Root, "Sessions");
    public string Models => Path.Combine(Root, "models");
    public string Downloads => Path.Combine(Root, "Downloads");
    public string Settings => Path.Combine(Root, "settings.json");
    public AppPaths(string? root = null)
    {
        Root = Path.GetFullPath(root ?? Environment.GetEnvironmentVariable("NEXTTHOUGHT_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought"));
        Directory.CreateDirectory(Root);
    }
    internal static async Task AtomicWriteAsync(string file, string content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temporary, content, new System.Text.UTF8Encoding(false), ct).ConfigureAwait(false); File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
