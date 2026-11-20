using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from AudioTimelineTests.swift: the short audio window behind "区分对方的每个人" —
/// a sentence is cut out again by the start and end the recognizer reported. Sample <c>i</c> of a
/// stream holds the value <c>i / 1000</c>, so a cut can be checked by eye.
/// </summary>
public sealed class AudioTimelineTestsParityTests
{
    private static readonly DateTimeOffset Origin = DateTimeOffset.FromUnixTimeSeconds(1_000);

    /// <summary>One second of the stream, second number <paramref name="second"/> (16 000 samples).</summary>
    private static short[] Second(int second) =>
        Enumerable.Range(0, 16_000)
            .Select(index => (short)((second * 16_000 + index) / 1_000))
            .ToArray();

    private static float Value(int index) => (short)(index / 1_000) / 32_768f;

    [Fact]
    public void CutsASentenceByTime()
    {
        var timeline = new AudioTimeline();
        for (var second = 0; second < 3; second++) timeline.Append(Second(second), Origin);
        var clip = timeline.Clip(Origin.AddSeconds(1.0), Origin.AddSeconds(1.5));
        Assert.NotNull(clip);
        Assert.Equal(8_000, clip!.Length);
        Assert.Equal(Value(16_000), clip[0]);      // sample 16 000 -> 16
        Assert.Equal(Value(23_999), clip[^1]);     // sample 23 999 -> 23
        Assert.Equal(3.0, timeline.HeldSeconds);
    }

    [Fact]
    public void ChunksOfAnySizeJoinUp()
    {
        var timeline = new AudioTimeline();
        var all = Enumerable.Range(0, 3).SelectMany(Second).ToArray();
        // The capture services hand over 1 600-sample (100 ms) chunks.
        var index = 0;
        while (index < all.Length)
        {
            timeline.Append(all[index..Math.Min(index + 1_600, all.Length)], Origin);
            index += 1_600;
        }
        var clip = timeline.Clip(Origin.AddSeconds(0.5), Origin.AddSeconds(2.5));
        Assert.NotNull(clip);
        Assert.Equal(32_000, clip!.Length);
        Assert.Equal(Value(8_000), clip[0]);
        Assert.Equal(Value(39_999), clip[^1]);
    }

    [Fact]
    public void OldAudioIsForgotten()
    {
        var timeline = new AudioTimeline(retentionSeconds: 2);
        for (var second = 0; second < 10; second++) timeline.Append(Second(second), Origin);
        // Held: the last 2 s of the 10 (trimmed back to the retention once 1 s of slack was used).
        Assert.Equal(2.0, timeline.HeldSeconds);
        var recent = timeline.Clip(Origin.AddSeconds(8), Origin.AddSeconds(10));
        Assert.NotNull(recent);
        Assert.Equal(32_000, recent!.Length);
        Assert.Equal(Value(128_000), recent[0]);
        Assert.Null(timeline.Clip(Origin, Origin.AddSeconds(2)));
    }

