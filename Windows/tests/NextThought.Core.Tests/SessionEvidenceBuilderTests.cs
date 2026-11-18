using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for evidence support matching and review freshness.</summary>
public sealed class SessionEvidenceBuilderTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(8));

    private static TranscriptLine Line(string text, int offset) => new() { Text = text, Date = Start.AddSeconds(offset) };

    private static readonly TranscriptLine[] Lines =
    [
        Line("我们用 Kubernetes 部署", 60),
        Line("缓存命中率提升到 95%", 125)
    ];

    [Fact]
    public void QuotesAreMatchedCaseInsensitively()
    {
        Assert.Equal(SessionEvidenceSupport.Quoted, SessionEvidenceBuilder.Support("kubernetes", null, Lines));
        Assert.Equal(SessionEvidenceSupport.QuoteNotFound, SessionEvidenceBuilder.Support("不存在", null, Lines));
    }

    [Fact]
    public void WithoutAQuoteTheLocationCanSupportTheReference()
    {
        Assert.Equal(SessionEvidenceSupport.TimeOnly, SessionEvidenceBuilder.Support(null, "01:05", Lines));
        Assert.Equal(SessionEvidenceSupport.Unlocated, SessionEvidenceBuilder.Support(null, "20:00", Lines));
        Assert.Equal(SessionEvidenceSupport.Unlocated, SessionEvidenceBuilder.Support(null, null, Lines));
    }

    [Fact]
    public void MalformedLocationsAreUnlocated()
    {
        Assert.Equal(SessionEvidenceSupport.Unlocated, SessionEvidenceBuilder.Support(null, "abc", Lines));
        Assert.Equal(SessionEvidenceSupport.Unlocated, SessionEvidenceBuilder.Support(null, "1:2:3:4", Lines));
    }

    [Fact]
    public void SecondsParsesMinutesAndHours()
    {
        Assert.Equal(65, SessionEvidenceBuilder.Seconds("01:05"));
        Assert.Equal(3661, SessionEvidenceBuilder.Seconds("1:01:01"));
        Assert.Equal(42, SessionEvidenceBuilder.Seconds("42"));
        Assert.Null(SessionEvidenceBuilder.Seconds(""));
        Assert.Null(SessionEvidenceBuilder.Seconds("a:b"));
    }

    [Fact]
    public void ReviewsAreCurrentOnlyWhileAllRevisionsMatch()
    {
        var review = new SessionEvidenceReview(SessionEvidenceDecision.Supported, "c1", "t1", "l1", DateTimeOffset.UtcNow);
        Assert.True(SessionEvidenceBuilder.IsReviewCurrent(review, "c1", "t1", "l1"));
        Assert.False(SessionEvidenceBuilder.IsReviewCurrent(review, "c2", "t1", "l1"));
        Assert.False(SessionEvidenceBuilder.IsReviewCurrent(review, "c1", "t2", "l1"));
        Assert.False(SessionEvidenceBuilder.IsReviewCurrent(review, "c1", "t1", "l2"));
        Assert.False(SessionEvidenceBuilder.IsReviewCurrent(null, "c1", "t1", "l1"));
    }

    [Fact]
    public void AttachingAChangedReviewMarksItStale()
    {
        var reference = new SessionEvidenceReference("id", "标题", "结论", SessionEvidenceSupport.Quoted,
            SessionEvidenceOrigin.TextNotes, ClaimRevision: "old", TranscriptSourceRevision: "old-t");
        var review = new SessionEvidenceReview(SessionEvidenceDecision.Supported, "new", "new-t", "l", DateTimeOffset.UtcNow);
        var updated = SessionEvidenceBuilder.WithReview(reference, review, "new", "new-t", "changed");
        Assert.True(updated.ReviewIsStale);
        Assert.Equal(SessionEvidenceDecision.Supported, updated.ReviewDecision);
        var current = SessionEvidenceBuilder.WithReview(reference, review, "new", "new-t", "l");
        Assert.False(current.ReviewIsStale);
    }
}
