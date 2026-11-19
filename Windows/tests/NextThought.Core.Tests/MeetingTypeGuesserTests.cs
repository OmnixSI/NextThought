using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for scenario guessing and series matching from titles.</summary>
public sealed class MeetingTypeGuesserTests
{
    [Theory]
    [InlineData("字节跳动 后端二面", SessionMode.Interview)]
    [InlineData("Phone screen with HR", SessionMode.Interview)]
    [InlineData("实验室组会", SessionMode.LabMeeting)]
    [InlineData("小组会议", SessionMode.Meeting)]
    [InlineData("项目组会议", SessionMode.Meeting)]
    [InlineData("课程设计答辩", SessionMode.GroupProject)]
    [InlineData("客户拜访", SessionMode.Client)]
    [InlineData("论文答辩", SessionMode.Academic)]
    [InlineData("讲座 第 3 讲 线性代数", SessionMode.Lesson)]
    [InlineData("小组讨论", SessionMode.Discussion)]
    [InlineData("周会", SessionMode.Meeting)]
    [InlineData("classic music", SessionMode.General)]
    [InlineData("", SessionMode.General)]
    public void GuessUsesOrderedKeywords(string title, SessionMode expected)
    {
        Assert.Equal(expected, MeetingTypeGuesser.Guess(title));
    }

    [Fact]
    public void LabMeetingKeywordExcludesSmallAndProjectMeetings()
    {
        Assert.Equal(SessionMode.LabMeeting, MeetingTypeGuesser.Guess("组会"));
        Assert.Equal(SessionMode.Meeting, MeetingTypeGuesser.Guess("小组会议"));
        Assert.Equal(SessionMode.Meeting, MeetingTypeGuesser.Guess("项目组会议"));
    }

    [Fact]
    public void SeriesBaseTitleRemovesRoundMarkers()
    {
        Assert.Equal("字节跳动 · 后端", SeriesMatcher.BaseTitle("字节跳动 · 后端二面"));
        Assert.Equal("线性代数", SeriesMatcher.BaseTitle("第 3 讲 线性代数"));
        Assert.Equal("lecture", SeriesMatcher.BaseTitle("Lecture 4"));
        Assert.Equal("复盘", SeriesMatcher.BaseTitle("Q3 复盘"));
        Assert.Equal("面经", SeriesMatcher.BaseTitle("面经 第 2 轮"));
    }

    [Fact]
    public void SeriesSimilarityIgnoresRoundMarkers()
    {
        Assert.True(SeriesMatcher.Similarity("字节跳动 · 后端二面", "字节跳动 · 后端") > 0.6);
        Assert.True(SeriesMatcher.Similarity("字节跳动 · 后端三面", "字节跳动 · 后端") > 0.6);
        Assert.True(SeriesMatcher.Similarity("字节跳动 · 后端", "完全不同的系列") < 0.3);
        Assert.Equal(0, SeriesMatcher.Similarity("a", "b"));
    }

    [Fact]
    public void BestMatchPrefersTheClosestSeries()
    {
        var backend = Guid.NewGuid();
        var frontend = Guid.NewGuid();
        var candidates = new[]
        {
            new SeriesMatcher.Candidate(backend, "字节跳动 · 后端", SessionMode.Interview, DateTimeOffset.UtcNow.AddDays(-2)),
            new SeriesMatcher.Candidate(frontend, "字节跳动 · 前端", SessionMode.Interview, DateTimeOffset.UtcNow)
        };
        Assert.Equal(backend, SeriesMatcher.BestMatch("字节跳动 · 后端三面", SessionMode.Interview, candidates));
    }

    [Fact]
    public void BestMatchBreaksTiesByModeThenRecency()
    {
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var candidates = new[]
        {
            new SeriesMatcher.Candidate(older, "项目 A", SessionMode.Meeting, DateTimeOffset.UtcNow.AddDays(-3)),
            new SeriesMatcher.Candidate(newer, "项目 A", SessionMode.Meeting, DateTimeOffset.UtcNow)
        };
        Assert.Equal(newer, SeriesMatcher.BestMatch("项目 A", SessionMode.Meeting, candidates));

        var interview = Guid.NewGuid();
        var meeting = Guid.NewGuid();
        var sameTitle = new[]
        {
            new SeriesMatcher.Candidate(meeting, "项目 A", SessionMode.Meeting, DateTimeOffset.UtcNow),
            new SeriesMatcher.Candidate(interview, "项目 A", SessionMode.Interview, DateTimeOffset.UtcNow.AddDays(-1))
        };
        Assert.Equal(interview, SeriesMatcher.BestMatch("项目 A", SessionMode.Interview, sameTitle));
    }

    [Fact]
    public void BestMatchReturnsNullBelowTheMinimum()
    {
        var candidates = new[]
        {
            new SeriesMatcher.Candidate(Guid.NewGuid(), "字节跳动 · 后端", SessionMode.Interview, DateTimeOffset.UtcNow)
        };
        Assert.Null(SeriesMatcher.BestMatch("完全无关的标题", null, candidates));
        Assert.Null(SeriesMatcher.BestMatch("字节跳动 · 后端", null, []));
    }
}
