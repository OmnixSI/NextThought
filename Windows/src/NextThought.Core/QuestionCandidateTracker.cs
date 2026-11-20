using System.Text.RegularExpressions;

namespace NextThought.Core;

public enum CandidateKind { None, Updated, Trigger, Uncertain }
public sealed record QuestionCandidate(Guid Id, string Text, IntentAssessment Assessment, Guid UtteranceId, DateTimeOffset FirstSeen,
    DateTimeOffset LastChanged, int Revision, bool IsFinal, bool Triggered = false, string? LeadIn = null, Guid? Supersedes = null)
{
    public string FullText => string.IsNullOrEmpty(LeadIn) ? Text : LeadIn + (QuestionIntentClassifier.CjkCount(LeadIn[^1..]) > 0 ? "" : " ") + Text;
}
public readonly record struct CandidateDecision(CandidateKind Kind, QuestionCandidate? Candidate = null);
public sealed class QuestionCandidateTracker
{
    /// <summary>B15: the Mac QuestionCandidateTracker.Config values.</summary>
    public sealed record Config
    {
        public double StabilityInterval { get; init; } = 1.2;
        public double StrongStabilityInterval { get; init; } = 0.6;
        public double EarlyTriggerScore { get; init; } = 0.8;
        public double LeadInWindow { get; init; } = 8;
        public double FragmentWindow { get; init; } = 8;
        public double DuplicateWindow { get; init; } = 20;
        public double MinimumTriggerInterval { get; init; } = 2.5;
    }

    public QuestionCandidateTracker() { }
    public QuestionCandidateTracker(MeetingMode mode, Config? config = null)
    {
        Mode = mode;
        Settings = config ?? new Config();
    }

    public Config Settings { get; init; } = new();
    public MeetingMode Mode { get; set; } = MeetingMode.Meeting;
    public bool HasPriorAnswer { get; set; }
    public QuestionCandidate? Current { get; private set; }
    private (string Text, DateTimeOffset Date)? fragment;
    private readonly List<(string Key, DateTimeOffset Date)> recent = [];
    private readonly List<(string Text, DateTimeOffset Date)> statements = [];
    private readonly Dictionary<Guid, (string Text, Guid Id)> early = [];
    private DateTimeOffset? lastTrigger;

    /// <summary>B15: clears every piece of state (Mac reset).</summary>
    public void Reset()
    {
        Current = null;
        fragment = null;
        recent.Clear();
        statements.Clear();
        early.Clear();
        lastTrigger = null;
        HasPriorAnswer = false;
    }

    /// <summary>B15: a bare follow-up like "为什么" becomes actionable after an answer.</summary>
    public void NoteAnswered() => HasPriorAnswer = true;

    /// <summary>B15: accept an uncertain candidate after the second-layer check.</summary>
    public bool Confirm(QuestionCandidate candidate, DateTimeOffset now)
    {
        var key = Normalize(candidate.Text);
        if (recent.Any(entry => entry.Key == key)) return false;
        recent.Add((key, now));
        lastTrigger = now;
        return true;
    }

