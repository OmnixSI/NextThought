using System.Text;

namespace NextThought.Core;

/// <summary>
/// Technical terms of a meeting's materials (port of MaterialTerms.swift): acronyms, words
/// with a capital inside, and letters joined to digits ("XL-MIMO", "TSIR", "RQ2"). They become
/// hotwords for recognition and spellings for answers; a term said once is too often a citation.
/// </summary>
public static class MaterialTerms
{
    private static readonly HashSet<string> Ordinary = new(StringComparer.Ordinal)
    {
        "PDF", "PPT", "URL", "HTTP", "HTTPS", "WWW", "USA", "UK", "OK", "II", "III", "IV", "VI", "VII", "VIII", "IX",
        "FIG", "TABLE", "IEEE", "ACM", "DOI", "ISBN", "ET", "AL"
    };

    public static IReadOnlyList<string> Extract(IReadOnlyList<string> texts, int limit = 60)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var text in texts)
        {
            var token = new StringBuilder();
            void Flush()
            {
                var word = token.ToString().Trim('-', '/');
                token.Clear();
                if (!IsTerm(word)) return;
                if (!counts.ContainsKey(word)) order.Add(word);
                counts[word] = counts.GetValueOrDefault(word) + 1;
            }

            foreach (var character in text)
            {
                if (character < 128 && (char.IsLetter(character) || char.IsNumber(character) || character == '-' || character == '/'))
                    token.Append(character);
                else
                    Flush();
            }
            Flush();
        }

        var repeated = order.Where(word => counts[word] >= 2).ToList();
        var candidates = repeated.Count > 0 ? repeated : order;
        return candidates
            .Select((word, index) => (word, index))
            .OrderByDescending(entry => counts[entry.word])
            .ThenBy(entry => entry.index)
            .Select(entry => entry.word)
            .Take(limit)
            .ToList();
    }

    public static bool IsTerm(string word)
    {
        if (word.Length is < 2 or > 24) return false;
        var first = word[0];
        if (!char.IsLetterOrDigit(first)) return false;
        var letters = word.Count(char.IsLetter);
        if (letters < 2 || Ordinary.Contains(word.ToUpperInvariant())) return false;
        var capitals = word.Count(char.IsUpper);
        var digits = word.Count(char.IsNumber);
        // A unit ("bit/s/Hz") is not a term.
        if (word.Contains('/')) return false;
        // A double-barrelled surname ("Nimier-David", or its tail "lez-Prelcic").
        var parts = word.Split('-');
        if (parts.Length > 1 && parts.Any(part =>
                part.Length > 2 && char.IsUpper(part[0]) && part.Skip(1).All(char.IsLower))) return false;
        // A year, a page range, a version: not a term.
        if (digits > letters) return false;
        // XL-MIMO, TSIR, DeepMIMO, RQ2; not a HEADING IN CAPITALS.
        if (capitals >= 2) return !(capitals == letters && letters > 8);
        // mmWave, eNodeB: a capital inside a word that starts small.
        if (capitals == 1 && !char.IsUpper(first)) return true;
        return false;
    }
}
