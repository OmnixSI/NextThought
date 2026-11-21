using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for SubtitleBar.swift.</summary>
public sealed class SubtitleBarTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mode_CyclesThroughTheThreeWindows()
    {
        Assert.Equal(LiveWindowMode.Subtitles, LiveWindowMode.Notes.Next());
        Assert.Equal(LiveWindowMode.Hidden, LiveWindowMode.Subtitles.Next());
        Assert.Equal(LiveWindowMode.Notes, LiveWindowMode.Hidden.Next());
        Assert.Equal("字幕条", LiveWindowMode.Subtitles.Title());
    }

    [Fact]
    public void Timing_HoldsThenFadesThenHides()
    {
        var timing = SubtitleBarTiming.Standard;
        var activity = Base;
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(Base.AddSeconds(1), activity));
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(Base.AddSeconds(4), activity));
        Assert.Equal(SubtitleBarTiming.Phase.Fading, timing.PhaseAt(Base.AddSeconds(4.25), activity));
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(Base.AddSeconds(4.5), activity));
        Assert.Equal(SubtitleBarTiming.Phase.Hidden, timing.PhaseAt(Base, null));
    }

    [Fact]
    public void Timing_OpacityIsOneThenALineToZero()
    {
        var timing = SubtitleBarTiming.Standard;
        var activity = Base;
        Assert.Equal(1, timing.OpacityAt(Base.AddSeconds(2), activity));
        Assert.Equal(0.5, timing.OpacityAt(Base.AddSeconds(4.25), activity), 6);
        Assert.Equal(0, timing.OpacityAt(Base.AddSeconds(5), activity));
        Assert.Equal(0, timing.OpacityAt(Base, null));
    }

    [Fact]
    public void Timing_ActivityInTheFutureCountsAsJustNow()
    {
        var timing = SubtitleBarTiming.Standard;
        Assert.Equal(SubtitleBarTiming.Phase.Visible, timing.PhaseAt(Base, Base.AddSeconds(3)));
        Assert.Equal(1, timing.OpacityAt(Base, Base.AddSeconds(3)));
    }

    [Fact]
    public void Text_NormalizesWhitespace()
    {
        Assert.Equal("你好 世界", SubtitleText.Normalized("  你好\n\n世界  "));
        Assert.Equal("a b", SubtitleText.Normalized("a\t b"));
        Assert.Equal("", SubtitleText.Normalized(null));
    }

    [Fact]
    public void Text_CountsWideCharactersTwice()
    {
        Assert.Equal(4, SubtitleText.Units("中文"));
        Assert.Equal(2, SubtitleText.Units("ab"));
        Assert.Equal(6, SubtitleText.Units("ab中文"));
    }

    [Fact]
    public void Text_TailKeepsTheNewestPartAndMarksTheCut()
    {
        Assert.Equal("今天讨论连续刚构桥", SubtitleText.Tail("今天讨论连续刚构桥", 18));
        Assert.Equal("…架桥的挠度", SubtitleText.Tail("我们先把大跨连续刚构架桥的挠度", 12));
    }

    [Fact]
    public void Text_TailNeverCutsInsideALatinWord()
    {
        var tail = SubtitleText.Tail("we should check the sensitivity analysis tomorrow", 30);
        Assert.StartsWith("…", tail);
        Assert.DoesNotContain(" y", tail[1..2]);
        Assert.DoesNotContain("sensitivi ", tail);
    }

    [Fact]
    public void Text_TailDropsLeadingPunctuation()
    {
        Assert.Equal("…好的", SubtitleText.Tail("前面的话，好的", 5));
    }

    [Fact]
    public void Text_DisplayPutsTheTranslationOnTop()
    {
        var line = new SubtitleLine("remote", "这个比例对不对？", "Does the ratio hold?");
        var display = SubtitleText.DisplayFor(line, 40, 20);
        Assert.Equal("Does the ratio hold?", display.Primary);
        Assert.Equal("这个比例对不对？", display.Secondary);

        var original = SubtitleText.DisplayFor(new SubtitleLine("me", "只用原文"), 40, 20);
        Assert.Equal("只用原文", original.Primary);
        Assert.Equal("", original.Secondary);
    }

    [Fact]
    public void Feed_PrefersTheNewestPartialAndIgnoresTheBaseline()
    {
        var feed = new SubtitleFeed();
        var finished = new SubtitleLine("remote", "上一句已经结束。");
        feed.Update([new SubtitleChannel("remote", ""), new SubtitleChannel("me", "")], finished, Base);
        // The baseline line is shown but is not news: the bar stays down until something new arrives.
        Assert.Equal("上一句已经结束。", feed.Line!.Text);
        Assert.Null(feed.LastActivity);

        feed.Update([new SubtitleChannel("remote", "正在说的"), new SubtitleChannel("me", "")], finished, Base.AddSeconds(1));
        Assert.NotNull(feed.Line);
        Assert.Equal("正在说的", feed.Line!.Text);
        Assert.Equal("remote", feed.Line.Speaker);
        Assert.True(feed.Line.IsPartial);
        Assert.Equal(Base.AddSeconds(1), feed.LastActivity);

        feed.Update([new SubtitleChannel("remote", "正在说的"), new SubtitleChannel("me", "我插一句")], finished, Base.AddSeconds(2));
        Assert.Equal("我插一句", feed.Line!.Text);
        Assert.Equal("me", feed.Line.Speaker);
    }

    [Fact]
    public void Feed_FallsBackToTheLastFinishedLine()
    {
        var feed = new SubtitleFeed();
        var finished = new SubtitleLine("room", "现场的一句话。");
        feed.Update([], null, Base);
        feed.Update([], finished, Base.AddSeconds(1));
        Assert.Equal("现场的一句话。", feed.Line!.Text);
        Assert.False(feed.Line.IsPartial);
        Assert.Equal(Base.AddSeconds(1), feed.LastActivity);
    }
}
