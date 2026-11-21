using System.Text;

namespace NextThought.Core;

/// <summary>
/// J5: the port of UtteranceText.swift (Mac v3.4.0, f015e8c6). The final words of an utterance,
/// chosen from the two passes of the local recognizer.
///
/// The live stream hears everything since the last utterance ended. The offline pass re-decodes
/// only the samples the voice-activity segmenter cut out, and the segmenter goes by loudness:
/// speech from across a room, or a quiet speaker, is recognised by the stream but is not "speech"
/// to it. When a loud sentence finally closed the segment, the offline text of those few seconds
/// used to replace everything the stream had shown for minutes (395 s of a talk came out as 61
/// characters). The offline text is better, so it wins whenever it covers what the stream heard;
/// otherwise the stream's words are kept.
/// </summary>
public static class UtteranceText
{
    /// <param name="streaming">What the live stream decoded since the last utterance ended.</param>
    /// <param name="streamSeconds">How much audio that is.</param>
    /// <param name="refined">The offline pass over the segment.</param>
    /// <param name="segmentSeconds">How much audio the segment holds.</param>
    public static string Final(string streaming, double streamSeconds, string refined, double segmentSeconds)
    {
        var heard = Units(streaming);
        var redone = Units(refined);
        if (redone == 0) return streaming;
        if (heard == 0) return refined;
        // The stream ran on well before the segment began, and the offline text is much shorter
        // than what was heard: it only has the end of it.
        if (streamSeconds > segmentSeconds + 2 && redone * 10 < heard * 7) return streaming;
        return refined;
    }

    /// <summary>Latin words and CJK characters, one each.</summary>
    public static int Units(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            var ideograph = (value >= 0x3400 && value <= 0x4DBF)
                || (value >= 0x4E00 && value <= 0x9FFF)
                || (value >= 0xF900 && value <= 0xFAFF)
                || (value >= 0x20000 && value <= 0x2FA1F);
            if (ideograph)
            {
                count++;
                inWord = false;
                continue;
            }
            var latin = (value >= 0x30 && value <= 0x39)
                || (value >= 0x41 && value <= 0x5A)
                || (value >= 0x61 && value <= 0x7A)
                || (value >= 0xC0 && value <= 0x24F)
                || value == 0x27
                || value == 0x2019;
            if (latin)
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else
            {
                inWord = false;
            }
        }
        return count;
    }
}

/// <summary>
/// When the live stream should be closed without the segmenter: the words that are on screen but
/// belong to no finished sentence yet (Mac PendingUtterance, 3.4.0).
///
/// Normally the segmenter closes an utterance 0.7 s after the speaker stops, or after 12 s of
/// talk. It never does for speech it does not count as speech, and then nothing became a sentence
/// for minutes: no line in the transcript, nothing for the notes, nothing a question could be
/// recognised in.
/// </summary>
public sealed class PendingUtterance
{
    /// <summary>The words have not changed for this long: the speaker stopped.</summary>
    public const double Quiet = 2.5;

    /// <summary>The words have been pending this long: the speaker goes on, the line is cut here.</summary>
    public const double Longest = 20;

    public string Text { get; private set; } = "";
    public DateTimeOffset? Since { get; private set; }
    public DateTimeOffset? ChangedAt { get; private set; }

    /// <summary>The live stream decoded <paramref name="text"/> for the utterance in progress.</summary>
    public void Heard(string text, DateTimeOffset now)
    {
        if (text.Length == 0) return;
        Since ??= now;
        if (text != Text)
        {
            Text = text;
            ChangedAt = now;
        }
    }

    /// <summary>The utterance was closed (by the segmenter, or here).</summary>
    public void Closed()
    {
        Text = "";
        Since = null;
        ChangedAt = null;
    }

    /// <summary>Nobody else is going to close it.</summary>
    public bool IsOverdue(DateTimeOffset now)
    {
        if (Since is not { } since || ChangedAt is not { } changedAt || Text.Length == 0) return false;
        return (now - changedAt).TotalSeconds >= Quiet || (now - since).TotalSeconds >= Longest;
    }
}
