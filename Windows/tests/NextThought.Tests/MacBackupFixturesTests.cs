using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// A16: the Mac 3.4.0 backup fixtures are imported, compared field by field, re-exported and read
/// back on Windows, and the Mac recovery points are listed/restored with the E30/A14 semantics.
/// The zip is the one the Mac app produced (see tests/Fixtures/mac-backup/README.md).
/// </summary>
public sealed class MacBackupFixturesTests
{
    private const string FixtureZipName = "nt-mac-fixtures-26.5.2-20261121.zip";
    private const string FixtureZipHash = "afbc3eb281bc2a9a5fc449a40709edc85c28a20f8fbe47f9dbd595710b4366d8";

    private static readonly string[] NtbackupHashes =
    [
        "89c60715cbf17225d51b3effa3ac133d2e2bb0c18369860fff2f2b23f6d2d507  01-text-only.ntbackup",
        "1261f7ba36468854bf31dd77a40f5fc049f2fa2058ce4c5e45917f613feaef86  02-voiceprints.ntbackup",
        "f7b902112dd294f7bca3213c0ae52bb5fbe0c3ddf0dbcd11c3dfb2c92fe72c74  03-glossary.ntbackup",
        "78637d64b3dcdaf77351ee45272b70c65fdf875cf9167b763a496593ace18f56  all-scenarios.ntbackup",
        "fc03b2bdda8fd3bd2d2dfa0ca2febccc8fc3e25c2505bd7137ff88af919bc8fe  04-incremental-chain-Backups.zip"
    ];

