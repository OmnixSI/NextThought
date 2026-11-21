using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>E7: the answer-progress helpers (AnswerPoints.swift) used by the insights strip.</summary>
public sealed class AnswerPointsInsightsTests
{
    [Fact]
    public void SplitCutsTheAnswerIntoSayablePieces()
    {
        // Mac splits on sentence enders, but a "." only when the piece is long enough (40+ chars),
        // so the first two short sentences stay together and the third one is its own point.
        var points = AnswerPoints.Split("We update the database first. Then we delete the cache. If the delete fails a retry queue covers it.");
        Assert.Equal(2, points.Count);
        Assert.StartsWith("We update the database first", points[0]);
        Assert.Contains("Then we delete the cache", points[0]);
        Assert.StartsWith("If the delete fails", points[1]);
    }

    [Fact]
    public void SplitRespectsTheLimitAndDropsMarkup()
    {
        var answer = "**Bold lead.** " + string.Join(" ", Enumerable.Range(0, 20).Select(index => "Sentence " + index + "."));
        var points = AnswerPoints.Split(answer, 4);
        Assert.Equal(4, points.Count);
        Assert.DoesNotContain("**", points[0]);
        Assert.DoesNotContain("`", points[0]);
    }

    [Fact]
    public void NextFindsTheFirstPointThatHasNotBeenSaid()
    {
        var covered = new HashSet<int> { 0, 1 };
        Assert.Equal(2, AnswerPoints.Next(covered, 4));
        Assert.Null(AnswerPoints.Next(new HashSet<int> { 0, 1, 2, 3 }, 4));
        Assert.Equal(0, AnswerPoints.Next(new HashSet<int>(), 4));
    }

    [Fact]
    public void HeuristicCoveredMatchesTheSpokenTerms()
    {
        const string point = "We update the database first and then delete the cache.";
        Assert.True(AnswerPoints.HeuristicCovered(point, "so we update the database first, then delete the cache"));
        Assert.False(AnswerPoints.HeuristicCovered(point, "the weather is nice today"));
    }

    [Fact]
    public void ChinesePointsCountCharacterPairs()
    {
        const string point = "先更新数据库，再删除缓存，删除失败进入重试队列。";
        Assert.True(AnswerPoints.HeuristicCovered(point, "我们是这样做的：先更新数据库，再删除缓存，删除失败进入重试队列。"));
        Assert.False(AnswerPoints.HeuristicCovered(point, "今天天气不错，我们出去走走。"));
    }

    [Fact]
    public void BilingualGlossesAreNotReadAloud()
    {
        var answer = "The cache is deleted after the write.\n> 译：写入之后再删除缓存。\n";
        var points = AnswerPoints.Split(BilingualAnswer.Spoken(answer));
        Assert.Single(points);
        Assert.DoesNotContain("译", points[0]);
    }
}
