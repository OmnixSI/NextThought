using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// A11 §4.1: read every Mac fixture and every file inside the four real .ntbackup archives through
/// the Windows types and the same serialization options the stores use, write them again and compare
/// key by key. Four things must hold: no key lost, no key added, no value changed, and UUID strings
/// keep the Mac's upper-case spelling. AppleDouble entries (__MACOSX/, ._*) are skipped.
/// </summary>
public sealed class MacFixtureRoundTripTests : IDisposable
{
    private static readonly JsonSerializerOptions Archive = JsonDefaults.ArchiveOptions;
    private static readonly JsonSerializerOptions Read = JsonDefaults.Options;

    private readonly string backups;
    private readonly List<string> issues = [];
    private readonly List<string> unknown = [];

    public MacFixtureRoundTripTests()
    {
        backups = Path.Combine(Path.GetTempPath(), "nt-a11-roundtrip-" + Guid.NewGuid().ToString("N"));
        if (BackupZip is { } zip && File.Exists(zip))
        {
            var outer = Path.Combine(backups, "outer");
            Directory.CreateDirectory(outer);
            ZipFile.ExtractToDirectory(zip, outer);
            var source = Path.Combine(outer, "nt-fixtures", "ntbackup");
            if (Directory.Exists(source))
                foreach (var archive in Directory.EnumerateFiles(source, "*.ntbackup").OrderBy(name => name, StringComparer.Ordinal))
                    Extract(archive, Path.Combine(backups, Path.GetFileNameWithoutExtension(archive)));
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(backups, recursive: true); } catch { }
    }

    private static string? BackupZip
    {
        get
        {
            var fixtures = Path.GetDirectoryName(MacFixtures.Directory);
            if (fixtures is null) return null;
            var folder = Path.Combine(fixtures, "mac-backup");
            if (!Directory.Exists(folder)) return null;
            return Directory.EnumerateFiles(folder, "*.zip").OrderBy(name => name, StringComparer.Ordinal).FirstOrDefault();
        }
    }

