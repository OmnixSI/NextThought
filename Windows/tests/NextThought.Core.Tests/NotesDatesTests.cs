using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for NotesDates (NotesTemplates.swift).</summary>
public sealed class NotesDatesTests
{
    // 2026-11-18 is a Wednesday.
    private static readonly DateTimeOffset Wednesday = new(2026, 11, 18, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void TodayTomorrowAndTheDayAfter()
    {
        Assert.Equal(new DateTime(2026, 11, 18), NotesDates.Resolve("今天", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 19), NotesDates.Resolve("明天下午", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 20), NotesDates.Resolve("后天", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 21), NotesDates.Resolve("大后天", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 19), NotesDates.Resolve("tomorrow", Wednesday));
    }

    [Fact]
    public void EndOfMonthIsTheLastDay()
    {
        Assert.Equal(new DateTime(2026, 11, 30), NotesDates.Resolve("月底前", Wednesday));
        Assert.Equal(new DateTime(2026, 2, 28), NotesDates.Resolve("end of the month", new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void MonthAndDayRollsToNextYearWhenLongPast()
    {
        Assert.Equal(new DateTime(2026, 11, 30), NotesDates.Resolve("11月30日", Wednesday));
        Assert.Equal(new DateTime(2027, 3, 5), NotesDates.Resolve("3 月 5 号", Wednesday));
    }

    [Fact]
    public void WeekdaysCountFromTheMeetingDay()
    {
        // A bare weekday is the next one to come, today counts.
        Assert.Equal(new DateTime(2026, 11, 19), NotesDates.Resolve("周四", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 18), NotesDates.Resolve("周三", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 23), NotesDates.Resolve("下周一", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 16), NotesDates.Resolve("本周一", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 26), NotesDates.Resolve("next Thursday", Wednesday));
        Assert.Equal(new DateTime(2026, 11, 20), NotesDates.Resolve("friday", Wednesday));
    }

    [Fact]
    public void VaguePhrasesStayUnresolved()
    {
        Assert.Null(NotesDates.Resolve("尽快", Wednesday));
        Assert.Null(NotesDates.Resolve("下周", Wednesday));
        Assert.True(NotesDates.LooksLikeDate("下周"));
        Assert.False(NotesDates.LooksLikeDate("尽快"));
    }

    [Fact]
    public void FormatMatchesTheNotesStyle()
    {
        Assert.Equal("11 月 19 日（周四）", NotesDates.Format(new DateTime(2026, 11, 19)));
        Assert.Equal("Thu, Nov 19", NotesDates.Format(new DateTime(2026, 11, 19), english: true));
    }
}
