using System.Text;
using System.Text.RegularExpressions;

namespace NextThought.Core;

/// <summary>How a suggested answer is laid out on the cue surface next to the camera.</summary>
public enum CueStyle
{
    /// <summary>The conclusion, then the lead-in of every point: enough to glance at.</summary>
    KeyPoints,
    /// <summary>Sentence by sentence, the way it would be said out loud.</summary>
    Script
}

/// <summary>Inclusive numeric range (Swift's <c>ClosedRange</c>).</summary>
public readonly record struct ClosedRange(double Lower, double Upper)
{
    public bool Contains(double value) => value >= Lower && value <= Upper;
}

/// <summary>
/// What the live coach watches for in a scene. Rates are syllables per minute
/// (<see cref="SpeechUnits"/>): one Chinese character or about two thirds of an English word.
/// </summary>
public sealed record CoachingProfile(
    double? MonologueLimit,
    ClosedRange? TalkShare = null,
    ClosedRange? Pace = null,
    bool NudgesPace = true)
{
    /// <summary>Syllables per minute that suit the scene (Swift default 150...310).</summary>
    public ClosedRange PaceBounds => Pace ?? new ClosedRange(150, 310);

    /// <summary>A scene where the user mostly listens (a lecture): nothing to coach.</summary>
    public static readonly CoachingProfile Listening = new(null, NudgesPace: false);
}

/// <summary>
/// Everything a scene decides beyond the meeting defaults: how cues look, whether they sit beside
/// the camera, and what the coach pays attention to. One table, so a scene is one decision for the
/// user instead of a page of switches. Ported from Scene.swift.
/// </summary>
public sealed record ScenePreset(MeetingMode Mode, CueStyle CueStyle, bool EyeLine, CoachingProfile Coaching)
{
    public static ScenePreset For(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => new ScenePreset(mode, CueStyle.KeyPoints, true,
            new CoachingProfile(120, new ClosedRange(0.4, 0.78))),
        MeetingMode.Academic => new ScenePreset(mode, CueStyle.Script, true,
            new CoachingProfile(null, Pace: new ClosedRange(150, 290))),
        MeetingMode.LabMeeting => new ScenePreset(mode, CueStyle.KeyPoints, false,
            new CoachingProfile(180)),
        MeetingMode.GroupProject => new ScenePreset(mode, CueStyle.KeyPoints, false,
            new CoachingProfile(120, new ClosedRange(0.12, 0.5))),
        MeetingMode.Client => new ScenePreset(mode, CueStyle.KeyPoints, true,
            new CoachingProfile(90, new ClosedRange(0.25, 0.6))),
        MeetingMode.Meeting => new ScenePreset(mode, CueStyle.KeyPoints, false,
            new CoachingProfile(120)),
        MeetingMode.Lesson => new ScenePreset(mode, CueStyle.KeyPoints, false, CoachingProfile.Listening),
        MeetingMode.Discussion => new ScenePreset(mode, CueStyle.KeyPoints, false,
            new CoachingProfile(90, new ClosedRange(0.1, 0.45))),
        _ => new ScenePreset(mode, CueStyle.KeyPoints, false,
            new CoachingProfile(150))
    };
}

/// <summary>One line on the cue surface.</summary>
public sealed record CueLine(int Id, CueLine.Kind Type, string Head, string Text, string Gloss = "")
{
    public enum Kind { Lead, Point, Detail }

    /// <summary>The whole line as it would be spoken, for matching against speech.</summary>
    public string Spoken => Head.Length == 0 ? Text : Text.Length == 0 ? Head : Head + "，" + Text;
}

/// <summary>
/// An answer to say in one language with the meaning alongside in another. The model marks those
/// lines as Markdown quotes: "&gt; 问：…" once for the question, "&gt; 译：…" under each sentence.
/// </summary>
public static class BilingualAnswer
{
    public abstract record Gloss
    {
        public sealed record Question(string Text) : Gloss;
        public sealed record Line(string Text) : Gloss;
    }

    /// <summary>Whether an answer carries glosses at all: it opens with what was asked, or marks a meaning.</summary>
    public static bool IsBilingual(string answer)
    {
        if (!answer.Contains('>')) return false;
        return answer.Replace("\r\n", "\n").Split('\n').Any(line => Marked(line) is not null);
    }

