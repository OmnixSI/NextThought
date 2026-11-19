using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NextThought.Core;

public static class BpeTokenization
{
    /// <summary>The pattern of the Qwen3 / GPT-4 family.</summary>
    public const string DefaultSplitPattern =
        @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
}

/// <summary>
/// A byte-level BPE tokenizer, the kind the Qwen3 / GPT-2 family uses (port of
/// ByteLevelBPETokenizer). Vocabulary and merges come from the model's tokenizer.json;
/// text is split by the model's pattern, turned into bytes, mapped to printable code points,
/// then merged until no merge applies.
/// </summary>
public sealed class ByteLevelBPETokenizer
{
    public sealed record Configuration(
        int EndOfTextToken = 151_643,
        bool AppendEndOfText = true,
        string SplitPattern = BpeTokenization.DefaultSplitPattern,
        bool NormalizeNfc = true)
    {
        /// <summary>The pattern of the Qwen3 / GPT-4 family.</summary>
        public const string DefaultSplitPattern =
            @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
    }

    public sealed class TokenizerException(string message) : Exception(message);

    private sealed record Pair(string First, string Second);

    private readonly Dictionary<string, int> tokenToId;
    private readonly Dictionary<int, string> idToToken;
    private readonly Dictionary<Pair, int> mergeRanks;
    private readonly Configuration configuration;
    private readonly string[] byteToScalar = new string[256];
    private readonly Dictionary<int, byte> scalarToByte = [];
    private readonly Regex? splitRegex;

    public ByteLevelBPETokenizer(string jsonPath, Configuration? configuration = null)
    {
        this.configuration = configuration ?? new Configuration();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new TokenizerException("无法读取分词器文件。");
        }
        using var json = document;
        var root = json.RootElement;
        if (!root.TryGetProperty("model", out var model) || !model.TryGetProperty("vocab", out var vocab)
            || !model.TryGetProperty("type", out var type) || type.GetString() != "BPE")
            throw new TokenizerException("分词器不是字节级 BPE 格式。");

        tokenToId = new Dictionary<string, int>(StringComparer.Ordinal);
        idToToken = [];
        foreach (var entry in vocab.EnumerateObject())
        {
            var id = entry.Value.GetInt32();
            tokenToId[entry.Name] = id;
            idToToken[id] = entry.Name;
        }
        if (root.TryGetProperty("added_tokens", out var added) && added.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in added.EnumerateArray())
            {
                if (!entry.TryGetProperty("content", out var content) || !entry.TryGetProperty("id", out var idValue)) continue;
                var text = content.GetString();
                if (text is null) continue;
                tokenToId[text] = idValue.GetInt32();
                idToToken[idValue.GetInt32()] = text;
            }
        }

        mergeRanks = [];
        var rank = 0;
        if (model.TryGetProperty("merges", out var merges) && merges.ValueKind == JsonValueKind.Array)
        {
            foreach (var merge in merges.EnumerateArray())
            {
                if (merge.ValueKind == JsonValueKind.Array)
                {
                    var parts = merge.EnumerateArray().Select(part => part.GetString()).ToArray();
                    if (parts.Length == 2 && parts[0] is { } first && parts[1] is { } second)
                        mergeRanks[new Pair(first, second)] = rank;
                    rank++;
                }
                else if (merge.ValueKind == JsonValueKind.String)
                {
                    var parts = (merge.GetString() ?? "").Split(' ', 2);
                    if (parts.Length == 2) mergeRanks[new Pair(parts[0], parts[1])] = rank;
                    rank++;
                }
            }
        }

        BuildByteTables();
        try
        {
            splitRegex = new Regex(this.configuration.SplitPattern, RegexOptions.Compiled, TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException)
        {
            splitRegex = null;
        }
    }

    /// <summary>Encodes text into token ids, appending the end-of-text token as the model was trained.</summary>
    public IReadOnlyList<int> Encode(string text)
    {
        var source = configuration.NormalizeNfc ? text.Normalize(NormalizationForm.FormC) : text;
        var ids = new List<int>();
        foreach (var piece in Split(source)) ids.AddRange(EncodePiece(piece));
        if (configuration.AppendEndOfText) ids.Add(configuration.EndOfTextToken);
        return ids;
    }

    public string Decode(IEnumerable<int> ids)
    {
        var bytes = new List<byte>();
        foreach (var id in ids)
        {
            if (!idToToken.TryGetValue(id, out var token)) continue;
            foreach (var rune in token.EnumerateRunes())
                if (scalarToByte.TryGetValue(rune.Value, out var value)) bytes.Add(value);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public int? TokenId(string token) => tokenToId.TryGetValue(token, out var id) ? id : null;

    private IReadOnlyList<string> Split(string text)
    {
        if (splitRegex is null) return text.Length == 0 ? [] : [text];
        return splitRegex.Matches(text).Select(match => match.Value).ToList();
    }

    /// <summary>One word: bytes, then code points, then merges.</summary>
    private List<int> EncodePiece(string piece)
    {
        var symbols = Encoding.UTF8.GetBytes(piece).Select(value => byteToScalar[value]).ToList();
        return Bpe(symbols).Select(token => tokenToId.GetValueOrDefault(token, -1)).Where(id => id >= 0).ToList();
    }

    /// <summary>Merges the pairs of code points that the model knows, lowest rank first.</summary>
    private List<string> Bpe(List<string> word)
    {
        if (word.Count <= 1) return word;
        var symbols = word;
        while (symbols.Count > 1)
        {
            var bestRank = int.MaxValue;
            var bestIndex = -1;
            for (var index = 0; index < symbols.Count - 1; index++)
            {
                var rank = mergeRanks.GetValueOrDefault(new Pair(symbols[index], symbols[index + 1]), int.MaxValue);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    bestIndex = index;
                }
            }
            if (bestIndex < 0) break;
            var first = symbols[bestIndex];
            var second = symbols[bestIndex + 1];
            var merged = new List<string>(symbols.Count);
            var position = 0;
            while (position < symbols.Count)
            {
                if (position < symbols.Count - 1 && symbols[position] == first && symbols[position + 1] == second)
                {
                    merged.Add(first + second);
                    position += 2;
                }
                else
                {
                    merged.Add(symbols[position]);
                    position++;
                }
            }
            symbols = merged;
        }
        return symbols;
    }

    /// <summary>GPT-2's reversible byte table: every byte gets a printable code point.</summary>
    private void BuildByteTables()
    {
        var bytes = Enumerable.Range(33, 94).Concat(Enumerable.Range(161, 12)).Concat(Enumerable.Range(174, 82)).ToList();
        var codes = bytes.ToList();
        var extra = 0;
        for (var value = 0; value < 256; value++)
        {
            if (bytes.Contains(value)) continue;
            bytes.Add(value);
            codes.Add(256 + extra);
            extra++;
        }
        for (var index = 0; index < bytes.Count; index++)
        {
            var text = char.ConvertFromUtf32(codes[index]);
            byteToScalar[bytes[index]] = text;
            scalarToByte[codes[index]] = (byte)bytes[index];
        }
    }
}
