using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for screen references, screen context and text similarity.</summary>
public sealed class ScreenContextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData("这一页讲了什么", true)]
    [InlineData("what is this slide", true)]
    [InlineData("今天天气不错", false)]
    public void DetectsScreenReferences(string question, bool expected)
    {
        Assert.Equal(expected, ScreenReference.IsReference(question));
    }

    [Theory]
    [InlineData("这个图怎么画", true)]
    [InlineData("hello world", false)]
    public void DetectsImageNeeds(string question, bool expected)
    {
        Assert.Equal(expected, ScreenReference.NeedsImage(question));
    }

    [Fact]
    public void ScreenContextPrefersEnhancedTextAndReportsConfidence()
    {
        var context = new ScreenContext
        {
            Regions = [new ScreenTextRegion("a", [0], 0.9), new ScreenTextRegion("b", [1], 0.95)]
        };
        Assert.Equal("a\nb", context.Text);
        Assert.False(context.LowConfidence);
        Assert.Equal("enhanced", (context with { EnhancedText = "enhanced" }).Text);
        Assert.True((context with { Regions = [new ScreenTextRegion("x", [0], 0.5)] }).LowConfidence);
        Assert.True((context with { Regions = [] }).LowConfidence);
    }

    [Fact]
    public void FreshnessUsesCaptureAge()
    {
        var context = new ScreenContext { CapturedAt = Now };
        Assert.True(context.IsFresh(Now.AddSeconds(1)));
        Assert.False(context.IsFresh(Now.AddSeconds(3)));
        Assert.False(context.IsFresh(Now.AddSeconds(-1)));
    }

    [Fact]
    public void TextSimilarityCountsBigrams()
    {
        Assert.Equal("abcd", new string(TextSimilarity.Normalized("A, b-c d!").ToArray()));
        Assert.Equal(3, TextSimilarity.Bigrams("abcd").Count);
        Assert.Single(TextSimilarity.Bigrams("a"));
        Assert.Empty(TextSimilarity.Bigrams(""));
    }

    [Fact]
    public void JaccardAndContainmentBehaveAtTheEdges()
    {
        var a = new HashSet<string> { "ab", "bc" };
        var b = new HashSet<string> { "ab", "cd" };
        Assert.Equal(1, TextSimilarity.Jaccard(a, a));
        Assert.Equal(0, TextSimilarity.Jaccard(a, new HashSet<string> { "xy" }));
        Assert.Equal(1.0 / 3.0, TextSimilarity.Jaccard(a, b), 5);
        Assert.Equal(0.5, TextSimilarity.Containment(a, b));
        Assert.Equal(0, TextSimilarity.Containment([], a));
    }

    [Fact]
    public void RelevanceNeedsEnoughSpeechOnScreen()
    {
        const string speech = "the gain is promising but one seed is not enough";
        const string screen = "the gain is promising but one seed is not enough to conclude";
        Assert.True(TextSimilarity.Relevant(screen, speech));
        Assert.False(TextSimilarity.Relevant(screen, "too short"));
        Assert.False(TextSimilarity.Relevant("something unrelated entirely", speech));
        Assert.True(ScreenReference.IsRelated("这一页", new ScreenContext { CapturedAt = Now }));
    }
}
