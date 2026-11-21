using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// A11 §4.1: for both meetings in <c>revisions.json</c>, read the inputs from the Mac fixture
/// session folders and recompute every value the Mac recorded: each line's transcriptRevision, the
/// meeting's transcriptSourceRevision, its transcriptSourceRevisionWithoutNotes and
/// structuredNotesRevision, and the decision's claimRevision and sourceRevision. The C# values must
/// equal the Mac values, otherwise an imported meeting would be considered stale.
/// </summary>
public sealed class MacRevisionParityTests
{
    private static readonly (string Id, string Folder)[] Meetings =
    [
        ("11111111-1111-4111-8111-111111111111", "sessions/11111111-1111-4111-8111-111111111111"),
        ("22222222-2222-4222-8222-222222222222", "sessions/22222222-2222-4222-8222-222222222222")
    ];

    private static SessionMeta Meta(string folder) =>
        MacFixtures.Json<SessionMeta>(MacFixtures.SessionPath(folder, "meta.json"));

    private static IReadOnlyList<TranscriptLine> Lines(string folder) =>
        MacFixtures.Lines<TranscriptLine>(MacFixtures.SessionPath(folder, "transcript.jsonl"));

    private static StructuredNotesDoc Notes(string folder) =>
        MacFixtures.Json<StructuredNotesDoc>(MacFixtures.SessionPath(folder, "structured-notes.json"));

    private static SessionSummary Summary(string folder) =>
        MacFixtures.Json<SessionSummary>(MacFixtures.SessionPath(folder, "summary.json"));

    private static JsonDocument Revisions => MacFixtures.Document(MacFixtures.FixturePath("revisions.json"));

    [Fact]
    public void EachMeetingMatchesTheMacRevisionValues()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures only exist in the repository checkout.
        using var revisions = Revisions;
        var meetings = revisions.RootElement.GetProperty("meetings");
        Assert.Equal(Meetings.Length, meetings.GetArrayLength());

        for (var index = 0; index < Meetings.Length; index++)
        {
            var (sessionId, folder) = Meetings[index];
            var expected = meetings[index];
            var meta = Meta(folder);
            var lines = Lines(folder);
            var notes = Notes(folder);
            var summary = Summary(folder);
            Assert.Equal(sessionId, meta.Id.ToString("D"));

            // 1. Every line's transcriptRevision.
            var lineRevisions = expected.GetProperty("lines").EnumerateArray()
                .ToDictionary(item => item.GetProperty("lineID").GetString()!,
                    item => item.GetProperty("transcriptRevision").GetString()!);
            Assert.Equal(lines.Count, lineRevisions.Count);
            foreach (var line in lines)
                Assert.Equal(lineRevisions[line.Id.ToString("D")], TranscriptRevisions.Of(line.Id, line.Text));

            // 2. transcriptSourceRevision — Mac builds the speaker labels from meta.voiceNames/meVoices.
            var transcriptSource = SessionProjectionBuilder.TranscriptSourceRevision(meta, lines, notes.ActiveCorrections());
            Assert.Equal(expected.GetProperty("transcriptSourceRevision").GetString(), transcriptSource);

            // 3. transcriptSourceRevisionWithoutNotes — the same transcript with no notes corrections.
            var withoutNotes = SessionProjectionBuilder.TranscriptSourceRevision(meta, lines, []);
            Assert.Equal(expected.GetProperty("transcriptSourceRevisionWithoutNotes").GetString(), withoutNotes);

            // 4. structuredNotesRevision — the JSONLines encoding of the notes snapshot.
            Assert.Equal(expected.GetProperty("structuredNotesRevision").GetString(),
                SessionProjectionBuilder.StructuredNotesRevision(notes));

            // 5./6. claimRevision and sourceRevision of the meeting's decision.
            var evidence = expected.GetProperty("decisionEvidence");
            var claim = evidence.GetProperty("claim").GetString()!;
            var reference = SessionEvidenceBuilder.Build(meta, lines, notes, summary, notes.ActiveCorrections())
                .Single(item => item.Claim == claim);
            Assert.Equal(evidence.GetProperty("claimRevision").GetString(), reference.ClaimRevision);
            Assert.Equal(evidence.GetProperty("sourceRevision").GetString(), reference.SourceRevision);

            // The summary saved on the Mac must agree with the values we just recomputed.
            Assert.Equal(transcriptSource, summary.TranscriptSourceRevision);
            Assert.Equal(expected.GetProperty("structuredNotesRevision").GetString(), summary.StructuredNotesSourceRevision);
        }
    }

    [Fact]
    public void TopLevelValuesAreTheFirstMeetingsValues()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures only exist in the repository checkout.
        using var revisions = Revisions;
        var expected = revisions.RootElement.GetProperty("expected").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet();
        Assert.Equal(3, expected.Count);

        var first = Meetings[0].Folder;
        var computed = new[]
        {
            "transcriptSourceRevision=" + SessionProjectionBuilder.TranscriptSourceRevision(
                Meta(first), Lines(first), Notes(first).ActiveCorrections()),
            "claimSourceRevision=" + revisions.RootElement.GetProperty("claimSourceRevision").GetString(),
            "sourceRevision=" + revisions.RootElement.GetProperty("sourceRevision").GetString()
        };
        foreach (var value in computed) Assert.Contains(value, expected);

        var reference = SessionEvidenceBuilder.Build(Meta(first), Lines(first), Notes(first), Summary(first),
                Notes(first).ActiveCorrections())
            .Single(item => item.Claim == "先按周五提交");
        Assert.Equal(revisions.RootElement.GetProperty("claimSourceRevision").GetString(), reference.ClaimRevision);
        Assert.Equal(revisions.RootElement.GetProperty("sourceRevision").GetString(), reference.SourceRevision);
    }

    [Fact]
    public void StructuredNotesBytesAreTheMacFileBytes()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures only exist in the repository checkout.
        // §4.2: the canonical JSON must be byte-identical to what Mac's JSONLines encoder writes
        // (compact, sorted keys, unescaped slashes, whole-second dates). The commit fixture file is
        // that encoder's output, so comparing bytes proves the digest input is the same data.
        foreach (var (_, folder) in Meetings)
        {
            var bytes = SessionProjectionBuilder.StructuredNotesBytes(Notes(folder));
            var expected = File.ReadAllBytes(MacFixtures.SessionPath(folder, "structured-notes.json"));
            Assert.Equal(Convert.ToBase64String(expected), Convert.ToBase64String(bytes));
        }
    }

    [Fact]
    public void TheFixtureInputsAreTheRenderedMacOnes()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures only exist in the repository checkout.
        // Speaker names and me-voices must come from meta.json; that is what makes the digest match.
        var meta = Meta(Meetings[0].Folder);
        Assert.Equal("王老师", meta.VoiceNames?["1"]);
        Assert.Empty(meta.MeVoices ?? []);
        Assert.Equal(new DateTimeOffset(2025, 11, 24, 16, 0, 0, TimeSpan.Zero), meta.StartedAt.ToUniversalTime());
        var notes = Notes(Meetings[0].Folder);
        Assert.Single(notes.Corrections!);
        Assert.Equal("Sherpa-ONNX", notes.Corrections![0].Corrected);
    }
}
