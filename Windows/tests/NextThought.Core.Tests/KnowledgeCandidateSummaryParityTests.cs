using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B17: translated from WorkspaceTests.swift knowledge-candidate matching rules.</summary>
public sealed class KnowledgeCandidateSummaryParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static WorkspaceSeries Series() =>
        new("series", "系列", Start, [], []) { UseKnowledge = true };

    private static (WorkspaceSeries Series, string Id) Collected(string originId = "session",
        DateTimeOffset? sourceStartedAt = null)
    {
        var source = new WorkspaceKnowledgeCandidateSource
        {
            SessionId = "session", StartedAt = sourceStartedAt ?? Start,
            TranscriptSourceRevision = "rev-1", ProjectionRevision = "projection-1"
        };
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(), ["先说结论再展开"],
            [], "session", Start, "rev-1", [source], originId);
        return (series, Assert.Single(added));
    }

    [Fact]
    public void OriginIdParticipatesInTheCandidateIdAndRerunsStayStable()
    {
        var (_, first) = Collected("origin-a");
        var (_, second) = Collected("origin-a");
        var (_, other) = Collected("origin-b");
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void SourceStartedAtKeepsSubMillisecondPrecision()
    {
        var (_, first) = Collected(sourceStartedAt: Start);
        var (_, second) = Collected(sourceStartedAt: Start.AddTicks(5_000));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void MatchesCurrentSummaryRequiresBothRevisionsAndTheSameCandidate()
    {
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(), ["先说结论再展开"], [],
            "session", Start, "rev-1", originId: "session");
        var id = Assert.Single(added);
        var candidate = Assert.Single(series.KnowledgeCandidates!);
        var summary = new SessionSummary
        {
            TranscriptSourceRevision = "rev-1",
            Lessons = ["先说结论再展开"]
        };
        Assert.Equal(id, candidate.Id);
        Assert.True(KnowledgeMerger.MatchesCurrentSummary(candidate, summary, "session", "rev-1"));
        Assert.False(KnowledgeMerger.MatchesCurrentSummary(candidate, summary, "session", "rev-2"));
        Assert.False(KnowledgeMerger.MatchesCurrentSummary(candidate, summary, "other", "rev-1"));
        summary = summary with { TranscriptSourceRevision = "rev-old" };
        Assert.False(KnowledgeMerger.MatchesCurrentSummary(candidate, summary, "session", "rev-1"));
    }
}
