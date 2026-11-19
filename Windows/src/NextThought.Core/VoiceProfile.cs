namespace NextThought.Core;

/// <summary>
/// "My voice": one averaged voiceprint on this machine (port of VoiceProfile.swift). A meeting
/// voice close enough to it is shown as "我". The voiceprint is biometric data: it never leaves
/// the machine, is not part of backups or exports, and can be deleted at any time.
/// </summary>
public sealed record VoiceProfile
{
    public const string ModelId = "wespeaker-resnet34-lm";

    /// <summary>Higher than the grouping threshold (0.5) on purpose: calling somebody else "我" is worse than missing.</summary>
    public const float Threshold = 0.55f;

    /// <summary>Recordings needed to build a profile.</summary>
    public const int MinClips = 3;

    public string Model { get; init; } = ModelId;
    public float[] Embedding { get; init; } = [];
    public int Clips { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Made by the current model and not empty or all zeros.</summary>
    public bool IsUsable => Model == ModelId && Embedding.Any(value => value != 0);

    /// <summary>Whether a meeting voice is the user, given the sum of the voiceprints that joined it.</summary>
    public bool Recognises(float[] clusterSum)
    {
        if (!IsUsable || clusterSum.Length != Embedding.Length) return false;
        return VoiceClusterer.Similarity(Embedding, clusterSum) >= Threshold;
    }
}

public enum VoiceEnrollmentFailure
{
    /// <summary>Fewer usable recordings than the minimum.</summary>
    TooFewClips,

    /// <summary>The recordings don't sound like one voice (somebody else spoke, or a lot of noise).</summary>
    Inconsistent
}

public sealed record VoiceEnrollmentResult(VoiceProfile? Profile, VoiceEnrollmentFailure? Failure, int UsableClips);

public static class VoiceEnrollment
{
    /// <summary>Each recording must be at least this similar to the average direction of the others.</summary>
    public const float Consistency = 0.5f;

    /// <summary>
    /// Averages the voiceprints of several recordings of one person. A recording that doesn't
    /// match the rest is dropped; fewer than the minimum left is a failure.
    /// </summary>
    public static VoiceEnrollmentResult Make(IReadOnlyList<float[]> clips, DateTimeOffset? now = null)
    {
        var size = clips.Count > 0 ? clips[0].Length : 0;
        var units = new List<float[]>();
        foreach (var clip in clips)
        {
            if (size == 0 || clip.Length != size) continue;
            if (Normalized(clip) is { } unit) units.Add(unit);
        }
        if (units.Count < VoiceProfile.MinClips)
            return new VoiceEnrollmentResult(null, VoiceEnrollmentFailure.TooFewClips, units.Count);

        var sum = Sum(units);
        var kept = units.Where(unit => VoiceClusterer.Similarity(unit, sum) >= Consistency).ToList();
        if (kept.Count < VoiceProfile.MinClips || Normalized(Sum(kept)) is not { } mean)
            return new VoiceEnrollmentResult(null, VoiceEnrollmentFailure.Inconsistent, units.Count);
        return new VoiceEnrollmentResult(
            new VoiceProfile { Embedding = mean, Clips = kept.Count, CreatedAt = now ?? DateTimeOffset.UtcNow }, null, units.Count);
    }

    internal static float[] Sum(IReadOnlyList<float[]> vectors)
    {
        if (vectors.Count == 0) return [];
        var output = new float[vectors[0].Length];
        foreach (var vector in vectors)
            for (var index = 0; index < output.Length && index < vector.Length; index++)
                output[index] += vector[index];
        return output;
    }

    internal static float[]? Normalized(float[] vector)
    {
        float squares = 0;
        foreach (var value in vector) squares += value * value;
        var length = Math.Sqrt(squares);
        if (!(length > 1e-6) || !double.IsFinite(length)) return null;
        return vector.Select(value => (float)(value / length)).ToArray();
    }
}
