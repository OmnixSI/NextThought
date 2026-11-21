using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §4.5 A16: the four real Mac 3.4.0 backups are imported through the production import path and
/// checked against tests/Fixtures/mac-backup/README.md: counts per archive, the Mac wrapper
/// (NextThought-Backup/ plus __MACOSX/._* AppleDouble entries), the base64 apiPresets in
/// settings.plist, and the four restore points of the incremental chain restored one by one with
/// a growing meeting count.
/// </summary>
public sealed class MacBackupParityTests
{
    private const string FixtureZipName = "nt-mac-fixtures-26.5.2-20261121.zip";
    private const string FixtureZipHash = "afbc3eb281bc2a9a5fc449a40709edc85c28a20f8fbe47f9dbd595710b4366d8";
    private const string Wrapper = "NextThought-Backup/";

    private static readonly string[] NtbackupHashes =
    [
        "89c60715cbf17225d51b3effa3ac133d2e2bb0c18369860fff2f2b23f6d2d507  01-text-only.ntbackup",
        "1261f7ba36468854bf31dd77a40f5fc049f2fa2058ce4c5e45917f613feaef86  02-voiceprints.ntbackup",
        "f7b902112dd294f7bca3213c0ae52bb5fbe0c3ddf0dbcd11c3dfb2c92fe72c74  03-glossary.ntbackup",
        "78637d64b3dcdaf77351ee45272b70c65fdf875cf9167b763a496593ace18f56  all-scenarios.ntbackup",
        "fc03b2bdda8fd3bd2d2dfa0ca2febccc8fc3e25c2505bd7137ff88af919bc8fe  04-incremental-chain-Backups.zip"
    ];

    private static string NewRoot(string tag) =>
        Path.Combine(Path.GetTempPath(), $"nt-a16-{tag}-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "tests", "Fixtures", "mac-backup", "README.md")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string FixtureZip() =>
        Path.Combine(RepoRoot(), "Windows", "tests", "Fixtures", "mac-backup", FixtureZipName);

    private static readonly Lazy<string> Extracted = new(() =>
    {
        var destination = NewRoot("fixture");
        Directory.CreateDirectory(destination);
        ZipFile.ExtractToDirectory(FixtureZip(), destination);
        return Path.Combine(destination, "nt-fixtures");
    });

    private static string Fixtures => Extracted.Value;

    private static string Ntbackup(string name) => Path.Combine(Fixtures, "ntbackup", name);

    private static string FixtureData(params string[] parts) =>
        Path.Combine([Fixtures, "data", .. parts]);

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static readonly JsonSerializerOptions ReadOptions = new(JsonDefaults.Options)
    {
        PropertyNameCaseInsensitive = true
    };

    private static SessionMeta FixtureMeta(Guid id) =>
        JsonSerializer.Deserialize<SessionMeta>(
            File.ReadAllText(FixtureData("Sessions", id.ToString().ToUpperInvariant(), "meta.json")), ReadOptions)!;

    private static IReadOnlyList<(Guid Id, SessionMeta Meta)> FixtureSessions() =>
        Directory.EnumerateDirectories(FixtureData("Sessions"))
            .Select(folder => (Guid.Parse(Path.GetFileName(folder)),
                JsonSerializer.Deserialize<SessionMeta>(
                    File.ReadAllText(Path.Combine(folder, "meta.json")), ReadOptions)!))
            .OrderBy(entry => entry.Item2.Title, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> FixtureTranscriptTexts(Guid id) =>
        File.ReadAllLines(FixtureData("Sessions", id.ToString().ToUpperInvariant(), "transcript.jsonl"))
            .Where(line => line.Trim().Length > 0)
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                return document.RootElement.GetProperty("text").GetString() ?? "";
            })
            .ToArray();

    private static JsonElement FixtureWorkspace(Guid id) =>
        JsonDocument.Parse(File.ReadAllText(FixtureData("Workspaces", id.ToString().ToUpperInvariant() + ".json"))).RootElement;

