using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from TranscriptContextBuilderTests.swift (keepsOnlyTheMostRecentFiveMinutesInOrder):
/// the entry just outside the five-minute window is dropped and the rest keep their order.
/// </summary>
public sealed class TranscriptContextBuilderParityTests
{
    [Fact]
    public void KeepsOnlyTheMostRecentFiveMinutesInOrder()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(10_000);
        var entries = new (DateTimeOffset Date, string Text)[]
        {
            (now.AddSeconds(-301), "old"),
            (now.AddSeconds(-200), "first"),
            (now, "second")
        };

        Assert.Equal("first\nsecond", TranscriptContextBuilder.Recent(entries, now));
    }
}
