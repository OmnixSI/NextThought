using System.Text.RegularExpressions;

namespace NextThought.Core;

public enum AnswerStyle
{
    Concise,
    Detailed
}

public sealed record ContextExcerpt(
    string Source, int Page, string Text, DocumentLocator? Locator = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("chunkID")] string? ChunkId = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("blockID")] string? BlockId = null)
{
    /// <summary>B19: source plus locator label, or the page number (ContextExcerpt.citation).</summary>
    public string Citation => Source + " " + (Locator?.Label ?? "第 " + Page + " 段");

    /// <summary>B19: whole excerpts only, while the character budget and count allow.</summary>
    public static IReadOnlyList<ContextExcerpt> Budget(IReadOnlyList<ContextExcerpt> excerpts, int characters, int count = 8)
    {
        var left = characters;
        var result = new List<ContextExcerpt>();
        foreach (var excerpt in excerpts)
        {
            if (result.Count >= count) break;
            if (excerpt.Text.Length <= left)
            {
                result.Add(excerpt);
                left -= excerpt.Text.Length;
            }
        }
        return result;
    }
}

/// <summary>
/// Small, deterministic, offline lexical index (port of SessionContextIndex). This is not
/// semantic/vector search; import work is bounded and tokenization happens once per rebuild.
/// </summary>
public sealed class SessionContextIndex
{
    private sealed record Chunk(ContextExcerpt Excerpt, HashSet<string> Terms, int Order);

    private readonly List<Chunk> chunks = [];
    private readonly Dictionary<string, int> documentFrequency = new(StringComparer.Ordinal);

    /// <summary>Materials per scenario.</summary>
    public const int MaximumDocuments = 1000;

    public SessionContextIndex(IReadOnlyList<ContextChunk> source)
    {
        foreach (var chunk in source)
        {
            chunks.Add(new Chunk(
                new ContextExcerpt(chunk.Source, chunk.Page, chunk.Text, chunk.Locator, chunk.Fingerprint, chunk.BlockId),
                Terms(chunk.Text), chunk.Order));
        }
        foreach (var chunk in chunks)
            foreach (var term in chunk.Terms)
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
    }

    public static SessionContextIndex FromDocuments(IReadOnlyList<MaterialDocument> documents) =>
        new(ContextChunker.Chunks(documents));

    public IReadOnlyList<ContextExcerpt> Retrieve(string question, int maximumCharacters = 8000)
    {
        if (maximumCharacters <= 0) return [];
        var remaining = Math.Min(maximumCharacters, 16000);
        var result = new List<ContextExcerpt>();
        foreach (var order in RankedOrders(question).Take(8))
        {
            if (remaining <= 0) break;
            if (order < 0 || order >= chunks.Count) continue;
            var excerpt = chunks[order].Excerpt;
            if (excerpt.Text.Length > remaining) continue;
            result.Add(excerpt);
            remaining -= excerpt.Text.Length;
        }
        return result;
    }

    /// <summary>The chunk orders that match the question, best first.</summary>
    public IReadOnlyList<int> RankedOrders(string question)
    {
        var query = Terms(question.Length > 2000 ? question[^2000..] : question);
        if (query.Count == 0) return [];
        return chunks
            .Select(chunk =>
            {
                var overlap = query.Where(chunk.Terms.Contains).ToList();
                if (overlap.Count == 0) return (Chunk: chunk, Score: (double?)null);
                var score = overlap.Sum(term => Math.Log(1 + (double)chunks.Count / (documentFrequency.GetValueOrDefault(term) + 1)))
                            / Math.Sqrt(Math.Max(1, chunk.Terms.Count));
                return (Chunk: chunk, Score: (double?)score);
            })
            .Where(item => item.Score is not null)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Order)
            .Select(item => item.Chunk.Order)
            .ToList();
    }

    /// <summary>Lower-cased Latin words longer than one letter plus CJK unigrams/bigrams.</summary>
    public static HashSet<string> Terms(string text)
    {
        var stop = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "a", "an", "is", "are", "was", "were", "of", "to", "and", "in", "on", "this", "that",
            "what", "how", "why", "you", "your", "explain", "can", "could", "please", "does", "do"
        };
        var result = new HashSet<string>(StringComparer.Ordinal);
        var cjk = new List<char>();
        var word = new System.Text.StringBuilder();

        void FlushWord()
        {
            if (word.Length > 1 && !stop.Contains(word.ToString())) result.Add(word.ToString());
            word.Clear();
        }
        void FlushCjk()
        {
            if (cjk.Count == 1) result.Add(cjk[0].ToString());
            if (cjk.Count > 1)
                for (var index = 0; index < cjk.Count - 1; index++)
                    result.Add(new string([cjk[index], cjk[index + 1]]));
            cjk.Clear();
        }

        foreach (var character in text.ToLowerInvariant())
        {
            if (character is >= '\u3400' and <= '\u9FFF')
            {
                FlushWord();
                cjk.Add(character);
            }
            else if (char.IsLetterOrDigit(character))
            {
                FlushCjk();
                word.Append(character);
            }
            else
            {
                FlushWord();
                FlushCjk();
            }
        }
        FlushWord();
        FlushCjk();
        return result;
    }
}

public static class ManualQuestionBuilder
{
    public static string? Make(string typed, string partial, IReadOnlyList<string> recent)
    {
        var explicitQuestion = typed.Trim();
        if (explicitQuestion.Length > 0) return explicitQuestion.Length > 8000 ? explicitQuestion[..8000] : explicitQuestion;
        var live = partial.Trim();
        var lines = recent.TakeLast(3).ToList();
        if (live.Length > 0) lines.Add(live);
        var text = string.Join("\n", lines);
        if (text.Trim().Length == 0) return null;
        return "请根据以下最新转录，回答其中的最后一个问题或解释最后一个知识点；如果没有明确对象，请指出，不要自造问题。\n"
               + (text.Length > 4000 ? text[^4000..] : text);
    }
}

/// <summary>
/// Extracts visible text from Office Open XML parts (port of OfficeXMLText): PowerPoint slides,
/// Excel shared strings and Word documents. Regex-based and never resolves external entities.
/// </summary>
public static class OfficeXMLText
{
    private static readonly Regex ParagraphPattern =
        new("<(a:p|w:p|si)[ >].*?</\\1>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TextPattern =
        new("<(?:a:t|w:t|t)(?: [^>]*)?>(.*?)</(?:a:t|w:t|t)>", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string Extract(string xml)
    {
        var paragraphs = new List<string>();
        foreach (Match match in ParagraphPattern.Matches(xml))
        {
            var pieces = TextPattern.Matches(match.Value).Select(text => Decode(text.Groups[1].Value));
            var line = string.Concat(pieces).Trim();
            if (line.Length > 0) paragraphs.Add(line);
        }
        return string.Join("\n", paragraphs);
    }

    public static string Decode(string text) =>
        text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"")
            .Replace("&apos;", "'").Replace("&amp;", "&");

    /// <summary>Sorts "ppt/slides/slide10.xml" after "slide9.xml".</summary>
    public static IReadOnlyList<string> NumericOrder(IReadOnlyList<string> names) =>
        names.OrderBy(name => int.TryParse(new string(name.Where(char.IsDigit).ToArray()), out var number) ? number : 0)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
}