    private static string ReadEntry(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    private static BackupService Service(string root) => new(new AppPaths(root));

    private static void Delete(params string[] roots)
    {
        foreach (var root in roots)
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
    }

    // MARK: hashes and the Mac wrapper -------------------------------------------------

    [Fact]
    public void TheFixtureZipAndEveryBackupMatchTheReadme()
    {
        Assert.Equal(FixtureZipHash, HashFile(FixtureZip()));
        var sums = File.ReadAllLines(Path.Combine(Fixtures, "ntbackup", "SHA256SUMS.txt"))
            .Where(line => line.Trim().Length > 0).ToArray();
        Assert.Equal(NtbackupHashes.Length, sums.Length);
        foreach (var expected in NtbackupHashes)
        {
            Assert.Contains(expected, sums);
            var parts = expected.Split("  ", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(parts[0], HashFile(Ntbackup(parts[1])));
        }
    }

    [Theory]
    [InlineData("all-scenarios.ntbackup")]
    [InlineData("01-text-only.ntbackup")]
    [InlineData("02-voiceprints.ntbackup")]
    [InlineData("03-glossary.ntbackup")]
    public void EveryArchiveKeepsTheMacWrapperAndAppleDoubleEntries(string archive)
    {
        using var zip = ZipFile.OpenRead(Ntbackup(archive));
        var names = zip.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains(names, name => name == Wrapper);
        Assert.Contains(names, name => name.StartsWith(Wrapper + "manifest.json", StringComparison.Ordinal));
        Assert.Contains(names, name => name.StartsWith("__MACOSX/", StringComparison.Ordinal));
        Assert.Contains(names, name => name.Split('/').Any(segment => segment.StartsWith("._", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ImportingTheMacWrapperLeavesNoAppleDoubleFilesBehind()
    {
        var root = NewRoot("wrapper");
        try
        {
            var result = await Service(root).ImportAsync(Ntbackup("all-scenarios.ntbackup"));
            Assert.Equal(4, result.SessionsAdded);
            Assert.Equal(4, result.WorkspacesAdded);

            var leftovers = Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal)
                               || path.Split(Path.DirectorySeparatorChar).Contains("__MACOSX"))
                .ToArray();
            Assert.Empty(leftovers);
            // The backup wrapper itself must not leak into the data directory.
            Assert.False(Directory.Exists(Path.Combine(root, "NextThought-Backup")));
        }
        finally { Delete(root); }
    }

    // MARK: all-scenarios ----------------------------------------------------------------

    [Fact]
    public async Task AllScenariosImportsTheReadmeCountsAndContent()
    {
        var root = NewRoot("full");
        try
        {
            var paths = new AppPaths(root);
            var result = await new BackupService(paths).ImportAsync(Ntbackup("all-scenarios.ntbackup"));
            Assert.Equal(4, result.SessionsAdded);
            Assert.Equal(4, result.WorkspacesAdded);
            Assert.True(result.SettingsRestored);

            // 4 meetings, field by field against the fixture sessions.
            var expectedSessions = FixtureSessions();
            Assert.Equal(4, expectedSessions.Count);
            var store = new SessionStore(paths);
            var imported = store.List().OrderBy(meta => meta.Title, StringComparer.Ordinal).ToList();
            Assert.Equal(expectedSessions.Select(entry => entry.Meta.Title), imported.Select(meta => meta.Title));
            foreach (var (id, expected) in expectedSessions)
            {
                var data = store.Load(id);
                Assert.Equal(expected.Title, data.Meta.Title);
                Assert.Equal(expected.Mode, data.Meta.Mode);
                // The fixture meta may omit transcriptCount; the transcript file is the truth.
                Assert.Equal(FixtureTranscriptTexts(id).Count, data.Lines.Count);
                Assert.Equal(FixtureTranscriptTexts(id), data.Lines.Select(line => line.Text));
                Assert.Equal(expected.VoiceNames ?? new Dictionary<string, string>(),
                    data.Meta.VoiceNames ?? new Dictionary<string, string>());
            }

            // 4 series with their Mac fields.
            var workspaces = new WorkspaceStore(root).Load().Series;
            Assert.Equal(4, workspaces.Count);
            foreach (var (id, meta) in expectedSessions)
            {
                if (string.IsNullOrEmpty(meta.WorkspaceId) || !Guid.TryParse(meta.WorkspaceId, out var workspaceId)) continue;
                var fixture = FixtureWorkspace(workspaceId);
                var series = Assert.Single(workspaces.Where(candidate => candidate.Id == workspaceId.ToString("D").ToUpperInvariant()));
                Assert.Equal(fixture.GetProperty("name").GetString(), series.Name);
                Assert.Equal(fixture.GetProperty("mode").GetString(), series.Mode);
                Assert.Equal(fixture.TryGetProperty("skillIDs", out var skillIds) ? skillIds.GetArrayLength() : 0,
                    series.SkillIds?.Count ?? 0);
            }

            // Global glossary (only the full export carries it), skills and the templates file.
            var expectedTerms = File.ReadAllText(FixtureData("Glossary", "glossary.json"));
            using var glossary = JsonDocument.Parse(expectedTerms);
            var canonicals = glossary.RootElement.GetProperty("entries").EnumerateArray()
                .Select(entry => entry.GetProperty("canonical").GetString() ?? "")
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
            Assert.Equal(canonicals,
                new GlossaryStore(root).Load().Entries.Select(entry => entry.Canonical)
                    .OrderBy(value => value, StringComparer.Ordinal).ToList());
            var skills = new SkillStore(root).Load();
            Assert.Single(skills);
            Assert.Equal("夹具 · 先给日期", skills[0].Name);
            // The template file is part of the full export; the fixture happens to have no user templates.
            using (var zip = ZipFile.OpenRead(Ntbackup("all-scenarios.ntbackup")))
                Assert.Contains(zip.Entries, entry => entry.FullName == Wrapper + "note-templates.json");
            Assert.Empty(new NotesTemplateStore(root).Load());

            // settings.plist: apiPresets is base64 JSON and must restore the OpenAI preset.
            var settings = new SettingsStore(paths).Load();
            var preset = Assert.Single(settings.ApiPresets);
            Assert.Equal("OpenAI", preset.Name);
            Assert.Equal("https://api.openai.com/v1", preset.BaseUrl);
            Assert.Equal("answer.api", preset.KeyAccount);
            Assert.Equal("846ADF47-227C-465A-A122-9769BA4C4E09", preset.Id.ToUpperInvariant());
        }
        finally { Delete(root); }
    }

    // MARK: the three series exports ----------------------------------------------------

    [Fact]
    public async Task TextOnlyBackupHasTwoLinesSummaryAndPersonalNotesButNoTranslationOrVoiceprints()
    {
        var root = NewRoot("text");
        try
        {
            var paths = new AppPaths(root);
            var result = await new BackupService(paths).ImportAsync(Ntbackup("01-text-only.ntbackup"));
            Assert.Equal(1, result.SessionsAdded);
            Assert.Equal(1, result.WorkspacesAdded);

            var meta = Assert.Single(new SessionStore(paths).List());
            var data = new SessionStore(paths).Load(meta.Id);
            Assert.Equal(2, data.Lines.Count);
            Assert.Contains("基线已复现", data.Summary);
            Assert.Equal("记得补消融实验的表格。",
                SessionUserContentStore.Load(new SessionStore(paths).Folder(meta.Id)).PersonalNotes);
            Assert.DoesNotContain(data.Lines, line => !string.IsNullOrEmpty(line.TranslatedText));
            Assert.True(data.Meta.VoiceNames is null || data.Meta.VoiceNames.Count == 0);
            Assert.DoesNotContain(data.Lines, line => line.Voice is not null);
            using var zip = ZipFile.OpenRead(Ntbackup("01-text-only.ntbackup"));
            Assert.DoesNotContain(zip.Entries, entry => entry.FullName.EndsWith("translations.jsonl", StringComparison.Ordinal));
            Assert.DoesNotContain(zip.Entries, entry => entry.FullName.EndsWith("voices.jsonl", StringComparison.Ordinal));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task VoiceprintBackupHasThreeSpeakersAndVoiceOneIsLiGong()
    {
        var root = NewRoot("voices");
        try
        {
            var paths = new AppPaths(root);
            var result = await new BackupService(paths).ImportAsync(Ntbackup("02-voiceprints.ntbackup"));
            Assert.Equal(1, result.SessionsAdded);
            Assert.Equal(1, result.WorkspacesAdded);

            var store = new SessionStore(paths);
            var meta = Assert.Single(store.List());
            var data = store.Load(meta.Id);
            Assert.Equal("李工", data.Meta.VoiceNames?["1"]);
            Assert.Equal(0, data.Meta.MeVoices?.Count ?? 0);
            var voices = data.Lines.Where(line => line.Voice is not null).Select(line => line.Voice!.Value).ToHashSet();
            Assert.Equal([1, 2], voices.OrderBy(value => value).ToArray());
            // 3 speakers: me plus voice 1 (李工) and voice 2.
            Assert.Contains(data.Lines, line => line.Speaker == "me");
            Assert.Equal(3, voices.Count + 1);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task GlossaryBackupCarriesTheSeriesGlossaryAndMountedSkill()
    {
        var root = NewRoot("glossary");
        try
        {
            var paths = new AppPaths(root);
            var result = await new BackupService(paths).ImportAsync(Ntbackup("03-glossary.ntbackup"));
            Assert.Equal(1, result.SessionsAdded);
            Assert.Equal(1, result.WorkspacesAdded);

            var series = Assert.Single(new WorkspaceStore(root).Load().Series);
            Assert.Contains(series.Glossary?.Entries ?? [], entry => entry.Canonical == "X-ASR");
            Assert.Contains("C4911013-369A-4F78-9BC4-79B5344CD37C",
                (series.SkillIds ?? []).Select(id => id.ToUpperInvariant()));
            var skill = Assert.Single(new SkillStore(root).Load());
            Assert.Equal("夹具 · 先给日期", skill.Name);
            // A workspace export never carries the global glossary.
            Assert.Empty(new GlossaryStore(root).Load().Entries);
        }
        finally { Delete(root); }
    }

    // MARK: settings.plist apiPresets ------------------------------------------------

    [Fact]
    public async Task MacSettingsPlistApiPresetsAreBase64Json()
    {
        using var zip = ZipFile.OpenRead(Ntbackup("all-scenarios.ntbackup"));
        var plist = zip.GetEntry(Wrapper + "settings.plist")!.Open().ReadAllBytes();
        var settings = MacSettingsPlist.Apply(plist, new AppSettings());
        var preset = Assert.Single(settings.ApiPresets);
        Assert.Equal("OpenAI", preset.Name);
        Assert.Equal("https://api.openai.com/v1", preset.BaseUrl);
        Assert.Equal("", preset.Model);
        Assert.Equal("answer.api", preset.KeyAccount);
        Assert.Equal("default", preset.Effort);
        Assert.Equal("846ADF47-227C-465A-A122-9769BA4C4E09", preset.Id.ToUpperInvariant());
        Assert.Equal("zipformer", string.IsNullOrEmpty(settings.CloudStt) ? "zipformer" : "cloud");

        // And an archive with the plist restores it through the real import path.
        var root = NewRoot("plist");
        try
        {
            _ = await Service(root).ImportAsync(Ntbackup("all-scenarios.ntbackup"));
            var imported = new SettingsStore(new AppPaths(root)).Load();
            Assert.Equal(preset, Assert.Single(imported.ApiPresets));
        }
        finally { Delete(root); }
    }

    // MARK: incremental chain ---------------------------------------------------------

    [Fact]
    public void IncrementalChainRestoresAllFourPointsWithGrowingMeetingCounts()
    {
        var extractedChain = NewRoot("chain");
        var destinationRoot = NewRoot("chain-out");
        try
        {
            Directory.CreateDirectory(extractedChain);
            ZipFile.ExtractToDirectory(Ntbackup("04-incremental-chain-Backups.zip"), extractedChain);
            var backups = Path.Combine(extractedChain, "Backups");
            var store = new IncrementalBackupStore(backups);

            // The Mac archive carries AppleDouble siblings; they are not restore points.
            var points = store.List().OrderBy(point => point.CreatedAt).ToList();
            Assert.Equal(4, points.Count);
            Assert.Equal(["session", "夹具 A", "夹具 B", "夹具 C"], points.Select(point => point.Reason).ToArray());

            var expectedCounts = new[] { 1, 2, 3, 4 };
            for (var index = 0; index < points.Count; index++)
            {
                var destination = Path.Combine(destinationRoot, index.ToString());
                store.Restore(points[index].Path, destination);
                var sessions = Path.Combine(destination, "Sessions");
                Assert.True(Directory.Exists(sessions));
                Assert.False(Directory.Exists(Path.Combine(destination, "Sessions", "essions")));
                var folders = Directory.GetDirectories(sessions);
                Assert.Equal(expectedCounts[index], folders.Length);
                foreach (var folder in folders)
                {
                    Assert.True(Guid.TryParse(Path.GetFileName(folder), out _));
                    Assert.True(File.Exists(Path.Combine(folder, "meta.json")));
                }
                Assert.True(File.Exists(Path.Combine(destination, "manifest.json")));
                using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "manifest.json")));
                Assert.Equal(expectedCounts[index], manifest.RootElement.GetProperty("sessionCount").GetInt32());
            }
        }
        finally { Delete(extractedChain, destinationRoot); }
    }

    // MARK: Windows export --------------------------------------------------------------

    private const string WinExportSha256 = "6e8d961fe962d76cbab1417f1bd3d42364752acac0c357ff05ff704c3340b92c";

    private static string WinExportFixture() =>
        Path.Combine(RepoRoot(), "Windows", "tests", "Fixtures", "win-export", "all-scenarios-from-windows.ntbackup");

    /// <summary>Files of a backup ZIP with the Mac wrapper/AppleDouble noise removed.</summary>
    private static Dictionary<string, byte[]> ArchiveFiles(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (name.EndsWith("/", StringComparison.Ordinal)) continue;
            if (name.StartsWith("__MACOSX/", StringComparison.Ordinal)) continue;
            if (name.Split('/').Any(segment => segment.StartsWith("._", StringComparison.Ordinal))) continue;
            var logical = name.StartsWith(Wrapper, StringComparison.Ordinal) ? name[Wrapper.Length..] : name;
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            files[logical] = memory.ToArray();
        }
        return files;
    }

    private static JsonNode? Sorted(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, Sorted(pair.Value)))),
        JsonArray array => new JsonArray(array.Select(Sorted).ToArray()),
        _ => node?.DeepClone()
    };

    private static string CanonicalJson(byte[] bytes) => Sorted(JsonNode.Parse(bytes)!)!.ToJsonString();

    private static string CanonicalJsonLines(byte[] bytes) => string.Join("\n",
        Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Split('\n')
            .Where(line => line.Trim().Length > 0)
            .Select(line => CanonicalJson(Encoding.UTF8.GetBytes(line))));

    private static Dictionary<string, XElement> PlistPairs(byte[] plist)
    {
        var document = XDocument.Parse(Encoding.UTF8.GetString(plist));
        var elements = document.Root!.Element("dict")!.Elements().ToList();
        var pairs = new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < elements.Count; index += 2)
            if (elements[index].Name == "key") pairs[elements[index].Value] = elements[index + 1];
        return pairs;
    }

