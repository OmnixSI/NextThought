using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from EchoSuppressionTests.swift: a delayed, quieter copy of the remote audio on the
/// microphone channel is echo, an independent speaker is not, and a user talking over a quiet echo
/// is still the user. The text filter still catches verbatim repeats without misreading replies.
/// </summary>
public sealed class EchoSuppressionTestsParityTests
{
    /// <summary>Same xorshift generator as the Swift test helper.</summary>
    private sealed class SeededGenerator(ulong seed)
    {
        private ulong state = unchecked(seed * 0x9E3779B97F4A7C15UL) | 1UL;

        public ulong Next()
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return state;
        }

        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

        public double Range(double lower, double upper) => lower + (upper - lower) * NextDouble();
    }

    /// <summary>Speech-like signal: noise shaped by a random syllable envelope (~4–6 Hz).</summary>
    private static double[] Speech(double seconds, ulong seed, double level = 0.2)
    {
        var generator = new SeededGenerator(seed);
        var count = (int)(seconds * 16_000);
        var envelope = new List<double>(count + 4_000);
        while (envelope.Count < count)
        {
            var syllable = (int)(generator.Range(0.08, 0.25) * 16_000);
            var gap = (int)(generator.Range(0.03, 0.15) * 16_000);
            var peak = generator.Range(0.4, 1.0);
            for (var index = 0; index < syllable; index++)
                envelope.Add(peak * Math.Sin(Math.PI * index / syllable));
            for (var index = 0; index < gap; index++) envelope.Add(0.01);
        }
        var samples = new double[count];
        for (var index = 0; index < count; index++)
            samples[index] = envelope[index] * level * generator.Range(-1.0, 1.0);
        return samples;
    }

    private static double[] Noise(int count, double amplitude, int seed = 17)
    {
        var random = new Random(seed);
        var values = new double[count];
        for (var index = 0; index < count; index++)
            values[index] = (random.NextDouble() * 2 - 1) * amplitude;
        return values;
    }

    private static double[] Delayed(double[] source, int delay, double gain, double[] other)
    {
        var result = (double[])other.Clone();
        for (var index = delay; index < Math.Min(source.Length, other.Length); index++)
            result[index] += source[index - delay] * gain;
        return result;
    }

    private static short[] Pcm(double[] values)
    {
        var samples = new short[values.Length];
        for (var index = 0; index < values.Length; index++)
            samples[index] = (short)Math.Clamp(values[index] * 32_767, -32_767, 32_767);
        return samples;
    }

    /// <summary>Feeds both channels in 100 ms chunks; the mic is delayed by <paramref name="delay"/> samples.</summary>
    private static (double EchoRatio, double Correlation) Run(double[] system, double[] microphone)
    {
        var monitor = new CrossTalkMonitor();
        var echoChunks = 0;
        var speechChunks = 0;
        var last = 0.0;
        const int chunk = 1_600;
        var end = Math.Min(system.Length, microphone.Length) - chunk;
        for (var start = 0; start < end; start += chunk)
        {
            var time = (start + chunk) / 16_000.0;
            monitor.AppendSystem(Pcm(system[start..(start + chunk)]), time);
            var echo = monitor.AppendMicrophone(Pcm(microphone[start..(start + chunk)]), time);
            if (monitor.MicrophoneHasSpeech && start > 16_000)
            {
                speechChunks++;
                if (echo) echoChunks++;
            }
            last = monitor.LastCorrelation;
        }
        return (speechChunks == 0 ? 0 : (double)echoChunks / speechChunks, last);
    }

    [Fact]
    public void DelayedQuietCopyIsEcho()
    {
        var remote = Speech(seconds: 6, seed: 1);
        const int delay = 2_400; // 150 ms acoustic + driver delay
        var noise = Noise(remote.Length, 0.002);
        var microphone = Delayed(remote, delay, 0.15, noise);
        var result = Run(remote, microphone);
        Assert.True(result.EchoRatio > 0.8, $"echoRatio={result.EchoRatio:F3}, correlation={result.Correlation:F3}");
    }

    [Fact]
    public void IndependentSpeakerIsNotEcho()
    {
        var remote = Speech(seconds: 6, seed: 2);
        var me = Speech(seconds: 6, seed: 3, level: 0.25);
        var result = Run(remote, me);
        Assert.True(result.EchoRatio < 0.2, $"echoRatio={result.EchoRatio:F3}, correlation={result.Correlation:F3}");
    }

    [Fact]
    public void UserTalkingOverEchoIsStillTheUser()
    {
        var remote = Speech(seconds: 6, seed: 4);
        var me = Speech(seconds: 6, seed: 5, level: 0.3);
        var microphone = Delayed(remote, 2_400, 0.1, me);
        var result = Run(remote, microphone);
        Assert.True(result.EchoRatio < 0.3, $"echoRatio={result.EchoRatio:F3}, correlation={result.Correlation:F3}");
    }

    [Fact]
    public void SilentSystemNeverMarksEcho()
    {
        var me = Speech(seconds: 4, seed: 6);
        var result = Run(new double[me.Length], me);
        Assert.Equal(0.0, result.EchoRatio);
    }

    [Fact]
    public void TextFilterCatchesRepeatedWords()
    {
        Assert.True(EchoTextFilter.IsLikelyEcho("那数据库和缓存怎么保证一致性",
            ["那数据库和缓存同时更新的时候，你们怎么保证一致性？"]));
        Assert.False(EchoTextFilter.IsLikelyEcho("我们用的是先更新数据库再删除缓存",
            ["那数据库和缓存同时更新的时候，你们怎么保证一致性？"]));
        Assert.True(EchoTextFilter.IsLikelyEcho("how do you handle cache invalidation",
            ["So how do you handle cache invalidation in production?"]));
        Assert.False(EchoTextFilter.IsLikelyEcho("好的", ["好的"]));
    }

    /// <summary>Replies reuse the question's words; they must stay the user's lines.</summary>
    [Fact]
    public void RepliesThatReuseWordsAreNotEcho()
    {
        var now = DateTimeOffset.UtcNow;
        var lines = new[]
        {
            new TimedLine("那数据库和缓存同时更新的时候，你们是怎么保证一致性的？",
                now.AddSeconds(-6), now.AddSeconds(-2)),
            new TimedLine("你们的缓存是放在 Redis 里吗？",
                now.AddSeconds(-12), now.AddSeconds(-9))
        };
        Assert.False(EchoTextFilter.IsEcho("我们保证一致性的方式是先更新数据库再删缓存",
            now, now.AddSeconds(3), lines, 0));
        Assert.False(EchoTextFilter.IsEcho("对，缓存放在 Redis 里",
            now, now.AddSeconds(2), lines, 0.1));
        // Old union matching would have called this echo (most bigrams appear somewhere above).
        Assert.False(EchoTextFilter.IsLikelyEcho("你们的数据库缓存是怎么更新的",
            lines.Select(line => line.Text).ToArray(), threshold: 0.8));
    }

    [Fact]
    public void EchoAtTheSameTimeIsCaught()
    {
        var now = DateTimeOffset.UtcNow;
        var line = new TimedLine("那数据库和缓存同时更新的时候，你们是怎么保证一致性的？",
            now, now.AddSeconds(4));
        // Near-verbatim repeat while the other side speaks: echo even without the audio monitor.
        Assert.True(EchoTextFilter.IsEcho("数据库和缓存同时更新的时候你们是怎么保证一致性的",
            now.AddSeconds(0.3), now.AddSeconds(4.2), [line], 0));
        // Partial repeat: echo only if the speakers were heard in the microphone.
        Assert.True(EchoTextFilter.IsEcho("缓存和数据库怎么保证一致性",
            now.AddSeconds(0.5), now.AddSeconds(4), [line], 0.7));
        Assert.False(EchoTextFilter.IsEcho("缓存和数据库怎么保证一致性",
            now.AddSeconds(0.5), now.AddSeconds(4), [line], 0));
        // The same words a minute later are the user, not echo.
        Assert.False(EchoTextFilter.IsEcho("数据库和缓存同时更新的时候你们是怎么保证一致性的",
            now.AddSeconds(60), now.AddSeconds(64), [line], 0));
    }
}
