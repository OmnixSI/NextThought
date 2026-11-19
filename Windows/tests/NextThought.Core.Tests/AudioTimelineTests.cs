using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for AudioTimeline.swift.</summary>
public sealed class AudioTimelineTests
{
    private static readonly DateTimeOffset Origin = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static short[] Samples(double seconds, short value = 1000)
        => Enumerable.Repeat(value, (int)Math.Round(seconds * AudioTimeline.SampleRate)).ToArray();

    [Fact]
    public void ClipReturnsTheWantedSpan()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Samples(2), Origin);
        var clip = timeline.Clip(Origin.AddSeconds(0.5), Origin.AddSeconds(1.0));
        Assert.NotNull(clip);
        Assert.Equal(AudioTimeline.SampleRate / 2, clip!.Length);
        Assert.Equal(1000f / 32768, clip[0], 6);
    }

    [Fact]
    public void ClipRejectsReversedOrEmptySpans()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Samples(1), Origin);
        Assert.Null(timeline.Clip(Origin.AddSeconds(0.6), Origin.AddSeconds(0.4)));
        Assert.Null(timeline.Clip(Origin, Origin));
    }

    [Fact]
    public void ClipNeedsEnoughCoverage()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Samples(1), Origin);
        // Only half the requested two seconds is held, below the 0.6 default.
        Assert.Null(timeline.Clip(Origin, Origin.AddSeconds(2)));
        Assert.NotNull(timeline.Clip(Origin.AddSeconds(0.2), Origin.AddSeconds(1), minimumCoverage: 0.9));
    }

    [Fact]
    public void ClipCutsLongSpansToTheirEnd()
    {
        var timeline = new AudioTimeline(retentionSeconds: 60);
        timeline.Append(Samples(30), Origin);
        var clip = timeline.Clip(Origin, Origin.AddSeconds(30), maxSeconds: 20);
        Assert.NotNull(clip);
        Assert.Equal(20 * AudioTimeline.SampleRate, clip!.Length);
    }

    [Fact]
    public void RetentionTrimsOldSamples()
    {
        var timeline = new AudioTimeline(retentionSeconds: 2);
        // Trimming happens on the next append once the excess passes the slack (max(1 s, retention/6) = 1 s).
        timeline.Append(Samples(2), Origin);
        timeline.Append(Samples(1.5), Origin);
        Assert.Equal(2.0, timeline.HeldSeconds, 6);
        // After trimming, the buffer holds stream seconds 1.5–3.5.
        var clip = timeline.Clip(Origin.AddSeconds(2.4), Origin.AddSeconds(2.8));
        Assert.NotNull(clip);
    }

    [Fact]
    public void NewOriginStartsANewRunAndKeepsOnlyTwo()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Samples(1), Origin);
        timeline.Append(Samples(1), Origin.AddSeconds(5));
        Assert.Equal(2, timeline.HeldSeconds, 1);
        timeline.Append(Samples(1), Origin.AddSeconds(10));
        // Only the two newest streams survive; 3 s of audio minus the dropped first one.
        Assert.Equal(2.0, timeline.HeldSeconds, 1);
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var timeline = new AudioTimeline();
        timeline.Append(Samples(2), Origin);
        timeline.Reset();
        Assert.Equal(0, timeline.HeldSeconds);
        Assert.Null(timeline.Clip(Origin, Origin.AddSeconds(1)));
    }

    [Fact]
    public void TrimSilenceCutsLeadAndTail()
    {
        var samples = new List<float>();
        samples.AddRange(Enumerable.Repeat(0f, 4000));            // 0.25 s silence
        samples.AddRange(Enumerable.Repeat(0.2f, 8000));          // 0.5 s tone
        samples.AddRange(Enumerable.Repeat(0f, 4000));            // 0.25 s silence
        var trimmed = AudioTimeline.TrimSilence(samples.ToArray());
        Assert.True(trimmed.Length < samples.Count);
        // Two 20 ms windows of padding survive at each end; the tone in the middle is untouched.
        Assert.InRange(trimmed.Length, 8000, 10000);
        Assert.All(trimmed[800..^800], value => Assert.True(value > 0.01f));
    }

    [Fact]
    public void TrimSilenceReturnsNothingForSilence()
    {
        var trimmed = AudioTimeline.TrimSilence(new float[16000]);
        Assert.Empty(trimmed);
    }

    [Fact]
    public void TrimSilenceKeepsVeryShortClips()
    {
        var samples = new float[1000];
        Assert.Same(samples, AudioTimeline.TrimSilence(samples));
    }
}
