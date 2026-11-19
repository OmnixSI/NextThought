namespace NextThought.Core;

/// <summary>What the live coach measured over a whole meeting, kept next to its notes.</summary>
public sealed record CoachSummary(
    double? Pace = null,
    double? TalkShare = null,
    double LongestMonologue = 0,
    double? EyeContact = null,
    IReadOnlyDictionary<string, int>? Nudges = null,
    int LookUpCues = 0)
{
    /// <summary>Nothing worth showing: no pace, share, long monologue or eye contact.</summary>
    public bool IsEmpty =>
        Pace is null && TalkShare is null && EyeContact is null && LongestMonologue < CoachSummaryAccumulator.MinimumMonologue;
}

/// <summary>
/// Folds a meeting's transcript segments and coach snapshots into a <see cref="CoachSummary"/>.
/// Ported from CoachSummary.swift.
/// </summary>
public sealed class CoachSummaryAccumulator
{
    /// <summary>A monologue shorter than this is just talking.</summary>
    public const double MinimumMonologue = 30;
    public const double MinimumSpeaking = 20;
    public const double MinimumTotal = 45;

    private double myDuration;
    private double theirDuration;
    private double myUnits;
    private double longest;
    private double? contact;
    private readonly Dictionary<string, int> nudges = [];
    private int lookUps;

    public void Ingest(SpokenSegment segment)
    {
        if (segment.End <= segment.Start || segment.Units <= 0) return;
        var length = segment.End - segment.Start;
        if (segment.IsMe) { myDuration += length; myUnits += segment.Units; }
        else theirDuration += length;
    }

    public void Observe(CoachSnapshot snapshot) => longest = Math.Max(longest, snapshot.Monologue);

    public void Record(CoachNudge nudge)
    {
        var key = nudge.ToString().ToLowerInvariant();
        nudges[key] = nudges.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    public void RecordLookUp() => lookUps++;

    /// <summary>The camera's running ratio; the last one is the meeting's.</summary>
    public void ObserveEyeContact(double? ratio)
    {
        if (ratio is { } value) contact = value;
    }

    public CoachSummary Summary() => new(
        Pace: myDuration >= MinimumSpeaking ? myUnits / myDuration * 60 : null,
        TalkShare: myDuration + theirDuration >= MinimumTotal ? myDuration / (myDuration + theirDuration) : null,
        LongestMonologue: longest,
        EyeContact: contact,
        Nudges: new Dictionary<string, int>(nudges),
        LookUpCues: lookUps);
}
