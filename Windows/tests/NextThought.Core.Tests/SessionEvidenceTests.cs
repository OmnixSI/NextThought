using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the session-evidence models.</summary>
public sealed class SessionEvidenceTests
{
    [Fact]
    public void SupportLabelsExplainTheMatch()
    {
        Assert.Equal("引文在所选转写版本中找到", SessionEvidenceSupport.Quoted.Label());
        Assert.Equal("仅关联时间 · 内容待核对", SessionEvidenceSupport.TimeOnly.Label());
        Assert.Equal("未找到所引原话 · 内容待核对", SessionEvidenceSupport.QuoteNotFound.Label());
        Assert.Equal("没有原话定位 · 内容待核对", SessionEvidenceSupport.Unlocated.Label());
    }

    [Fact]
    public void ReferenceDefaultsAreEmptyAndUnstale()
    {
        var reference = new SessionEvidenceReference("id", "标题", "结论", SessionEvidenceSupport.Unlocated, SessionEvidenceOrigin.TextNotes);
        Assert.Null(reference.Quote);
        Assert.Null(reference.LineId);
        Assert.Equal("", reference.TranscriptSourceRevision);
        Assert.Equal(SessionDerivedContentStatus.Unknown, reference.SourceStatus);
        Assert.Null(reference.ReviewDecision);
        Assert.False(reference.ReviewIsStale);
        Assert.False(reference.NeedsConfirmation);
    }

    [Fact]
    public void ReferenceCarriesTheFullProvenance()
    {
        var session = Guid.NewGuid();
        var line = Guid.NewGuid();
        var notes = Guid.NewGuid();
        var target = new SessionStructuredNotesTarget(NotesFormat.Fishbone, "categories[0].causes[1]", "rev");
        var reference = new SessionEvidenceReference(
            "id", "标题", "结论", SessionEvidenceSupport.Quoted, SessionEvidenceOrigin.StructuredNotes,
            SessionId: session, Quote: "原话", Location: "05:20", LineId: line, SourceRevision: "r1",
            SourceVariant: SessionEvidenceSourceVariant.Corrected, SourceText: "校对后", NeedsConfirmation: true,
            NotesItemId: notes, StructuredTarget: target, Kind: NotesSectionKind.Actions, Owner: "我",
            Due: "周五", DueDate: new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero), ClaimRevision: "c1",
            TranscriptSourceRevision: "t1", SourceStatus: SessionDerivedContentStatus.Current,
            ReviewDecision: SessionEvidenceDecision.Supported);
        Assert.Equal(session, reference.SessionId);
        Assert.Equal(line, reference.LineId);
        Assert.Equal(SessionEvidenceSourceVariant.Corrected, reference.SourceVariant);
        Assert.True(reference.NeedsConfirmation);
        Assert.Equal(target, reference.StructuredTarget);
        Assert.Equal(NotesSectionKind.Actions, reference.Kind);
        Assert.Equal(SessionEvidenceDecision.Supported, reference.ReviewDecision);
        Assert.Equal("t1", reference.TranscriptSourceRevision);
    }

    [Fact]
    public void ReviewsAndStructuredTargetsCompareByValue()
    {
        var at = DateTimeOffset.UtcNow;
        Assert.Equal(
            new SessionEvidenceReview(SessionEvidenceDecision.Supported, "c", "t", "l", at),
            new SessionEvidenceReview(SessionEvidenceDecision.Supported, "c", "t", "l", at));
        Assert.NotEqual(
            new SessionStructuredNotesTarget(NotesFormat.Outline, "a", "r1"),
            new SessionStructuredNotesTarget(NotesFormat.Outline, "a", "r2"));
    }

    [Fact]
    public void EvidenceDecisionsAndOriginsAreDistinct()
    {
        Assert.NotEqual(SessionEvidenceDecision.Supported, SessionEvidenceDecision.Unsupported);
        Assert.NotEqual(SessionEvidenceOrigin.TextNotes, SessionEvidenceOrigin.StructuredNotes);
    }
}