    private static void Extract(string archive, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue;
            if (entry.FullName.StartsWith("__MACOSX/", StringComparison.Ordinal)) continue;
            if (Path.GetFileName(entry.FullName).StartsWith("._", StringComparison.Ordinal)) continue;
            var target = Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    // ---- fixture and extracted-backup discovery -------------------------------------------------

    private IEnumerable<(string Path, string Label)> Files(string name, Func<string, bool>? accept = null)
    {
        foreach (var directory in new[] { MacFixtures.Directory }.Concat(ArchiveDirectories()))
        {
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory, name, SearchOption.AllDirectories)
                         .Where(path => !path.Contains("__MACOSX") && !Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal))
                         .Where(path => accept is null || accept(path))
                         .OrderBy(path => path, StringComparer.Ordinal))
                yield return (file, Relative(file));
        }
    }

    private static string Relative(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        foreach (var (root, prefix) in new[]
                 {
                     (MacFixtures.Directory, "mac"),
                     (Path.Combine(Path.GetTempPath(), "nt-a11-roundtrip-"), "backup")
                 })
        {
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = path[root.Length..].TrimStart(separator).Replace(separator, '/');
            return prefix + (prefix == "backup" ? rest[(rest.IndexOf('/') + 1)..] : rest);
        }
        return Path.GetFileName(path);
    }

    // ---- comparison ----------------------------------------------------------------------------

    private void Compare(string label, string originalJson, string rewrittenJson)
    {
        using var expected = JsonDocument.Parse(originalJson);
        using var actual = JsonDocument.Parse(rewrittenJson);
        Compare(label, "$", expected.RootElement, actual.RootElement);
    }

    private void Compare(string label, string path, JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            issues.Add($"{label} {path}: kind {expected.ValueKind} became {actual.ValueKind}");
            return;
        }
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedKeys = expected.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
                var actualKeys = actual.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var key in expectedKeys.Except(actualKeys).OrderBy(name => name, StringComparer.Ordinal))
                    issues.Add($"{label} {path}: key lost: {key}");
                foreach (var key in actualKeys.Except(expectedKeys).OrderBy(name => name, StringComparer.Ordinal))
                    issues.Add($"{label} {path}: key added: {key}");
                foreach (var property in expected.EnumerateObject())
                    if (actual.TryGetProperty(property.Name, out var value))
                        Compare(label, path + "." + property.Name, property.Value, value);
                break;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                if (expectedItems.Count != actualItems.Count)
                {
                    issues.Add($"{label} {path}: {expectedItems.Count} items became {actualItems.Count}");
                    return;
                }
                for (var index = 0; index < expectedItems.Count; index++)
                    Compare(label, $"{path}[{index}]", expectedItems[index], actualItems[index]);
                break;
            case JsonValueKind.String:
                var before = expected.GetString() ?? "";
                var after = actual.GetString() ?? "";
                if (!string.Equals(before, after, StringComparison.Ordinal))
                    issues.Add($"{label} {path}: value changed ({before} -> {after})");
                break;
            case JsonValueKind.Number:
                if (expected.GetDouble() != actual.GetDouble())
                    issues.Add($"{label} {path}: value changed ({expected.GetRawText()} -> {actual.GetRawText()})");
                break;
            default:
                break; // true / false / null: the kind comparison above is the whole value.
        }
    }

    // ---- round-trips through the real write paths ----------------------------------------------

    /// <summary>SessionStore writes meta as archive JSON minus the two Windows-only fields.</summary>
    private static string WriteMeta(SessionMeta meta)
    {
        var node = JsonSerializer.SerializeToNode(meta, Archive) as System.Text.Json.Nodes.JsonObject
                   ?? throw new InvalidOperationException("meta did not serialize");
        node.Remove("platform");
        node.Remove("inPerson");
        return node.ToJsonString(Archive);
    }

    private void CheckMeta(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var meta = JsonSerializer.Deserialize<SessionMeta>(text, Read)!;
        // §4.4: the storage layer deliberately drops platform/inPerson from the shared Mac meta file,
        // so the expected document excludes them too (the handling itself is covered below).
        var expected = System.Text.Json.Nodes.JsonNode.Parse(text) as System.Text.Json.Nodes.JsonObject
                       ?? throw new InvalidDataException($"{Relative(path)} is not an object");
        expected.Remove("platform");
        expected.Remove("inPerson");
        Compare(Relative(path), expected.ToJsonString(Archive), WriteMeta(meta));
    }

    private void CheckJsonl<T>(string path) where T : class
    {
        var label = Relative(path);
        var raw = File.ReadAllLines(path, Encoding.UTF8).Where(line => line.Trim().Length > 0).ToList();
        var records = JSONLines.Decode<T>(File.ReadAllBytes(path), Archive);
        if (raw.Count != records.Count)
            issues.Add($"{label}: {raw.Count} lines decoded into {records.Count} records");
        // SessionStore.AppendAsync: JsonSerializer.Serialize(item, ArchiveOptions) + '\n'.
        for (var index = 0; index < Math.Min(raw.Count, records.Count); index++)
            Compare($"{label}:{index + 1}", raw[index], JsonSerializer.Serialize(records[index], Archive));
    }

    private void CheckModel<T>(string path, JsonSerializerOptions write, JsonSerializerOptions? read = null)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var value = JsonSerializer.Deserialize<T>(text, read ?? Archive)
                    ?? throw new InvalidDataException($"{Relative(path)} did not decode");
        Compare(Relative(path), text, JsonSerializer.Serialize(value, write));
    }

    private void CheckGlossary(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var glossary = JsonSerializer.Deserialize<Glossary>(text, Read) ?? Glossary.Empty;
        Compare(Relative(path), text, JsonSerializer.Serialize(glossary, GlossaryStore.Options));
    }

    private void CheckManifest(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var options = new JsonSerializerOptions(BackupArchive.Options) { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<BackupManifest>(text, options)!;
        Compare(Relative(path), text, JsonSerializer.Serialize(manifest, BackupArchive.Options));
    }

    // ---- one test per fixture row ---------------------------------------------------------------

    [Fact]
    public void SessionMetaRoundTrips()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("meta.json")) CheckMeta(path);
        var top = MacFixtures.FixturePath("session-meta.json");
        if (File.Exists(top)) CheckMeta(top);
        AssertClean();
    }

    [Fact]
    public void TranscriptLinesRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("transcript.jsonl")) CheckJsonl<TranscriptLine>(path);
        AssertClean();
    }

    [Fact]
    public void TranslationsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("translations.jsonl")) CheckJsonl<SessionTranslation>(path);
        AssertClean();
    }

    [Fact]
    public void VoiceTagsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("voices.jsonl")) CheckJsonl<SessionVoiceTag>(path);
        AssertClean();
    }

    [Fact]
    public void QuestionEventsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("questions.jsonl")) CheckJsonl<SessionQuestionEvent>(path);
        AssertClean();
    }

    [Fact]
    public void AnswerEventsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("answers.jsonl")) CheckJsonl<SessionAnswerEvent>(path);
        AssertClean();
    }

    [Fact]
    public void SummariesRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("summary.json")) CheckModel<SessionSummary>(path, Archive);
        AssertClean();
    }

    [Fact]
    public void StructuredNotesRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("structured-notes.json")) CheckModel<StructuredNotesDoc>(path, Archive);
        AssertClean();
    }

    [Fact]
    public void UserContentRoundTrips()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("user-content.json")) CheckModel<SessionUserContent>(path, Archive);
        AssertClean();
    }

    [Fact]
    public void ContextSnapshotsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("context.json")) CheckModel<SessionContextSnapshot>(path, Archive);
        AssertClean();
    }

    [Fact]
    public void WorkspacesRoundTrip()
    {
        if (!MacFixtures.Available) return;
        var top = MacFixtures.FixturePath("workspace.json");
        if (File.Exists(top)) CheckModel<WorkspaceSeries>(top, WorkspaceStore.Options);
        foreach (var (path, _) in Files("*.json", path => Path.GetFileName(Path.GetDirectoryName(path)) == "Workspaces"))
            CheckModel<WorkspaceSeries>(path, BackupArchive.Options);
        AssertClean();
    }

    [Fact]
    public void SkillsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        var top = MacFixtures.FixturePath("skills.json");
        if (File.Exists(top)) CheckModel<List<Skill>>(top, SkillStore.Options);
        foreach (var (path, _) in Files("skills.json"))
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            var skills = JsonSerializer.Deserialize<List<Skill>>(text, Archive)!;
            Compare(Relative(path), text, JsonSerializer.Serialize(skills, SkillStore.Options));
        }
        AssertClean();
    }

    [Fact]
    public void TemplatesRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var name in new[] { "templates.json", "note-templates.json" })
            foreach (var (path, _) in Files(name))
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                var templates = JsonSerializer.Deserialize<List<NotesTemplate>>(text, Archive)!;
                Compare(Relative(path), text, JsonSerializer.Serialize(templates, NotesTemplateStore.Options));
            }
        AssertClean();
    }

    [Fact]
    public void GlossaryRoundTrips()
    {
        if (!MacFixtures.Available) return;
        foreach (var (path, _) in Files("glossary.json")) CheckGlossary(path);
        AssertClean();
    }

    [Fact]
    public void BackupManifestsRoundTrip()
    {
        if (!MacFixtures.Available) return;
        foreach (var name in new[] { "backup-manifest.json", "manifest.json" })
            foreach (var (path, _) in Files(name))
                CheckManifest(path);
        AssertClean();
    }

    [Fact]
    public void FourRealBackupArchivesAreCovered()
    {
        if (!MacFixtures.Available) return;
        Assert.NotNull(BackupZip);
        var extracted = Directory.Exists(backups)
            ? Directory.EnumerateDirectories(backups).Where(path => Path.GetFileName(path) != "outer").ToList()
            : [];
        Assert.Equal(4, extracted.Count);
        foreach (var directory in extracted) Assert.True(Directory.Exists(directory));
        // The AppleDouble entries inside the archives must not have been extracted.
        Assert.Empty(Directory.EnumerateFiles(backups, "._*", SearchOption.AllDirectories));
        AssertClean();
    }

    [Fact]
    public void EveryFileInTheBackupsIsUnderstood()
    {
        if (!MacFixtures.Available) return;
        foreach (var directory in ArchiveDirectories())
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                var known = name is "meta.json" or "transcript.jsonl" or "translations.jsonl" or "voices.jsonl"
                    or "questions.jsonl" or "answers.jsonl" or "summary.json" or "structured-notes.json"
                    or "user-content.json" or "context.json" or "glossary.json" or "skills.json"
                    or "note-templates.json" or "manifest.json"
                    || Path.GetFileName(Path.GetDirectoryName(file)) == "Workspaces";
                if (!known && name != "settings.plist") unknown.Add(Relative(file));
            }
        }
        Assert.Empty(unknown);
        AssertClean();
    }

    private IEnumerable<string> ArchiveDirectories() =>
        Directory.Exists(backups)
            ? Directory.EnumerateDirectories(backups).Where(path => Path.GetFileName(path) != "outer")
            : [];

    private void AssertClean()
    {
        if (issues.Count == 0) return;
        var shown = string.Join(Environment.NewLine, issues.Take(80));
        var more = issues.Count > 80 ? $"{Environment.NewLine}… {issues.Count - 80} more" : "";
        Assert.Fail($"{issues.Count} round-trip difference(s):{Environment.NewLine}{shown}{more}");
    }
}