    private static string NewRoot(string tag = "a16") =>
        Path.Combine(Path.GetTempPath(), $"nt-{tag}-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Windows", "tests", "Fixtures", "mac-backup", "README.md")))
                return directory;
            directory = Path.GetDirectoryName(directory);
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string FixtureZip() =>
        Path.Combine(RepoRoot(), "Windows", "tests", "Fixtures", "mac-backup", FixtureZipName);

    private static readonly Lazy<string> Extracted = new(() =>
    {
        var destination = NewRoot("a16-fixture");
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
            .OrderBy(session => session.Item2.Title, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> FixtureTranscript(Guid id) =>
        File.ReadAllLines(FixtureData("Sessions", id.ToString().ToUpperInvariant(), "transcript.jsonl"))
            .Where(line => line.Trim().Length > 0).ToArray();

    private static string FixtureSummary(Guid id)
    {
        var path = FixtureData("Sessions", id.ToString().ToUpperInvariant(), "summary.json");
        if (!File.Exists(path)) return "";
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("markdown", out var markdown) ? markdown.GetString() ?? "" : "";
    }

    private static IReadOnlyList<JsonElement> FixtureWorkspaceDocuments()
    {
        var directory = FixtureData("Workspaces");
        return Directory.EnumerateFiles(directory, "*.json")
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.Clone())
            .ToList();
    }

    private static IReadOnlyList<string> FixtureGlossaryCanonicals()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixtureData("Glossary", "glossary.json")));
        return document.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("canonical").GetString() ?? "")
            .OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    // MARK: hashes -------------------------------------------------------------------

    [Fact]
    public void FixtureZipAndSumsMatchTheReadme()
    {
        Assert.Equal(FixtureZipHash, HashFile(FixtureZip()));
        var sums = File.ReadAllLines(Path.Combine(Fixtures, "ntbackup", "SHA256SUMS.txt"))
            .Where(line => line.Trim().Length > 0).ToArray();
        Assert.Equal(NtbackupHashes.Length, sums.Length);
        foreach (var expected in NtbackupHashes)
        {
            Assert.Contains(expected, sums);
            var name = expected.Split("  ", StringSplitOptions.RemoveEmptyEntries)[1];
            Assert.Equal(expected.Split("  ")[0], HashFile(Ntbackup(name)));
        }
    }

    // MARK: Mac full archive → Windows import ----------------------------------------

    [Fact]
    public async Task MacFullArchiveImportsEveryScenarioFieldByField()
    {
        var root = NewRoot();
        try
        {
            var paths = new AppPaths(root);
            var result = await new BackupService(paths).ImportAsync(Ntbackup("all-scenarios.ntbackup"));

            var expectedSessions = FixtureSessions();
            Assert.Equal(4, expectedSessions.Count);
            Assert.Equal(4, result.SessionsAdded);
            Assert.Equal(4, result.WorkspacesAdded);
            Assert.True(result.SettingsRestored);

            var store = new SessionStore(paths);
            var imported = store.List().OrderBy(meta => meta.Title, StringComparer.Ordinal).ToList();
            Assert.Equal(expectedSessions.Select(session => session.Meta.Title), imported.Select(meta => meta.Title));

            foreach (var (id, expected) in expectedSessions)
            {
                var data = store.Load(id);
                Assert.Equal(expected.Title, data.Meta.Title);
                Assert.Equal(expected.Mode, data.Meta.Mode);
                Assert.Equal(expected.VoiceNames ?? new Dictionary<string, string>(), data.Meta.VoiceNames ?? new Dictionary<string, string>());
                Assert.Equal(expected.MeVoices ?? [], data.Meta.MeVoices ?? []);
                var fixtureTexts = FixtureTranscript(id).Select(line =>
                {
                    using var document = JsonDocument.Parse(line);
                    return document.RootElement.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
                }).ToArray();
                Assert.Equal(fixtureTexts, data.Lines.Select(line => line.Text));
                Assert.Equal(FixtureSummary(id), data.Summary);
            }

            // Workspaces keep the Mac fields the UI does not expose yet.
            var workspaces = new WorkspaceStore(root).Load().Series;
            Assert.Equal(4, workspaces.Count);
            foreach (var expected in FixtureWorkspaceDocuments())
            {
                var id = expected.GetProperty("id").GetString()!;
                var series = Assert.Single(workspaces.Where(candidate => candidate.Id == id));
                Assert.Equal(expected.GetProperty("name").GetString(), series.Name);
                Assert.Equal(expected.GetProperty("preferences").GetString(), series.Preferences);
                Assert.Equal(expected.GetProperty("skillIDs").GetArrayLength(), series.SkillIds?.Count ?? 0);
                if (expected.TryGetProperty("glossary", out var glossary))
                {
                    var canonicals = glossary.GetProperty("entries").EnumerateArray()
                        .Select(entry => entry.GetProperty("canonical").GetString()).OrderBy(value => value, StringComparer.Ordinal).ToList();
                    Assert.Equal(canonicals, series.Glossary?.Entries.Select(entry => entry.Canonical).OrderBy(value => value, StringComparer.Ordinal).ToList() ?? []);
                }
            }

            // Global glossary, skills, templates.
            Assert.Equal(FixtureGlossaryCanonicals(),
                new GlossaryStore(root).Load().Entries.Select(entry => entry.Canonical).OrderBy(value => value, StringComparer.Ordinal).ToList());
            Assert.Equal(1, new SkillStore(root).Load().Count);
            Assert.Empty(new NotesTemplateStore(root).Load());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // MARK: each series export -------------------------------------------------------

    [Theory]
    [InlineData("01-text-only.ntbackup")]
    [InlineData("02-voiceprints.ntbackup")]
    [InlineData("03-glossary.ntbackup")]
    public async Task EachMacSeriesExportImportsItsSessionAndWorkspace(string archive)
    {
        var root = NewRoot("a16-workspace");
        try
        {
            var paths = new AppPaths(root);
            var result = await new BackupService(paths).ImportAsync(Ntbackup(archive));
            Assert.Equal(1, result.WorkspacesAdded);
            Assert.Equal(1, result.SessionsAdded);

            var workspaces = new WorkspaceStore(root).Load().Series;
            var workspace = Assert.Single(workspaces);
            var expectedWorkspace = FixtureWorkspaceDocuments()
                .Select(document => (Document: document, Id: document.GetProperty("id").GetString()!))
                .Single(entry => entry.Id == workspace.Id);
            Assert.Equal(expectedWorkspace.Document.GetProperty("name").GetString(), workspace.Name);

            var session = Assert.Single(new SessionStore(paths).List());
            var expected = Path.Combine(Fixtures, "data", "Sessions", session.Id.ToString().ToUpperInvariant());
            if (File.Exists(Path.Combine(expected, "meta.json")))
            {
                var meta = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(Path.Combine(expected, "meta.json")), ReadOptions)!;
                Assert.Equal(meta.Title, session.Title);
                Assert.Equal(meta.VoiceNames ?? new Dictionary<string, string>(), session.VoiceNames ?? new Dictionary<string, string>());
                Assert.Equal(meta.MeVoices ?? [], session.MeVoices ?? []);
            }
            if (archive.StartsWith("02", StringComparison.Ordinal))
                Assert.Contains(new SessionStore(paths).Load(session.Id).Lines, line => line.Voice is not null);
            if (archive.StartsWith("03", StringComparison.Ordinal))
                Assert.Contains(workspace.Glossary?.Entries ?? [], entry => entry.Canonical == "X-ASR");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // MARK: Windows export → read back -------------------------------------------------

    [Fact]
    public async Task WindowsExportStaysMacReadableAndReimportsFieldByField()
    {
        var source = NewRoot("a16-src");
        var copy = NewRoot("a16-dst");
        var export = Path.Combine(NewRoot("a16-export"), "windows-export.ntbackup");
        Directory.CreateDirectory(Path.GetDirectoryName(export)!);
        try
        {
            var sourcePaths = new AppPaths(source);
            await new BackupService(sourcePaths).ImportAsync(Ntbackup("all-scenarios.ntbackup"));
            await new BackupService(sourcePaths).ExportAsync(export);

            // The Mac import (DataBackup.prepareImport → locateRoot) needs a manifest at the root or
            // one directory down, plus these files where applyImport reads them.
            using (var zip = ZipFile.OpenRead(export))
            {
                var names = zip.Entries.Select(entry => entry.FullName).ToArray();
                Assert.Contains("manifest.json", names);
                Assert.Contains("settings.plist", names);
                Assert.Contains("glossary.json", names);
                Assert.Contains("skills.json", names);
                Assert.Contains("note-templates.json", names);
                Assert.DoesNotContain(names, name => name.Contains("__MACOSX", StringComparison.Ordinal));
                Assert.Equal(4, names.Count(name => name.StartsWith("Sessions/", StringComparison.Ordinal) && name.EndsWith("/meta.json", StringComparison.Ordinal)));
                Assert.Equal(4, names.Count(name => name.StartsWith("Workspaces/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)));

                using var manifest = JsonDocument.Parse(ReadEntry(zip, "manifest.json"));
                Assert.Equal("nextthought-backup", manifest.RootElement.GetProperty("format").GetString());
                Assert.Equal(1, manifest.RootElement.GetProperty("version").GetInt32());
                Assert.Equal(4, manifest.RootElement.GetProperty("sessionCount").GetInt32());
                Assert.Equal(4, manifest.RootElement.GetProperty("workspaceCount").GetInt32());
                Assert.False(manifest.RootElement.GetProperty("includesSecrets").GetBoolean());
            }

            var copyPaths = new AppPaths(copy);
            var readBack = await new BackupService(copyPaths).ImportAsync(export);
            Assert.Equal(4, readBack.SessionsAdded);
            Assert.Equal(4, readBack.WorkspacesAdded);

            var sourceStore = new SessionStore(sourcePaths);
            var copyStore = new SessionStore(copyPaths);
            foreach (var meta in sourceStore.List())
            {
                var before = sourceStore.Load(meta.Id);
                var after = copyStore.Load(meta.Id);
                Assert.Equal(before.Meta.Title, after.Meta.Title);
                Assert.Equal(before.Meta.VoiceNames ?? new Dictionary<string, string>(), after.Meta.VoiceNames ?? new Dictionary<string, string>());
                Assert.Equal(before.Meta.MeVoices ?? [], after.Meta.MeVoices ?? []);
                Assert.Equal(before.Summary, after.Summary);
                Assert.Equal(before.Lines.Select(line => (line.Text, line.Speaker, line.Voice)),
                    after.Lines.Select(line => (line.Text, line.Speaker, line.Voice)));
            }
            var sourceSeries = new WorkspaceStore(source).Load().Series.OrderBy(series => series.Id).ToList();
            var copySeries = new WorkspaceStore(copy).Load().Series.OrderBy(series => series.Id).ToList();
            Assert.Equal(sourceSeries.Select(series => (series.Id, series.Name, series.Preferences)),
                copySeries.Select(series => (series.Id, series.Name, series.Preferences)));
            Assert.Equal(new GlossaryStore(source).Load().Entries.Select(entry => entry.Canonical).OrderBy(value => value, StringComparer.Ordinal),
                new GlossaryStore(copy).Load().Entries.Select(entry => entry.Canonical).OrderBy(value => value, StringComparer.Ordinal));
        }
        finally
        {
            foreach (var root in new[] { source, copy })
                try { Directory.Delete(root, true); } catch { }
        }
    }

    // MARK: Mac recovery points → Windows E30/A14 -------------------------------------

    [Fact]
    public async Task MacRecoveryPointsListRestoreAndKeepTheE30AndA14Semantics()
    {
        var source = NewRoot("a16-points");
        var restoreRoot = NewRoot("a16-restore");
        var destination = Path.Combine(restoreRoot, "restored");
        Directory.CreateDirectory(destination);
        try
        {
            CopyDirectory(FixtureData("Backups"), Path.Combine(source, "Backups"));
            var paths = new AppPaths(source);
            var service = new BackupService(paths);
            var points = service.ListBackups().Where(point => point.IsRecoveryPoint).ToList();
            Assert.Equal(4, points.Count);
            Assert.Equal(new[] { "session", "夹具 A", "夹具 B", "夹具 C" },
                points.Select(point => point.Reason).OrderBy(reason => reason, StringComparer.Ordinal).ToArray());
            Assert.True(points.Zip(points.Skip(1)).All(pair => pair.First.Date >= pair.Second.Date));

            var fixtureAPoint = points.Single(point => point.Reason == "夹具 A");
            var store = new IncrementalBackupStore(Path.Combine(paths.Root, "Backups"));
            store.Restore(fixtureAPoint.Path, destination);
            Assert.False(Directory.Exists(Path.Combine(destination, "Sessions", "essions")));
            Assert.True(Directory.Exists(Path.Combine(destination, "Sessions")));
            Assert.True(File.Exists(Path.Combine(destination, "manifest.json")));
            Assert.All(Directory.EnumerateDirectories(Path.Combine(destination, "Sessions")),
                folder => Assert.True(Guid.TryParse(Path.GetFileName(folder), out _)));

            // The Mac point imports on Windows when the whole Backups/ directory was moved over
            // (the store only restores points it owns, exactly like the Mac).
            var result = await service.ImportAsync(fixtureAPoint.Path);
            Assert.True(result.SessionsAdded >= 1 || result.WorkspacesAdded >= 1);

            // E30: deleting one point keeps every object the remaining points reference.
            var objectHashes = points.Select(point => ObjectHashes(point.Path)).ToList();
            var pointIndex = points.FindIndex(point => point.Reason == "夹具 A");
            var remaining = objectHashes.Where((_, index) => index != pointIndex).SelectMany(set => set).ToHashSet(StringComparer.Ordinal);
            var deletedOnly = objectHashes[pointIndex].Except(remaining).ToList();
            // A14: an unreadable manifest still owns its objects, so pruning keeps them.
            string? protectedObject = null;
            if (deletedOnly.Count > 0)
            {
                protectedObject = Path.Combine(paths.Root, "Backups", "Objects", deletedOnly[0]);
                Assert.True(File.Exists(protectedObject));
                File.WriteAllText(Path.Combine(paths.Root, "Backups", "RestorePoints", "corrupt.ntrestore"),
                    "unreadable manifest that still owns " + deletedOnly[0]);
            }
            var pointsBeforeDelete = service.ListBackups().Count(point => point.IsRecoveryPoint);
            service.DeleteRecoveryPoint(fixtureAPoint);
            Assert.Equal(pointsBeforeDelete - 1, service.ListBackups().Count(point => point.IsRecoveryPoint));
            Assert.All(remaining, hash => Assert.True(File.Exists(Path.Combine(paths.Root, "Backups", "Objects", hash)), hash));
            if (protectedObject is not null) Assert.True(File.Exists(protectedObject));
        }
        finally
        {
            foreach (var root in new[] { source, restoreRoot })
                try { Directory.Delete(root, true); } catch { }
        }
    }

    private static HashSet<string> ObjectHashes(string pointPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(pointPath));
        return document.RootElement.GetProperty("files").EnumerateObject()
            .Select(property => property.Value.GetString() ?? "")
            .Where(hash => hash.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var folder in Directory.EnumerateDirectories(source))
            CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }
    private static string ReadEntry(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}
