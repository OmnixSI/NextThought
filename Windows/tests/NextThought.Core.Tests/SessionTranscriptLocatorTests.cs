using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Port of SessionTranscriptLocatorTests.swift.</summary>
public sealed class SessionTranscriptLocatorTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static TranscriptLine Line(int seconds, string text = "line", int? start = null, int? end = null) =>
        new()
        {
            Date = Start.AddSeconds(seconds),
            Text = text,
            StartedAt = start is { } from ? Start.AddSeconds(from) : null,
            EndedAt = end is { } to ? Start.AddSeconds(to) : null
        };

    [Fact]
    public void DistantAndGapReferencesDoNotBorrowTheNearestLine()
    {
        var lines = new[] { Line(0, "开场"), Line(175, "02:55 的原话") };
        Assert.Null(SessionTranscriptLocator.Index(null, 18 * 60 + 22, Start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, 60, Start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, -1, Start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, 175, Start, []));
        Assert.Equal(1, SessionTranscriptLocator.Index(null, 178, Start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, 179, Start, lines));
    }

    [Fact]
    public void RealUtteranceIntervalsResolveWithOnlyASmallBoundaryTolerance()
    {
        var lines = new[]
        {
            Line(10, "持续的一句话", start: 10, end: 30),
            Line(80, "另一个话题")
        };
        Assert.Equal(0, SessionTranscriptLocator.Index(null, 25, Start, lines));
        Assert.Equal(0, SessionTranscriptLocator.Index(null, 33, Start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, 34, Start, lines));
        Assert.Null(SessionTranscriptLocator.Index(null, 60, Start, lines));
    }

    [Fact]
    public void StableLineIdsTakePrecedenceAndMissingIdsCannotGuess()
    {
        var line = Line(5, "已标记的原话");
        Assert.Equal(0, SessionTranscriptLocator.Index(line.Id, 1102, Start, [line]));
        Assert.Null(SessionTranscriptLocator.Index(Guid.NewGuid(), 1102, Start, [line]));
        Assert.Null(SessionTranscriptLocator.Index(Guid.NewGuid(), null, Start, [line]));
        Assert.Equal(0, SessionTranscriptLocator.Index(Guid.NewGuid(), 5, Start, [line]));
    }

    [Fact]
    public void MalformedIntervalsDoNotExpandTheAcceptedRange()
    {
        var line = Line(20, "旧锚点", start: 30, end: 10);
        Assert.Null(SessionTranscriptLocator.Index(null, 25, Start, [line]));
        Assert.Equal(0, SessionTranscriptLocator.Index(null, 20, Start, [line]));
    }
}
