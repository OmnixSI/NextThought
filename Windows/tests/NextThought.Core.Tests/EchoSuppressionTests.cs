using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for EchoSuppression.swift.</summary>
public sealed class EchoSuppressionTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnitsUseCjkBigramsAndLatinWords()
    {
        Assert.Equal(["今天", "天讨", "讨论"], EchoTextFilter.Units("今天讨论"));
        Assert.Equal(["hello", "world"], EchoTextFilter.Units("Hello, world!"));
        Assert.Equal(["so", "温度", "度补", "补偿", "works"], EchoTextFilter.Units("so 温度补偿 works"));
    }

    [Fact]
    public void OverlapMeasuresTheShareOfUnits()
    {
        Assert.Equal(1.0, EchoTextFilter.Overlap("温度补偿", "温度补偿"), 6);
        Assert.True(EchoTextFilter.Overlap("温度补偿之后", "温度补偿") > 0.5);
        Assert.Equal(0, EchoTextFilter.Overlap("完全不同的话", "温度补偿"));
        // Fewer than three units is never echo on text alone.
        Assert.Equal(0, EchoTextFilter.Overlap("好", "好"));
    }

    [Fact]
    public void IsLikelyEchoComparesAgainstSingleLines()
    {
        Assert.True(EchoTextFilter.IsLikelyEcho("温度补偿之后", ["我们先把温度补偿做完"]));
        Assert.False(EchoTextFilter.IsLikelyEcho("我觉得可以", ["我们先把温度补偿做完", "今天讨论到这里"]));
        Assert.False(EchoTextFilter.IsLikelyEcho("今天讨论", []));
    }

    [Fact]
    public void IsEchoNeedsNearbyLines()
    {
        var lines = new List<TimedLine> { new("我们先把温度补偿做完", Base, Base.AddSeconds(4)) };
        Assert.True(EchoTextFilter.IsEcho("我们先把温度补偿做完", Base, Base.AddSeconds(4), lines, 0));
        Assert.False(EchoTextFilter.IsEcho("我们先把温度补偿做完", Base.AddSeconds(30), Base.AddSeconds(34), lines, 0));
    }

    [Fact]
    public void IsEchoUsesAcousticEvidenceForPartialRepeats()
    {
        var line = new TimedLine("温度补偿之后残差还有百分之十二", Base, Base.AddSeconds(5));
        var lines = new List<TimedLine> { line };
        var partial = "温度补偿后残差约十二";
        Assert.False(EchoTextFilter.IsEcho(partial, Base, Base.AddSeconds(5), lines, 0));
        Assert.True(EchoTextFilter.IsEcho(partial, Base, Base.AddSeconds(5), lines, 0.5));
    }

    [Fact]
    public void CorrelationIsScaleInvariant()
    {
        var a = new[] { 1.0, 2, 3, 2, 1, 2, 3, 2 };
        var b = a.Select(value => value * 100).ToArray();
        Assert.Equal(1.0, CrossTalkMonitor.Correlation(a, b), 6);
        var flat = new[] { 1.0, 1, 1, 1 };
        Assert.Equal(0, CrossTalkMonitor.Correlation(flat, flat));
    }

    [Fact]
    public void MicrophoneFollowingTheSystemEnvelopeIsEcho()
    {
        var monitor = new CrossTalkMonitor();
        // 20 ms frames of a syllable rhythm: loud, quiet, loud, quiet over 2 s.
        var frames = 100;
        var system = new List<short>();
        var microphone = new List<short>();
        for (var frame = 0; frame < frames; frame++)
        {
            var loud = frame % 4 < 2;
            var amplitude = loud ? 8000 : 300;
            system.AddRange(Enumerable.Repeat((short)amplitude, 320));
            // The microphone hears the same rhythm 60 ms (3 frames) later, quieter.
            var micFrame = frame - 3;
            var micLoud = micFrame >= 0 && micFrame % 4 < 2;
            microphone.AddRange(Enumerable.Repeat((short)(micLoud ? 2000 : 80), 320));
        }
        for (var chunk = 0; chunk < frames / 10; chunk++)
        {
            monitor.AppendSystem(system.Skip(chunk * 3200).Take(3200).ToArray(), (chunk + 1) * 0.2);
            monitor.AppendMicrophone(microphone.Skip(chunk * 3200).Take(3200).ToArray(), (chunk + 1) * 0.2);
        }
        Assert.True(monitor.MicrophoneHasSpeech);
        Assert.True(monitor.MicrophoneIsEcho, $"correlation={monitor.LastCorrelation:F3}");
    }

    [Fact]
    public void SilentMicrophoneIsNotEcho()
    {
        var monitor = new CrossTalkMonitor();
        monitor.AppendSystem(Enumerable.Repeat((short)8000, 3200).ToArray(), 0.2);
        Assert.False(monitor.AppendMicrophone(new short[3200], 0.2));
        Assert.False(monitor.MicrophoneHasSpeech);
    }
}
