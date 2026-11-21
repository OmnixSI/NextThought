using System.Text.RegularExpressions;

namespace NextThought.Core;

/// <summary>One piece of a material: the unit the lexical and semantic indexes share.</summary>
public sealed record ContextChunk(
    string Source, int Page, string Text, int Order, string Fingerprint,
    DocumentLocator? Locator = null, string? BlockId = null);

/// <summary>FNV-1a hash of a text; it only has to notice a change.</summary>
public static class ContextFingerprint
{
    public static string Hash(string text)
    {
        var value = 0xcbf29ce484222325UL;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(text))
        {
            value ^= b;
            value = unchecked(value * 0x100000001b3UL);
        }
        return value.ToString("x");
    }
}

/// <summary>
/// Splits materials into the chunks both indexes share (port of ContextChunker):
/// 900 characters per chunk with 120 characters of overlap; short neighbouring paragraphs merge.
/// </summary>
public static class ContextChunker
{
    public const string Version = "3-merged-small";
    public const int ChunkCharacters = 900;
    public const int OverlapCharacters = 120;

    public static IReadOnlyList<ContextChunk> Chunks(IReadOnlyList<MaterialDocument> documents)
    {
        var result = new List<ContextChunk>();
        foreach (var document in documents)
        {
            var blocks = new List<StructuredDocumentBlock>();
            foreach (var block in document.Blocks)
            {
                var locator = new DocumentLocator { Page = block.Page, Section = block.Page is null ? null : 0 };
                blocks.AddRange(DocumentStructure.Parse(block.Text, locator));
            }
            foreach (var block in Merged(blocks.Where(block => block.Kind != "heading").ToList()))
            {
                var pieces = DocumentStructure.Pieces(block, ChunkCharacters);
                for (var part = 0; part < pieces.Count; part++)
                {
                    var path = string.Join(" > ", new[] { document.Name }.Concat(block.Locator.HeadingPath));
                    var body = path + "\n" + pieces[part];
                    var fingerprint = ContextFingerprint.Hash(Version + document.Id + "\n" + block.Id + "\n" + part + "\n" + body);
                    result.Add(new ContextChunk(document.Name, block.Locator.Page ?? block.Locator.Slide ?? block.Locator.Section ?? 0,
                        body, result.Count, fingerprint, block.Locator, block.Id));
                }
            }
        }
        return result;
    }

    /// <summary>B22: chunk imported ContextDocuments, honouring their enabled flag.</summary>
    public static IReadOnlyList<ContextChunk> Chunks(IReadOnlyList<ContextDocument> documents)
    {
        var result = new List<ContextChunk>();
        foreach (var document in documents.Where(document => document.Enabled))
        {
            if (document.Content is { } content && content.ValueKind == System.Text.Json.JsonValueKind.Object
                && content.TryGetProperty("blocks", out var blocks) && blocks.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var material = new MaterialDocument { Id = document.Id, Name = document.Name, Extension = System.IO.Path.GetExtension(document.Name) };
                foreach (var block in blocks.EnumerateArray())
                {
                    var text = block.TryGetProperty("markdown", out var markdown) && markdown.ValueKind == System.Text.Json.JsonValueKind.String
                        ? markdown.GetString() : block.TryGetProperty("text", out var plain) && plain.ValueKind == System.Text.Json.JsonValueKind.String
                        ? plain.GetString() : null;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    int? page = null;
                    if (block.TryGetProperty("locator", out var locator) && locator.ValueKind == System.Text.Json.JsonValueKind.Object
                        && locator.TryGetProperty("page", out var pageValue) && pageValue.TryGetInt32(out var pageNumber)) page = pageNumber;
                    material.Blocks.Add(new DocumentBlock("段 " + (material.Blocks.Count + 1), text, page));
                }
                result.AddRange(Chunks([material]));
                continue;
            }
            for (var index = 0; index < document.Pages.Count; index++)
            {
                var locator = new DocumentLocator { Section = index + 1 };
                var parsed = DocumentStructure.Parse(document.Pages[index], locator);
                foreach (var block in Merged(parsed.Where(block => block.Kind != "heading").ToList()))
                {
                    var pieces = DocumentStructure.Pieces(block, ChunkCharacters);
                    for (var part = 0; part < pieces.Count; part++)
                    {
                        var body = document.Name + "\n" + pieces[part];
                        var fingerprint = ContextFingerprint.Hash(Version + document.Id + "\n" + block.Id + "\n" + part + "\n" + body);
                        result.Add(new ContextChunk(document.Name, locator.Section ?? 0, body, result.Count, fingerprint, locator, block.Id));
                    }
                }
            }
        }
        return result;
    }

