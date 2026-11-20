using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>A13: settings/glossary/skills/templates merge, credentials are accepted, Originals come back.</summary>
public sealed class BackupMergeImportTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "nt-a13-" + Guid.NewGuid().ToString("N"));

    private static string Archive(string root, Action<ZipArchive> write)
    {
        var path = Path.Combine(root, "in.ntbackup");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        write(zip);
        return path;
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }

    [Fact]
    public async Task SettingsMergeKeepsCurrentValuesAndAddsImportedOnes()
    {
        var root = NewRoot();
        try
        {
            var paths = new AppPaths(root);
            await new SettingsStore(paths).SaveAsync(new AppSettings { BaseUrl = "https://keep.test", Model = "old" });
            var plist = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict><key>apiModel</key><string>imported</string></dict></plist>";
            var archive = Archive(root, zip =>
            {
                Write(zip, "manifest.json", "{\"format\":\"nextthought-backup\",\"version\":1,\"includesSecrets\":false,\"sessionCount\":0,\"workspaceCount\":0}");
                Write(zip, "settings.plist", plist);
            });
            var result = await new BackupService(paths).ImportAsync(archive);
            Assert.True(result.SettingsRestored);
            var settings = new SettingsStore(paths).Load();
            Assert.Equal("https://keep.test", settings.BaseUrl);
            Assert.Equal("imported", settings.Model);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task GlossarySkillsAndTemplatesMergeInsteadOfOverwriting()
    {
        var root = NewRoot();
        try
        {
            var paths = new AppPaths(root);
            new GlossaryStore(root).Save(new Glossary([new GlossaryEntry("Kubernetes", GlossaryKind.Product)]));
            new SkillStore(root).Save([new Skill { Id = Guid.NewGuid().ToString("D"), Name = "existing", Content = "a" }]);
            new NotesTemplateStore(root).Save([new NotesTemplate("existing", "Existing", [new NotesSectionSpec("s", "t", "i")])]);
            var archive = Archive(root, zip =>
            {
                Write(zip, "manifest.json", "{\"format\":\"nextthought-backup\",\"version\":1,\"includesSecrets\":false,\"sessionCount\":0,\"workspaceCount\":0}");
                Write(zip, "glossary.json", "{\"entries\":[{\"canonical\":\"Docker\",\"variants\":[],\"kind\":\"product\"}]}");
                Write(zip, "skills.json", "[{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"imported\",\"content\":\"b\"}]");
                Write(zip, "note-templates.json", "[{\"id\":\"imported\",\"name\":\"Imported\",\"sections\":[{\"id\":\"s1\",\"title\":\"T\",\"instruction\":\"I\",\"kind\":\"items\"}]}]");
            });
            await new BackupService(paths).ImportAsync(archive);
            var glossary = new GlossaryStore(root).Load();
            Assert.Contains(glossary.Entries, entry => entry.Canonical == "Kubernetes");
            Assert.Contains(glossary.Entries, entry => entry.Canonical == "Docker");
            var skills = new SkillStore(root).Load();
            Assert.Contains(skills, skill => skill.Name == "existing");
            Assert.Contains(skills, skill => skill.Name == "imported");
            var templates = new NotesTemplateStore(root).Load();
            Assert.Contains(templates, template => template.Name == "Existing");
            Assert.Contains(templates, template => template.Name == "Imported");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task CredentialBearingArchiveIsAcceptedAndOriginalsComeBack()
    {
        var root = NewRoot();
        try
        {
            var paths = new AppPaths(root);
            var bytes = Encoding.UTF8.GetBytes("original-pdf");
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var archive = Archive(root, zip =>
            {
                Write(zip, "manifest.json", "{\"format\":\"nextthought-backup\",\"version\":1,\"includesSecrets\":true,\"sessionCount\":0,\"workspaceCount\":0}");
                Write(zip, "secrets.plist", "<plist/>");
                using var stream = zip.CreateEntry("Originals/" + hash + ".pdf").Open();
                stream.Write(bytes);
            });
            var result = await new BackupService(paths).ImportAsync(archive);
            Assert.Equal(0, result.SessionsAdded);
            Assert.True(File.Exists(Path.Combine(paths.Root, "Originals", hash + ".pdf")));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
