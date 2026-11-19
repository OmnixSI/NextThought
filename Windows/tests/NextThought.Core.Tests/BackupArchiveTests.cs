using System.IO.Compression;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for writing and reading .ntbackup archives.</summary>
public sealed class BackupArchiveTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-backup-archive-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static BackupManifest Manifest(int workspaces = 1, int sessions = 0) => new()
    {
        Kind = BackupManifest.BackupKind.Full,
        AppVersion = "1.0.3",
        AppBuild = "103",
        WorkspaceCount = workspaces,
        SessionCount = sessions
    };

    private static WorkspaceData Workspaces() => new([
        new WorkspaceSeries("s1", "结构组会", DateTimeOffset.UtcNow, ["m1"],
            [new WorkspaceLesson { Id = "l1", Text = "经验" }])
    ]);

    [Fact]
    public void ArchiveContainsManifestSettingsAndWorkspaces()
    {
        var path = Path.Combine(root, "backup.ntbackup");
        BackupArchive.Write(path, Manifest(), Workspaces(), new Dictionary<string, object?>
        {
            ["apiModel"] = "gpt",
            ["notExported"] = 42
        });
        using var archive = ZipFile.OpenRead(path);
        Assert.NotNull(archive.GetEntry("manifest.json"));
        Assert.NotNull(archive.GetEntry("settings.json"));
        Assert.NotNull(archive.GetEntry("Workspaces/s1.json"));

        using var settings = new StreamReader(archive.GetEntry("settings.json")!.Open());
        var json = settings.ReadToEnd();
        Assert.Contains("\"apiModel\": \"gpt\"", json);
        Assert.DoesNotContain("notExported", json);

        var manifest = BackupArchive.ReadManifest(path);
        Assert.NotNull(manifest);
        Assert.Equal(BackupManifest.FormatIdentifier, manifest!.Format);
        Assert.Equal(1, manifest.WorkspaceCount);
    }

    [Fact]
    public void SessionFoldersAreCopiedWhenGiven()
    {
        var sessions = Path.Combine(root, "Sessions");
        Directory.CreateDirectory(Path.Combine(sessions, "sess1"));
        File.WriteAllText(Path.Combine(sessions, "sess1", "meta.json"), "{}");
        File.WriteAllText(Path.Combine(sessions, "sess1", "transcript.jsonl"), "{}\n");
        var path = Path.Combine(root, "with-sessions.ntbackup");
        BackupArchive.Write(path, Manifest(sessions: 1), Workspaces(), new Dictionary<string, object?>(), sessions);
        using var archive = ZipFile.OpenRead(path);
        Assert.NotNull(archive.GetEntry("Sessions/sess1/meta.json"));
        Assert.NotNull(archive.GetEntry("Sessions/sess1/transcript.jsonl"));
    }

    [Fact]
    public void AnInvalidManifestIsRejectedBeforeWriting()
    {
        var path = Path.Combine(root, "bad.ntbackup");
        Assert.Throws<BackupManifest.ValidationException>(() =>
            BackupArchive.Write(path, Manifest() with { Format = "other" }, Workspaces(), new Dictionary<string, object?>()));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ReadingANonBackupReturnsNull()
    {
        var path = Path.Combine(root, "plain.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a backup");
        }
        Assert.Null(BackupArchive.ReadManifest(path));
    }
}
