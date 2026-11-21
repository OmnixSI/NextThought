using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
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

    // MARK: Windows export (generated after the §4.3/4.4 serialization fixes land) -------

    [Fact(Skip = "win-export fixture is generated and committed once the serialization fixes (runtime-gaps §4.3/4.4) land")]
    public void WindowsExportMatchesTheMacArchiveFileByFile()
    {
        // Implemented together with the committed fixture: compare every non-AppleDouble entry of
        // all-scenarios.ntbackup with all-scenarios-from-windows.ntbackup, requiring byte equality,
        // then JSON key-order/whitespace equality, and zero substantive differences.
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
