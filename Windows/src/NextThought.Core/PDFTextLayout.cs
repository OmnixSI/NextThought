using System.Text;
using System.Text.RegularExpressions;

namespace NextThought.Core;

/// <summary>
/// Turns the text layer of PDF pages into text worth indexing (port of PDFTextLayout):
/// drops running headers/footers and page numbers, joins wrapped lines, marks certain headings.
/// Text work only, so it stays conservative.
/// </summary>
public static class PDFTextLayout
{
    private static readonly Regex Leaders = new(@"(\s*\.){4,}\s*", RegexOptions.Compiled);
    private static readonly Regex PageNumber = new(
        @"^(page\s*|第\s*|[-–—]\s*)?\d{1,4}(\s*(/|of|页|[-–—])\s*\d{0,4})?\s*(页)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Appendix = new(@"^APPENDIX( [A-Z])?$", RegexOptions.Compiled);
    private static readonly Regex Roman = new(@"^(I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII)\.\s+[A-Z]", RegexOptions.Compiled);
    private static readonly Regex Subsection = new(@"^[A-H]\.\s+[A-Z][a-z]", RegexOptions.Compiled);
    private static readonly Regex Numbered = new(@"^\d{1,2}(\.\d{1,2}){0,3}\.?\s+", RegexOptions.Compiled);

    private static readonly HashSet<string> Named = new(StringComparer.Ordinal)
    {
        "abstract", "introduction", "references", "bibliography", "acknowledgements", "acknowledgments",
        "acknowledgement", "conclusion", "conclusions", "related work", "contents", "table of contents",
        "摘要", "引言", "绪论", "参考文献", "致谢", "结论", "结语", "附录", "目录"
    };

    /// <summary>Lines that repeat across the pages and belong to none of them.</summary>
    public static IReadOnlySet<string> Furniture(IReadOnlyList<string> pages)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (pages.Count < 4) return result;
        var anywhere = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new Dictionary<string, int>(StringComparer.Ordinal);
        var repeated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            var keys = page.Replace("\r\n", "\n").Split('\n').Select(Key).Where(IsFurnitureCandidate).ToList();
            var onPage = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var key in keys) onPage[key] = onPage.GetValueOrDefault(key) + 1;
            foreach (var (key, count) in onPage)
            {
                if (count > 2) repeated.Add(key);
                anywhere[key] = anywhere.GetValueOrDefault(key) + 1;
            }
            foreach (var key in keys.Take(2).Concat(keys.TakeLast(2)).ToHashSet()) edges[key] = edges.GetValueOrDefault(key) + 1;
        }
        var common = Math.Max(3, (int)Math.Ceiling(pages.Count * 0.5));
        var atEdges = Math.Max(3, (int)Math.Ceiling(pages.Count * 0.3));
        foreach (var (key, count) in anywhere) if (count >= common) result.Add(key);
        foreach (var (key, count) in edges) if (count >= atEdges) result.Add(key);
        result.ExceptWith(repeated);
        return result;
    }

    internal static bool IsFurnitureCandidate(string key) => key.Length <= 80 && key.Count(char.IsLetter) >= 4;

    /// <summary>One page as Markdown: furniture and page numbers removed, wrapped lines joined, headings marked.</summary>
    public static string Markdown(string page, IReadOnlySet<string>? furniture = null, bool numberedHeadings = true)
    {
        furniture ??= new HashSet<string>();
        var lines = page.Replace("\r\n", "\n").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        // Furniture is a line or two; if it would take a third of the page it is not furniture here.
        var kept = lines.Where(line => !furniture.Contains(Key(line))).ToList();
        if (kept.Count * 3 >= lines.Count * 2) lines = kept;
        if (lines.Count > 0 && IsPageNumber(lines[^1])) lines.RemoveAt(lines.Count - 1);
        if (lines.Count > 0 && IsPageNumber(lines[0])) lines.RemoveAt(0);
        if (lines.Count == 0) return "";

        var widths = lines.Select(Width).Where(width => width >= 20).OrderBy(width => width).ToList();
        int? full = widths.Count >= 6 ? widths[widths.Count * 3 / 4] : null;

        var output = new List<string>();
        var paragraph = "";
        void Flush()
        {
            if (paragraph.Length == 0) return;
            output.Add(paragraph);
            paragraph = "";
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var line = Leaders.Replace(lines[index], " … ");
            var next = index + 1 < lines.Count ? lines[index + 1] : null;
            var numbered = numberedHeadings && full is { } fullWidth && Width(line) < fullWidth * 82 / 100;
            if (Heading(line, next, numbered) is { } heading)
            {
                Flush();
                if (output.Count > 0) output.Add("");
                output.Add(new string('#', heading.Level) + " " + heading.Title);
                output.Add("");
                continue;
            }
            paragraph = paragraph.Length == 0 ? line : Join(paragraph, line);
            if (full is not { } fullLine || next is null) { Flush(); continue; }
            var isFull = Width(line) >= fullLine * 82 / 100;
            var ends = line.Length > 0 && ".。!?！？:：;；".Contains(line[^1]);
            var continues = isFull || (!ends && next.Length > 0 && char.IsLower(next[0]));
            if (continues && Heading(next, null, numberedHeadings && Width(next) < fullLine * 82 / 100) is null) continue;
            // A short line that ends a sentence closes the paragraph; anything else is a line of its own.
            if (ends && paragraph.Length >= 200) { Flush(); output.Add(""); }
            else Flush();
        }
        Flush();
        var result = new List<string>();
        foreach (var line in output)
            if (!(line.Length == 0 && (result.Count == 0 || result[^1].Length == 0)))
                result.Add(line);
        while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
        return string.Join("\n", result);
    }

    /// <summary>A report or paper rather than slides: its pages are full of text.</summary>
    public static bool IsProse(IReadOnlyList<string> pages) =>
        pages.Count > 0 && pages.Sum(page => page.Length) / pages.Count > 1200;

    /// <summary>Comparison key: lower case, digits ignored, whitespace collapsed.</summary>
    public static string Key(string line)
    {
        var kept = new StringBuilder();
        foreach (var character in line.ToLowerInvariant().Where(character => !char.IsDigit(character))) kept.Append(character);
        return string.Join(" ", kept.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>True for a lone page number such as "Page 12", "第 3 页" or "- 12 -".</summary>
    public static bool IsPageNumber(string line) => line.Length <= 14 && PageNumber.IsMatch(line);

    /// <summary>Printed width: a CJK character takes the room of two Latin ones.</summary>
    internal static int Width(string line)
    {
        var width = 0;
        foreach (var rune in line.EnumerateRunes()) width += rune.Value >= 0x2E80 ? 2 : 1;
        return width;
    }

    /// <summary>Joins a wrapped line: no space inside Chinese, the hyphen of a broken word removed.</summary>
    public static string Join(string head, string tail)
    {
        if (head.Length == 0) return tail;
        if (tail.Length == 0) return head;
        var last = head.EnumerateRunes().Last();
        var first = tail.EnumerateRunes().First();
        if (last.Value == '-' && head.Length >= 2 && char.IsLetter(head[^2]) && char.IsLower(tail[0])) return head[..^1] + tail;
        return last.Value >= 0x2E80 || first.Value >= 0x2E80 ? head + tail : head + " " + tail;
    }

    /// <summary>Numbered journal sections ("II. SYSTEM MODEL") and "APPENDIX B".</summary>
    public static (int Level, string Title)? JournalHeading(string line, string? next)
    {
        if (Appendix.IsMatch(line)) return (2, line);
        var letters = line.Where(char.IsLetter).ToList();
        if (Roman.IsMatch(line))
        {
            if (letters.Count < 4) return null;
            if (!letters.All(char.IsUpper)) return null;
            if (line.Contains(',') && line.Length >= 70) return null;
            return (2, line);
        }
        if (!Subsection.IsMatch(line)) return null;
        // An author in the reference list starts the same way: "G. Marcus, and A. Keller, …".
        var title = line[2..].Trim();
        if (title.Any(character => ".,;“”\"".Contains(character))) return null;
        if (title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 10) return null;
        if (title.Length == 0 || !char.IsLetter(title[^1])) return null;
        if (next is { Length: > 0 } && char.IsLower(next[0])) return null;
        return (3, line);
    }

    /// <summary>"3.2 Research questions" → level 3; "Abstract" → level 2. Null unless it can only be a title.</summary>
    public static (int Level, string Title)? Heading(string line, string? next, bool numbered = true)
    {
        if (line.Length > 90) return null;
        if (Named.Contains(line.Trim(' ', ':', '：', '.').ToLowerInvariant())) return (2, line);
        if (!numbered) return null;
        if (JournalHeading(line, next) is { } journal) return journal;
        var match = Numbered.Match(line);
        if (!match.Success) return null;
        var number = match.Value.Trim(' ', '.');
        var title = line[match.Length..];
        var depth = number.Split('.').Length;
        if (title.Length == 0) return null;
        var first = title.EnumerateRunes().First().Value;
        if (first < 0x2E80 && !char.IsUpper(title[0])) return null;
        if (depth <= 1 && !(int.TryParse(number, out var value) && value is >= 1 and <= 20)) return null;
        if (!char.IsLetter(title[^1]) && !")）".Contains(title[^1])) return null;
        if (title.Any(character => "…。，；！？".Contains(character))) return null;
        if (title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 12 || Width(title) > 64) return null;
        var letters = title.Count(char.IsLetter);
        var visible = title.Count(character => !char.IsWhiteSpace(character));
        if (letters < 2 || (double)letters / Math.Max(1, visible) < 0.7) return null;
        if (next is { Length: > 0 } && char.IsLower(next[0])) return null;
        return (Math.Min(depth + 1, 4), number + " " + title);
    }
}
