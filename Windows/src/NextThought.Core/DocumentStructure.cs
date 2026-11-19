using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NextThought.Core;

/// <summary>Physical provenance, distinct from a position in a Markdown section array.</summary>
public sealed record DocumentLocator
{
    public int? Page { get; init; }
    public int? Slide { get; init; }
    public string? Sheet { get; init; }
    public int? Section { get; init; }
    public IReadOnlyList<string> HeadingPath { get; init; } = [];

    public string Label
    {
        get
        {
            var parts = new List<string>();
            if (Page is { } page) parts.Add("p." + page);
            if (Slide is { } slide) parts.Add("幻灯片 " + slide);
            if (Sheet is { } sheet) parts.Add("工作表 " + sheet);
            if (HeadingPath.Count > 0) parts.Add(string.Join(" > ", HeadingPath));
            if (parts.Count == 0 && Section is { } section) parts.Add("第 " + section + " 段");
            return string.Join(" · ", parts);
        }
    }
}

public sealed record StructuredDocumentBlock
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Kind { get; init; } = "paragraph";
    public string Markdown { get; init; } = "";
    public DocumentLocator Locator { get; init; } = new();
}

public sealed record StructuredDocument
{
    public int SchemaVersion { get; init; } = 1;
    public string Pipeline { get; init; } = "";
    public string? SourceHash { get; init; }
    public List<StructuredDocumentBlock> Blocks { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public string Status => Warnings.Count == 0 ? "complete" : "degraded";
    public Dictionary<string, string> Components { get; init; } = [];
    public string Markdown => string.Join("\n\n", Blocks.Select(block => block.Markdown));
}

/// <summary>
/// A small block parser (port of DocumentStructure.swift); tables, fenced code and display math
/// are atomic. Blocks carry a stable id derived from their place and content.
/// </summary>
public static class DocumentStructure
{
    public static IReadOnlyList<StructuredDocumentBlock> Parse(string markdown, DocumentLocator? locator = null)
    {
        locator ??= new DocumentLocator();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var result = new List<StructuredDocumentBlock>();
        var headingPath = locator.HeadingPath.ToList();
        var position = 0;
        while (position < lines.Length)
        {
            var line = lines[position];
            var trimmed = line.Trim();
            if (trimmed.Length == 0) { position++; continue; }

            var kind = "paragraph";
            var body = new List<string> { line };
            if (Heading(trimmed) is { } heading)
            {
                headingPath = headingPath.Take(Math.Max(0, heading.Level - 1)).ToList();
                headingPath.Add(heading.Title);
                kind = "heading";
                position++;
            }
            else if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                kind = "code";
                var marker = trimmed[..3];
                position++;
                while (position < lines.Length)
                {
                    body.Add(lines[position]);
                    position++;
                    if (body[^1].Trim().StartsWith(marker, StringComparison.Ordinal)) break;
                }
            }
            else if (trimmed.StartsWith("$$", StringComparison.Ordinal) || trimmed.StartsWith("\\[", StringComparison.Ordinal))
            {
                kind = "formula";
                position++;
                var end = trimmed.StartsWith("$$", StringComparison.Ordinal) ? "$$" : "\\]";
                if (!(trimmed.Length > end.Length && trimmed.EndsWith(end, StringComparison.Ordinal)))
                {
                    while (position < lines.Length)
                    {
                        body.Add(lines[position]);
                        position++;
                        if (body[^1].Trim().EndsWith(end, StringComparison.Ordinal)) break;
                    }
                }
            }
            else if (trimmed.StartsWith("<table", StringComparison.Ordinal))
            {
                kind = "table";
                position++;
                while (!string.Join("", body).Contains("</table>", StringComparison.OrdinalIgnoreCase) && position < lines.Length)
                {
                    body.Add(lines[position]);
                    position++;
                }
            }
            else if (trimmed.Contains('|') && position + 1 < lines.Length && IsTableSeparator(lines[position + 1]))
            {
                kind = "table";
                position++;
                while (position < lines.Length && lines[position].Contains('|'))
                {
                    body.Add(lines[position]);
                    position++;
                }
            }
            else
            {
                position++;
                while (position < lines.Length)
                {
                    var next = lines[position].Trim();
                    if (next.Length == 0 || Heading(next) is not null || next.StartsWith("```", StringComparison.Ordinal)
                        || next.StartsWith("~~~", StringComparison.Ordinal) || next.StartsWith("$$", StringComparison.Ordinal)
                        || next.StartsWith("\\[", StringComparison.Ordinal) || next.StartsWith("<table", StringComparison.Ordinal))
                        break;
                    if (next.Contains('|') && position + 1 < lines.Length && IsTableSeparator(lines[position + 1])) break;
                    body.Add(lines[position]);
                    position++;
                }
            }

            var location = locator with { HeadingPath = headingPath.ToList() };
            var text = string.Join("\n", body);
            var key = Fingerprint(location.Label + "\n" + kind + "\n" + text);
            var occurrence = result.Count(block => block.Id.StartsWith(key + "-", StringComparison.Ordinal));
            result.Add(new StructuredDocumentBlock { Id = key + "-" + occurrence, Kind = kind, Markdown = text, Locator = location });
        }
        return result;
    }

