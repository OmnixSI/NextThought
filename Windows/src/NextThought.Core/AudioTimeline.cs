namespace NextThought.Core;

/// <summary>
/// The last minute or so of one audio channel (16 kHz mono), addressed by wall-clock time, so a
/// finished sentence can be cut out again by the start and end the recognizer reported for it.
/// Ported from AudioTimeline.swift: capture hands the recognizer 16 kHz chunks together with the
/// stream origin, the recognizer reports <c>origin + sample index / 16000</c>, and this buffer
/// keeps the same bookkeeping so the times match exactly. The audio lives only in memory and is
/// only used for that sentence's voiceprint.
/// </summary>
public sealed class AudioTimeline
{
    public const int SampleRate = 16_000;

    private sealed class Run
    {
        public required DateTimeOffset Origin { get; init; }
        public int FirstSample { get; set; }
        public List<short> Samples { get; } = [];
    }

    private readonly Lock gate = new();
    private readonly List<Run> runs = [];
    private readonly int retentionSamples;
    private readonly int slackSamples;

    public AudioTimeline(double retentionSeconds = 60)
    {
        retentionSamples = (int)(retentionSeconds * SampleRate);
        slackSamples = Math.Max(SampleRate, retentionSamples / 6);
    }

    /// <summary>Appends the next chunk of the stream that began at <paramref name="origin"/>.</summary>
    public void Append(IReadOnlyList<short> samples, DateTimeOffset origin)
    {
        if (samples.Count == 0) return;
        lock (gate)
        {
            if (runs.Count > 0 && runs[^1].Origin == origin)
            {
                var run = runs[^1];
                run.Samples.AddRange(samples);
                var excess = run.Samples.Count - retentionSamples;
                if (excess > slackSamples)
                {
                    run.Samples.RemoveRange(0, excess);
                    run.FirstSample += excess;
                }
            }
            else
            {
                var run = new Run { Origin = origin };
                run.Samples.AddRange(samples);
                runs.Add(run);
                // The previous stream may still deliver a last sentence after a pause; older ones are gone.
                if (runs.Count > 2) runs.RemoveRange(0, runs.Count - 2);
            }
        }
    }

    /// <summary>
    /// The audio between <paramref name="start"/> and <paramref name="end"/> as floats in -1…1, or
    /// null when too little of it is still held (<paramref name="minimumCoverage"/> is the share of
    /// the wanted span that must be present). A span longer than <paramref name="maxSeconds"/> is cut
    /// to its last <paramref name="maxSeconds"/>: 20 s is plenty to know a voice, and the end of a
    /// long sentence is the part most likely still in the buffer.
    /// </summary>
    public float[]? Clip(DateTimeOffset start, DateTimeOffset end, double maxSeconds = 20, double minimumCoverage = 0.6)
    {
        if (end <= start) return null;
        var wantedEnd = end;
        var latest = end.AddSeconds(-maxSeconds);
        var wantedStart = start > latest ? start : latest;
        lock (gate)
        {
            (int Run, int Lower, int Upper)? best = null;
            var wanted = 0;
            for (var index = 0; index < runs.Count; index++)
            {
                var run = runs[index];
                var lowerWanted = (int)Math.Round((wantedStart - run.Origin).TotalSeconds * SampleRate);
                var upperWanted = (int)Math.Round((wantedEnd - run.Origin).TotalSeconds * SampleRate);
                wanted = Math.Max(wanted, upperWanted - lowerWanted);
                var lower = Math.Max(lowerWanted, run.FirstSample);
                var upper = Math.Min(upperWanted, run.FirstSample + run.Samples.Count);
                if (upper <= lower) continue;
                if (upper - lower > (best is { } b ? b.Upper - b.Lower : 0)) best = (index, lower, upper);
            }
            if (best is not { } chosen || wanted <= 0 || chosen.Upper - chosen.Lower < minimumCoverage * wanted) return null;
            var held = runs[chosen.Run];
            var from = chosen.Lower - held.FirstSample;
            var count = chosen.Upper - chosen.Lower;
            var result = new float[count];
            for (var i = 0; i < count; i++) result[i] = held.Samples[from + i] / 32768f;
            return result;
        }
    }

    /// <summary>
    /// Cuts the quiet lead-in and tail off a clip. A sentence's reported span starts where the
    /// previous one ended, so it can carry seconds of silence that would only blur the voiceprint
    /// and the length used to decide whether a line is long enough to start a new voice.
    /// Returns an empty array for a clip that is silent all through; clips under 80 ms are kept.
    /// </summary>
    public static float[] TrimSilence(float[] samples, int window = 320, int paddingWindows = 2)
    {
        var windows = samples.Length / window;
        if (windows < 4) return samples;
        var levels = new float[windows];
        var peak = 0f;
        for (var w = 0; w < windows; w++)
        {
            var sum = 0f;
            for (var i = w * window; i < (w + 1) * window; i++) sum += samples[i] * samples[i];
            levels[w] = (float)Math.Sqrt(sum / window);
            peak = Math.Max(peak, levels[w]);
        }
        // Below this the whole clip is room noise; above it, "quiet" means a tenth of the loudest 20 ms.
        if (peak < 0.002f) return [];
        var threshold = Math.Max(0.002f, peak * 0.1f);
        var first = Array.FindIndex(levels, value => value >= threshold);
        if (first < 0) return [];
        var last = Array.FindLastIndex(levels, value => value >= threshold);
        var lower = Math.Max(0, first - paddingWindows) * window;
        var upperWindow = last + 1 + paddingWindows;
        var upper = upperWindow >= windows ? samples.Length : upperWindow * window;
        return samples[lower..upper];
    }

    /// <summary>Forgets everything (a new meeting).</summary>
    public void Reset()
    {
        lock (gate) runs.Clear();
    }

    /// <summary>Seconds of audio currently held, across streams.</summary>
    public double HeldSeconds
    {
        get
        {
            lock (gate) return runs.Sum(run => run.Samples.Count) / (double)SampleRate;
        }
    }
}
