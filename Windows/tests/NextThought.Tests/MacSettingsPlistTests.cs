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

    // ---- A12: the real Mac backup fixture (fixture-gated) --------------------------------

    private const string FixtureSkipReason =
        "SKIP: Windows/tests/Fixtures/mac-backup/nt-mac-fixtures-26.5.2-20261121.zip is missing; "
        + "the real Mac settings.plist check cannot run without the Mac fixture";

    private static string? RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Windows", "tests", "Fixtures")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }

    /// <summary>Extracts the inner all-scenarios.ntbackup and returns its real settings.plist bytes.</summary>
    private static byte[]? LoadRealMacSettingsPlist(out string skipReason)
    {
        skipReason = FixtureSkipReason;
        var root = RepositoryRoot();
        if (root is null) return null;
        var zipPath = Path.Combine(root, "Windows", "tests", "Fixtures", "mac-backup",
            "nt-mac-fixtures-26.5.2-20261121.zip");
        if (!File.Exists(zipPath)) return null;
        var staging = Path.Combine(Path.GetTempPath(), "nt-a12-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            using (var outer = ZipFile.OpenRead(zipPath))
            {
                var entry = outer.Entries.FirstOrDefault(item => item.FullName.EndsWith("/all-scenarios.ntbackup", StringComparison.Ordinal));
                if (entry is null) { skipReason = "SKIP: the Mac fixture zip has no all-scenarios.ntbackup"; return null; }
                var archive = Path.Combine(staging, "all-scenarios.ntbackup");
                entry.ExtractToFile(archive, true);
                using var inner = ZipFile.OpenRead(archive);
                var settings = inner.Entries.FirstOrDefault(item =>
                    item.FullName.EndsWith("NextThought-Backup/settings.plist", StringComparison.Ordinal)
                    && !item.FullName.StartsWith("__MACOSX/", StringComparison.Ordinal)
                    && !Path.GetFileName(item.FullName).StartsWith("._", StringComparison.Ordinal));
                if (settings is null) { skipReason = "SKIP: the Mac backup has no settings.plist"; return null; }
                using var stream = settings.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                skipReason = "";
                return memory.ToArray();
            }
        }
        catch (InvalidDataException exception)
        {
            skipReason = "SKIP: the Mac fixture zip is not readable: " + exception.Message;
            return null;
        }
        finally
        {
            try { Directory.Delete(staging, true); } catch (IOException) { }
        }
    }

    private static Dictionary<string, XElement> PlistValues(byte[] plist)
    {
        var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(plist));
        var children = document.Root!.Element("dict")!.Elements().ToList();
        var values = new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < children.Count; index += 2)
            if (children[index].Name == "key") values[children[index].Value] = children[index + 1];
        return values;
    }

    [Fact]
    public void RealMacBackupPlistHasTheKeysAndTypesThisPortMaps()
    {
        var plist = LoadRealMacSettingsPlist(out var skipReason);
        if (plist is null) { Console.WriteLine(skipReason); return; }

        // The fixture is the preferences of the Mac fixture run: only the keys it set are present.
        var values = PlistValues(plist);
        Assert.Equal(new[] { "activeWorkspaceID", "apiPresets", "transcriptionBackend" },
            values.Keys.OrderBy(key => key, StringComparer.Ordinal));

        // Types follow the Mac preference registry: two strings and the apiPresets data blob.
        Assert.Equal("string", values["activeWorkspaceID"].Name.LocalName);
        Assert.Equal("string", values["transcriptionBackend"].Name.LocalName);
        Assert.Equal("zipformer", values["transcriptionBackend"].Value);
        Assert.Equal("AD139925-ECF7-4DEF-9207-63578DBFF79B", values["activeWorkspaceID"].Value);
        Assert.True(MacPreferences.IsKnown("activeWorkspaceID"));
        Assert.Equal(MacPreferenceKind.String, MacPreferences.Find("activeWorkspaceID")!.Kind);

        // apiPresets is base64 JSON data, not an array of dictionary plist values (Mac UserDefaults).
        Assert.Equal("data", values["apiPresets"].Name.LocalName);
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
            string.Concat(values["apiPresets"].Value.Where(character => !char.IsWhiteSpace(character)))));
        using var document = JsonDocument.Parse(json);
        var preset = document.RootElement.EnumerateArray().Single();
        Assert.Equal("846ADF47-227C-465A-A122-9769BA4C4E09", preset.GetProperty("id").GetString());
        Assert.Equal("OpenAI", preset.GetProperty("name").GetString());
        Assert.Equal("https://api.openai.com/v1", preset.GetProperty("baseURL").GetString());
        Assert.Equal("answer.api", preset.GetProperty("keyAccount").GetString());
        Assert.Equal("default", preset.GetProperty("effort").GetString());
        Assert.Equal("", preset.GetProperty("model").GetString());

        // Applying the real plist maps the Mac keys into the Windows settings.
        var applied = MacSettingsPlist.Apply(plist, new AppSettings());
        Assert.Equal("", applied.CloudStt); // transcriptionBackend = zipformer
        var imported = Assert.Single(applied.ApiPresets);
        Assert.Equal("OpenAI", imported.Name);
        Assert.Equal("https://api.openai.com/v1", imported.BaseUrl);
        Assert.Equal("answer.api", imported.KeyAccount);
        // G6: the Mac-only activeWorkspaceID is kept raw for the round trip.
        Assert.NotNull(applied.AdditionalProperties);
        Assert.True(applied.AdditionalProperties!.TryGetValue("activeWorkspaceID", out var workspace));
        Assert.Equal("AD139925-ECF7-4DEF-9207-63578DBFF79B", workspace.GetString());
    }

    [Fact]
    public void RealMacBackupPlistExportsBackToTheSameKeysAndValues()
    {
        var plist = LoadRealMacSettingsPlist(out var skipReason);
        if (plist is null) { Console.WriteLine(skipReason); return; }

        var first = MacSettingsPlist.Apply(plist, new AppSettings());
        var exported = MacSettingsPlist.Write(first);
        var values = PlistValues(exported);

        // Every Windows-exported key is present; the Mac-only key keeps its raw value.
        Assert.Equal(SettingsSnapshot.ExportedKeys.ToHashSet(StringComparer.Ordinal), values.Keys.ToHashSet(StringComparer.Ordinal));
        Assert.Equal("AD139925-ECF7-4DEF-9207-63578DBFF79B", values["activeWorkspaceID"].Value);
        Assert.Equal("zipformer", values["transcriptionBackend"].Value);

        // apiPresets keeps the Mac data form and the same JSON fields.
        Assert.Equal("data", values["apiPresets"].Name.LocalName);
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
            string.Concat(values["apiPresets"].Value.Where(character => !char.IsWhiteSpace(character)))));
        Assert.Contains("OpenAI", json, StringComparison.Ordinal);
        Assert.Contains("https://api.openai.com/v1", json, StringComparison.Ordinal);

        // Importing our own export gives every mapped key back unchanged (configuration keys and the
        // Mac-only keys), so a Mac -> Windows -> Mac round trip loses nothing it understood.
        var second = MacSettingsPlist.Apply(exported, new AppSettings());
        var before = MacPreferenceMap.ReadAll(first);
        var after = MacPreferenceMap.ReadAll(second);
        foreach (var key in MacPreferenceMap.MappedKeys)
            Assert.True(MacPreferences.ValuesEqual(before[key], after[key]), key);
        Assert.True(first.AdditionalProperties!.ContainsKey("activeWorkspaceID"));
        Assert.True(second.AdditionalProperties!.TryGetValue("activeWorkspaceID", out var workspace));
        Assert.Equal("AD139925-ECF7-4DEF-9207-63578DBFF79B", workspace.GetString());
        // No key material: the fixture never contains any and the export must not add one.
        var text = System.Text.Encoding.UTF8.GetString(exported);
        Assert.DoesNotContain("apiKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KeyMaterialIsNeverImportedOrExported()
    {
        var plist = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict>"
            + "<key>apiModel</key><string>m</string>"
            + "<key>apiKey</key><string>sk-hidden</string>"
            + "<key>secretToken</key><string>hidden</string>"
            + "<key>macDataOnly</key><data>aGVsbG8=</data>"
            + "</dict></plist>";
        var applied = MacSettingsPlist.Apply(System.Text.Encoding.UTF8.GetBytes(plist), new AppSettings());
        Assert.Equal("m", applied.Model);
        Assert.NotNull(applied.AdditionalProperties);
        Assert.False(applied.AdditionalProperties!.ContainsKey("apiKey"));
        Assert.False(applied.AdditionalProperties!.ContainsKey("secretToken"));
        // A non-JSON <data> Mac-only key is preserved as data, not flattened to a string.
        Assert.True(applied.AdditionalProperties!.TryGetValue("macDataOnly", out var data));
        Assert.True(data.TryGetProperty("$data", out var encoded));
        Assert.Equal("aGVsbG8=", encoded.GetString());
        var exported = System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(applied));
        Assert.DoesNotContain("sk-hidden", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", exported, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<data>aGVsbG8=</data>", exported, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSearchModeAndMacOnlyShortcutsKeepTheirMeaning()
    {
        // A12: the Mac values are off / auto / always. Windows keeps them as a bool (WebSearch) plus
        // the Mac-style string (WebSearchMode); the mapping is one-way today (see compare.md).
        var off = MacSettingsPlist.Apply(System.Text.Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict>"
            + "<key>webSearchMode</key><string>off</string>"
            + "<key>customShortcuts</key><dict><key>answer</key><string>Ctrl+Alt+Enter</string></dict>"
            + "</dict></plist>"), new AppSettings());
        Assert.False(off.WebSearch);
        // The Mac-only shortcut map is preserved raw and exported again.
        Assert.NotNull(off.AdditionalProperties);
        Assert.True(off.AdditionalProperties!.TryGetValue("customShortcuts", out _));
        var exported = System.Text.Encoding.UTF8.GetString(MacSettingsPlist.Write(off));
        Assert.Contains("customShortcuts", exported, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+Enter", exported, StringComparison.Ordinal);
        Assert.Contains("webSearchMode", exported, StringComparison.Ordinal);

        var auto = MacSettingsPlist.Apply(System.Text.Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict>"
            + "<key>webSearchMode</key><string>always</string>"
            + "</dict></plist>"), new AppSettings());
        Assert.True(auto.WebSearch);
    }
}
