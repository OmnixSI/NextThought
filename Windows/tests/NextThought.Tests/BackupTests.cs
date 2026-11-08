using System.IO.Compression;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;
public sealed class BackupTests
{
    [Fact]
    public async Task WindowsRoundTripPreservesSessionsSourcesAndSkipsConflicts()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var source = new AppPaths(Path.Combine(root, "source")); var sessions = new SessionStore(source); var meta = new SessionMeta { Title = "迁移验收" };
            await sessions.CreateAsync(meta); await sessions.AppendTranscriptAsync(meta, new TranscriptLine { Date = meta.StartedAt, Text = "保留原始转写" }); await sessions.EndAsync(meta);
            var text = Path.Combine(root, "source.md"); await File.WriteAllTextAsync(text, "资料引用测试"); await new MaterialStore(source).ImportAsync(text);
            await File.WriteAllTextAsync(Path.Combine(source.Root, "api-key.dpapi"), "excluded-fixture");
            var archive = Path.Combine(root, "test.ntbackup"); await new BackupService(source).ExportAsync(archive);
            using (var zip = ZipFile.OpenRead(archive)) Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("api-key"));
            var target = new AppPaths(Path.Combine(root, "target")); var importer = new BackupService(target); var result = await importer.ImportAsync(archive);
            Assert.Equal(1, result.SessionsAdded); Assert.Equal(1, result.MaterialsAdded); Assert.True(File.Exists(result.SafetyBackup));
            Assert.Equal("保留原始转写", new SessionStore(target).Load(meta.Id).Lines.Single().Text); Assert.Single(new MaterialStore(target).List());
            var repeated = await importer.ImportAsync(archive); Assert.Equal(2, repeated.ConflictsSkipped); Assert.Equal(0, repeated.SessionsAdded);
            await importer.ExportAsync(Path.Combine(root, "reexport.ntbackup"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("C:/escape.txt")]
    [InlineData("Sessions/CON/file")]
    public async Task RejectsTraversalAndReservedNamesBeforeChangingExistingData(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-bad-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var paths = new AppPaths(Path.Combine(root, "data")); await File.WriteAllTextAsync(Path.Combine(paths.Root, "sentinel"), "unchanged");
            var archive = Path.Combine(root, "bad.ntbackup"); using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("bad"); }
            await Assert.ThrowsAsync<InvalidDataException>(() => new BackupService(paths).ImportAsync(archive));
            Assert.Equal("unchanged", await File.ReadAllTextAsync(Path.Combine(paths.Root, "sentinel"))); Assert.False(Directory.Exists(Path.Combine(paths.Root, "Sessions")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task ImportsMacVersionOnePagesAndKeepsCompleteArchive()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-mac-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "mac.ntbackup"); var id = Guid.NewGuid();
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                void Write(string name, object value) { using var writer = new StreamWriter(zip.CreateEntry("NextThought/" + name).Open()); writer.Write(JsonSerializer.Serialize(value, JsonDefaults.ArchiveOptions)); }
                Write("manifest.json", new { format = "nextthought-backup", version = 1, kind = "workspace", createdAt = DateTimeOffset.UtcNow, appVersion = "1", appBuild = "1", includesSecrets = false, workspaceCount = 1, sessionCount = 0 });
                Write("Workspaces/" + id + ".json", new { id, name = "Mac 场景", documents = new[] { new { id = Guid.NewGuid(), name = "原资料.pdf", pages = new[] { "第一页", "负责人是林工" } } }, lessons = new[] { "保持原信息" } });
            }
            var target = new AppPaths(Path.Combine(root, "data")); var result = await new BackupService(target).ImportAsync(archive);
            var document = new MaterialStore(target).List().Single(); Assert.Equal("负责人是林工", document.Blocks[1].Text); Assert.Equal(2, document.Blocks[1].Page);
            Assert.Equal(File.ReadAllBytes(archive), File.ReadAllBytes(result.PreservedArchive));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CorruptedPayloadAndFutureVersionAreRejectedBeforeCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-hash-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "bad.ntbackup");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); writer.Write("{\"format\":\"nextthought-backup\",\"version\":99,\"includesSecrets\":false}");
            }
            var target = new AppPaths(Path.Combine(root, "data")); await Assert.ThrowsAsync<InvalidDataException>(() => new BackupService(target).ImportAsync(archive)); Assert.Empty(Directory.GetFiles(target.Root, "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task HashTamperAndMidCommitFailureLeaveExistingDataUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-rollback-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var source = new AppPaths(Path.Combine(root, "source")); var store = new SessionStore(source); var meta = new SessionMeta(); await store.CreateAsync(meta); await store.EndAsync(meta);
            var input = Path.Combine(root, "doc.md"); await File.WriteAllTextAsync(input, "hash fixture"); var document = await new MaterialStore(source).ImportAsync(input);
            var archive = Path.Combine(root, "source.ntbackup"); await new BackupService(source).ExportAsync(archive);
            var target = new AppPaths(Path.Combine(root, "target")); Directory.CreateDirectory(Path.Combine(target.Root, "Materials"));
            var blocked = Path.Combine(target.Root, "Materials", document.Id); await File.WriteAllTextAsync(blocked, "original sentinel");
            await Assert.ThrowsAsync<IOException>(() => new BackupService(target).ImportAsync(archive));
            Assert.Empty(new SessionStore(target).List()); Assert.Equal("original sentinel", await File.ReadAllTextAsync(blocked));
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
            { var name = "Materials/" + document.Id + "/source.md"; zip.GetEntry(name)!.Delete(); using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("tampered"); }
            await Assert.ThrowsAsync<InvalidDataException>(() => new BackupService(target).ImportAsync(archive)); Assert.Empty(new SessionStore(target).List());
        }
        finally { Directory.Delete(root, true); }
    }
}
