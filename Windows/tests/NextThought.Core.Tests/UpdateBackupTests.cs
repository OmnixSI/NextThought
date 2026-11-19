using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for pre-update program-file backups and rollback.</summary>
public sealed class UpdateBackupTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-update-backup-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private string AppDirectory()
    {
        var app = Path.Combine(root, "app");
        Directory.CreateDirectory(Path.Combine(app, "runtimes"));
        File.WriteAllText(Path.Combine(app, "NextThought.exe"), "old");
        File.WriteAllText(Path.Combine(app, "NextThought.dll"), "dll");
        File.WriteAllText(Path.Combine(app, "runtimes", "native.dll"), "native");
        return app;
    }

    [Fact]
    public void CreateCopiesTheWholeProgramDirectory()
    {
        var backup = UpdateBackup.Create(AppDirectory(), Path.Combine(root, "backups"), "0.65.0");
        Assert.True(Directory.Exists(backup.Path));
        Assert.Equal("old", File.ReadAllText(Path.Combine(backup.Path, "NextThought.exe")));
        Assert.Equal("native", File.ReadAllText(Path.Combine(backup.Path, "runtimes", "native.dll")));
        Assert.StartsWith("0.65.0-", Path.GetFileName(backup.Path));
    }

    [Fact]
    public void ListReturnsNewestFirst()
    {
        var app = AppDirectory();
        var rootPath = Path.Combine(root, "backups");
        var first = UpdateBackup.Create(app, rootPath, "1.0.0");
        Thread.Sleep(20);
        var second = UpdateBackup.Create(app, rootPath, "2.0.0");
        var listed = UpdateBackup.List(rootPath);
        Assert.Equal([second.Path, first.Path], listed.Select(item => item.Path));
        Assert.Equal(["2.0.0", "1.0.0"], listed.Select(item => item.Version));
    }

    [Fact]
    public void PruneKeepsTheNewestBackups()
    {
        var app = AppDirectory();
        var rootPath = Path.Combine(root, "backups");
        for (var index = 0; index < 4; index++)
        {
            UpdateBackup.Create(app, rootPath, "1.0." + index);
            Thread.Sleep(20);
        }
        UpdateBackup.Prune(rootPath, 2);
        Assert.Equal(2, UpdateBackup.List(rootPath).Count);
        Assert.DoesNotContain(UpdateBackup.List(rootPath), item => item.Version == "1.0.0");
    }

    [Fact]
    public void RestoreCopiesBackAndOverwritesChangedFiles()
    {
        var app = AppDirectory();
        var backup = UpdateBackup.Create(app, Path.Combine(root, "backups"), "1.0.0");
        File.WriteAllText(Path.Combine(app, "NextThought.exe"), "new");
        File.Delete(Path.Combine(app, "runtimes", "native.dll"));
        File.WriteAllText(Path.Combine(app, "extra.txt"), "new file");
        UpdateBackup.Restore(backup.Path, app);
        Assert.Equal("old", File.ReadAllText(Path.Combine(app, "NextThought.exe")));
        Assert.Equal("native", File.ReadAllText(Path.Combine(app, "runtimes", "native.dll")));
        Assert.True(File.Exists(Path.Combine(app, "extra.txt")));
    }

    [Fact]
    public void MissingInputsAreRejected()
    {
        Assert.Throws<DirectoryNotFoundException>(() => UpdateBackup.Create(Path.Combine(root, "missing"), Path.Combine(root, "backups"), "1"));
        Assert.Throws<ArgumentException>(() => UpdateBackup.Create(AppDirectory(), Path.Combine(root, "backups"), "../escape"));
        Assert.Empty(UpdateBackup.List(Path.Combine(root, "none")));
        Assert.Throws<DirectoryNotFoundException>(() => UpdateBackup.Restore(Path.Combine(root, "missing"), Path.Combine(root, "app")));
    }

    [Fact]
    public void EmptyBackupRootPrunesNothing()
    {
        UpdateBackup.Prune(Path.Combine(root, "none"), 1);
        Assert.Empty(UpdateBackup.List(Path.Combine(root, "none")));
    }
}
