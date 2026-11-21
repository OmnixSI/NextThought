using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// A11: recompute the revision fingerprints the Mac fixtures were emitted with. Every expected value
/// in <c>revisions.json</c> comes from NextThoughtCore's SessionProjectionBuilder/SessionEvidenceBuilder,
/// so a matching C# value proves the Windows port hashes the same inputs the same way.
/// </summary>
public sealed class MacFixtureRevisionTests
{
    private static readonly (string SessionId, string Folder)[] Meetings =
    [
        ("11111111-1111-4111-8111-111111111111", "sessions/11111111-1111-4111-8111-111111111111"),
        ("22222222-2222-4222-8222-222222222222", "sessions/22222222-2222-4222-8222-222222222222")
    ];

    private static SessionMeta Meta(string folder) => MacFixtures.Json<SessionMeta>(MacFixtures.SessionPath(folder, "meta.json"));
    private static IReadOnlyList<TranscriptLine> Lines(string folder) =>
        MacFixtures.Lines<TranscriptLine>(MacFixtures.SessionPath(folder, "transcript.jsonl"));
    private static StructuredNotesDoc Notes(string folder) =>
        MacFixtures.Json<StructuredNotesDoc>(MacFixtures.SessionPath(folder, "structured-notes.json"));
    private static SessionSummary Summary(string folder) =>
        MacFixtures.Json<SessionSummary>(MacFixtures.SessionPath(folder, "summary.json"));

    private static JsonDocument Revisions => MacFixtures.Document(MacFixtures.FixturePath("revisions.json"));

    [Fact]
    public void FixturesArePresent()
    {
        if (!MacFixtures.Available) return; // SKIP: the fixture directory is only in the repository checkout.
        foreach (var (name, folder) in Meetings.Select((meeting, index) => ($"meeting{index + 1}", meeting.Folder)))
        {
            Assert.True(File.Exists(MacFixtures.SessionPath(folder, "meta.json")), folder);
            Assert.True(File.Exists(MacFixtures.SessionPath(folder, "transcript.jsonl")), folder);
            Assert.True(File.Exists(MacFixtures.SessionPath(folder, "structured-notes.json")), folder);
            Assert.True(File.Exists(MacFixtures.SessionPath(folder, "summary.json")), folder);
            _ = name;
        }
    }

