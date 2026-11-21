namespace NextThought.Core;

public sealed record SpeechSegment(Guid Id, long StartSample, long EndSample, short[] Samples);

public sealed record SpeechSegmentEvent(
    SpeechSegmentEvent.KindValue Kind,
    Guid Id,
    long StartSample,
    long EndSample,
    short[] Samples)
{
    public enum KindValue
    {
        Partial,
        Final
    }
}

/// <summary>
/// RMS voice activity detector with an adaptive noise floor (port of
/// VoiceActivitySegmenter.swift). Emits live partials while speaking and a final segment after
/// 0.7 s of silence or at the 12-second limit (with a half-second overlap).
/// </summary>
public sealed class VoiceActivitySegmenter
{
    public const int SampleRate = 16000;
    private const double BaseThreshold = 0.008;

    private readonly List<short> samples = [];
    private readonly List<short> preRoll = [];
    private double noiseFloor;
    private long totalSamples;
    private Guid? currentId;
    private long startSample;
    private int silenceSamples;
    private int speechSamples;
    private bool hasNewSpeech;
    private int lastPartialCount;

    /// <summary>Silence that ends an utterance (0.7 s at 16 kHz).</summary>
    public int EndSilenceSamples { get; init; } = 11200;

    /// <summary>New audio needed between live partial previews (1.5 s).</summary>
    public int PartialInterval { get; init; } = 24000;

    public bool IsSpeaking { get; private set; }

    public IReadOnlyList<SpeechSegmentEvent> Append(short[] input)
    {
        if (input.Length == 0) return [];
        totalSamples += input.Length;

        double squareTotal = 0;
        foreach (var sample in input)
        {
            var value = sample / 32768.0;
            squareTotal += value * value;
        }
        var level = Math.Sqrt(squareTotal / input.Length);
        var threshold = Math.Max(BaseThreshold, Math.Min(0.03, noiseFloor * 3));
        IsSpeaking = level >= threshold;
        // Floor follower: drops quickly to quieter chunks, rises slowly (~1 min) so a constant
        // background hum is eventually ignored without swallowing speech.
        if (noiseFloor == 0) noiseFloor = Math.Min(level, 0.01);
        else if (level < noiseFloor) noiseFloor = noiseFloor * 0.8 + level * 0.2;
        else noiseFloor = noiseFloor * 0.998 + level * 0.002;
        if (IsSpeaking) hasNewSpeech = true;

        if (currentId is null)
        {
            if (!IsSpeaking)
            {
                preRoll.AddRange(input);
                if (preRoll.Count > 4000) preRoll.RemoveRange(0, preRoll.Count - 4000);
                return [];
            }
            currentId = Guid.NewGuid();
            samples.AddRange(preRoll);
            samples.AddRange(input);
            startSample = totalSamples - samples.Count;
            preRoll.Clear();
            silenceSamples = 0;
            speechSamples = input.Length;
            lastPartialCount = 0;
        }
        else
        {
            samples.AddRange(input);
            if (IsSpeaking)
            {
                silenceSamples = 0;
                speechSamples += input.Length;
            }
            else
            {
                silenceSamples += input.Length;
            }
        }

        var events = new List<SpeechSegmentEvent>();
        if (IsSpeaking && samples.Count - lastPartialCount >= PartialInterval && samples.Count >= 16000 && currentId is { } partialId)
        {
            events.Add(MakeEvent(SpeechSegmentEvent.KindValue.Partial, partialId));
            lastPartialCount = samples.Count;
        }

        var reachedSilence = silenceSamples >= EndSilenceSamples;
        var reachedLimit = samples.Count >= SampleRate * 12;
        if ((reachedSilence || reachedLimit) && currentId is { } finalId)
        {
            if (speechSamples >= SampleRate / 2 && hasNewSpeech)
                events.Add(MakeEvent(SpeechSegmentEvent.KindValue.Final, finalId));
            if (reachedLimit && IsSpeaking)
            {
                var overlapCount = Math.Min(SampleRate / 2, samples.Count);
                var overlap = samples.Skip(samples.Count - overlapCount).ToArray();
                currentId = Guid.NewGuid();
                samples.Clear();
                samples.AddRange(overlap);
                startSample = totalSamples - overlap.Length;
                silenceSamples = 0;
                speechSamples = overlap.Length;
                hasNewSpeech = false;
                lastPartialCount = overlap.Length;
            }
            else
            {
                ResetCurrent();
            }
        }
        return events;
    }

    /// <summary>Final segment at the end of a meeting, when the last utterance is still open.</summary>
    public SpeechSegmentEvent? Flush()
    {
        if (!hasNewSpeech || speechSamples < SampleRate / 2 || currentId is not { } id)
        {
            ResetCurrent();
            return null;
        }
        var eventValue = MakeEvent(SpeechSegmentEvent.KindValue.Final, id);
        ResetCurrent();
        return eventValue;
    }

    public void Reset()
    {
        totalSamples = 0;
        noiseFloor = 0;
        ResetCurrent();
        preRoll.Clear();
    }

    private SpeechSegmentEvent MakeEvent(SpeechSegmentEvent.KindValue kind, Guid id) =>
        new(kind, id, startSample, startSample + samples.Count, samples.ToArray());

    private void ResetCurrent()
    {
        currentId = null;
        samples.Clear();
        silenceSamples = 0;
        speechSamples = 0;
        hasNewSpeech = false;
        lastPartialCount = 0;
        startSample = totalSamples;
    }
}
