using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for MeetingEndWatch.swift.</summary>
public sealed class MeetingEndWatchTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static MeetingEndWatch Create(MeetingEndWatch.Mode mode = MeetingEndWatch.Mode.Prompt, double silence = 600)
        => new(new MeetingEndWatch.Config(mode, silence), Start);

    [Fact]
    public void QuietMeetingPromptsBeforeEnding()
    {
        var watch = Create();
        Assert.Null(watch.Tick(Start.AddSeconds(599), said: true));
        Assert.Null(watch.Tick(Start.AddSeconds(600), said: true));
        Assert.NotNull(watch.PendingDeadline);
        Assert.Equal(60, watch.Remaining(Start.AddSeconds(600))!.Value, 3);
        // Still inside the countdown: nothing yet.
        Assert.Null(watch.Tick(Start.AddSeconds(630), said: true));
        Assert.Equal(MeetingEndWatch.Reason.Silence, watch.Tick(Start.AddSeconds(661), said: true));
        Assert.Null(watch.PendingDeadline);
    }

    [Fact]
    public void HearingWordsOrKeepingGoingCancelsTheReminder()
    {
        var watch = Create();
        watch.Tick(Start.AddSeconds(600), said: true);
        Assert.NotNull(watch.PendingDeadline);
        watch.Heard(Start.AddSeconds(610));
        Assert.Null(watch.PendingDeadline);
        Assert.Equal(Start.AddSeconds(610), watch.QuietSince);

        watch.Tick(Start.AddSeconds(1220), said: true);
        Assert.NotNull(watch.PendingDeadline);
        watch.KeepGoing(Start.AddSeconds(1230));
        Assert.Null(watch.PendingDeadline);
        Assert.Equal(Start.AddSeconds(1230), watch.QuietSince);
    }

    [Fact]
    public void EndModeStopsImmediately()
    {
        var watch = Create(MeetingEndWatch.Mode.End);
        Assert.Null(watch.Tick(Start.AddSeconds(599), said: true));
        Assert.Equal(MeetingEndWatch.Reason.Silence, watch.Tick(Start.AddSeconds(600), said: true));
    }

    [Fact]
    public void OffModeNeverEnds()
    {
        var watch = Create(MeetingEndWatch.Mode.Off);
        Assert.Null(watch.Tick(Start.AddHours(5), said: true));
        Assert.Null(watch.PendingDeadline);
    }

    [Fact]
    public void EmptyMeetingStopsQuietlyWithNobodySpoke()
    {
        var watch = Create();
        Assert.Null(watch.Tick(Start.AddSeconds(599), said: false));
        Assert.Equal(MeetingEndWatch.Reason.NobodySpoke, watch.Tick(Start.AddSeconds(600), said: false));
        Assert.Null(watch.PendingDeadline);
    }

    [Fact]
    public void PausedMeetingIsNotCounted()
    {
        var watch = Create(MeetingEndWatch.Mode.End, silence: 60);
        Assert.Null(watch.Tick(Start.AddSeconds(120), said: true, paused: true));
        Assert.Equal(Start.AddSeconds(120), watch.QuietSince);
        // The quiet restarts from the resume moment, not from the start.
        Assert.Null(watch.Tick(Start.AddSeconds(150), said: true));
        Assert.Equal(MeetingEndWatch.Reason.Silence, watch.Tick(Start.AddSeconds(180), said: true));
    }

    [Fact]
    public void ConfigFallsBackToDefaults()
    {
        Assert.Equal(600, MeetingEndWatch.Config.FromSettings(null, null).SilenceAfterSeconds);
        Assert.Equal(MeetingEndWatch.Mode.Prompt, MeetingEndWatch.Config.FromSettings("nonsense", 15).Mode);
        Assert.Equal(900, MeetingEndWatch.Config.FromSettings("end", 15).SilenceAfterSeconds);
        Assert.Equal(MeetingEndWatch.Mode.End, MeetingEndWatch.Config.FromSettings("end", 15).Mode);
        Assert.Equal(600, MeetingEndWatch.Config.FromSettings("prompt", 0).SilenceAfterSeconds);
        Assert.Equal(240 * 60, MeetingEndWatch.Config.FromSettings("prompt", 999).SilenceAfterSeconds);
    }

    [Fact]
    public void EndTimeSitsJustAfterTheLastWords()
    {
        var last = Start.AddMinutes(30);
        var now = Start.AddMinutes(45);
        Assert.Equal(last.AddSeconds(5), MeetingEndWatch.EndTime(last, Start, now));
        // Never before the start.
        Assert.Equal(Start, MeetingEndWatch.EndTime(Start.AddSeconds(-30), Start, now));
        // Never after now.
        Assert.Equal(now, MeetingEndWatch.EndTime(now.AddSeconds(30), Start, now));
        // No words at all: now.
        Assert.Equal(now, MeetingEndWatch.EndTime(null, Start, now));
    }
}
