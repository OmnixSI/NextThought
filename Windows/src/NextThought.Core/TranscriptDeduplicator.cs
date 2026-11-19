namespace NextThought.Core;

/// <summary>
/// Removes the part of a new sentence that repeats the tail of the previous one. Streaming
/// recognizers sometimes report a sentence twice when the same audio is fed again after a
/// pause, or when a partial turns into a final with its lead-in. Ported from
/// TranscriptDeduplicator.swift.
/// </summary>
public static class TranscriptDeduplicator
{
    private readonly record struct Token(string Value, int Start, int End);

    public static string RemoveOverlappingPrefix(string previous, string incoming, DateTimeOffset? previousEnd, DateTimeOffset incomingStart)
    {
        if (previousEnd is { } end && incomingStart < end) return RemoveRepeatedPrefix(previous, incoming);
        return incoming.Trim();
    }

    public static string RemoveRepeatedPrefix(string previous, string incoming)
    {
        var text = incoming.Trim();
        if (text.Length == 0) return "";
        var left = Tokens(previous);
        var right = Tokens(text);
        if (left.Count == right.Count && left.Count > 0
            && left.Zip(right).All(pair => string.Equals(pair.First.Value, pair.Second.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return "";
        }
        if (ContainsCjk(previous) && ContainsCjk(text)) return RemoveCjkOverlap(previous, text);

        var limit = Math.Min(6, Math.Min(left.Count, right.Count));
        if (limit < 2) return text;
        for (var count = limit; count >= 2; count--)
        {
            var matches = true;
            for (var index = 0; index < count; index++)
            {
                var first = left[left.Count - count + index].Value;
                var second = right[index].Value;
                if (!string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) { matches = false; break; }
            }
            if (matches) return TrimLeadingSeparators(text[right[count - 1].End..]);
        }
        return text;
    }

    private static string RemoveCjkOverlap(string previous, string incoming)
    {
        var left = TextElements(previous);
        var right = TextElements(incoming);
        var limit = Math.Min(60, Math.Min(left.Count, right.Count));
        if (limit >= 4)
        {
            for (var count = limit; count >= 4; count--)
            {
                if (left.Skip(left.Count - count).SequenceEqual(right.Take(count), StringComparer.Ordinal))
                    return TrimLeadingSeparators(string.Concat(right.Skip(count)));
            }
        }
        return incoming;
    }

    private static List<Token> Tokens(string text)
    {
        var result = new List<Token>();
        var start = -1;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsLetterOrDigit(character))
            {
                if (start < 0) start = index;
            }
            else
            {
                if (start >= 0) result.Add(new Token(text[start..index], start, index));
                start = -1;
            }
        }
        if (start >= 0) result.Add(new Token(text[start..], start, text.Length));
        return result;
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value is >= 0x4E00 and <= 0x9FFF) return true;
        return false;
    }

    private static string TrimLeadingSeparators(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (char.IsWhiteSpace(character) || IsPunctuation(character)) index++;
            else break;
        }
        return text[index..].Trim();
    }

    private static bool IsPunctuation(char character) => char.IsPunctuation(character) || char.IsSymbol(character);

    private static List<string> TextElements(string text)
    {
        var elements = new List<string>(text.Length);
        var starts = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            elements.Add(text[starts[i]..end]);
        }
        return elements;
    }
}
