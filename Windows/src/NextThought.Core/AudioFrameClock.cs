namespace NextThought.Core;

/// <summary>Catch up short timer delays without compressing the recorded timeline.</summary>
public sealed class AudioFrameClock
{
    private long emitted;
    private TimeSpan previous;
    public int Advance(TimeSpan elapsed)
    {
        if (elapsed < previous) throw new InvalidOperationException("音频时钟发生回退。");
        if (previous > TimeSpan.Zero && elapsed - previous > TimeSpan.FromMilliseconds(1500))
            throw new IOException("音频时间线中断，请结束后重新开始会议。");
        previous = elapsed;
        var target = elapsed.Ticks / TimeSpan.FromMilliseconds(100).Ticks;
        var due = checked((int)(target - emitted)); emitted = target; return due;
    }
}

public static class PcmAudio
{
    public static void ApplyGain(Span<short> samples, double gain)
    {
        if (!double.IsFinite(gain) || gain is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(gain));
        if (gain == 1) return;
        for (var i = 0; i < samples.Length; i++) samples[i] = (short)Math.Clamp((int)Math.Round(samples[i] * gain), short.MinValue, short.MaxValue);
    }
}

public static class EchoDetector
{
    public static bool IsLikelyEcho(TranscriptUpdate update, IEnumerable<TranscriptLine> recent, bool inPerson)
    {
        if (inPerson || update.Source != AudioSource.Microphone) return false;
        var text = QuestionCandidateTracker.Normalize(update.Text);
        if (text.Length < 8) return false;
        return recent.TakeLast(12).Any(line => line.Speaker == "remote" && QuestionCandidateTracker.Normalize(line.Text) == text
            && update.Start <= (line.EndedAt ?? line.Date).AddSeconds(2)
            && update.End >= (line.StartedAt ?? line.Date).AddSeconds(-2));
    }
}
