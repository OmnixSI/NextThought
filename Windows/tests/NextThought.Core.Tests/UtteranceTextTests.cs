using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J5: translated from Tests/NextThoughtCoreTests/UtteranceTextTests.swift (Mac v3.4.0,
/// f015e8c6). The offline pass wins only when it covers what the live stream heard; words left
/// pending are closed when the speaker stops (2.5 s quiet) or goes on (20 s longest).
/// </summary>
public sealed class UtteranceTextTests
{
    [Fact]
    public void TheOfflinePassWinsWhenItCoversWhatWasHeard()
    {
        var text = UtteranceText.Final(
            "how does reusing the previous prediction reduce pilot overhead", 6.1,
            "How does reusing the previous prediction reduce pilot overhead?", 5.8);
        Assert.Equal("How does reusing the previous prediction reduce pilot overhead?", text);
    }

    [Fact]
    public void MinutesOfQuietSpeechAreNotReplacedByTheirLastSentence()
    {
        var heard = string.Join(" ",
            Enumerable.Repeat("that is all my work and I will introduce the plan for the next year", 12));
        var text = UtteranceText.Final(heard, 395, "So, radio map.", 4);
        Assert.Equal(heard, text);
    }

    [Fact]
    public void AShortOfflineTextOfTheSameAudioStillWins()
    {
        // The stream stutters, the offline pass cleans it up: same audio, fewer words.
        var text = UtteranceText.Final("嗯 那个 我们 我们 这个 方法 是 是 可以 的", 5, "我们这个方法是可以的。", 4.6);
        Assert.Equal("我们这个方法是可以的。", text);
    }

    [Fact]
    public void EmptySidesFallBack()
    {
        Assert.Equal("hello there", UtteranceText.Final("hello there", 2, "", 2));
        Assert.Equal("Hello.", UtteranceText.Final("", 0, "Hello.", 2));
        Assert.Equal(7, UtteranceText.Units("XL-MIMO 的 near field 效应"));
    }

    [Fact]
    public void WordsLeftPendingAreClosedWhenTheSpeakerStopsOrGoesOn()
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(1_000);
        var pending = new PendingUtterance();
        Assert.False(pending.IsOverdue(start));
        pending.Heard("so the radio map", start);
        Assert.False(pending.IsOverdue(start.AddSeconds(1)));
        // Stopped: the same words for two and a half seconds.
        Assert.True(pending.IsOverdue(start.AddSeconds(2.6)));
        // Still talking: words keep changing, but twenty seconds is long enough for a line.
        for (var second = 1; second <= 19; second++)
            pending.Heard($"so the radio map {second}", start.AddSeconds(second));
        Assert.False(pending.IsOverdue(start.AddSeconds(19.5)));
        Assert.True(pending.IsOverdue(start.AddSeconds(20.1)));
        pending.Closed();
        Assert.False(pending.IsOverdue(start.AddSeconds(60)));
    }
}