    [Fact]
    public void TooLittleCoverageGivesNothing()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Second(0), Origin);      // 1 s held
        // Asking for 2 s of which 1 s exists: 50 % < the default 60 %.
        Assert.Null(timeline.Clip(Origin.AddSeconds(-1), Origin.AddSeconds(1)));
        var partial = timeline.Clip(Origin.AddSeconds(-1), Origin.AddSeconds(1), minimumCoverage: 0.4);
        Assert.NotNull(partial);
        Assert.Equal(16_000, partial!.Length);
        Assert.Equal(Value(0), partial[0]);
        // Nothing wanted, or backwards.
        Assert.Null(timeline.Clip(Origin.AddSeconds(1), Origin.AddSeconds(1)));
        Assert.Null(timeline.Clip(Origin.AddSeconds(2), Origin.AddSeconds(1)));
    }

    [Fact]
    public void ALongSentenceIsCutToItsLastTwentySeconds()
    {
        var timeline = new AudioTimeline();
        for (var second = 0; second < 30; second++) timeline.Append(Second(second), Origin);
        var clip = timeline.Clip(Origin, Origin.AddSeconds(30));
        Assert.NotNull(clip);
        Assert.Equal(320_000, clip!.Length);
        Assert.Equal(Value(160_000), clip[0]);
        Assert.Equal(Value(479_999), clip[^1]);
        var shorter = timeline.Clip(Origin, Origin.AddSeconds(30), maxSeconds: 5);
        Assert.NotNull(shorter);
        Assert.Equal(80_000, shorter!.Length);
    }

    [Fact]
    public void AResumedStreamHasItsOwnTimeline()
    {
        var timeline = new AudioTimeline();
        var later = Origin.AddSeconds(100);
        timeline.Append(Second(0), Origin);
        timeline.Append(Second(0), later);       // after a pause the stream starts over
        timeline.Append(Second(1), later);
        // A late sentence of the first stream still finds its audio.
        var first = timeline.Clip(Origin.AddSeconds(0.25), Origin.AddSeconds(0.75));
        Assert.NotNull(first);
        Assert.Equal(8_000, first!.Length);
        Assert.Equal(Value(4_000), first[0]);
        var resumed = timeline.Clip(later.AddSeconds(1.0), later.AddSeconds(1.5));
        Assert.NotNull(resumed);
        Assert.Equal(8_000, resumed!.Length);
        Assert.Equal(Value(16_000), resumed[0]);
        // Between the two streams there is no audio.
        Assert.Null(timeline.Clip(Origin.AddSeconds(50), Origin.AddSeconds(51)));
    }

    [Fact]
    public void SilenceIsTrimmedOffBothEnds()
    {
        // 1 s of silence, 1 s at 0.1, 1 s of silence: 150 windows of 320 samples; speech is windows 50...99.
        var clip = Enumerable.Repeat(0f, 16_000)
            .Concat(Enumerable.Repeat(0.1f, 16_000))
            .Concat(Enumerable.Repeat(0f, 16_000))
            .ToArray();
        var trimmed = AudioTimeline.TrimSilence(clip);
        // Kept: windows 48...101 (two padding windows each side) = 54 * 320.
        Assert.Equal(17_280, trimmed.Length);
        Assert.Equal(0f, trimmed[0]);
        Assert.Equal(0.1f, trimmed[640]);
        Assert.Equal(0.1f, trimmed[trimmed.Length - 641]);
        Assert.Equal(0f, trimmed[^1]);
    }

    [Fact]
    public void TrimmingKeepsSpeechRunningToTheEdges()
    {
        var speech = Enumerable.Repeat(0.05f, 16_000).ToArray();
        Assert.Equal<float>(speech, AudioTimeline.TrimSilence(speech));
        // Leading silence only: the tail is speech to the last sample, so it is kept whole.
        var lead = Enumerable.Repeat(0f, 8_000).Concat(speech).ToArray();
        Assert.Equal(16_000 + 640, AudioTimeline.TrimSilence(lead).Length);
    }

    [Fact]
    public void SilentOrTinyClips()
    {
        Assert.Empty(AudioTimeline.TrimSilence(Enumerable.Repeat(0.0005f, 32_000).ToArray()));
        Assert.Empty(AudioTimeline.TrimSilence(new float[32_000]));
        var tiny = Enumerable.Repeat(0.3f, 900).ToArray();
        Assert.Equal<float>(tiny, AudioTimeline.TrimSilence(tiny));
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Second(0), Origin);
        timeline.Reset();
        Assert.Equal(0.0, timeline.HeldSeconds);
        Assert.Null(timeline.Clip(Origin, Origin.AddSeconds(1)));
        timeline.Append(Array.Empty<short>(), Origin);
        Assert.Equal(0.0, timeline.HeldSeconds);
    }
}
