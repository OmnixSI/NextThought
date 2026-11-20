using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>A12: the exported plist carries every Mac-exported key and the Mac web-search values.</summary>
public sealed class MacSettingsPlistTests
{
    [Fact]
    public void PlistWritesEveryExportedKeyWithADoctype()
    {
        var settings = new AppSettings { WebSearch = true, Appearance = "dark", CueWidth = "wide" };
        var text = System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(settings));
        Assert.Contains("<!DOCTYPE plist PUBLIC", text);
        var document = XDocument.Parse(text);
        var keys = document.Root!.Element("dict")!.Elements("key").Select(element => element.Value).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(SettingsSnapshot.ExportedKeys.ToHashSet(StringComparer.Ordinal), keys);
        var webSearch = document.Root.Element("dict")!.Elements().Select((element, index) => (element, index))
            .First(pair => pair.element.Name == "key" && pair.element.Value == "webSearchMode").index;
        Assert.Equal("auto", document.Root.Element("dict")!.Elements().ElementAt(webSearch + 1).Value);
        Assert.Contains("dark", text);
    }

    [Fact]
    public async Task ExportSkipsWindowsOnlyDerivedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-a12-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var id = Guid.NewGuid();
            var folder = Path.Combine(paths.Sessions, id.ToString().ToUpperInvariant());
            Directory.CreateDirectory(folder);
            var meta = new SessionMeta { Id = id, Title = "t", StartedAt = DateTimeOffset.UtcNow, EndedAt = DateTimeOffset.UtcNow };
            await File.WriteAllTextAsync(Path.Combine(folder, "meta.json"), JsonSerializer.Serialize(meta, JsonDefaults.ArchiveOptions));
            await File.WriteAllTextAsync(Path.Combine(folder, "structured-notes.md"), "windows only");
            await File.WriteAllTextAsync(Path.Combine(folder, "structured-notes.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(folder, "corrections.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(folder, "summary-job.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(folder, "meta.json.bak"), "{}");
            var archive = Path.Combine(root, "out.ntbackup");
            await new BackupService(paths).ExportAsync(archive);
            using var zip = ZipFile.OpenRead(archive);
            var names = zip.Entries.Select(entry => entry.FullName).ToList();
            Assert.DoesNotContain(names, name => name.Contains("structured-notes", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("corrections.json", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("summary-job.json", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(names, name => name == "settings.plist");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void UnknownPlistKeysSurviveApplyAndWrite()
    {
        var plist = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict>"
            + "<key>apiModel</key><string>m</string>"
            + "<key>macShortcutCodes</key><array><integer>1</integer><integer>2</integer></array>"
            + "<key>macOnlyFlag</key><true/>"
            + "<key>macNote</key><string>keep me</string>"
            + "</dict></plist>";
        var applied = MacSettingsPlist.Apply(System.Text.Encoding.UTF8.GetBytes(plist), new AppSettings());
        Assert.Equal("m", applied.Model);
        Assert.NotNull(applied.AdditionalProperties);
        Assert.True(applied.AdditionalProperties!.ContainsKey("macShortcutCodes"));
        var output = System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(applied));
        Assert.Contains("macShortcutCodes", output);
        Assert.Contains("macOnlyFlag", output);
        Assert.Contains("macNote", output);
        Assert.Contains("keep me", output);
    }
}
