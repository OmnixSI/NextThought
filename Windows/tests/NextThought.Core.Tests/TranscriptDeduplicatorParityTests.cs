using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Swift parity: Tests/NextThoughtCoreTests/TranscriptDeduplicatorTests.swift.</summary>
public sealed class TranscriptDeduplicatorParityTests
{
    [Fact]
    public void TrimsRepeatedEnglishWordsAtSegmentBoundary()
    {
        Assert.Equal("for repeated requests", TranscriptDeduplicator.RemoveRepeatedPrefix(
            previous: "We should use a cache",
            incoming: "a cache for repeated requests"));
    }

    [Fact]
    public void TrimsRepeatedChineseCharactersAtSegmentBoundary()
    {
        Assert.Equal("减少重复请求", TranscriptDeduplicator.RemoveRepeatedPrefix(
            previous: "这个方案通过缓存提升速度",
            incoming: "缓存提升速度，减少重复请求"));
    }

    [Fact]
    public void KeepsTextWithoutAUsefulOverlap()
    {
        Assert.Equal("A separate question about storage", TranscriptDeduplicator.RemoveRepeatedPrefix(
            previous: "The first topic is caching",
            incoming: "A separate question about storage"));
    }
}
