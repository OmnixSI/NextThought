using System.Text;

namespace NextThought.Core;

/// <summary>
/// How much of an answer has been said, and the sentence splitting the cue surfaces need.
/// Partial port of AnswerPoints from LiveInsights.swift: <see cref="Split"/> and
/// <see cref="StripMarkup"/> are here because CueScript depends on them; the coverage
/// heuristics come with the LiveInsights port.
/// </summary>
public static class AnswerPoints
{
    private const string Enders = "。！？!?；;";

    /// <summary>The answer as the sentences the user reads out, at most <paramref name="limit"/>.</summary>
    public static List<string> Split(string answer, int limit = 10)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        void Flush()
        {
            var piece = StripMarkup(current.ToString().Trim());
            if (piece.Length > 0) pieces.Add(piece);
            current.Clear();
        }

        var inCode = false;
        // The meaning written under each sentence is read, not said.
        foreach (var line in BilingualAnswer.Spoken(answer).Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal)) { inCode = !inCode; Flush(); continue; }
            if (inCode) continue;
            var text = trimmed.TrimStart('-', '•', '*', '·', '#', '>', ' ');
            foreach (var character in text)
            {
                current.Append(character);
                if (Enders.Contains(character) || (character == '.' && current.Length > 40)) Flush();
            }
            Flush();
        }
        // Very short pieces ("比如说：") belong with the next one.
        var merged = new List<string>();
        foreach (var piece in pieces)
        {
            if (merged.Count > 0 && merged[^1].Length < 8) merged[^1] = merged[^1] + piece;
            else merged.Add(piece);
        }
        return merged.Take(limit).ToList();
    }

    public static string StripMarkup(string text) => text.Replace("**", "").Replace("`", "").Trim();
}
