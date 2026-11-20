using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from MeetingEndWatchTests.swift: silence (recognised words only) ends a meeting
/// nobody ended. Times are seconds after <c>t0</c>; the defaults are 600 s of silence and a 60 s
/// reminder.
/// </summary>
public sealed class MeetingEndWatchParityTests
{
    // Date(timeIntervalSinceReferenceDate: 1_000_000) in the Swift tests.
    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(1_000_000);

    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    private static MeetingEndWatch Watch(MeetingEndWatch.Mode mode = MeetingEndWatch.Mode.Prompt) =>
        new(new MeetingEndWatch.Config(mode), T0);

    [Fact]
    public void DefaultsAreTenMinutesAndOneMinute()
    {
        var config = new MeetingEndWatch.Config();
        Assert.Equal(MeetingEndWatch.Mode.Prompt, config.Mode);
        Assert.Equal(600, config.SilenceAfterSeconds);
        Assert.Equal(60, config.CountdownSeconds);
    }

    [Fact]
    public void DoesNothingBeforeTheSilenceTime()
    {
        var watch = Watch();
        Assert.Null(watch.Tick(At(1), said: true));
        Assert.Null(watch.Tick(At(599), said: true));
        Assert.Null(watch.PendingDeadline);
    }

    [Fact]
    public void SilenceStartsTheReminder()
    {
        var watch = Watch();
        // 600 s without words: the reminder starts and ends at 600 + 60.
        Assert.Null(watch.Tick(At(600), said: true));
        Assert.Equal(At(660), watch.PendingDeadline);
        Assert.Equal(30.0, watch.Remaining(At(630))!.Value);
        Assert.Null(watch.Tick(At(659), said: true));
        Assert.NotNull(watch.PendingDeadline);
    }

    [Fact]
    public void SpeakingDuringTheCountdownCancelsIt()
    {
        var watch = Watch();
        watch.Tick(At(600), said: true);
        watch.Heard(At(630));
        Assert.Null(watch.PendingDeadline);
        // Counting again from 630: nothing at 660 or 1229, the reminder at 1230 (630 + 600).
        Assert.Null(watch.Tick(At(660), said: true));
        Assert.Null(watch.Tick(At(1229), said: true));
        Assert.Null(watch.PendingDeadline);
        Assert.Null(watch.Tick(At(1230), said: true));
        Assert.Equal(At(1290), watch.PendingDeadline);
    }

    [Fact]
    public void WordsBeforeTheReminderPushItBack()
    {
        var watch = Watch();
        watch.Heard(At(300));
        Assert.Null(watch.Tick(At(899), said: true));
        Assert.Null(watch.PendingDeadline);
        Assert.Null(watch.Tick(At(900), said: true));
        Assert.Equal(At(960), watch.PendingDeadline);
    }

    [Fact]
    public void EndsWhenTheCountdownRunsOut()
    {
        var watch = Watch();
        watch.Tick(At(600), said: true);
        Assert.Equal(MeetingEndWatch.Reason.Silence, watch.Tick(At(660), said: true));
        Assert.Null(watch.PendingDeadline);
        // A stop that didn't happen doesn't fire again at once: the quiet restarted at 660.
        Assert.Null(watch.Tick(At(661), said: true));
        Assert.Null(watch.PendingDeadline);
        Assert.Null(watch.Tick(At(1259), said: true));
    }

    [Fact]
    public void KeepGoingStartsTheCountAgain()
    {
        var watch = Watch();
        watch.Tick(At(600), said: true);
        watch.KeepGoing(At(620));
        Assert.Null(watch.PendingDeadline);
        Assert.Null(watch.Tick(At(1219), said: true));
        Assert.Null(watch.PendingDeadline);
        Assert.Null(watch.Tick(At(1220), said: true));
        Assert.Equal(At(1280), watch.PendingDeadline);
    }

    [Fact]
    public void EndModeEndsWithoutAReminder()
    {
        var watch = Watch(MeetingEndWatch.Mode.End);
        Assert.Null(watch.Tick(At(599), said: true));
        Assert.Equal(MeetingEndWatch.Reason.Silence, watch.Tick(At(600), said: true));
        Assert.Null(watch.PendingDeadline);
    }

    [Fact]
    public void OffModeNeverEnds()
    {
        var watch = Watch(MeetingEndWatch.Mode.Off);
        Assert.Null(watch.Tick(At(600), said: true));
        Assert.Null(watch.Tick(At(100_000), said: true));
        Assert.Null(watch.Tick(At(200_000), said: false));
        Assert.Null(watch.PendingDeadline);
    }

    // Not translated: switchingToOffWithdrawsTheReminder mutates `watch.config.mode = .off` on a live
    // watch. The Windows MeetingEndWatch exposes only a read-only `CurrentConfig` (the private Config
    // field has no setter), so the mid-meeting mode change cannot be expressed; no API to report
    // beyond a mutable config / SetMode on MeetingEndWatch.

