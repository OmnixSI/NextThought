using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// A6/§4.3–4.4: the Mac v3.4.0 serialisation contract. UUIDs are upper-case with hyphens, read-only
/// computed properties and Windows-only state never reach files shared with the Mac, optional fields
/// that were absent stay absent, and the per-meeting meta keeps its Mac key set.
/// </summary>
public sealed class MacFixtureSerializationTests
{
    private static readonly JsonSerializerOptions Write = JsonDefaults.ArchiveOptions;

    private static JsonObject Node<T>(T value) =>
        JsonSerializer.SerializeToNode(value, Write)!.AsObject();

    private static JsonObject Keys(string path) =>
        JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!.AsObject();

    private static string Temp(string tag) =>
        Path.Combine(Path.GetTempPath(), "nt-a6-" + tag + "-" + Guid.NewGuid().ToString("N"));

    private static readonly Guid LowerId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    // ---- §4.3 UUID casing -----------------------------------------------------------

    [Fact]
    public void GuidFieldsSerializeUppercaseWithHyphens()
    {
        var meta = new SessionMeta { Id = LowerId, Title = "t" };
        Assert.Equal("11111111-1111-4111-8111-111111111111", Node(meta)["id"]!.GetValue<string>());

        var line = new TranscriptLine { Id = LowerId, Date = DateTimeOffset.UnixEpoch, Text = "x" };
        Assert.Equal("11111111-1111-4111-8111-111111111111", Node(line)["id"]!.GetValue<string>());

        var voice = new SessionVoiceTag(LowerId, 0);
        Assert.Equal("11111111-1111-4111-8111-111111111111", Node(voice)["id"]!.GetValue<string>());

        var answer = new AnswerRecord { Id = LowerId };
        Assert.Equal("11111111-1111-4111-8111-111111111111", Node(answer)["id"]!.GetValue<string>());
    }

