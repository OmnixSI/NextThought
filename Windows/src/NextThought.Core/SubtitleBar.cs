using System.Globalization;
using System.Text;

namespace NextThought.Core;

/// <summary>What the meeting shows on screen while it is being recorded.</summary>
public enum LiveWindowMode
{
    Notes,
    Subtitles,
    Hidden
}

public static class LiveWindowModeExtensions
{
    /// <summary>纪要窗口 → 字幕条 → 隐藏 → 纪要窗口 …</summary>
    public static LiveWindowMode Next(this LiveWindowMode mode) => mode switch
    {
        LiveWindowMode.Notes => LiveWindowMode.Subtitles,
        LiveWindowMode.Subtitles => LiveWindowMode.Hidden,
        _ => LiveWindowMode.Notes
    };

    public static string Title(this LiveWindowMode mode) => mode switch
    {
        LiveWindowMode.Notes => "纪要窗口",
        LiveWindowMode.Subtitles => "字幕条",
        _ => "隐藏"
    };
}

/// <summary>
/// How long the subtitle bar stays up after the last piece of text, and how long it takes
/// to fade out. Pure arithmetic on dates so the rule can be tested.
/// </summary>
public sealed record SubtitleBarTiming
{
    public enum Phase { Visible, Fading, Hidden }

    public SubtitleBarTiming(double holdSeconds = 4, double fadeSeconds = 0.5)
    {
        HoldSeconds = Math.Max(0, holdSeconds);
        FadeSeconds = Math.Max(0, fadeSeconds);
    }

    public double HoldSeconds { get; }
    public double FadeSeconds { get; }

    public static readonly SubtitleBarTiming Standard = new();

    /// <summary><paramref name="lastActivity"/> is the time of the latest text change (a partial included).</summary>
    public Phase PhaseAt(DateTimeOffset now, DateTimeOffset? lastActivity)
    {
        if (lastActivity is not { } activity) return Phase.Hidden;
        var elapsed = Math.Max(0, (now - activity).TotalSeconds);
        if (elapsed <= HoldSeconds) return Phase.Visible;
        if (elapsed < HoldSeconds + FadeSeconds) return Phase.Fading;
        return Phase.Hidden;
    }

    /// <summary>1 while visible, then a straight line down to 0 over <see cref="FadeSeconds"/>.</summary>
    public double OpacityAt(DateTimeOffset now, DateTimeOffset? lastActivity)
    {
        if (lastActivity is not { } activity) return 0;
        var elapsed = Math.Max(0, (now - activity).TotalSeconds);
        if (elapsed <= HoldSeconds) return 1;
        if (FadeSeconds <= 0 || elapsed >= HoldSeconds + FadeSeconds) return 0;
        return 1 - (elapsed - HoldSeconds) / FadeSeconds;
    }
}

/// <summary>One sentence for the bar: the original, and its translation when there is one.</summary>
public sealed record SubtitleLine(string Speaker, string Text, string Translation = "", bool IsPartial = false);

/// <summary>The half-sentence one audio channel is recognizing right now.</summary>
public sealed record SubtitleChannel(string Speaker, string Partial, string PartialTranslation = "");

/// <summary>What is drawn: the main text (the translation when there is one) and, smaller beneath it, the original.</summary>
public sealed record SubtitleDisplay(string Primary, string Secondary, string Speaker, bool IsPartial);

/// <summary>
/// Follows the transcript and decides what the bar shows and when it last had news.
/// Ported from SubtitleBar.swift; the speaker stays a string so it matches the
/// persisted <see cref="TranscriptLine.Speaker"/> values ("remote" / "me" / "room").
/// </summary>
public sealed class SubtitleFeed
{
    private string[] channelSignatures = [];
    private DateTimeOffset[] channelChangedAt = [];
    private string? finalSignature;
    private bool hasBaseline;
    private int version;

    public SubtitleLine? Line { get; private set; }
    public DateTimeOffset? LastActivity { get; private set; }

    public void Update(IReadOnlyList<SubtitleChannel> channels, SubtitleLine? lastFinal, DateTimeOffset now)
    {
        if (channelSignatures.Length != channels.Count)
        {
            channelSignatures = new string[channels.Count];
            channelChangedAt = new DateTimeOffset[channels.Count];
            Array.Fill(channelSignatures, "");
            Array.Fill(channelChangedAt, DateTimeOffset.MinValue);
        }

        for (var index = 0; index < channels.Count; index++)
        {
            var channel = channels[index];
            var partial = SubtitleText.Normalized(channel.Partial);
            var translation = SubtitleText.Normalized(channel.PartialTranslation);
            var signature = partial.Length == 0 ? "" : partial + "\u0001" + translation;
            if (signature == channelSignatures[index]) continue;
            channelSignatures[index] = signature;
            if (partial.Length > 0)
            {
                channelChangedAt[index] = now;
                LastActivity = now;
            }
        }

        var final = lastFinal is null ? null : $"{lastFinal.Speaker}\u0001{lastFinal.Text}\u0001{lastFinal.Translation}";
        // The first look is only a baseline: a line from before the bar existed is not news.
        if (hasBaseline && final is not null && final != finalSignature) LastActivity = now;
        finalSignature = final;
        hasBaseline = true;

        // Newest partial; on a tie the earlier channel (the other side) wins.
        int? best = null;
        for (var index = 0; index < channels.Count; index++)
        {
            if (SubtitleText.Normalized(channels[index].Partial).Length == 0) continue;
            if (best is { } current && channelChangedAt[index] <= channelChangedAt[current]) continue;
            best = index;
        }
        if (best is { } chosen)
        {
            var channel = channels[chosen];
            Line = new SubtitleLine(channel.Speaker, SubtitleText.Normalized(channel.Partial),
                SubtitleText.Normalized(channel.PartialTranslation), true);
        }
        else if (lastFinal is { } finished && SubtitleText.Normalized(finished.Text).Length > 0)
        {
            Line = finished with { IsPartial = false };
        }
        else
        {
            Line = null;
        }
        version++;
    }

