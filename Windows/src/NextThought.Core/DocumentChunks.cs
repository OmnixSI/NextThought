using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NextThought.Core;

public sealed record RetrievalChunk(string Id, string DocumentId, string DocumentName, string Sha256, string Location, string Text, int? Page, int StartCharacter);
public sealed record RankedChunk(RetrievalChunk Chunk, double Score);
public static class DocumentChunks
{
    public const string Version = "chunks-v3-600-100-row-once";
    public static IReadOnlyList<RetrievalChunk> Build(IEnumerable<MaterialDocument> documents)
    {
        var chunks = new List<RetrievalChunk>();
        foreach (var document in documents.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var blocks = document.Extension == ".xlsx" ? document.Blocks.GroupBy(b => Regex.Replace(b.Location, @"![A-Z]+(\d+)$", "!$1"))
                .Select(row => new DocumentBlock(row.First().Location, string.Join("；", row.Select(b => b.Location + "：" + b.Text)))).ToArray() : document.Blocks.ToArray();
            foreach (var block in blocks)
            for (int offset = 0; offset < block.Text.Length; offset += 500)
            {
                var length = Math.Min(600, block.Text.Length - offset);
                // Keep UTF-16 pairs intact so the quoted source remains valid Unicode.
                if (length > 0 && char.IsHighSurrogate(block.Text[offset + length - 1])) length--;
                var start = offset > 0 && char.IsLowSurrogate(block.Text[offset]) ? offset - 1 : offset;
                var text = block.Text.Substring(start, offset + length - start);
                if (string.IsNullOrWhiteSpace(text)) continue;
                var identity = System.Text.Json.JsonSerializer.Serialize(new { Version, document.Id, document.Sha256, block.Location, start, text });
                chunks.Add(new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity))), document.Id, document.Name, document.Sha256, block.Location, text, block.Page, start));
                if (offset + length >= block.Text.Length) break;
            }
        }
        return chunks;
    }
    public static IReadOnlyList<SourceCitation> Citations(IEnumerable<RetrievalChunk> chunks, int limit = 6, int characterBudget = 7000)
    {
        var result = new List<SourceCitation>(); var seen = new HashSet<string>();
        foreach (var chunk in chunks)
        {
            if (result.Count >= limit || characterBudget < 100) break;
            if (!seen.Add(chunk.Id)) continue;
            var quote = chunk.Text[..Math.Min(characterBudget, chunk.Text.Length)]; characterBudget -= quote.Length;
            result.Add(new("D" + (result.Count + 1), chunk.DocumentId, chunk.DocumentName, chunk.Sha256, chunk.Location, quote, chunk.Page, chunk.Id));
        }
        return result;
    }
    public static IReadOnlyList<RetrievalChunk> FitTokenBudget(IReadOnlyList<RetrievalChunk> chunks, Func<string, int> tokenCount, int maximum)
    {
        var result = new List<RetrievalChunk>();
        foreach (var chunk in chunks)
        {
            if (tokenCount(chunk.Text) <= maximum) { result.Add(chunk); continue; }
            int offset = 0;
            while (offset < chunk.Text.Length)
            {
                int low = 1, high = chunk.Text.Length - offset, accepted = 0;
                while (low <= high)
                {
                    int length = (low + high) / 2;
                    var safeLength = char.IsHighSurrogate(chunk.Text[offset + length - 1]) ? length - 1 : length;
                    if (tokenCount(chunk.Text.Substring(offset, safeLength)) <= maximum) { accepted = safeLength; low = length + 1; } else high = length - 1;
                }
                if (accepted <= 0) throw new InvalidDataException("无法在模型词元预算内分块。");
                if (char.IsHighSurrogate(chunk.Text[offset + accepted - 1])) accepted--;
                if (accepted <= 0) throw new InvalidDataException("模型词元预算过小。");
                var text = chunk.Text.Substring(offset, accepted);
                var id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"fit-v1:{chunk.Id}:{offset}:{text}")));
                result.Add(chunk with { Id = id, Text = text, StartCharacter = chunk.StartCharacter + offset });
                if (offset + accepted == chunk.Text.Length) break;
                offset += Math.Max(1, accepted - Math.Min(80, accepted / 4));
                if (offset < chunk.Text.Length && char.IsLowSurrogate(chunk.Text[offset])) offset++;
            }
        }
        return result;
    }
}