    [Fact]
    public void PausedTimeIsNotCounted()
    {
        var watch = Watch();
        // Paused from 100 to 1000: the quiet restarts at the last paused tick.
        Assert.Null(watch.Tick(At(100), said: true, paused: true));
        Assert.Null(watch.Tick(At(1000), said: true, paused: true));
        Assert.Null(watch.Tick(At(1001), said: true));
        Assert.Null(watch.Tick(At(1599), said: true));
        Assert.Null(watch.PendingDeadline);
        // 1000 + 600.
        Assert.Null(watch.Tick(At(1600), said: true));
        Assert.Equal(At(1660), watch.PendingDeadline);
    }

    [Fact]
    public void PausingWithdrawsAShowingReminder()
    {
        var watch = Watch();
        watch.Tick(At(600), said: true);
        Assert.NotNull(watch.PendingDeadline);
        Assert.Null(watch.Tick(At(610), said: true, paused: true));
        Assert.Null(watch.PendingDeadline);
        // Resumed at 611: not ended at the old deadline 660, and no new reminder before 610 + 600.
        Assert.Null(watch.Tick(At(611), said: true));
        Assert.Null(watch.Tick(At(700), said: true));
        Assert.Null(watch.PendingDeadline);
    }

    [Fact]
    public void NobodySpokeStopsQuietlyWithoutAReminder()
    {
        var watch = Watch();
        Assert.Null(watch.Tick(At(599), said: false));
        Assert.Null(watch.PendingDeadline);
        Assert.Equal(MeetingEndWatch.Reason.NobodySpoke, watch.Tick(At(600), said: false));
        Assert.Null(watch.PendingDeadline);
        var direct = Watch(MeetingEndWatch.Mode.End);
        Assert.Equal(MeetingEndWatch.Reason.NobodySpoke, direct.Tick(At(600), said: false));
    }

    [Fact]
    public void AMeetingThatNeverSpokeRespectsPause()
    {
        var watch = Watch();
        Assert.Null(watch.Tick(At(500), said: false, paused: true));
        Assert.Null(watch.Tick(At(1000), said: false));
        Assert.Null(watch.Tick(At(1099), said: false));
        Assert.Equal(MeetingEndWatch.Reason.NobodySpoke, watch.Tick(At(1100), said: false));
    }

    [Fact]
    public void CustomTimesAreUsed()
    {
        var watch = new MeetingEndWatch(new MeetingEndWatch.Config(MeetingEndWatch.Mode.Prompt, 300, 30), T0);
        Assert.Null(watch.Tick(At(299), said: true));
        Assert.Null(watch.Tick(At(300), said: true));
        Assert.Equal(At(330), watch.PendingDeadline);
        Assert.Equal(MeetingEndWatch.Reason.Silence, watch.Tick(At(330), said: true));
    }

    [Fact]
    public void SettingsMapToAConfig()
    {
        Assert.Equal(new MeetingEndWatch.Config(), MeetingEndWatch.Config.FromSettings(null, null));
        Assert.Equal(new MeetingEndWatch.Config(MeetingEndWatch.Mode.End, 1_200, 60),
            MeetingEndWatch.Config.FromSettings("end", 20));
        Assert.Equal(MeetingEndWatch.Mode.Off, MeetingEndWatch.Config.FromSettings("off", 5).Mode);
        // Unknown or nonsensical values fall back instead of misfiring.
        Assert.Equal(new MeetingEndWatch.Config(), MeetingEndWatch.Config.FromSettings("banana", 0));
        Assert.Equal(600, MeetingEndWatch.Config.FromSettings("prompt", -3).SilenceAfterSeconds);
        Assert.Equal(14_400, MeetingEndWatch.Config.FromSettings("prompt", 100_000).SilenceAfterSeconds);
        Assert.Contains(MeetingEndWatch.Config.DefaultMinutes, MeetingEndWatch.Config.MinuteChoices);
    }

    // MARK: End time

    [Fact]
    public void EndTimeIsTheLastLineNotNow()
    {
        // Last words ended at 1000; now is 1700 (after 600 s of quiet and a 60 s reminder): 1000 + 5.
        Assert.Equal(At(1005), MeetingEndWatch.EndTime(At(1000), T0, At(1700)));
        Assert.Equal(At(1000), MeetingEndWatch.EndTime(At(1000), T0, At(1700), padding: 0));
    }

    [Fact]
    public void EndTimeStaysInsideTheMeeting()
    {
        // Never later than now.
        Assert.Equal(At(1702), MeetingEndWatch.EndTime(At(1699), T0, At(1702)));
        Assert.Equal(At(1702), MeetingEndWatch.EndTime(At(1701), T0, At(1702)));
        // Never before it started.
        Assert.Equal(T0, MeetingEndWatch.EndTime(At(-10), T0, At(900)));
        // No words at all: now.
        Assert.Equal(At(900), MeetingEndWatch.EndTime(null, T0, At(900)));
    }
}