    public int Version => version;
}

/// <summary>Text fitting for the bar (SubtitleText.swift): half-em widths, tail from the end.</summary>
public static class SubtitleText
{
    private static readonly HashSet<string> ClosingPunctuation =
    [
        "，", "。", "！", "？", "、", "；", "：", "）", "】", "」", "』", "”", "’", "》", "…",
        ",", ".", "!", "?", ";", ":", ")", "]", "}"
    ];

    /// <summary>Newlines and runs of spaces become one space; the ends are trimmed.</summary>
    public static string Normalized(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (builder.Length > 0) pendingSpace = true;
                continue;
            }
            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(rune.ToString());
        }
        return builder.ToString();
    }

    /// <summary>Width in half-em units: Chinese, Japanese, Korean and emoji are twice a Latin letter.</summary>
    public static int Width(Rune rune) => IsWide(rune) ? 2 : 1;

    public static int Units(string text)
    {
        var total = 0;
        foreach (var rune in text.EnumerateRunes()) total += Width(rune);
        return total;
    }

    /// <summary>
    /// The newest part of <paramref name="text"/> that fits in <paramref name="maxUnits"/> half-em
    /// units, with a leading "…" when the start was dropped. The cut never falls inside a Latin
    /// word (a word longer than the whole budget is cut anyway) and never leaves a comma or full
    /// stop at the start. Chinese can be cut between any two characters.
    /// </summary>
    public static string Tail(string? text, int maxUnits)
    {
        var value = Normalized(text);
        if (maxUnits <= 0 || value.Length == 0) return "";
        if (Units(value) <= maxUnits) return value;

        var elements = TextElements(value);
        var budget = Math.Max(1, maxUnits - 1); // the ellipsis takes one unit
        var start = elements.Count;
        var used = 0;
        while (start > 0)
        {
            var columns = Width(FirstRune(elements[start - 1]));
            if (used + columns > budget) break;
            used += columns;
            start--;
        }
        if (start == elements.Count) start = elements.Count - 1; // one wide character into a budget of 1

        if (start > 0 && IsWordCharacter(elements[start - 1]) && IsWordCharacter(elements[start]))
        {
            var end = start;
            while (end < elements.Count && IsWordCharacter(elements[end])) end++;
            if (end < elements.Count) start = end;
        }
        while (start < elements.Count - 1 && (IsWhitespace(elements[start]) || ClosingPunctuation.Contains(elements[start]))) start++;
        return "…" + string.Concat(elements.Skip(start));
    }

    /// <summary>With a translation the translation is the main line and the original goes beneath it.</summary>
    public static SubtitleDisplay DisplayFor(SubtitleLine line, int primaryUnits, int secondaryUnits)
    {
        var original = Normalized(line.Text);
        var translation = Normalized(line.Translation);
        if (translation.Length == 0)
            return new SubtitleDisplay(Tail(original, primaryUnits), "", line.Speaker, line.IsPartial);
        return new SubtitleDisplay(Tail(translation, primaryUnits), Tail(original, secondaryUnits), line.Speaker, line.IsPartial);
    }

    private static List<string> TextElements(string text)
    {
        var elements = new List<string>(text.Length);
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            elements.Add(text[starts[i]..end]);
        }
        return elements;
    }

    private static Rune FirstRune(string element)
    {
        foreach (var rune in element.EnumerateRunes()) return rune;
        return Rune.ReplacementChar;
    }

    private static bool IsWide(Rune rune) => rune.Value switch
    {
        >= 0x1100 and <= 0x115F => true,   // Hangul Jamo
        >= 0x2E80 and <= 0xA4CF => true,   // CJK radicals, kana, CJK symbols, Han, Yi
        >= 0xAC00 and <= 0xD7AF => true,   // Hangul syllables
        >= 0xF900 and <= 0xFAFF => true,   // CJK compatibility ideographs
        >= 0xFE30 and <= 0xFE4F => true,   // CJK compatibility forms
        >= 0xFF00 and <= 0xFF60 => true,   // Fullwidth forms
        >= 0xFFE0 and <= 0xFFE6 => true,
        >= 0x1F300 and <= 0x1FAFF => true, // Emoji and pictographs
        >= 0x20000 and <= 0x3FFFF => true, // Han extensions
        _ => false
    };

    /// <summary>Letters and digits of a Latin-style word, with the apostrophe and hyphen that join its parts.</summary>
    private static bool IsWordCharacter(string element)
    {
        var rune = FirstRune(element);
        if (IsWide(rune)) return false;
        if (Rune.IsLetterOrDigit(rune)) return true;
        return element is "'" or "\u2019" or "-" or "_";
    }

    private static bool IsWhitespace(string element) => element.Length == 1 && char.IsWhiteSpace(element[0]);
}
