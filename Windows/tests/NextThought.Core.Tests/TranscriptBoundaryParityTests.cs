using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B21: translated from QuestionDetector/Deduplicator/SpeakerClustering boundary tests.</summary>
public sealed class TranscriptBoundaryParityTests
{
    [Fact]
    public void FollowUpTrimmingRemovesAllPunctuation()
    {
        Assert.True(QuestionDetector.IsShortFollowUp("为什么……"));
        Assert.True(QuestionDetector.IsShortFollowUp("「为什么？」"));
        Assert.True(QuestionIntentClassifier.Assess("为什么？", MeetingMode.Meeting, false).Intent == UtteranceIntent.FollowUp);
    }

    [Fact]
    public void DeduplicationTrimsPunctuationButKeepsSymbols()
    {
        Assert.Equal("→后面", TranscriptDeduplicator.RemoveRepeatedPrefix("我们先看缓存", "我们先看缓存→后面"));
        Assert.Equal("后面", TranscriptDeduplicator.RemoveRepeatedPrefix("我们先看缓存", "我们先看缓存，后面"));
    }

    [Fact]
    public void SpeakerNamesTruncateByGraphemeCluster()
    {
        const string family = "👩‍👩‍👧‍👦";
        var longName = string.Concat(Enumerable.Repeat(family, 30));
        var cleaned = SpeakerLabels.CleanName(longName)!;
        Assert.Equal(24, System.Globalization.StringInfo.ParseCombiningCharacters(cleaned).Length);
        Assert.Equal(family, cleaned[..family.Length]);
        Assert.EndsWith(family, cleaned);
    }

    [Fact]
    public void IdeographicRangesIncludeCjkExtensionAndCompatibility()
    {
        Assert.True(SpeakerLabels.Substantive(new string('\u3400', 4)));
        Assert.True(SpeakerLabels.Substantive(new string('\uF900', 4)));
        Assert.True(SpeakerLabels.Substantive("ああああ"));
        Assert.True(SpeakerLabels.Substantive("가나다라"));
    }
}