    public CandidateDecision Ingest(string raw, bool final, Guid utteranceId, DateTimeOffset now)
    {
        Expire(now); var incoming = raw.Trim(); if (incoming.Length > 2000) incoming = incoming[^2000..];
        if (incoming.Length == 0) return default;
        var text = fragment is { } f ? f.Text + (NeedsSpace(f.Text, incoming) ? " " : "") + incoming : incoming;
        var assessment = QuestionIntentClassifier.Assess(text, Mode, HasPriorAnswer);
        if (fragment.HasValue)
        {
            var own = QuestionIntentClassifier.Assess(incoming, Mode, HasPriorAnswer);
            if (own.Reasons.Any(x => x is "speaker narration" or "audio / screen check")) { text = incoming; assessment = own; if (final) fragment = null; }
        }
        var candidate = Current is { IsFinal: false } c && c.UtteranceId == utteranceId
            ? c with { Text = text, Assessment = assessment, IsFinal = final, Revision = c.Revision + (c.Text != text ? 1 : 0), LastChanged = c.Text != text ? now : c.LastChanged }
            : new QuestionCandidate(Guid.NewGuid(), text, assessment, utteranceId, now, now, 0, final);
        if (!final) { Current = candidate; return new(assessment.IsActionable || assessment.Intent == UtteranceIntent.Incomplete ? CandidateKind.Updated : CandidateKind.None, candidate); }
        fragment = null; Current = null;
        if (assessment.Intent == UtteranceIntent.Incomplete) { fragment = (text.Length > 2000 ? text[^2000..] : text, now); return new(CandidateKind.Updated, candidate); }
        if (early.TryGetValue(utteranceId, out var prior))
        {
            var key = Normalize(text); var overlap = Similarity(prior.Text, key);
            if (overlap >= .6 && key.Length <= prior.Text.Length + 6) return default;
            if (key.StartsWith(prior.Text) || overlap >= .3) candidate = candidate with { Supersedes = prior.Id };
        }
        var decision = Decide(candidate, now);
        if (decision.Kind is not (CandidateKind.Trigger or CandidateKind.Uncertain) && !assessment.IsActionable)
        { statements.Add((text.Length > 300 ? text[^300..] : text, now)); if (statements.Count > 3) statements.RemoveAt(0); }
        return decision;
    }
    public CandidateDecision Tick(DateTimeOffset now)
    {
        Expire(now);
        if (Current is not { IsFinal: false, Triggered: false } c || !c.Assessment.IsActionable || c.Assessment.Score < Settings.EarlyTriggerScore) return default;
        var strong = c.Text.TrimEnd().EndsWith('?') || c.Text.TrimEnd().EndsWith('？') || c.Text.EndsWith('吗') || c.Text.EndsWith('呢') || c.Assessment.Score >= .95;
        if ((now-c.LastChanged).TotalSeconds < (strong ? Settings.StrongStabilityInterval : Settings.StabilityInterval)) return default;
        var decision = Decide(c, now);
        if (decision.Kind == CandidateKind.Trigger) { Current = decision.Candidate; early[c.UtteranceId] = (Normalize(c.Text), c.Id); }
        return decision;
    }
    private CandidateDecision Decide(QuestionCandidate c, DateTimeOffset now)
    {
        if (!c.Assessment.IsActionable) return default;
        var key = Normalize(c.Text);
        if (key.Length == 0 || recent.Any(x => x.Key == key)) return default;
        var lead = string.Join(" ", statements.Where(x => (c.FirstSeen-x.Date).TotalSeconds >= 0 && (c.FirstSeen-x.Date).TotalSeconds <= Settings.LeadInWindow).Select(x => x.Text));
        if (lead.Length > 400) lead = lead[^400..];
        c = c with { LeadIn = c.LeadIn ?? (lead.Length > 0 ? lead : null) };
        if (c.Assessment.Score < QuestionIntentClassifier.Thresholds(Mode).Actionable) return new(CandidateKind.Uncertain, c);
        if (lastTrigger.HasValue && (now-lastTrigger.Value).TotalSeconds < Settings.MinimumTriggerInterval && c.Assessment.Intent == UtteranceIntent.FollowUp) return default;
        recent.Add((key, now)); if (recent.Count > 40) recent.RemoveAt(0); lastTrigger = now; statements.Clear();
        return new(CandidateKind.Trigger, c with { Triggered = true });
    }
    private void Expire(DateTimeOffset now)
    { recent.RemoveAll(x => (now-x.Date).TotalSeconds > Settings.DuplicateWindow); statements.RemoveAll(x => (now-x.Date).TotalSeconds > Settings.LeadInWindow); if (fragment is { } f && (now-f.Date).TotalSeconds > Settings.FragmentWindow) fragment = null; if (early.Count > 50) early.Clear(); }
    private static bool NeedsSpace(string a, string b) => !(QuestionIntentClassifier.CjkCount(a[^1..]) > 0 && QuestionIntentClassifier.CjkCount(b[..1]) > 0);
    public static string Normalize(string text) => Regex.Replace(string.Concat(text.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ')), " +", " ").Trim();
    private static double Similarity(string a, string b)
    {
        static HashSet<string> Pairs(string x) { x = string.Concat(x.Where(c => !char.IsWhiteSpace(c))); return x.Length < 2 ? [x] : Enumerable.Range(0, x.Length-1).Select(i => x.Substring(i, 2)).ToHashSet(); }
        var left = Pairs(a); var right = Pairs(b); return (double)left.Intersect(right).Count() / Math.Max(1, left.Union(right).Count());
    }
}
