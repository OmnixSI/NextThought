using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the portable part of the session text projection.</summary>
public sealed class SessionTextProjectionTests
{
    private static SessionTextProjection Projection(
        IReadOnlyList<SessionProjectedText>? text = null, SessionProjectedSummary? summary = null) =>
        new(Guid.NewGuid(), "rev", "t1", new SessionProjectionOptions(), [], text ?? [], summary);

    [Fact]
    public void OptionsDefaultToCorrectedBilingual()
    {
        var options = new SessionProjectionOptions();
        Assert.Equal(SessionTranscriptVariant.Corrected, options.TranscriptVariant);
        Assert.Equal(SessionLanguageView.Bilingual, options.LanguageView);
    }

    [Fact]
    public void ProjectedTextCarriesItsLocatorAndAlternates()
    {
        var source = Guid.NewGuid();
        var line = Guid.NewGuid();
        var text = new SessionProjectedText("line:1", SessionProjectionTextKind.Transcript, "Kubernetes", source, line,
            "05:20", "r1", false, SessionDerivedContentStatus.Current, "K8s");
        Assert.Equal("line:1", text.Id);
        Assert.Equal(source, text.SourceId);
        Assert.Equal("K8s", text.AlternateText);
        Assert.Equal(SessionDerivedContentStatus.Current, text.SourceStatus);
    }

    [Fact]
    public void AnswersAreCompleteUnlessInterruptedOrFailed()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.True(new SessionProjectedAnswer(id, now, "q", "a", "r").IsComplete);
        Assert.True(new SessionProjectedAnswer(id, now, "q", "a", "r", AnswerCompletion.Complete).IsComplete);
        Assert.False(new SessionProjectedAnswer(id, now, "q", "a", "r", AnswerCompletion.Interrupted).IsComplete);
        Assert.False(new SessionProjectedAnswer(id, now, "q", "a", "r", AnswerCompletion.Failed).IsComplete);
    }

    [Fact]
    public void StaleDerivedContentWins()
    {
        var projection = Projection(
            [
                new SessionProjectedText("a", SessionProjectionTextKind.Notes, "n", SourceStatus: SessionDerivedContentStatus.Current),
                new SessionProjectedText("b", SessionProjectionTextKind.Summary, "s", SourceStatus: SessionDerivedContentStatus.Stale)
            ],
            new SessionProjectedSummary("标题", "#", SourceStatus: SessionDerivedContentStatus.Current));
        Assert.Equal(SessionDerivedContentStatus.Stale, projection.DerivedContentStatus);
    }

    [Fact]
    public void UnknownIsReportedWhenAnyDerivedContentIsUnknown()
    {
        var projection = Projection(
            [new SessionProjectedText("a", SessionProjectionTextKind.Transcript, "t")],
            new SessionProjectedSummary("标题", "#", SourceStatus: SessionDerivedContentStatus.Unknown));
        Assert.Equal(SessionDerivedContentStatus.Unknown, projection.DerivedContentStatus);
    }

    [Fact]
    public void OriginalTranscriptAloneIsCurrent()
    {
        var projection = Projection([new SessionProjectedText("a", SessionProjectionTextKind.Transcript, "t")]);
        Assert.Equal(SessionDerivedContentStatus.Current, projection.DerivedContentStatus);
    }

    [Fact]
    public void SummaryDefaultsToUnknown()
    {
        var summary = new SessionProjectedSummary("标题", "正文");
        Assert.Null(summary.SourceTranscriptRevision);
        Assert.Equal(SessionDerivedContentStatus.Unknown, summary.SourceStatus);
    }
}
