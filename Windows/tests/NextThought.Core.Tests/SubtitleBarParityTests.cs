using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from SubtitleBarTests.swift: the subtitle bar's window modes, when the bar is up,
/// which sentence it shows and how the text is cut to fit.
/// </summary>
public sealed class SubtitleBarParityTests
{
    // Date(timeIntervalSinceReferenceDate: 1_000) in the Swift tests.
    private static readonly DateTimeOffset Origin =
        new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(1_000);

    private static DateTimeOffset At(double seconds) => Origin.AddSeconds(seconds);

    // MARK: Window mode

    [Fact]
    public void ModesCycleNotesSubtitlesHiddenAndBack()
    {
        Assert.Equal(LiveWindowMode.Subtitles, LiveWindowMode.Notes.Next());
        Assert.Equal(LiveWindowMode.Hidden, LiveWindowMode.Subtitles.Next());
        Assert.Equal(LiveWindowMode.Notes, LiveWindowMode.Hidden.Next());
        var allCases = new[] { LiveWindowMode.Notes, LiveWindowMode.Subtitles, LiveWindowMode.Hidden };
        Assert.Equal(allCases, Enum.GetValues<LiveWindowMode>());
        // Three presses are a full circle from any start.
        foreach (var mode in Enum.GetValues<LiveWindowMode>()) Assert.Equal(mode, mode.Next().Next().Next());
    }

    [Fact]
    public void ModeValuesAreStable()
    {
        // Swift's raw values are what gets stored; the Windows record stores the enum as the same
        // camel-case strings through JsonDefaults (JsonStringEnumConverter with camel case).
        Assert.Equal("\"notes\"", JsonSerializer.Serialize(LiveWindowMode.Notes, JsonDefaults.Options));
        Assert.Equal("字幕条", LiveWindowMode.Subtitles.Title());
        var data = JsonSerializer.SerializeToUtf8Bytes(Enum.GetValues<LiveWindowMode>(), JsonDefaults.Options);
        Assert.Equal(Enum.GetValues<LiveWindowMode>(), JsonSerializer.Deserialize<LiveWindowMode[]>(data, JsonDefaults.Options));
    }

    // MARK: Visibility

