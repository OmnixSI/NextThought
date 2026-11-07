using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NextThought.Platform;

/// <summary>Qwen's pinned NFC/Split/ByteLevel BPE, verified against tokenizers reference outputs.</summary>
public sealed class QwenTokenizer
{
    private readonly Dictionary<string, long> vocabulary;
    private readonly Dictionary<(string, string), int> ranks = [];
    private readonly Dictionary<string, long> special;
    private readonly Dictionary<string, long[]> cache = [];
    private readonly Regex split, specialPattern;
    private readonly char[] bytes = new char[256];
    private readonly long end;
    public QwenTokenizer(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(file)); var root = json.RootElement;
        if (root.GetProperty("normalizer").GetProperty("type").GetString() != "NFC") throw new InvalidDataException("不支持的分词器归一化格式。");
        vocabulary = root.GetProperty("model").GetProperty("vocab").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt64());
        int rank = 0;
        foreach (var merge in root.GetProperty("model").GetProperty("merges").EnumerateArray())
        {
            var pair = merge.ValueKind == JsonValueKind.Array ? merge.EnumerateArray().Select(x => x.GetString()!).ToArray() : merge.GetString()!.Split(' ');
            ranks.Add((pair[0], pair[1]), rank++);
        }
        special = root.GetProperty("added_tokens").EnumerateArray().ToDictionary(p => p.GetProperty("content").GetString()!, p => p.GetProperty("id").GetInt64());
        specialPattern = new Regex(string.Join('|', special.Keys.OrderByDescending(s => s.Length).Select(Regex.Escape)), RegexOptions.Compiled);
        var pattern = root.GetProperty("pre_tokenizer").GetProperty("pretokenizers")[0].GetProperty("pattern").GetProperty("Regex").GetString()!;
        split = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(2)); end = special["<|endoftext|>"];
        var visible = Enumerable.Range(33, 94).Concat(Enumerable.Range(161, 12)).Concat(Enumerable.Range(174, 82)).ToHashSet(); int next = 256;
        for (int b = 0; b < 256; b++) bytes[b] = (char)(visible.Contains(b) ? b : next++);
    }
    public long[] Encode(string input, int maxTokens = 512)
    {
        var text = input.Normalize(NormalizationForm.FormC); var ids = new List<long>(); int position = 0;
        foreach (Match token in specialPattern.Matches(text))
        {
            Append(text[position..token.Index]); ids.Add(special[token.Value]); position = token.Index + token.Length;
        }
        Append(text[position..]);
        if (ids.Count >= maxTokens) ids.RemoveRange(maxTokens - 1, ids.Count - maxTokens + 1);
        ids.Add(end); return ids.ToArray();
        void Append(string value)
        {
            foreach (Match piece in split.Matches(value))
            {
                var word = string.Concat(Encoding.UTF8.GetBytes(piece.Value).Select(b => bytes[b]));
                if (!cache.TryGetValue(word, out var encoded))
                {
                    var symbols = word.Select(c => c.ToString()).ToList();
                    while (symbols.Count > 1)
                    {
                        var bestRank = int.MaxValue; var best = -1;
                        for (int i = 0; i < symbols.Count - 1; i++)
                            if (ranks.TryGetValue((symbols[i], symbols[i + 1]), out var current) && current < bestRank) { bestRank = current; best = i; }
                        if (best < 0) break;
                        symbols[best] += symbols[best + 1]; symbols.RemoveAt(best + 1);
                    }
                    encoded = symbols.Select(s => vocabulary[s]).ToArray(); if (cache.Count >= 4096) cache.Clear(); cache[word] = encoded;
                }
                ids.AddRange(encoded);
            }
        }
    }
}