    private static string PlistValue(XElement element) => element.ToString(SaveOptions.DisableFormatting);

    private static byte[] PlistData(XElement element) =>
        Convert.FromBase64String(string.Concat(element.Value.Where(character => !char.IsWhiteSpace(character))));

    /// <summary>
    /// §4.5.5: the committed Windows export must hold the same data as the Mac all-scenarios backup.
    /// Bytes are compared first; JSON/JSONL files may only differ by key order or whitespace; the
    /// manifest and settings.plist may only differ in export metadata/format (documented in
    /// Fixtures/win-export/README.md); there must be no substantive differences and no missing file.
    /// </summary>
    [Fact]
    public void WindowsExportMatchesTheMacArchiveFileByFile()
    {
        var fixture = WinExportFixture();
        Assert.True(File.Exists(fixture), "tests/Fixtures/win-export/all-scenarios-from-windows.ntbackup is missing");
        Assert.Equal(WinExportSha256, HashFile(fixture));

        var original = ArchiveFiles(Ntbackup("all-scenarios.ntbackup"));
        var export = ArchiveFiles(fixture);
        var missing = original.Keys.Except(export.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.Empty(missing);
        var extras = export.Keys.Except(original.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
        Assert.Equal(
        [
            "Glossary/glossary.json",
            "Migration/78637d64b3dcdaf77351ee45272b70c65fdf875cf9167b763a496593ace18f56.ntbackup",
            "settings.json"
        ], extras);

        var exact = new List<string>();
        var equivalent = new List<string>();
        var metadata = new List<string>();
        var substantive = new List<string>();
        foreach (var name in original.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            var before = original[name];
            var after = export[name];
            if (before.AsSpan().SequenceEqual(after)) { exact.Add(name); continue; }
            if (name == "manifest.json") { metadata.Add(name); continue; }
            if (name.EndsWith(".jsonl", StringComparison.Ordinal))
            {
                if (CanonicalJsonLines(before) == CanonicalJsonLines(after)) equivalent.Add(name); else substantive.Add(name);
                continue;
            }
            if (name.EndsWith(".json", StringComparison.Ordinal))
            {
                if (CanonicalJson(before) == CanonicalJson(after)) equivalent.Add(name); else substantive.Add(name);
                continue;
            }
            if (name == "settings.plist")
            {
                var originalPairs = PlistPairs(before);
                var exportPairs = PlistPairs(after);
                foreach (var (key, value) in originalPairs)
                {
                    Assert.True(exportPairs.ContainsKey(key), "settings.plist lost the Mac key " + key);
                    if (key == "apiPresets")
                        Assert.Equal(CanonicalJson(PlistData(value)), CanonicalJson(PlistData(exportPairs[key])));
                    else
                        Assert.Equal(PlistValue(value), PlistValue(exportPairs[key]));
                }
                Assert.True(exportPairs.Count > originalPairs.Count);
                metadata.Add(name);
                continue;
            }
            substantive.Add(name);
        }

        Assert.Empty(substantive);
        Assert.Equal(21, exact.Count);
        Assert.Equal(7, equivalent.Count);
        Assert.Equal(["manifest.json", "settings.plist"], metadata.OrderBy(name => name, StringComparer.Ordinal).ToArray());

        // The two files that differ in shape only differ in export metadata/format.
        using (var before = JsonDocument.Parse(original["manifest.json"]))
        using (var after = JsonDocument.Parse(export["manifest.json"]))
        {
            foreach (var property in new[] { "format", "version", "kind", "includesSecrets", "workspaceCount", "sessionCount" })
                Assert.Equal(before.RootElement.GetProperty(property).GetRawText(), after.RootElement.GetProperty(property).GetRawText());
            Assert.Equal(1, after.RootElement.GetProperty("windowsExtensionVersion").GetInt32());
            Assert.True(after.RootElement.TryGetProperty("fileHashes", out _));
            Assert.NotEqual("0.2.0", after.RootElement.GetProperty("appVersion").GetString());
            Assert.Equal("windows", after.RootElement.GetProperty("appBuild").GetString());
        }
    }
}

internal static class ZipEntryStreamExtensions
{
    public static byte[] ReadAllBytes(this Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
