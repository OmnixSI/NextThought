using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B12: translated from MeetingTypeAndNotesTests.swift relativeDatesBecomeDates.</summary>
public sealed class NotesDatesParityTests
{
    private static readonly TimeZoneInfo East8 = TimeZoneInfo.CreateCustomTimeZone("east8", TimeSpan.FromHours(8), "east8", "east8");
    private static readonly DateTimeOffset Monday = new(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(8));

    private static int[]? Ymd(string phrase, DateTimeOffset? reference = null) =>
        NotesDates.Resolve(phrase, reference ?? Monday, East8) is { } date ? [date.Year, date.Month, date.Day] : null;

    [Fact]
    public void RelativeDatesBecomeDates()
    {
        Assert.Equal([2026, 10, 26], Ymd("今天"));
        Assert.Equal([2026, 10, 27], Ymd("明天上午"));
        Assert.Equal([2026, 10, 28], Ymd("后天"));
        Assert.Equal([2026, 10, 29], Ymd("周四交付"));
        Assert.Equal([2026, 10, 27], Ymd("周二下午"));
        Assert.Equal([2026, 10, 30], Ymd("本周五"));
        Assert.Equal([2026, 10, 29], Ymd("这周四"));
        Assert.Equal([2026, 11, 2], Ymd("下周一前"));
        Assert.Equal([2026, 11, 1], Ymd("星期天"));
        Assert.Equal([2026, 10, 30], Ymd("10月30日"));
        Assert.Equal([2026, 10, 30], Ymd("10 月 30 号"));
        Assert.Equal([2026, 10, 31], Ymd("月底"));
        Assert.Equal([2026, 11, 2], Ymd("next Monday"));
        Assert.Equal([2026, 10, 30], Ymd("by Friday"));
        Assert.Equal([2026, 10, 27], Ymd("tomorrow"));
        Assert.Null(Ymd("尽快"));
        Assert.Null(Ymd("下周"));
        Assert.Equal("10 月 30 日（周五）", NotesDates.Format(new DateTime(2026, 10, 30), false));
        Assert.Equal("Fri, Oct 30", NotesDates.Format(new DateTime(2026, 10, 30), true));
    }

    [Fact]
    public void RelativeDatesUseTheLocalCalendarDayAndFullWidthDigits()
    {
        // 07:00 on the 26th in UTC+8 is still 23:00 on the 25th in UTC.
        var utcReference = new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal([2026, 10, 27], Ymd("明天", utcReference));
        Assert.Equal([2026, 10, 26], Ymd("今天", utcReference));
        Assert.Equal([2026, 10, 30], Ymd("１０月３０日"));
        Assert.Equal([2026, 10, 27], Ymd("tomorrow", utcReference));
    }
}
