using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B5: only explicitly supported current text-notes evidence keeps a series item active.</summary>
public sealed class SeriesBriefingTests
{
    private static SessionEvidenceReference Evidence(string id, string sourceRevision, string claimRevision,
        SessionEvidenceOrigin origin = SessionEvidenceOrigin.TextNotes, Guid? lineId = null) =>
        new(id, "决定", "改为周一周交付", SessionEvidenceSupport.Quoted, origin,
            SessionId: null, Quote: null, Location: null, LineId: lineId ?? Guid.NewGuid(),
            SourceRevision: sourceRevision, ClaimRevision: claimRevision, TranscriptSourceRevision: "transcript-v1",
            ReviewDecision: SessionEvidenceDecision.Supported, ReviewIsStale: false);

    [Fact]
    public void ChangedSourceRevisionShowsAcceptedItemAsStale()
    {
        var sourceId = Guid.NewGuid();
        var series = new WorkspaceSeries("series", "项目交付", DateTimeOffset.UtcNow, [sourceId.ToString("D")], [])
        {
            SeriesItems =
            [
                new WorkspaceSeriesItem
                {
                    Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "改为周一交付",
                    SourceSessionId = sourceId.ToString("D"), EvidenceId = "notes:decisions:delivery",
                    ClaimRevision = "claim-v1", SourceRevision = "line-v1",
                    TranscriptSourceRevision = "transcript-v1"
                }
            ]
        };
        var session = new SessionMeta { Id = sourceId, StartedAt = DateTimeOffset.UtcNow };
        var briefing = SeriesBriefingBuilder.Build(series, [session],
            evidenceReferences: [Evidence("notes:decisions:delivery", "line-v2", "claim-v2")]);
        Assert.Empty(briefing.Decisions);
        Assert.Empty(briefing.OpenActions);
        Assert.Equal("改为周一交付", Assert.Single(briefing.StaleItems).Text);
    }

    [Fact]
    public void StructuredNotesOriginDoesNotKeepALegacyItemActive()
    {
        var sourceId = Guid.NewGuid();
        var series = new WorkspaceSeries("series", "项目交付", DateTimeOffset.UtcNow, [sourceId.ToString("D")], [])
        {
            SeriesItems =
            [
                new WorkspaceSeriesItem
                {
                    Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "改为周一交付",
                    SourceSessionId = sourceId.ToString("D"), EvidenceId = "notes:decisions:delivery",
                    ClaimRevision = "claim-v1", SourceRevision = "line-v1",
                    TranscriptSourceRevision = "transcript-v1"
                }
            ]
        };
        var session = new SessionMeta { Id = sourceId, StartedAt = DateTimeOffset.UtcNow };
        var briefing = SeriesBriefingBuilder.Build(series, [session],
            evidenceReferences: [Evidence("notes:decisions:delivery", "line-v1", "claim-v1",
                SessionEvidenceOrigin.StructuredNotes)]);
        Assert.Empty(briefing.Decisions);
        Assert.Single(briefing.StaleItems);
    }
}