    public static (int Level, string Title)? Heading(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == '#') count++;
        if (count is < 1 or > 6) return null;
        if (count >= line.Length || line[count] != ' ') return null;
        return (count, line[(count + 1)..].Trim());
    }

    internal static bool IsTableSeparator(string line)
    {
        var text = line.Trim();
        return text.Contains('-') && text.Contains('|') && text.All(character => "|:- \t".Contains(character));
    }

    /// <summary>
    /// Preserve the canonical table; retrieval-only groups repeat the header.
    /// Groups complete HTML rows, keeping every rowspan within its group.
    /// </summary>
    public static IReadOnlyList<string> HtmlTablePieces(string text, int target)
    {
        var matches = Regex.Matches(text, "<tr(?:\\s[^>]*)?>[\\s\\S]*?</tr>", RegexOptions.IgnoreCase);
        var rows = matches.Select(match => match.Value).ToList();
        if (rows.Count <= 2) return [text];

        static int Span(string row)
        {
            var match = Regex.Match(row, "rowspan=[\"']?(\\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 1;
        }

        var headerCount = 1;
        while (headerCount < rows.Count && rows[headerCount].Contains("<th", StringComparison.OrdinalIgnoreCase)
               && !rows[headerCount].Contains("<td", StringComparison.OrdinalIgnoreCase))
            headerCount++;
        var headerEnd = 0;
        for (var index = 0; index < headerCount; index++) headerEnd = Math.Max(headerEnd, index + Span(rows[index]));
        headerCount = Math.Min(rows.Count, Math.Max(headerCount, headerEnd));
        var header = rows.Take(headerCount).ToList();

        var output = new List<string>();
        var group = new List<string>();
        var position = headerCount;
        while (position < rows.Count)
        {
            var end = Math.Min(rows.Count, position + Span(rows[position]));
            var cursor = position;
            while (cursor < end)
            {
                end = Math.Min(rows.Count, Math.Max(end, cursor + Span(rows[cursor])));
                cursor++;
            }
            var connected = rows.Skip(position).Take(end - position).ToList();
            if (group.Count > 0 && string.Concat(header.Concat(group).Concat(connected)).Length + 20 > target)
            {
                output.Add("<table>\n" + string.Join("\n", header.Concat(group)) + "\n</table>");
                group = [];
            }
            group.AddRange(connected);
            position = end;
        }
        if (group.Count > 0) output.Add("<table>\n" + string.Join("\n", header.Concat(group)) + "\n</table>");
        return output.Count == 0 ? [text] : output;
    }

    public static IReadOnlyList<string> Pieces(StructuredDocumentBlock block, int target = 900)
    {
        var text = block.Markdown;
        if (text.Length <= target || block.Kind is "formula" or "code") return [text];
        if (block.Kind == "table")
        {
            var rows = text.Split('\n');
            if (text.Trim().StartsWith("<table", StringComparison.OrdinalIgnoreCase)) return HtmlTablePieces(text, target);
            if (rows.Length <= 2 || !IsTableSeparator(rows[1])) return [text];
            var header = rows[0] + "\n" + rows[1];
            var group = header;
            var pieces = new List<string>();
            foreach (var row in rows.Skip(2))
            {
                if (group != header && group.Length + row.Length + 1 > target)
                {
                    pieces.Add(group);
                    group = header;
                }
                group += "\n" + row;
            }
            if (group != header) pieces.Add(group);
            return pieces.Count == 0 ? [text] : pieces;
        }

        var start = 0;
        var chunks = new List<string>();
        while (start < text.Length)
        {
            var end = Math.Min(start + target, text.Length);
            chunks.Add(text[start..end]);
            if (end == text.Length) break;
            start = Math.Max(start + 1, end - 120);
        }
        return chunks;
    }

    private static string Fingerprint(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
