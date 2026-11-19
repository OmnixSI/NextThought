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
    public async Task ImportsMacMaterialsFolderWithMarkdownOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-mac-materials-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var archive = Path.Combine(root, "mac-materials.ntbackup");
        var id = Guid.NewGuid().ToString("D").ToUpperInvariant();
        using (var zip = System.IO.Compression.ZipFile.Open(archive, System.IO.Compression.ZipArchiveMode.Create))
        {
            void Write(string name, string text)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(text);
            }
            Write("manifest.json", "{\"format\":\"nextthought-backup\",\"version\":1,\"includesSecrets\":false,\"sessionCount\":0,\"workspaceCount\":0}");
            Write("Materials/series/" + id + ".md", "## 第 1 页\nMac 资料正文");
        }
        var target = new AppPaths(Path.Combine(root, "target-mac"));
        var result = await new BackupService(target).ImportAsync(archive);
        Assert.Equal(1, result.MaterialsAdded);
        var material = Assert.Single(new MaterialStore(target).List());
        Assert.Contains("Mac 资料正文", material.Blocks[0].Text);
    }

    [Fact]
    public async Task ImportsMacWorkspaceFilesIntoTheSeriesStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-mac-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var archive = Path.Combine(root, "mac-workspaces.ntbackup");
        var id = Guid.NewGuid().ToString("D").ToUpperInvariant();
        using (var zip = System.IO.Compression.ZipFile.Open(archive, System.IO.Compression.ZipArchiveMode.Create))
        {
            void Write(string name, string text)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(text);
            }
            Write("manifest.json", "{\"format\":\"nextthought-backup\",\"version\":1,\"includesSecrets\":false,\"sessionCount\":0,\"workspaceCount\":1}");
            Write("Workspaces/" + id + ".json",
                "{\"id\":\"" + id + "\",\"name\":\"Mac 系列\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"sessionIDs\":[\"s1\"],\"lessons\":[]}");
        }
        var target = new AppPaths(Path.Combine(root, "target-mac-workspace"));
        var result = await new BackupService(target).ImportAsync(archive);
        Assert.Equal(1, result.WorkspacesAdded);
        var series = Assert.Single(new WorkspaceStore(target.Root).Load().Series);
        Assert.Equal("Mac 系列", series.Name);
        Assert.Equal("s1", series.SessionIds.Single());
    }

    [Fact]
    public async Task RoundTripRestoresNonSecretSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-settings-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = new AppPaths(Path.Combine(root, "source"));
            await new SettingsStore(source).SaveAsync(new AppSettings { BaseUrl = "https://example.test/v1", Model = "backup-model" });
            new GlossaryStore(source.Root).Save(new Glossary([new GlossaryEntry("Kubernetes", GlossaryKind.Product)]));
            var archive = Path.Combine(root, "settings.ntbackup");
            await new BackupService(source).ExportAsync(archive);
            using (var zip = ZipFile.OpenRead(archive))
                Assert.Contains(zip.Entries, entry => entry.FullName == "settings.json");

            var target = new AppPaths(Path.Combine(root, "target"));
            var result = await new BackupService(target).ImportAsync(archive);
            Assert.True(result.SettingsRestored);
            var restored = new SettingsStore(target).Load();
            Assert.Equal("backup-model", restored.Model);
            Assert.Equal("https://example.test/v1", restored.BaseUrl);
            Assert.Contains(new GlossaryStore(target.Root).Load().Entries, entry => entry.Canonical == "Kubernetes");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task RoundTripRestoresSkillsAndCustomTemplates()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-skills-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = new AppPaths(Path.Combine(root, "source"));
            Directory.CreateDirectory(source.Root);
            await File.WriteAllTextAsync(Path.Combine(source.Root, "skills.json"),
                "[{\"id\":\"" + Guid.NewGuid().ToString("D") + "\",\"name\":\"简洁回答\",\"content\":\"先说结论\"}]");
            await File.WriteAllTextAsync(Path.Combine(source.Root, "note-templates.json"),
                "[{\"id\":\"custom-1\",\"name\":\"客户会\",\"sections\":[]}]");
            var archive = Path.Combine(root, "skills.ntbackup");
            await new BackupService(source).ExportAsync(archive);
            using (var zip = ZipFile.OpenRead(archive))
            {
                Assert.Contains(zip.Entries, entry => entry.FullName == "skills.json");
                Assert.Contains(zip.Entries, entry => entry.FullName == "note-templates.json");
            }

            var target = new AppPaths(Path.Combine(root, "target"));
            await new BackupService(target).ImportAsync(archive);
            Assert.Contains("简洁回答", await File.ReadAllTextAsync(Path.Combine(target.Root, "skills.json")));
            Assert.Contains("客户会", await File.ReadAllTextAsync(Path.Combine(target.Root, "note-templates.json")));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
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
