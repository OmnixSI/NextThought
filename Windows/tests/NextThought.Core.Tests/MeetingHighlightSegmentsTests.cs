using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>E8: highlights merged into the 重点 paragraphs under the personal notes.</summary>
public sealed class MeetingHighlightSegmentsTests
{
    private static SessionHighlight Highlight(int offset, string quote) => new()
    {
        LineId = Guid.NewGuid(),
        OffsetSeconds = offset,
        Quote = quote
    };

    [Fact]
    public void AnEmptyListHasNoSegments()
    {
        Assert.Empty(MeetingHighlightSegments.Join([]));
    }

    [Fact]
    public void CloseHighlightsAreMergedIntoOneParagraph()
    {
        var segments = MeetingHighlightSegments.Join([
            Highlight(10, "first"),
            Highlight(25, "second"),
            Highlight(40, "third")
        ]);
        Assert.Single(segments);
        Assert.Equal(10, segments[0].StartSeconds);
        Assert.Equal(40, segments[0].EndSeconds);
        Assert.Equal(3, segments[0].Highlights.Count);
        Assert.Equal(NotesTime.Text(10) + "–" + NotesTime.Text(40), segments[0].RangeText);
    }

    [Fact]
    public void ARealPauseStartsANewParagraph()
    {
        var segments = MeetingHighlightSegments.Join([
            Highlight(10, "first"),
            Highlight(20, "second"),
            Highlight(90, "far away"),
            Highlight(100, "after the pause")
        ]);
        Assert.Equal(2, segments.Count);
        Assert.Equal(2, segments[0].Highlights.Count);
        Assert.Equal(90, segments[1].StartSeconds);
        Assert.Equal(2, segments[1].Highlights.Count);
    }

    [Fact]
    public void HighlightsAreOrderedByTimeAndEmptyQuotesAreDropped()
    {
        // 45 s apart is beyond the 30 s gap, so the two highlights land in separate paragraphs,
        // and the blank quote is dropped entirely.
        var segments = MeetingHighlightSegments.Join([
            Highlight(50, "later"),
            Highlight(5, "earlier"),
            Highlight(7, "   ")
        ]);
        Assert.Equal(2, segments.Count);
        Assert.Equal(["earlier"], segments[0].Highlights.Select(item => item.Quote));
        Assert.Equal(5, segments[0].StartSeconds);
        Assert.Equal(["later"], segments[1].Highlights.Select(item => item.Quote));
        Assert.Equal(50, segments[1].StartSeconds);
    }

    [Fact]
    public void ASingleHighlightKeepsItsExactTimeLabel()
    {
        var segments = MeetingHighlightSegments.Join([Highlight(65, "only")]);
        Assert.Equal(NotesTime.Text(65), segments[0].RangeText);
        Assert.Equal(65, segments[0].EndSeconds);
    }

    [Fact]
    public void AShortGapCanBeConfigured()
    {
        var highlights = new[] { Highlight(10, "a"), Highlight(20, "b") };
        Assert.Single(MeetingHighlightSegments.Join(highlights, gapSeconds: 30));
        Assert.Equal(2, MeetingHighlightSegments.Join(highlights, gapSeconds: 5).Count);
    }
}
