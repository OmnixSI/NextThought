using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J3: port of MeetingStatsTests (the stats checks live in LiveNotesTests.swift's
/// recordingTimeAndQuestionsAddUpByMonthAndBySeries) — the numbers must match the Mac rules:
/// meetings/seconds/questions/answers, month window, series filter, floor minutes.
/// </summary>
public sealed class MeetingStatsTests
{
    private static SessionMeta Meeting(int day, double minutes, int questions, int answers,
        Guid? series = null, bool ended = true, int month = 11)
    {
        var start = new DateTimeOffset(2026, month, day, 10, 0, 0, TimeSpan.Zero);
        return new SessionMeta
        {
            Id = Guid.NewGuid(),
            Title = "test",
            StartedAt = start,
            EndedAt = ended ? start.AddMinutes(minutes) : null,
            QuestionCount = questions,
            AnswerCount = answers,
            WorkspaceId = series?.ToString("D").ToUpperInvariant()
        };
    }

    [Fact]
    public void DurationTextFollowsTheMacWording()
    {
        Assert.Equal("1 小时 32 分", MeetingStatsText.Duration(new MeetingStats(Meetings: 1, Seconds: 92 * 60 + 30)));
        Assert.Equal("42 分钟", MeetingStatsText.Duration(new MeetingStats(Meetings: 1, Seconds: 42 * 60 + 10)));
        Assert.Equal("不到 1 分钟", MeetingStatsText.Duration(new MeetingStats(Meetings: 1, Seconds: 40)));
        Assert.Equal("0 分钟", MeetingStatsText.Duration(new MeetingStats()));
    }

    [Fact]
    public void SeriesStatsLineMatchesTheMacSeriesView()
    {
        var series = Guid.NewGuid();
        var metas = new[]
        {
            Meeting(3, minutes: 62, questions: 4, answers: 2, series: series),
            Meeting(18, minutes: 30.7, questions: 9, answers: 9, series: series)
        };
        Assert.Equal("共 2 场 · 1 小时 32 分 · 提问 13 次 · 回答 11 次",
            MeetingStatsText.SeriesLine(MeetingStats.Of(metas, series: series)));
        Assert.Equal("", MeetingStatsText.SeriesLine(MeetingStats.Of(metas, series: Guid.NewGuid())));
    }
}