    /// <summary>A line the model marked: "&gt; 问：…" or "&gt; 译：…".</summary>
    public static Gloss? Marked(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('>')) return null;
        var body = trimmed.TrimStart('>', ' ');
        foreach (var marker in new[] { "问：", "问:" })
            if (body.StartsWith(marker, StringComparison.Ordinal)) return new Gloss.Question(body[marker.Length..].Trim());
        foreach (var marker in new[] { "译：", "译:" })
            if (body.StartsWith(marker, StringComparison.Ordinal)) return new Gloss.Line(body[marker.Length..].Trim());
        return null;
    }

    /// <summary>
    /// The gloss a line of a bilingual answer is, if it is one. Models drop the "译：" mark as often
    /// as they write it: in such an answer every quoted line is a meaning.
    /// </summary>
    public static Gloss? GlossOf(string line, bool bilingual = true)
    {
        if (Marked(line) is { } marked) return marked;
        var trimmed = line.Trim();
        if (!bilingual || !trimmed.StartsWith('>')) return null;
        var body = trimmed.TrimStart('>', ' ');
        return body.Length == 0 ? null : new Gloss.Line(body);
    }

    /// <summary>What the other side asked, in the user's language.</summary>
    public static string? Question(string answer)
    {
        var lines = answer.Replace("\r\n", "\n").Split('\n').Where(line => line.Trim().Length > 0).ToList();
        for (var index = 0; index < lines.Count; index++)
        {
            if (Marked(lines[index]) is not Gloss.Question { Text.Length: > 0 } question) continue;
            // Now and then the question is repeated as it was asked and its meaning follows on a
            // line of its own: the meaning is what is wanted.
            if (index + 1 < lines.Count && Marked(lines[index + 1]) is Gloss.Line { Text.Length: > 0 } meaning) return meaning.Text;
            return question.Text;
        }
        return null;
    }

    /// <summary>A sentence to say, without a source note the model left at its end ("(报告第 5 页)").</summary>
    public static string WithoutSource(string sentence)
    {
        var trimmed = sentence.Trim();
        if (trimmed.Length == 0) return sentence;
        var last = trimmed[^1];
        if (last is not (')' or '）')) return sentence;
        var open = trimmed.LastIndexOfAny(['(', '（']);
        if (open < 0) return sentence;
        var note = trimmed[open..].ToLowerInvariant();
        if (!(note.Contains('页') || note.Contains("p.") || note.Contains('§') || note.Contains("page") || note.Contains("slide") || note.Contains('节')))
            return sentence;
        var kept = trimmed[..open].Trim();
        return kept.Length == 0 ? sentence : kept;
    }

    /// <summary>The answer without its glosses: the words to say.</summary>
    public static string Spoken(string answer)
    {
        if (!IsBilingual(answer)) return answer;
        return string.Join('\n', answer.Replace("\r\n", "\n").Split('\n').Where(line => GlossOf(line) is null));
    }
}

/// <summary>Turns a Markdown answer into lines short enough to read at a glance.</summary>
public static partial class CueScript
{
    public static List<CueLine> Lines(string answer, CueStyle style, int limit = 14)
    {
        var raw = answer.Replace("\r\n", "\n").Split('\n');
        // With glosses the model wrote one sentence per line: keep its lines, each with its meaning.
        if (BilingualAnswer.IsBilingual(answer))
        {
            var result = new List<CueLine>();
            // Sentences still waiting for their meaning; usually each meaning follows its sentence.
            var waiting = new Queue<int>();
            var glossing = false;
            int? lastGlossed = null;
            foreach (var line in raw)
            {
                switch (BilingualAnswer.GlossOf(line))
                {
                    case BilingualAnswer.Gloss.Question:
                        continue;
                    case BilingualAnswer.Gloss.Line text:
                        glossing = true;
                        if (waiting.Count > 0)
                        {
                            var index = waiting.Dequeue();
                            result[index] = result[index] with { Gloss = text.Text };
                            lastGlossed = index;
                        }
                        else if (lastGlossed is { } previous)
                        {
                            result[previous] = result[previous] with { Gloss = result[previous].Gloss + " " + text.Text };
                        }
                        break;
                    default:
                        if (result.Count >= limit) continue;
                        var plain = BilingualAnswer.WithoutSource(BulletBody(line.Trim()) ?? line);
                        var sentences = AnswerPoints.Split(plain, 3);
                        if (sentences.Count == 0) continue;
                        if (glossing) { waiting.Clear(); glossing = false; }
                        // One meaning covers the whole line: it goes with the line's first sentence.
                        waiting.Enqueue(result.Count);
                        foreach (var sentence in sentences)
                            result.Add(new CueLine(result.Count, result.Count == 0 ? CueLine.Kind.Lead : CueLine.Kind.Detail, "", sentence));
                        break;
                }
            }
            return result;
        }
        if (style == CueStyle.Script)
        {
            var sentences = AnswerPoints.Split(answer, limit);
            return sentences.Select((sentence, index) =>
                new CueLine(index, index == 0 ? CueLine.Kind.Lead : CueLine.Kind.Detail, "", sentence)).ToList();
        }
        return KeyPoints(answer, limit);
    }

