using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for TranscriptDeduplicator.swift.</summary>
public sealed class TranscriptDeduplicatorTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IdenticalSentencesCollapse()
    {
        Assert.Equal("", TranscriptDeduplicator.RemoveRepeatedPrefix("we should check the data", "We should check the data"));
    }

    [Fact]
    public void OverlappingLatinPrefixIsRemoved()
    {
        var result = TranscriptDeduplicator.RemoveRepeatedPrefix(
            "so the residual is about twelve percent", "the residual is about twelve percent of the signal");
        Assert.Equal("of the signal", result);
    }

    [Fact]
    public void TwoTokenOverlapIsEnough()
    {
        Assert.Equal("numbers look fine", TranscriptDeduplicator.RemoveRepeatedPrefix("so hello world", "hello world numbers look fine"));
    }

    [Fact]
    public void SingleTokenOverlapIsKept()
    {
        // A one-word overlap is too weak to prove a repeat.
        Assert.Equal("world numbers look fine", TranscriptDeduplicator.RemoveRepeatedPrefix("hello", "world numbers look fine"));
    }

    [Fact]
    public void CjkOverlapIsRemoved()
    {
        var result = TranscriptDeduplicator.RemoveRepeatedPrefix("我们先把温度补偿做完", "温度补偿做完之后再看残差");
        Assert.Equal("之后再看残差", result);
    }

    [Fact]
    public void ShortCjkOverlapIsKept()
    {
        Assert.Equal("好的我们继续", TranscriptDeduplicator.RemoveRepeatedPrefix("那就这样", "好的我们继续"));
    }

    [Fact]
    public void TimeBasedOverlapOnlyAppliesWhenSpansOverlap()
    {
        Assert.Equal("新的句子", TranscriptDeduplicator.RemoveOverlappingPrefix("上一句", "新的句子", Base, Base.AddSeconds(-5)));
        Assert.Equal("上一句的结尾", TranscriptDeduplicator.RemoveOverlappingPrefix("上一句", "上一句的结尾", Base.AddSeconds(3), Base.AddSeconds(1)));
    }

    [Fact]
    public void EmptyInputsAreSafe()
    {
        Assert.Equal("", TranscriptDeduplicator.RemoveRepeatedPrefix("", ""));
        Assert.Equal("only", TranscriptDeduplicator.RemoveRepeatedPrefix("", "  only  "));
    }
}
