using System.IO.Compression;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class MacSettingsTests
{
    [Fact]
    public void PlistRoundTripsTheMappedSettings()
    {
        var settings = new AppSettings
        {
            BaseUrl = "https://example.test/v1", Model = "m1", NotesModel = "n1", LocalModel = "xasrOffline",
            SecondPass = false, CaptureMicrophone = false, AutomaticAnswers = true, AutoSummarize = true,
            UseSemanticRetrieval = true, WebSearch = false, CodexModel = "c1"
        };
        var restored = MacSettingsPlist.Apply(MacSettingsPlist.Write(settings), new AppSettings());
        Assert.Equal(settings.BaseUrl, restored.BaseUrl);
        Assert.Equal(settings.Model, restored.Model);
        Assert.Equal(settings.NotesModel, restored.NotesModel);
        Assert.Equal(settings.LocalModel, restored.LocalModel);
        Assert.False(restored.SecondPass);
        Assert.False(restored.CaptureMicrophone);
        Assert.True(restored.AutomaticAnswers);
        Assert.True(restored.AutoSummarize);
        Assert.True(restored.UseSemanticRetrieval);
        Assert.False(restored.WebSearch);
        Assert.Equal(settings.CodexModel, restored.CodexModel);
    }

    [Fact]
    public async Task MacStyleBackupRestoresPlistAndRootGlossary()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-mac-style-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "mac.ntbackup");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                void Write(string name, string text)
                {
                    var entry = zip.CreateEntry(name);
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(text);
                }
                Write("manifest.json", "{\"format\":\"nextthought-backup\",\"version\":1,\"includesSecrets\":false,\"sessionCount\":0,\"workspaceCount\":0}");
                Write("settings.plist", System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(new AppSettings { BaseUrl = "https://mac.test/v1", Model = "mac-model" })));
                Write("glossary.json", "{\"entries\":[{\"canonical\":\"Docker\",\"kind\":\"product\"}]}");
            }
            var target = new AppPaths(Path.Combine(root, "target"));
            var result = await new BackupService(target).ImportAsync(archive);
            Assert.True(result.SettingsRestored);
            Assert.Equal("https://mac.test/v1", new SettingsStore(target).Load().BaseUrl);
            Assert.Contains(new GlossaryStore(target.Root).Load().Entries, entry => entry.Canonical == "Docker");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