    private static List<CueLine> KeyPoints(string answer, int limit)
    {
        var result = new List<CueLine>();
        var inCode = false;
        var sawLead = false;
        foreach (var rawLine in answer.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal)) { inCode = !inCode; continue; }
            if (inCode || trimmed.Length == 0 || IsTableRule(trimmed)) continue;
            if (BulletBody(trimmed) is { } body)
            {
                var (head, rest) = SplitHead(body);
                result.Add(new CueLine(result.Count, CueLine.Kind.Point, head, rest));
            }
            else
            {
                var heading = trimmed.StartsWith('#');
                var text = AnswerPoints.StripMarkup(trimmed.TrimStart('#', '>', ' '));
                if (text.Length == 0) continue;
                if (heading)
                    result.Add(new CueLine(result.Count, CueLine.Kind.Point, text, ""));
                else
                {
                    result.Add(new CueLine(result.Count, sawLead ? CueLine.Kind.Detail : CueLine.Kind.Lead, "", text));
                    sawLead = true;
                }
            }
            if (result.Count >= limit) break;
        }
        return result;
    }

    private static bool IsTableRule(string line) => line.Contains('|') && line.All(character => "|-: ".Contains(character));

    /// <summary>The text of a list item, or null when the line is not one.</summary>
    public static string? BulletBody(string line)
    {
        foreach (var marker in new[] { "- ", "* ", "• ", "· " })
            if (line.StartsWith(marker, StringComparison.Ordinal)) return line[marker.Length..].Trim();
        var match = NumberedBullet().Match(line);
        return match.Success ? line[match.Length..].Trim() : null;
    }

    [GeneratedRegex(@"^\d{1,2}[.)、]\s*")]
    private static partial Regex NumberedBullet();

    /// <summary>"**Cache Aside**：写库成功后删除缓存" → ("Cache Aside", "写库成功后删除缓存").</summary>
    public static (string Head, string Tail) SplitHead(string body)
    {
        const string separators = "：:—–，,";
        string Clean(string text) => AnswerPoints.StripMarkup(text.TrimStart(separators.ToCharArray()).Trim());
        if (body.StartsWith("**", StringComparison.Ordinal))
        {
            var close = body.IndexOf("**", 2, StringComparison.Ordinal);
            if (close > 0)
            {
                var head = AnswerPoints.StripMarkup(body[2..close]).Trim('：', ':', ' ');
                return (head, Clean(body[(close + 2)..]));
            }
        }
        var colon = body.IndexOfAny(['：', ':']);
        if (colon >= 0)
        {
            var head = body[..colon];
            var next = colon + 1;
            // "https://…" and times ("10:30") are not lead-ins.
            var looksLikeValue = next < body.Length && (body[next] == '/' || char.IsNumber(body[next]));
            if (head.Length is > 0 and <= 14 && !looksLikeValue)
                return (AnswerPoints.StripMarkup(head), Clean(body[next..]));
        }
        return ("", AnswerPoints.StripMarkup(body));
    }
}

/// <summary>
/// Speech length that compares across Chinese and English: one unit per Chinese character
/// (a syllable), one and a half per Latin word.
/// </summary>
public static class SpeechUnits
{
    public static double Count(string text)
    {
        var units = 0.0;
        var inWord = false;
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            var cjk = value is >= 0x4E00 and <= 0x9FFF or >= 0x3400 and <= 0x4DBF
                or >= 0x3040 and <= 0x30FF or >= 0xAC00 and <= 0xD7AF;
            if (cjk)
            {
                units += 1;
                inWord = false;
            }
            else if (Rune.IsLetterOrDigit(rune))
            {
                if (!inWord) units += 1.5;
                inWord = true;
            }
            else
            {
                inWord = false;
            }
        }
        return units;
    }
}

