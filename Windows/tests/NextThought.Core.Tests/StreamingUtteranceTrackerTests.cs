using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J5: the state machine of Sources/NextThought/ZipformerStreamingTranscriber.swift, extracted so
/// the Windows recogniser session can share it:
///  - the offline re-decode may only replace the text the segment covers
///    (<see cref="UtteranceText.Final"/>; a few seconds of loud speech must not replace minutes of
///    quiet speech the stream heard);
///  - overlapping audio the segmenter re-sends after splitting a long utterance is dropped before
///    re-decoding, so words are neither lost nor duplicated;
///  - words that no segmenter will close are closed by the 2.5 s / 20 s pending watch;
///  - a final is delivered once: a replayed or stale final is dropped.
/// </summary>
public sealed class StreamingUtteranceTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    private const int SampleRate = 16_000;

    [Fact]
    public void OfflineTextOnlyReplacesTheAudioItCovers()
    {
        // The minutes of quiet talk the stream heard are not replaced by the last loud sentence.
        var heard = string.Join(" ",
            Enumerable.Repeat("that is all my work and I will introduce the plan for the next year", 12));
        Assert.Equal(heard, StreamingUtteranceTracker.FinalText(heard, 395, "So, radio map.", 4));
        // When the offline pass covers the same audio it wins (better words and punctuation).
        Assert.Equal("How does reusing the previous prediction reduce pilot overhead?",
            StreamingUtteranceTracker.FinalText(
                "how does reusing the previous prediction reduce pilot overhead", 6.1,
                "How does reusing the previous prediction reduce pilot overhead?", 5.8));
        // Same audio, fewer words from the cleaner pass: still the offline text.
        Assert.Equal("我们这个方法是可以的。",
            StreamingUtteranceTracker.FinalText("嗯 那个 我们 我们 这个 方法 是 是 可以 的", 5,
                "我们这个方法是可以的。", 4.6));
        // An empty side falls back to the other.
        Assert.Equal("hello there", StreamingUtteranceTracker.FinalText("hello there", 2, "", 2));
    }

    [Fact]
    public void PendingClosesAfterQuietOrAfterTheLongestTalk()
    {
        var tracker = new StreamingUtteranceTracker();
        Assert.False(tracker.IsOverdue(Start));
        tracker.Heard("so the radio map", Start);
        Assert.False(tracker.IsOverdue(Start.AddSeconds(1)));
        // Quiet for two and a half seconds: closed here because no segmenter will.
        Assert.True(tracker.IsOverdue(Start.AddSeconds(2.6)));
        // Still talking: the words keep changing, but 20 s is the longest a line stays pending.
        for (var second = 1; second <= 19; second++)
            tracker.Heard("so the radio map " + second, Start.AddSeconds(second));
        Assert.False(tracker.IsOverdue(Start.AddSeconds(19.5)));
        Assert.True(tracker.IsOverdue(Start.AddSeconds(20.1)));
        tracker.ClosedPending();
        Assert.False(tracker.IsOverdue(Start.AddSeconds(60)));
    }

    [Fact]
    public void PendingCloseKeepsTheWordsTheStreamHeard()
    {
        var tracker = new StreamingUtteranceTracker();
        var text = string.Join(" ", Enumerable.Repeat("the quiet speaker keeps talking for minutes", 30));
        tracker.Heard(text, Start);
        Assert.True(tracker.IsOverdue(Start.AddSeconds(2.6)));
        // The pending text is what the final line carries; the offline pass that later arrives for a
        // few seconds cannot replace it.
        Assert.Equal(text, tracker.Pending.Text);
        Assert.Equal(text, StreamingUtteranceTracker.FinalText(tracker.Pending.Text, 300, "a loud sentence", 3));
    }

    [Fact]
    public void OverlapAlreadyCommittedIsDroppedBeforeRefining()
    {
        var tracker = new StreamingUtteranceTracker();
        // The previous utterance was committed through sample 3 s.
        tracker.MarkCommittedThrough(3 * SampleRate);
        // The segmenter splits a long utterance and re-sends 0.5 s: the segment begins at 2.5 s.
        Assert.Equal(8_000, tracker.OverlapToDrop(2 * SampleRate + 8_000, null, Start));
        // Nothing duplicated when the segment starts after the committed point.
        Assert.Equal(0, tracker.OverlapToDrop(4 * SampleRate, null, Start));

        // An utterance closed here (not by the segmenter) is marked by the clock: a segment that
        // reaches back before that moment is trimmed by time too.
        var timed = new StreamingUtteranceTracker();
        timed.MarkClosedWithoutSegment(Start.AddSeconds(3));
        Assert.Equal(8_000, timed.OverlapToDrop(2 * SampleRate + 8_000, Start, Start.AddSeconds(2.5)));
        Assert.Equal(0, timed.OverlapToDrop(4 * SampleRate, Start, Start.AddSeconds(4)));
    }

    [Fact]
    public void StaleFinalsAndPartialsAfterAFinalAreNotDelivered()
    {
        var tracker = new StreamingUtteranceTracker();
        Assert.True(tracker.ShouldDeliver(true, Start.AddSeconds(5)));
        tracker.Delivered(true, Start.AddSeconds(5));
        // The same final replayed, or a partial belonging to the already-finished utterance, is dropped.
        Assert.False(tracker.ShouldDeliver(true, Start.AddSeconds(5)));
        Assert.False(tracker.ShouldDeliver(false, Start.AddSeconds(4.9)));
        // The next utterance's partial and final go through.
        Assert.True(tracker.ShouldDeliver(false, Start.AddSeconds(5.1)));
        Assert.True(tracker.ShouldDeliver(true, Start.AddSeconds(6)));
    }

    [Fact]
    public void ResetStartsAFreshUtterance()
    {
        var tracker = new StreamingUtteranceTracker();
        tracker.Heard("old words", Start);
        tracker.MarkCommittedThrough(5 * SampleRate);
        tracker.MarkClosedWithoutSegment(Start.AddSeconds(5));
        tracker.Delivered(true, Start.AddSeconds(5));
        tracker.Reset();
        Assert.Equal(0, tracker.CommittedThroughSample);
        Assert.Equal(DateTimeOffset.MinValue, tracker.CommittedThroughDate);
        Assert.Equal(DateTimeOffset.MinValue, tracker.LastFinalizedAt);
        Assert.Equal("", tracker.Pending.Text);
    }
}