    [Fact]
    public void WorkspaceFileAndSeriesIdentifiersAreUppercase()
    {
        var root = Temp("workspace-id");
        try
        {
            var series = WorkspaceSeries.Create("A6 UUID") with { Id = LowerId.ToString("D") };
            new WorkspaceStore(root).Save(new WorkspaceData([series]));
            var file = Assert.Single(Directory.GetFiles(Path.Combine(root, "Workspaces"), "*.json"));
            Assert.Equal("11111111-1111-4111-8111-111111111111.json", Path.GetFileName(file));
            var node = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            Assert.Equal("11111111-1111-4111-8111-111111111111", node["id"]!.GetValue<string>());

            // Identity comparisons happen on parsed GUIDs, not on the string spelling.
            var loaded = new WorkspaceStore(root).Load().Find(LowerId.ToString("D").ToLowerInvariant());
            Assert.NotNull(loaded);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- §4.4 extra keys ------------------------------------------------------------

    [Fact]
    public void WorkspaceSerializesTheSameKeysAsMac()
    {
        var fixture = Keys(MacFixtures.FixturePath("workspace.json"));
        var series = MacFixtures.Json<WorkspaceSeries>(MacFixtures.FixturePath("workspace.json"));
        var written = Node(series);
        Assert.Equal(fixture.Select(pair => pair.Key).OrderBy(key => key),
            written.Select(pair => pair.Key).OrderBy(key => key));
        foreach (var removed in new[] { "effectiveMode", "profile", "retrievalDocuments", "documentIds" })
            Assert.False(written.ContainsKey(removed), removed);
    }

    [Fact]
    public void WorkspaceWithoutOptionalContentDoesNotInventEmptyArrays()
    {
        var series = new WorkspaceSeries("11111111-1111-4111-8111-111111111111", "empty",
            DateTimeOffset.UnixEpoch, []);
        var written = Node(series);
        Assert.False(written.ContainsKey("seriesItems"));
        Assert.False(written.ContainsKey("seriesChangeCandidates"));
        Assert.False(written.ContainsKey("documentIds"));
        Assert.False(written.ContainsKey("knowledgeCandidates"));
        Assert.False(written.ContainsKey("sourceDeletionTombstones"));
    }

    [Fact]
    public void WindowsOnlyDocumentIdsLiveInAWindowsSidecar()
    {
        var root = Temp("docids");
        try
        {
            var series = WorkspaceSeries.Create("docs") with
            {
                Id = LowerId.ToString("D"),
                DocumentIds = ["doc-1"],
                Documents = [new ContextDocument { Id = "doc-1", Name = "d", Enabled = true }]
            };
            new WorkspaceStore(root).Save(new WorkspaceData([series]));
            var file = Assert.Single(Directory.GetFiles(Path.Combine(root, "Workspaces"), "*.json"));
            Assert.False(JsonNode.Parse(File.ReadAllText(file))!.AsObject().ContainsKey("documentIds"));
            var sidecar = Path.Combine(root, "WindowsState", "workspace-documents.json");
            Assert.True(File.Exists(sidecar));
            Assert.Contains("doc-1", File.ReadAllText(sidecar));
            var loaded = new WorkspaceStore(root).Load().Find(LowerId.ToString("D"));
            Assert.NotNull(loaded);
            Assert.Equal(["doc-1"], loaded!.DocumentIds);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void SummaryDoesNotGainStoredFormats()
    {
        var path = MacFixtures.SessionPath("sessions/11111111-1111-4111-8111-111111111111", "summary.json");
        var original = Keys(path);
        var written = Node(MacFixtures.Json<SessionSummary>(path));
        Assert.Equal(original.Select(pair => pair.Key).OrderBy(key => key),
            written.Select(pair => pair.Key).OrderBy(key => key));
        Assert.False(written.ContainsKey("storedFormats"));
    }

    [Fact]
    public void NotesUserContentAndPayloadsDoNotGainHasContent()
    {
        var notesPath = MacFixtures.SessionPath("sessions/11111111-1111-4111-8111-111111111111", "structured-notes.json");
        var notes = Node(MacFixtures.Json<StructuredNotesDoc>(notesPath));
        Assert.False(notes.ContainsKey("hasContent"));

        var userPath = MacFixtures.FixturePath("user-content.json");
        var original = Keys(userPath);
        var written = Node(MacFixtures.Json<SessionUserContent>(userPath));
        Assert.Equal(original.Select(pair => pair.Key).OrderBy(key => key),
            written.Select(pair => pair.Key).OrderBy(key => key));
        Assert.False(written.ContainsKey("hasContent"));

        var payload = new StructuredNotesPayload(NotesFormat.Fishbone);
        Assert.False(Node(payload).ContainsKey("hasContent"));
    }

    [Fact]
    public void TemplatesDoNotGainIsBuiltIn()
    {
        var path = MacFixtures.FixturePath("templates.json");
        var original = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var templates = JsonSerializer.Deserialize<List<NotesTemplate>>(File.ReadAllText(path), Write)!;
        var written = JsonSerializer.SerializeToNode(templates, Write)!.AsArray();
        Assert.Equal(original.Count, written.Count);
        Assert.All(written, node => Assert.False(node!.AsObject().ContainsKey("isBuiltIn")));
        Assert.Equal(original[0]!.AsObject().Select(pair => pair.Key).OrderBy(key => key),
            written[0]!.AsObject().Select(pair => pair.Key).OrderBy(key => key));
    }

    [Fact]
    public void AnswerLinesDoNotGainIsComplete()
    {
        var path = MacFixtures.FixturePath("answers.jsonl");
        var line = File.ReadAllLines(path).First(text => text.Trim().Length > 0);
        var original = JsonNode.Parse(line)!.AsObject();
        var answer = JsonSerializer.Deserialize<SessionAnswerEvent>(line, Write)!;
        var written = Node(answer);
        Assert.Equal(original.Select(pair => pair.Key).OrderBy(key => key),
            written.Select(pair => pair.Key).OrderBy(key => key));
        Assert.False(written.ContainsKey("isComplete"));
    }

    [Fact]
    public void MacTranslationLinesStayMinimal()
    {
        var path = MacFixtures.FixturePath("translations.jsonl");
        var line = File.ReadAllLines(path).First(text => text.Trim().Length > 0);
        var original = JsonNode.Parse(line)!.AsObject();
        var translation = JsonSerializer.Deserialize<SessionTranslation>(line, Write)!;
        var written = Node(translation);
        Assert.Equal(original.Select(pair => pair.Key).OrderBy(key => key),
            written.Select(pair => pair.Key).OrderBy(key => key));
        Assert.False(written.ContainsKey("revision"));
        Assert.False(written.ContainsKey("date"));
        Assert.False(written.ContainsKey("sourceHash"));
        Assert.False(written.ContainsKey("direction"));
        Assert.False(written.ContainsKey("model"));
    }

    [Fact]
    public void WindowsTranslationsKeepTheirFullShape()
    {
        var translation = new SessionTranslation(LowerId, 2, "hash", "zh-en", "text", "model",
            DateTimeOffset.Parse("2025-11-24T16:00:00Z"));
        var written = Node(translation);
        Assert.Equal(2, written["revision"]!.GetValue<int>());
        // Archive dates use Foundation's seconds-since-2001 number, the same as every other Mac file.
        var seconds = written["date"]!.GetValue<double>();
        var macEpoch = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(DateTimeOffset.Parse("2025-11-24T16:00:00Z"), macEpoch.AddSeconds(seconds));
        Assert.Equal("11111111-1111-4111-8111-111111111111", written["id"]!.GetValue<string>());
    }
}