    /// <summary>Short paragraphs of one place (same page or slide) become one block.</summary>
    internal static IReadOnlyList<StructuredDocumentBlock> Merged(IReadOnlyList<StructuredDocumentBlock> blocks)
    {
        var result = new List<StructuredDocumentBlock>();
        var run = new List<StructuredDocumentBlock>();
        var length = 0;
        void Flush()
        {
            if (run.Count == 0) return;
            result.Add(run.Count == 1
                ? run[0]
                : run[0] with { Kind = "paragraph", Markdown = string.Join("\n", run.Select(block => block.Markdown)) });
            run.Clear();
            length = 0;
        }
        foreach (var block in blocks)
        {
            var place = block.Locator.Label;
            var canMerge = block.Kind == "paragraph"
                           && (run.Count == 0 || run[0].Locator.Label == place)
                           && length + block.Markdown.Length <= ChunkCharacters;
            if (!canMerge)
            {
                Flush();
                if (block.Kind != "paragraph") { result.Add(block); continue; }
            }
            run.Add(block);
            length += block.Markdown.Length;
        }
        Flush();
        return result;
    }
}

/// <summary>
/// The embeddings of the material chunks (port of SemanticIndex). Vectors are stored in chunk
/// order; each remembers the hash of the text it was made for, so changed text counts as missing.
/// </summary>
public sealed class SemanticIndex
{
    public sealed record Entry(string Fingerprint, float[] Vector);

    private readonly List<Entry> entries = [];

    public SemanticIndex(IReadOnlyList<Entry>? entries = null)
    {
        if (entries is not null) this.entries.AddRange(entries);
    }

    public IReadOnlyList<Entry> Entries => entries;
    public bool IsEmpty => entries.All(entry => entry.Vector.Length == 0);
    public int EmbeddedCount => entries.Count(entry => entry.Vector.Length > 0);
    public int Dimension => entries.Count > 0 ? entries[0].Vector.Length : 0;

    /// <summary>Writes one chunk vector, growing the storage when needed.</summary>
    public void SetVector(float[] vector, string fingerprint, int order)
    {
        if (order < 0) return;
        while (entries.Count <= order) entries.Add(new Entry("", []));
        entries[order] = new Entry(fingerprint, vector);
    }

    /// <summary>The vector of a chunk, but only when it was made for the current text.</summary>
    public float[]? Vector(int order, string fingerprint)
    {
        if (order < 0 || order >= entries.Count) return null;
        var entry = entries[order];
        return entry.Vector.Length > 0 && entry.Fingerprint == fingerprint ? entry.Vector : null;
    }

    public void RemoveAll() => entries.Clear();

    /// <summary>Cosine similarity between the query and every embedded chunk, best first.</summary>
    public IReadOnlyList<(int Order, double Score)> RankedOrders(float[] query, IReadOnlyList<string> fingerprints)
    {
        if (query.Length == 0 || query.Any(value => !float.IsFinite(value))) return [];
        var result = new List<(int Order, double Score)>();
        for (var order = 0; order < entries.Count; order++)
        {
            var entry = entries[order];
            if (entry.Vector.Length == 0) continue;
            if (order >= fingerprints.Count || fingerprints[order] != entry.Fingerprint) continue;
            if (entry.Vector.Length != query.Length || entry.Vector.Any(value => !float.IsFinite(value))) continue;
            result.Add((order, Dot(query, entry.Vector)));
        }
        return result.OrderByDescending(item => item.Score).ThenBy(item => item.Order).ToList();
    }

    public static double Dot(float[] left, float[] right)
    {
        var count = Math.Min(left.Length, right.Length);
        var sum = 0.0;
        for (var index = 0; index < count; index++) sum += (double)left[index] * right[index];
        return sum;
    }
}

/// <summary>Reciprocal Rank Fusion of the lexical and semantic rankings.</summary>
public static class HybridRanker
{
    public const double Constant = 60.0;

    public static IReadOnlyList<(int Order, double Score)> Fuse(
        IReadOnlyList<int> lexical, IReadOnlyList<int> semantic, double constant = Constant)
    {
        var scores = new Dictionary<int, double>();
        for (var rank = 0; rank < lexical.Count; rank++)
            scores[lexical[rank]] = scores.GetValueOrDefault(lexical[rank]) + 1 / (constant + rank + 1);
        for (var rank = 0; rank < semantic.Count; rank++)
            scores[semantic[rank]] = scores.GetValueOrDefault(semantic[rank]) + 1 / (constant + rank + 1);
        return scores.Select(pair => (Order: pair.Key, Score: pair.Value))
            .OrderByDescending(item => item.Score).ThenBy(item => item.Order).ToList();
    }
}