/// <summary>One finished stretch of speech, in seconds since the meeting started.</summary>
public sealed class SpokenSegment
{
    public SpokenSegment(bool isMe, double start, double end, string text)
    {
        IsMe = isMe;
        Start = start;
        End = Math.Max(end, start);
        Text = text;
        Units = SpeechUnits.Count(text);
    }

    public bool IsMe { get; }
    public double Start { get; }
    public double End { get; }
    public string Text { get; }
    public double Units { get; }
    public double Duration => End - Start;
}

public enum CoachNudge
{
    /// <summary>"说了两分钟了，收个尾，把话交回去。"</summary>
    WrapUp,
    SlowDown,
    /// <summary>The user is doing most of the talking in a scene that is about listening.</summary>
    LetThemTalk,
    /// <summary>The user has barely spoken in a scene where they are expected to.</summary>
    SpeakUp
}

public sealed record CoachSnapshot(double? Pace = null, double? TalkShare = null, double Monologue = 0, CoachNudge? Nudge = null);

/// <summary>
/// Pace, share of the talking and monologue length, from finished transcript lines. Pure arithmetic
/// over timestamps: no model, no audio. Ported from SpeakingCoach (Scene.swift).
/// </summary>
public sealed class SpeakingCoach
{
    public const double PaceWindow = 40;
    public const double ShareWindow = 300;
    /// <summary>A pause this long (or the other side speaking) ends a monologue.</summary>
    public const double MonologueGap = 3.5;
    /// <summary>"该你说两句了" waits until this much of the meeting has passed.</summary>
    public const double SpeakUpGrace = 180;
    /// <summary>The user has to have said enough for a share to mean anything.</summary>
    public const double MinimumOwnSpeech = 10;

    private readonly List<SpokenSegment> segments = [];

    public SpeakingCoach(CoachingProfile profile) => Profile = profile;

    public CoachingProfile Profile { get; set; }
    public IReadOnlyList<SpokenSegment> Segments => segments;

    public void Ingest(SpokenSegment segment)
    {
        if (segment.End <= segment.Start || segment.Units <= 0) return;
        segments.Add(segment);
        // Ten minutes of history is more than either window needs.
        if (segments.Count > 400) segments.RemoveAll(existing => existing.End < segments[^1].End - 600);
    }

    public void Reset() => segments.Clear();

    public CoachSnapshot Snapshot(double now)
    {
        var pace = PaceAt(now);
        var share = TalkShareAt(now);
        var monologue = MonologueAt(now);
        CoachNudge? nudge = null;
        if (Profile.MonologueLimit is { } limit && monologue >= limit) nudge = CoachNudge.WrapUp;
        else if (Profile.NudgesPace && pace is { } currentPace && currentPace > Profile.PaceBounds.Upper) nudge = CoachNudge.SlowDown;
        else if (share is { } currentShare && Profile.TalkShare is { } range)
        {
            if (currentShare > range.Upper) nudge = CoachNudge.LetThemTalk;
            else if (currentShare < range.Lower && now >= SpeakUpGrace) nudge = CoachNudge.SpeakUp;
        }
        return new CoachSnapshot(pace, share, monologue, nudge);
    }

    public double? PaceAt(double now)
    {
        var recent = segments.Where(segment => segment.IsMe && segment.End >= now - PaceWindow).ToList();
        var duration = recent.Sum(segment => segment.Duration);
        if (duration < 6) return null;
        return recent.Sum(segment => segment.Units) / duration * 60;
    }

    public double? TalkShareAt(double now)
    {
        var recent = segments.Where(segment => segment.End >= now - ShareWindow).ToList();
        var mine = recent.Where(segment => segment.IsMe).Sum(segment => segment.Duration);
        var theirs = recent.Where(segment => !segment.IsMe).Sum(segment => segment.Duration);
        if (mine < MinimumOwnSpeech || theirs <= 0 || mine + theirs < 45) return null;
        return mine / (mine + theirs);
    }

    public double MonologueAt(double now)
    {
        if (segments.Count == 0) return 0;
        var last = segments[^1];
        if (!last.IsMe || now - last.End > MonologueGap) return 0;
        var start = last.Start;
        var previousStart = last.Start;
        for (var index = segments.Count - 2; index >= 0; index--)
        {
            var segment = segments[index];
            if (!segment.IsMe || previousStart - segment.End > MonologueGap) break;
            start = segment.Start;
            previousStart = segment.Start;
        }
        return Math.Max(0, last.End - start);
    }
}
