namespace NextThought.Core;

// Port of LiveTranslationRevisionGate.swift: in-flight translations of an earlier
// recognizer snapshot must never overwrite the sentence currently on screen.

/// <summary>Identifies one recognizer snapshot of the utterance being translated.</summary>
public readonly record struct LiveTranslationRevision(int Generation, int Value);

/// <summary>
/// Tracks changing recognizer snapshots. <see cref="Revise"/> marks a new snapshot,
/// <see cref="Invalidate"/> starts a new utterance, and <see cref="Accept"/> lets a
/// useful intermediate translation appear while a later one is still in flight.
/// </summary>
public sealed class LiveTranslationRevisionGate
{
    private int displayed;

    public int Generation { get; private set; }

    public int Value { get; private set; }

    public LiveTranslationRevision Current => new(Generation, Value);

    /// <summary>A newer snapshot of the same utterance.</summary>
    public LiveTranslationRevision Revise()
    {
        Value++;
        return Current;
    }

    /// <summary>A new utterance (or the end of this one): everything in flight is stale.</summary>
    public void Invalidate()
    {
        Generation++;
        Value = 0;
        displayed = 0;
    }

    /// <summary>True when this revision is the newest snapshot issued so far.</summary>
    public bool IsLatest(LiveTranslationRevision revision) =>
        revision.Generation == Generation && revision.Value == Value;

    /// <summary>
    /// Accepts an intermediate response only if it is newer than what is visible, so a
    /// later snapshot can still replace it. Returns false for stale or duplicate results.
    /// </summary>
    public bool Accept(LiveTranslationRevision revision)
    {
        if (revision.Generation != Generation || revision.Value <= displayed || revision.Value > Value) return false;
        displayed = revision.Value;
        return true;
    }
}

/// <summary>
/// Fixed-rate throttle for partial translation requests (LiveTranslationPartialThrottle).
/// Unlike a debounce, new speech does not restart the timer, so a long utterance keeps
/// producing work.
/// </summary>
public sealed class LiveTranslationPartialThrottle
{
    private readonly double intervalSeconds;
    private double? lastDispatch;

    public LiveTranslationPartialThrottle(double intervalSeconds)
    {
        this.intervalSeconds = Math.Max(0, intervalSeconds);
    }

    /// <summary>True when a request may be sent at this (monotonic) time, in seconds.</summary>
    public bool ShouldDispatch(double nowSeconds)
    {
        if (lastDispatch is null)
        {
            lastDispatch = nowSeconds;
            return true;
        }
        if (nowSeconds - lastDispatch.Value < intervalSeconds) return false;
        lastDispatch = nowSeconds;
        return true;
    }

    public void Reset() => lastDispatch = null;
}

/// <summary>One partial translation the caller should send to the answer service.</summary>
public sealed record LiveTranslationRequest(LiveTranslationRevision Revision, string Source, string Text);

/// <summary>
/// Orchestrates live (partial) translation of one channel without knowing about the
/// network: feed every partial snapshot to <see cref="Observe"/>, run the returned
/// request, then hand the result to <see cref="Complete"/>. Out-of-order or stale
/// results are dropped; <see cref="Translation"/> is always the best visible text.
/// </summary>
public sealed class LiveTranslationCoordinator
{
    private readonly LiveTranslationRevisionGate gate = new();
    private readonly LiveTranslationPartialThrottle throttle;
    private string lastText = "";
    private string source = "";

    public LiveTranslationCoordinator(double throttleSeconds = 0.7)
    {
        throttle = new LiveTranslationPartialThrottle(throttleSeconds);
    }

    /// <summary>The most recent translation accepted for the current utterance.</summary>
    public string Translation { get; private set; } = "";

    /// <summary>The channel the current utterance belongs to ("remote", "me", "room").</summary>
    public string Source => source;

    /// <summary>
    /// Records a partial snapshot. Returns a request when one should be sent now
    /// (first snapshot of an utterance, or the throttle interval has passed).
    /// </summary>
    public LiveTranslationRequest? Observe(string source, string text, double nowSeconds)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (this.source != source)
        {
            this.source = source;
            gate.Invalidate();
            throttle.Reset();
            lastText = "";
            Translation = "";
        }
        if (text != lastText)
        {
            lastText = text;
            gate.Revise();
        }
        if (!throttle.ShouldDispatch(nowSeconds)) return null;
        return new LiveTranslationRequest(gate.Current, source, text);
    }

    /// <summary>Applies a finished translation; false when it lost to a newer visible one.</summary>
    public bool Complete(LiveTranslationRevision revision, string translation)
    {
        if (string.IsNullOrWhiteSpace(translation)) return false;
        if (!gate.Accept(revision)) return false;
        Translation = translation;
        return true;
    }

    /// <summary>The utterance ended (final line arrived, recording stopped, channel cleared).</summary>
    public void Invalidate()
    {
        gate.Invalidate();
        throttle.Reset();
        lastText = "";
        Translation = "";
    }
}