    [Fact]
    public void EveryTranscriptLineMatchesItsMacTranscriptRevision()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var meetingsNode = revisions.RootElement.GetProperty("meetings");
        for (var index = 0; index < Meetings.Length; index++)
        {
            var (_, folder) = Meetings[index];
            var lines = Lines(folder);
            var expected = new Dictionary<string, string>();
            foreach (var line in meetingsNode[index].GetProperty("lines").EnumerateArray())
                expected[line.GetProperty("lineID").GetString()!] = line.GetProperty("transcriptRevision").GetString()!;
            foreach (var line in lines)
            {
                Assert.True(expected.TryGetValue(line.Id.ToString("D"), out var want),
                    $"{folder}: no expected revision for line {line.Id}");
                Assert.Equal(want, TranscriptRevisions.Of(line.Id, line.Text));
                Assert.Equal(want, TranscriptRevisions.Of(line.Id, line.Text)); // deterministic
            }
            Assert.Equal(lines.Count, expected.Count);
        }
    }

    [Fact]
    public void TranscriptSourceRevisionMatchesTheMacValueWithAndWithoutNotes()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var meetingsNode = revisions.RootElement.GetProperty("meetings");
        for (var index = 0; index < Meetings.Length; index++)
        {
            var (sessionId, folder) = Meetings[index];
            var meta = Meta(folder);
            var lines = Lines(folder);
            var notes = Notes(folder);
            var node = meetingsNode[index];
            Assert.Equal(sessionId, meta.Id.ToString("D"));

            var withoutNotes = SessionProjectionBuilder.TranscriptSourceRevision(meta, lines, []);
            Assert.Equal(node.GetProperty("transcriptSourceRevisionWithoutNotes").GetString(), withoutNotes);

            var withNotes = SessionProjectionBuilder.TranscriptSourceRevision(meta, lines, notes.ActiveCorrections());
            Assert.Equal(node.GetProperty("transcriptSourceRevision").GetString(), withNotes);
        }
    }

    [Fact]
    public void StructuredNotesRevisionMatchesTheMacValue()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var meetingsNode = revisions.RootElement.GetProperty("meetings");
        for (var index = 0; index < Meetings.Length; index++)
        {
            var (_, folder) = Meetings[index];
            var notes = Notes(folder);
            Assert.Equal(meetingsNode[index].GetProperty("structuredNotesRevision").GetString(),
                SessionProjectionBuilder.StructuredNotesRevision(notes));
        }
    }

    [Fact]
    public void ClaimAndSourceRevisionsMatchTheMacEvidence()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var meetingsNode = revisions.RootElement.GetProperty("meetings");
        for (var index = 0; index < Meetings.Length; index++)
        {
            var (_, folder) = Meetings[index];
            var meta = Meta(folder);
            var lines = Lines(folder);
            var notes = Notes(folder);
            var summary = Summary(folder);
            var evidenceNode = meetingsNode[index].GetProperty("decisionEvidence");
            var claim = evidenceNode.GetProperty("claim").GetString()!;

            var references = SessionEvidenceBuilder.Build(meta, lines, notes, summary, notes.ActiveCorrections());
            var reference = Assert.Single(references.Where(candidate => candidate.Claim == claim));
            Assert.Equal(evidenceNode.GetProperty("evidenceID").GetString(), reference.Id);
            Assert.Equal(evidenceNode.GetProperty("claimRevision").GetString(), reference.ClaimRevision);
            Assert.Equal(evidenceNode.GetProperty("sourceRevision").GetString(), reference.SourceRevision);
            Assert.Equal(evidenceNode.GetProperty("transcriptSourceRevision").GetString(), reference.TranscriptSourceRevision);
            Assert.Equal(evidenceNode.GetProperty("lineID").GetString(), reference.LineId?.ToString("D"));
            Assert.Equal(evidenceNode.GetProperty("quote").GetString(), reference.Quote);
        }
    }

    [Fact]
    public void TopLevelExpectedValuesAreTheFirstMeetingSummaries()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var expected = revisions.RootElement.GetProperty("expected").EnumerateArray()
            .Select(value => value.GetString()!).ToHashSet();
        Assert.Equal(3, expected.Count);

        var first = Meetings[0].Folder;
        var meta = Meta(first);
        var lines = Lines(first);
        var notes = Notes(first);
        var summary = Summary(first);
        var computed = new[]
        {
            "transcriptSourceRevision=" + SessionProjectionBuilder.TranscriptSourceRevision(
                meta, lines, notes.ActiveCorrections()),
            "claimSourceRevision=" + revisions.RootElement.GetProperty("claimSourceRevision").GetString(),
            "sourceRevision=" + revisions.RootElement.GetProperty("sourceRevision").GetString()
        };
        foreach (var value in computed) Assert.Contains(value, expected);

        // The second meeting's claim is the one that replaced the first in the series item.
        var claim = SessionEvidenceBuilder.Build(Meta(Meetings[1].Folder), Lines(Meetings[1].Folder),
                Notes(Meetings[1].Folder), Summary(Meetings[1].Folder))
            .Single(reference => reference.Claim == "正式改为周一提交");
        Assert.Equal(revisions.RootElement.GetProperty("seriesItem").GetProperty("claimRevision").GetString(),
            claim.ClaimRevision);
        Assert.Equal(revisions.RootElement.GetProperty("seriesItem").GetProperty("sourceRevision").GetString(),
            claim.SourceRevision);
    }

    [Fact]
    public void SeriesItemRevisionHistoryMatchesTheMacSnapshot()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var series = MacFixtures.Json<WorkspaceSeries>(MacFixtures.FixturePath("workspace.json"));
        var expected = revisions.RootElement.GetProperty("seriesItem");
        var item = Assert.Single(series.SeriesItems);

        Assert.Equal(expected.GetProperty("id").GetString(), item.Id);
        Assert.Equal(expected.GetProperty("revision").GetInt32(), item.Revision);
        Assert.Equal(expected.GetProperty("claimRevision").GetString(), item.ClaimRevision);
        Assert.Equal(expected.GetProperty("sourceRevision").GetString(), item.SourceRevision);

        var history = Assert.Single(item.History);
        Assert.Equal(1, history.Revision);
        Assert.Equal(expected.GetProperty("historyRevisionID").GetString(), history.Id);
        Assert.Equal(expected.GetProperty("historyClaimRevision").GetString(), history.ClaimRevision);
        Assert.Equal(revisions.RootElement.GetProperty("sourceRevision").GetString(), history.SourceRevision);
    }

    [Fact]
    public void TheRevisionInputsAreTheFixtureFiles()
    {
        if (!MacFixtures.Available) return; // SKIP: fixtures live in the repository checkout.
        using var revisions = Revisions;
        var transcript = revisions.RootElement.GetProperty("transcriptSourceRevision").GetString();
        Assert.Equal(transcript, revisions.RootElement.GetProperty("meetings")[0]
            .GetProperty("transcriptSourceRevision").GetString());

        // Same ids and dates the fixture directory carries, so the digest inputs are unambiguous.
        var meta = Meta(Meetings[0].Folder);
        Assert.Equal(new DateTimeOffset(2025, 11, 24, 16, 0, 0, TimeSpan.Zero), meta.StartedAt.ToUniversalTime());
        Assert.Equal("A15 第一次交付会", meta.Title);
        Assert.Equal(4, Lines(Meetings[0].Folder).Count);
    }
}
