using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record ReleaseFile(string Path, long Size, string Sha256);
public sealed record ReleaseManifest(string Product, string Version, string Revision, int ArchiveSchema, string Signing, ReleaseFile[] Files);
public sealed record InstalledRelease(string Product, string CurrentVersion, string? PreviousVersion, int ArchiveSchema, string ShortcutGroup, bool DesktopShortcut, DateTimeOffset ActivatedAt);

/// <summary>Version selection and integrity checks; never deletes or migrates user data.</summary>
public sealed class DeploymentStore
{
    public const string Product = "NextThought.Windows.Preview";
    public string Root { get; }
    public string StateFile => Path.Combine(Root, "installed-release.json");
    public DeploymentStore(string root, string? protectedDataRoot = null)
    {
        Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var data = Path.GetFullPath(protectedDataRoot ?? Environment.GetEnvironmentVariable("NEXTTHOUGHT_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought"));
        if (Inside(Root, data) || Inside(data, Root) || Root.Length < 4) throw new InvalidDataException("安装目录必须与会议和模型数据目录分离。");
        RejectLinks(Root);
    }
    private static bool Inside(string path, string parent) => string.Equals(path.TrimEnd('\\'), parent.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    private static void RejectLinks(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("安装目录不支持符号链接或联接点。");
    }
    private static void CheckVersion(string version)
    {
        if (!Regex.IsMatch(version, @"^\d{1,5}\.\d{1,5}\.\d{1,5}$", RegexOptions.CultureInvariant)) throw new InvalidDataException("安装版本格式无效。");
    }
    public string VersionDirectory(string version) { CheckVersion(version); return Path.Combine(Root, "versions", version); }
    public InstalledRelease? Load()
    {
        if (!File.Exists(StateFile)) return null;
        var state = JsonSerializer.Deserialize<InstalledRelease>(File.ReadAllText(StateFile), JsonDefaults.Options) ?? throw new InvalidDataException("安装状态无效。");
        if (state.Product != Product || state.ArchiveSchema != 1) throw new InvalidDataException("安装状态的产品或归档版本不兼容。");
        CheckVersion(state.CurrentVersion); if (state.PreviousVersion is { } previous) CheckVersion(previous);
        CheckGroup(state.ShortcutGroup); return state;
    }
    private static void CheckGroup(string group)
    {
        if (!Regex.IsMatch(group, @"^NextThought[A-Za-z0-9 ._-]{0,70}$", RegexOptions.CultureInvariant)) throw new InvalidDataException("快捷方式分组无效。");
    }
    public async Task<ReleaseManifest> VerifyAsync(string version, CancellationToken ct = default)
    {
        return await VerifyDirectoryAsync(VersionDirectory(version), version, ct);
    }
    public static async Task<ReleaseManifest> VerifyDirectoryAsync(string directory, string version, CancellationToken ct = default)
    {
        CheckVersion(version); directory = Path.GetFullPath(directory); RejectLinks(directory);
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(await File.ReadAllTextAsync(Path.Combine(directory, "release-manifest.json"), ct), JsonDefaults.Options) ?? throw new InvalidDataException("发布清单无效。");
        if (manifest.Product != Product || manifest.Version != version || manifest.ArchiveSchema != 1 || manifest.Files.Length is < 1 or > 3000)
            throw new InvalidDataException("发布清单与目标版本不符。");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Path) || entry.Path.Contains(':') || entry.Path.Contains('\\') || Path.IsPathRooted(entry.Path)
                || entry.Path.Split('/').Any(p => p is "." or ".." or "") || !names.Add(entry.Path)) throw new InvalidDataException("发布清单包含无效或重复路径。");
            var target = Path.GetFullPath(Path.Combine(directory, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!Inside(target, directory)) throw new InvalidDataException("发布文件越出版本目录。");
            RejectLinks(Path.GetDirectoryName(target)!);
            if (!File.Exists(target) || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("发布文件缺失或为链接：" + entry.Path);
            await using var file = File.OpenRead(target);
            if (file.Length != entry.Size || !Regex.IsMatch(entry.Sha256, "^[a-f0-9]{64}$") || Convert.ToHexStringLower(await SHA256.HashDataAsync(file, ct)) != entry.Sha256)
                throw new InvalidDataException("发布文件校验失败：" + entry.Path);
        }
        if (!names.Contains("NextThought.exe")) throw new InvalidDataException("发布清单缺少主程序。");
        var pending = new Stack<string>(); pending.Push(directory);
        while (pending.TryPop(out var folder))
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("版本目录包含链接。");
            if (entry is DirectoryInfo) pending.Push(entry.FullName);
            else
            {
                var relative = Path.GetRelativePath(directory, entry.FullName).Replace('\\', '/');
                if (relative != "release-manifest.json" && !names.Contains(relative)) throw new InvalidDataException("版本目录包含清单之外的文件：" + relative);
            }
        }
        return manifest;
    }
    public async Task<InstalledRelease> ActivateAsync(string version, string group, bool desktop, CancellationToken ct = default, Func<InstalledRelease, Task>? applyShell = null)
    {
        CheckGroup(group); Directory.CreateDirectory(Root);
        await using var lease = new FileStream(Path.Combine(Root, ".deployment.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await VerifyAsync(version, ct); var current = Load();
        var previous = current?.CurrentVersion == version ? current.PreviousVersion : current?.CurrentVersion;
        var state = new InstalledRelease(Product, version, previous, 1, group, desktop, DateTimeOffset.UtcNow);
        await SaveAsync(state, current, ct, applyShell); return state;
    }
    public async Task<InstalledRelease> RollbackAsync(CancellationToken ct = default, Func<InstalledRelease, Task>? applyShell = null)
    {
        await using var lease = new FileStream(Path.Combine(Root, ".deployment.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var current = Load() ?? throw new InvalidDataException("尚无安装状态。");
        var previous = current.PreviousVersion ?? throw new InvalidOperationException("没有可回退的上一版本。");
        await VerifyAsync(previous, ct);
        var state = current with { CurrentVersion = previous, PreviousVersion = current.CurrentVersion, ActivatedAt = DateTimeOffset.UtcNow };
        await SaveAsync(state, current, ct, applyShell); return state;
    }
    private async Task SaveAsync(InstalledRelease state, InstalledRelease? previous, CancellationToken ct, Func<InstalledRelease, Task>? applyShell)
    {
        if (previous is not null) await AppPaths.AtomicWriteAsync(StateFile + ".previous", JsonSerializer.Serialize(previous, JsonDefaults.Options), ct);
        if (applyShell is not null) await applyShell(state);
        await AppPaths.AtomicWriteAsync(StateFile, JsonSerializer.Serialize(state, JsonDefaults.Options), ct);
    }
}