    [Fact]
    public void BarShowsForFourSecondsThenFadesAndGoes()
    {
        var timing = SubtitleBarTiming.Standard;
        Assert.Equal(4.0, timing.HoldSeconds);
        Assert.Equal(0.5, timing.FadeSeconds);
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(At(0), null));
        Assert.Equal(0, timing.OpacityAt(At(0), null));

        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(At(0), At(0)));
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(At(4), At(0)));
        Assert.Equal(1, timing.OpacityAt(At(4), At(0)));

        Assert.Equal(SubtitleBarTiming.Phase.Fading, timing.PhaseAt(At(4.25), At(0)));
        Assert.True(Math.Abs(timing.OpacityAt(At(4.25), At(0)) - 0.5) < 1e-9);

        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(At(4.5), At(0)));
        Assert.Equal(0, timing.OpacityAt(At(4.5), At(0)));
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(At(60), At(0)));
    }

    [Fact]
    public void NewTextBringsTheBarBack()
    {
        var timing = SubtitleBarTiming.Standard;
        // Faded out after the first sentence, visible again right after the next one.
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(At(10), At(0)));
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(At(10), At(10)));
    }

    [Fact]
    public void AClockStepBackwardsDoesNotHideTheBar()
    {
        var timing = SubtitleBarTiming.Standard;
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(At(0), At(5)));
        Assert.Equal(1, timing.OpacityAt(At(0), At(5)));
    }

    [Fact]
    public void ZeroFadeNeverDividesByZero()
    {
        var timing = new SubtitleBarTiming(holdSeconds: 2, fadeSeconds: 0);
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(At(2), At(0)));
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(At(2.1), At(0)));
        Assert.Equal(0, timing.OpacityAt(At(2.1), At(0)));
        Assert.Equal(new SubtitleBarTiming(holdSeconds: 0, fadeSeconds: 0),
            new SubtitleBarTiming(holdSeconds: -3, fadeSeconds: -1));
    }

    // MARK: Which sentence

    private static SubtitleChannel[] Channels(string remote = "", string remoteTranslation = "",
        string me = "", string meTranslation = "") =>
    [
        new SubtitleChannel("remote", remote, remoteTranslation),
        new SubtitleChannel("me", me, meTranslation)
    ];

    [Fact]
    public void ALineFromBeforeTheBarExistedIsNotNews()
    {
        var feed = new SubtitleFeed();
        feed.Update(Channels(), new SubtitleLine("remote", "old line"), At(100));
        Assert.Null(feed.LastActivity);
        Assert.Equal("old line", feed.Line?.Text);
        Assert.False(feed.Line?.IsPartial);
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, SubtitleBarTiming.Standard.PhaseAt(At(100), feed.LastActivity));
    }

    [Fact]
    public void AHalfSentenceCountsAndEachRevisionKeepsTheBarUp()
    {
        var feed = new SubtitleFeed();
        var old = new SubtitleLine("remote", "old line");
        feed.Update(Channels(), old, At(100));
        feed.Update(Channels(remote: "hello"), old, At(101));
        Assert.Equal(At(101), feed.LastActivity);
        Assert.Equal(new SubtitleLine("remote", "hello", "", true), feed.Line);

        // The same text again is no news.
        feed.Update(Channels(remote: "hello"), old, At(102));
        Assert.Equal(At(101), feed.LastActivity);

        // A longer partial with a translation is.
        feed.Update(Channels(remote: "hello wor", remoteTranslation: "你好"), old, At(103));
        Assert.Equal(At(103), feed.LastActivity);
        Assert.Equal(new SubtitleLine("remote", "hello wor", "你好", true), feed.Line);
    }

    [Fact]
    public void WhoeverSpokeLastIsShown()
    {
        var feed = new SubtitleFeed();
        var old = new SubtitleLine("remote", "old line");
        feed.Update(Channels(), old, At(100));
        feed.Update(Channels(remote: "hello wor"), old, At(103));
        feed.Update(Channels(remote: "hello wor", me: "hi"), old, At(104));
        Assert.Equal("me", feed.Line?.Speaker);
        Assert.Equal("hi", feed.Line?.Text);

        // Nothing changes: the later speaker stays.
        feed.Update(Channels(remote: "hello wor", me: "hi"), old, At(105));
        Assert.Equal("me", feed.Line?.Speaker);
        Assert.Equal(At(104), feed.LastActivity);

        // The other side goes on talking and takes the bar back.
        feed.Update(Channels(remote: "hello wor!", me: "hi"), old, At(106));
        Assert.Equal("remote", feed.Line?.Speaker);
        Assert.Equal("hello wor!", feed.Line?.Text);
        Assert.Equal(At(106), feed.LastActivity);
    }

    [Fact]
    public void AFinishedLineStaysUntilTheBarFadesAndItsLateTranslationRenewsIt()
    {
        var feed = new SubtitleFeed();
        var old = new SubtitleLine("remote", "old line");
        feed.Update(Channels(), old, At(100));
        feed.Update(Channels(remote: "hello world"), old, At(106));

        // The recognizer finishes: the partial is gone, the finished line takes over.
        var finished = new SubtitleLine("remote", "hello world");
        feed.Update(Channels(), finished, At(107));
        Assert.Equal(finished, feed.Line);
        Assert.Equal(At(107), feed.LastActivity);

        // Its translation arrives two seconds later.
        var translated = new SubtitleLine("remote", "hello world", "你好世界");
        feed.Update(Channels(), translated, At(109));
        Assert.Equal(translated, feed.Line);
        Assert.Equal(At(109), feed.LastActivity);
    }

    [Fact]
    public void TheFirstFinishedLineAfterAnEmptyStartCounts()
    {
        var feed = new SubtitleFeed();
        feed.Update(Channels(), null, At(200));
        Assert.Null(feed.Line);
        Assert.Null(feed.LastActivity);
        feed.Update(Channels(), new SubtitleLine("remote", "first"), At(201));
        Assert.Equal("first", feed.Line?.Text);
        Assert.Equal(At(201), feed.LastActivity);
    }

    [Fact]
    public void APartialAlreadyUnderwayAtStartCounts()
    {
        var feed = new SubtitleFeed();
        feed.Update(Channels(remote: "already talking"), null, At(300));
        Assert.Equal("already talking", feed.Line?.Text);
        Assert.Equal(At(300), feed.LastActivity);
    }

    [Fact]
    public void SpacesAndNewlinesInRecognizedTextAreTidied()
    {
        var feed = new SubtitleFeed();
        feed.Update(Channels(remote: "  hello \n  world  "), null, At(1));
        Assert.Equal("hello world", feed.Line?.Text);
    }

    // MARK: Fitting text

    [Fact]
    public void ShortTextIsKeptWholeAndWhitespaceIsCollapsed()
    {
        Assert.Equal("short", SubtitleText.Tail("short", 10));
        Assert.Equal("hello world", SubtitleText.Tail("hello\n  world", 50));
        Assert.Equal("", SubtitleText.Tail("hello world", 0));
        Assert.Equal("", SubtitleText.Tail("   ", 5));
        Assert.Equal("", SubtitleText.Tail("", 5));
    }

    [Fact]
    public void ChineseAndEmojiAreTwoUnitsWide()
    {
        Assert.Equal(3, SubtitleText.Units("abc"));
        Assert.Equal(4, SubtitleText.Units("你好"));
        Assert.Equal(7, SubtitleText.Units("你好 ok"));
        Assert.Equal(2, SubtitleText.Units("，"));
    }

    [Fact]
    public void EnglishKeepsTheNewestWholeWords()
    {
        // 43 characters into 20 units: the raw cut would start inside "jumps".
        Assert.Equal("…over the lazy dog", SubtitleText.Tail("the quick brown fox jumps over the lazy dog", 20));
        // A contraction is one word.
        Assert.Equal("…know", SubtitleText.Tail("we really don't know", 10));
        // A hyphenated word too.
        Assert.Equal("…everything", SubtitleText.Tail("well-known fact about everything", 14));
    }

    [Fact]
    public void AWordLongerThanTheWholeBudgetIsCutRatherThanDropped()
    {
        Assert.Equal("…ilistic", SubtitleText.Tail("supercalifragilistic", 8));
    }

    [Fact]
    public void ChineseIsCutBetweenAnyTwoCharacters()
    {
        // 20 characters = 40 units; 10 units of text and the ellipsis fit in 11.
        Assert.Equal("…一致性问题", SubtitleText.Tail("今天我们来讨论一下分布式系统的一致性问题", 11));
    }

    [Fact]
    public void ACutNeverStartsWithAComma()
    {
        // The raw cut would be "，但是成本太高".
        Assert.Equal("…但是成本太高", SubtitleText.Tail("我觉得这个方案不错，但是成本太高", 15));
    }

    [Fact]
    public void MixedTextDoesNotBreakALatinWordNextToChinese()
    {
        // The raw cut would leave "etes 部署服务到生产环境".
        Assert.Equal("…部署服务到生产环境", SubtitleText.Tail("我们用 Kubernetes 部署服务到生产环境", 22));
    }

    [Fact]
    public void TheResultNeverExceedsTheBudget()
    {
        string[] samples =
        [
            "the quick brown fox jumps over the lazy dog",
            "今天我们来讨论一下分布式系统的一致性问题",
            "我们用 Kubernetes 部署服务到生产环境",
            "Hello, world. This is a test of the subtitle bar."
        ];
        foreach (var text in samples)
        {
            for (var budget = 4; budget <= 60; budget++)
            {
                var cut = SubtitleText.Tail(text, budget);
                Assert.True(SubtitleText.Units(cut) <= budget, $"{text} @ {budget} -> {cut}");
                Assert.NotEmpty(cut);
            }
        }
    }

    // MARK: What is drawn

    [Fact]
    public void WithoutATranslationOnlyTheOriginalShows()
    {
        var line = new SubtitleLine("remote", "Could you walk me through the design?", "", true);
        var display = SubtitleText.DisplayFor(line, 80, 40);
        Assert.Equal("Could you walk me through the design?", display.Primary);
        Assert.Equal("", display.Secondary);
        Assert.Equal("remote", display.Speaker);
        Assert.True(display.IsPartial);
    }

    [Fact]
    public void WithATranslationTheTranslationLeadsAndTheOriginalFollows()
    {
        var line = new SubtitleLine("remote", "Could you walk me through the design?", "你能给我讲讲这个设计吗？");
        var display = SubtitleText.DisplayFor(line, 80, 40);
        Assert.Equal("你能给我讲讲这个设计吗？", display.Primary);
        Assert.Equal("Could you walk me through the design?", display.Secondary);
        Assert.False(display.IsPartial);
    }

    [Fact]
    public void EachLineIsFittedToItsOwnBudget()
    {
        var line = new SubtitleLine("me", "the quick brown fox jumps over the lazy dog",
            "今天我们来讨论一下分布式系统的一致性问题");
        var display = SubtitleText.DisplayFor(line, 11, 20);
        Assert.Equal("…一致性问题", display.Primary);
        Assert.Equal("…over the lazy dog", display.Secondary);
        Assert.Equal("me", display.Speaker);
    }
}
