namespace NextThought.Core;

public sealed record SpeechSegment(Guid Id, long StartSample, long EndSample, short[] Samples);

/// <summary>Port of the upstream RMS VAD. No neural VAD model or network dependency.</summary>
public sealed class VoiceActivitySegmenter
{
    public const int SampleRate = 16000;
    private double noiseFloor;
    private long totalSamples;
    private Guid? currentId;
    private long startSample;
    private readonly List<short> samples = [];
    private readonly List<short> preRoll = [];
    private int silenceSamples, speechSamples;
    private bool hasNewSpeech;
    public int EndSilenceSamples { get; init; } = 11200;
    public bool IsSpeaking { get; private set; }

    public SpeechSegment? Append(short[] input)
    {
        if (input.Length == 0) return null;
        totalSamples += input.Length;
        double power = 0;
        foreach (var sample in input) { var value = sample / 32768.0; power += value * value; }
        var rms = Math.Sqrt(power / input.Length);
        IsSpeaking = rms >= Math.Max(.008, Math.Min(.03, noiseFloor * 3));
        noiseFloor = noiseFloor == 0 ? Math.Min(rms, .01) : rms < noiseFloor ? noiseFloor * .8 + rms * .2 : noiseFloor * .998 + rms * .002;
        if (IsSpeaking) hasNewSpeech = true;
        if (currentId is null)
        {
            if (!IsSpeaking)
            {
                preRoll.AddRange(input);
                if (preRoll.Count > 4000) preRoll.RemoveRange(0, preRoll.Count - 4000);
                return null;
            }
            currentId = Guid.NewGuid(); samples.AddRange(preRoll); samples.AddRange(input);
            startSample = totalSamples - samples.Count; preRoll.Clear(); silenceSamples = 0; speechSamples = input.Length;
        }
        else
        {
            samples.AddRange(input);
            if (IsSpeaking) { silenceSamples = 0; speechSamples += input.Length; }
            else silenceSamples += input.Length;
        }
        var limit = samples.Count >= SampleRate * 12;
        if (silenceSamples < EndSilenceSamples && !limit) return null;
        var result = speechSamples >= SampleRate / 2 && hasNewSpeech ? Event() : null;
        if (limit && IsSpeaking)
        {
            var overlap = samples.TakeLast(SampleRate / 2).ToArray();
            ResetCurrent(); currentId = Guid.NewGuid(); samples.AddRange(overlap);
            startSample = totalSamples - overlap.Length; speechSamples = overlap.Length; hasNewSpeech = false;
        }
        else ResetCurrent();
        return result;
    }
    public SpeechSegment? Flush()
    {
        var result = currentId.HasValue && hasNewSpeech && speechSamples >= SampleRate / 2 ? Event() : null;
        ResetCurrent(); return result;
    }
    private SpeechSegment Event() => new(currentId!.Value, startSample, startSample + samples.Count, samples.ToArray());
    private void ResetCurrent() { currentId = null; samples.Clear(); silenceSamples = speechSamples = 0; hasNewSpeech = false; startSample = totalSamples; }
}
