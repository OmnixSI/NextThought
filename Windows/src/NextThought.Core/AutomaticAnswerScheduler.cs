namespace NextThought.Core;

/// <summary>Keep the latest detected question while the local participant speaks.</summary>
public sealed class AutomaticAnswerScheduler
{
    private QuestionCandidate? pending;
    private DateTimeOffset expires, quietSince;
    private bool quiet;
    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaximumWait { get; init; } = TimeSpan.FromSeconds(15);
    public void Enqueue(QuestionCandidate candidate, DateTimeOffset now)
    { pending = candidate; expires = now + MaximumWait; quiet = false; }
    public QuestionCandidate? Tick(DateTimeOffset now, bool microphoneActive)
    {
        if (pending == null) return null;
        if (now >= expires) { Clear(); return null; }
        if (microphoneActive) { quiet = false; return null; }
        if (!quiet) { quietSince = now; quiet = true; }
        if (now - quietSince < QuietPeriod) return null;
        var result = pending; Clear(); return result;
    }
    public void Clear() { pending = null; quiet = false; }
}