public static class RetrievalPolicy
{
    /// <summary>Validation midpoint (v2); a low-similarity list may be empty.</summary>
    public const double MinimumSemanticScore = 0.43;
}

/// <summary>
/// The retrieval the app uses (port of HybridContextIndex): the lexical index plus the semantic
/// index when an embedding model is installed. Explicit page or file requests win over similarity.
/// </summary>
public sealed class HybridContextIndex
{
    private static readonly Regex PagePattern =
        new("(?:第\\s*(\\d+)\\s*[页張张]|p\\.?\\s*(\\d+)|(?:slide|幻灯片)\\s*(\\d+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public HybridContextIndex(IReadOnlyList<MaterialDocument> documents, SemanticIndex? semantic = null)
    {
        Chunks = ContextChunker.Chunks(documents);
        Lexical = new SessionContextIndex(Chunks);
        Semantic = semantic ?? new SemanticIndex();
    }

    public IReadOnlyList<ContextChunk> Chunks { get; }
    public SessionContextIndex Lexical { get; }
    public SemanticIndex Semantic { get; }

    /// <summary>True when the semantic index can contribute to the ranking.</summary>
    public bool HasVectors => !Semantic.IsEmpty;

    public IReadOnlyList<ContextExcerpt> Retrieve(
        string question, float[]? queryVector = null, int maximumCharacters = 8000,
        double minimumSemanticScore = RetrievalPolicy.MinimumSemanticScore)
    {
        if (maximumCharacters <= 0) return [];
        var lexicalOrders = Lexical.RankedOrders(question);
        var fingerprints = Chunks.Select(chunk => chunk.Fingerprint).ToList();
        var semanticRanking = queryVector is null ? [] : Semantic.RankedOrders(queryVector, fingerprints);
        var scores = semanticRanking.ToDictionary(item => item.Order, item => item.Score);
        var semanticOrders = semanticRanking.Where(item => item.Score >= minimumSemanticScore).Select(item => item.Order).ToList();
        var eligibleLexical = lexicalOrders.Where(order => !scores.TryGetValue(order, out var score) || score >= minimumSemanticScore).ToList();
        IReadOnlyList<(int Order, double Score)> fused = semanticOrders.Count == 0
            ? eligibleLexical.Select(order => (order, 0.0)).ToList()
            : HybridRanker.Fuse(eligibleLexical, semanticOrders);

        var lower = question.ToLowerInvariant();
        var pageMatch = PagePattern.Match(lower);
        int? page = null;
        if (pageMatch.Success)
            for (var group = 1; group < pageMatch.Groups.Count; group++)
                if (pageMatch.Groups[group].Success && int.TryParse(pageMatch.Groups[group].Value, out var number)) { page = number; break; }

        static string Normalize(string text) => new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var questionKey = Normalize(lower);
        var named = Chunks.Where(chunk =>
        {
            var file = Path.GetFileNameWithoutExtension(chunk.Source);
            var stem = file.Split('_').FirstOrDefault() ?? chunk.Source;
            var key = Normalize(stem);
            return key.Length >= 4 && questionKey.Contains(key, StringComparison.Ordinal);
        }).Select(chunk => chunk.Source).ToHashSet(StringComparer.Ordinal);

        var explicitOrders = Chunks.Where(chunk =>
        {
            var physical = chunk.Locator?.Page ?? chunk.Locator?.Slide;
            return (page is not null && physical == page && (named.Count == 0 || named.Contains(chunk.Source)))
                   || (page is null && named.Contains(chunk.Source));
        }).Select(chunk => chunk.Order).ToList();
        var explicitSet = explicitOrders.ToHashSet();
        var ranking = explicitOrders.Select(order => (Order: order, Score: 1.0))
            .Concat(fused.Where(item => !explicitSet.Contains(item.Order))).ToList();

        var remaining = Math.Min(maximumCharacters, 16000);
        var result = new List<ContextExcerpt>();
        foreach (var entry in ranking.Take(8))
        {
            if (remaining <= 0) break;
            if (entry.Order < 0 || entry.Order >= Chunks.Count) continue;
            var chunk = Chunks[entry.Order];
            if (chunk.Text.Length > remaining) continue;
            if (chunk.Text.Trim().Length == 0) continue;
            result.Add(new ContextExcerpt(chunk.Source, chunk.Page, chunk.Text, chunk.Locator, chunk.Fingerprint, chunk.BlockId));
            remaining -= chunk.Text.Length;
        }
        return result;
    }
}
