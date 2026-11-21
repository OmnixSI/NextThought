using SkiaSharp;

namespace NextThought.Platform;

/// <summary>
/// PDF export through the SkiaSharp already shipped with Avalonia. Layout follows
/// SessionDocumentRenderer.swift: A4 595×842, 44 pt margins (content 507 pt), 10.5 pt body,
/// 18/15/12.5 pt headings, 9 pt code and table rows, no footer text.
/// </summary>
public static class PdfWriter
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    /// <summary>SessionDocumentRenderer.pdfData draws into CGRect(x:44,y:44,width:507,height:754).</summary>
    private const float Margin = 44f;
    /// <summary>At 9 pt, 84 monospaced cells leave a safe margin inside the A4 page.</summary>
    private const int MaximumTableWidth = 84;

    public static void Write(string markdown, string path, string? title = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = File.Create(path);
        using var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
        {
            Title = title ?? "NextThought meeting",
            Creator = "NextThought",
            Producer = "NextThought / SkiaSharp"
        });
        using var regular = LoadCjkFont();
        using var mono = LoadMonoFont();
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black };
        using var font = new SKFont();

        var canvas = document.BeginPage(PageWidth, PageHeight);
        var y = Margin;
        void NewPage()
        {
            document.EndPage();
            canvas = document.BeginPage(PageWidth, PageHeight);
            y = Margin;
        }
        void EnsureSpace(float height)
        {
            if (y + height > PageHeight - Margin) NewPage();
        }
        void Draw(string text, float size, SKTypeface typeface, float before = 0, float after = 0,
            float extraIndent = 0, float lineHeight = 2)
        {
            if (before > 0)
            {
                y += before;
                if (y > PageHeight - Margin) NewPage();
            }
            font.Typeface = typeface;
            font.Size = size;
            font.SkewX = 0;
            var width = PageWidth - Margin * 2 - extraIndent;
            foreach (var part in Wrap(text, font, width))
            {
                EnsureSpace(size + lineHeight);
                canvas.DrawText(part, Margin + extraIndent, y, SKTextAlign.Left, font, paint);
                y += size + lineHeight;
            }
            y += after;
        }

        /// <summary>
        /// Inline formatting (SessionDocumentRenderer.inlineRuns/pdfStyle): links become 文字 (URL),
        /// inline code uses the system monospace face, bold/italic/strike carry their emphasis.
        /// </summary>
        void DrawRich(string markdown, float size, bool boldBase, bool italicBase, float before, float after,
            float extraIndent)
        {
            if (before > 0)
            {
                y += before;
                if (y > PageHeight - Margin) NewPage();
            }
            var runs = InlineRuns(markdown);
            var startX = Margin + extraIndent;
            var wrapWidth = PageWidth - Margin * 2 - extraIndent;
            var x = startX;
            font.SkewX = italicBase ? -0.2f : 0;
            foreach (var run in runs)
            {
                font.Typeface = run.Code ? mono : regular;
                font.Size = size;
                font.SkewX = run.Italic || italicBase ? -0.2f : 0;
                var runFont = new SKFont(font.Typeface, size) { SkewX = font.SkewX };
                foreach (var rune in run.Text.EnumerateRunes())
                {
                    var piece = rune.ToString();
                    var advance = runFont.MeasureText(piece);
                    if (x + advance > startX + wrapWidth && x > startX)
                    {
                        y += size + 2;
                        EnsureSpace(size + 2);
                        x = startX;
                    }
                    canvas.DrawText(piece, x, y, SKTextAlign.Left, runFont, paint);
                    x += advance;
                }
                runFont.Dispose();
            }
            y += size + 2 + after;
        }

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var codeFence = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                codeFence = !codeFence;
                y += 3;
                continue;
            }
            if (line.Length == 0)
            {
                y += 5;
                if (y > PageHeight - Margin) NewPage();
                continue;
            }
            if (codeFence)
            {
                Draw(Plain(line), 9, mono, extraIndent: 8, lineHeight: 2);
                continue;
            }
            if (IsTableRow(line))
            {
                var rows = new List<IReadOnlyList<string>>();
                while (index < lines.Length && IsTableRow(lines[index].TrimEnd()))
                {
                    var cells = TableCells(lines[index].TrimEnd());
                    index++;
                    if (IsTableSeparator(cells)) continue;
                    rows.Add(cells);
                }
                index--;
                DrawTable(rows, regular, Draw);
                y += 6;
                continue;
            }

            float size;
            var indent = 0f;
            string text;
            var boldBase = false;
            var italicBase = false;
            var heading = false;
            if (line.StartsWith("# ", StringComparison.Ordinal)) { size = 18; text = line[2..]; boldBase = heading = true; }
            else if (line.StartsWith("## ", StringComparison.Ordinal)) { size = 15; text = line[3..]; boldBase = heading = true; }
            else if (line.StartsWith("### ", StringComparison.Ordinal)) { size = 12.5f; text = line[4..]; boldBase = heading = true; }
            else if (line.StartsWith("#### ", StringComparison.Ordinal)) { size = 11.5f; text = line[5..]; boldBase = heading = true; }
            else if (line.StartsWith("> ", StringComparison.Ordinal)) { size = 10.5f; text = line[2..]; italicBase = true; indent = 16; }
            else if (line.StartsWith("  - ", StringComparison.Ordinal)) { size = 10.5f; text = "• " + line[4..]; indent = 28; }
            else if (line.StartsWith("- ", StringComparison.Ordinal)) { size = 10.5f; text = "• " + line[2..]; indent = 14; }
            else { size = 10.5f; text = line; }
            DrawRich(text, size, boldBase, italicBase, before: heading ? 8 : 0, after: heading ? 4 : 5, indent);
        }

        document.EndPage();
        document.Close();
    }

    private static void DrawTable(IReadOnlyList<IReadOnlyList<string>> rows, SKTypeface typeface,
        Action<string, float, SKTypeface, float, float, float, float> draw)
    {
        if (rows.Count == 0) return;
        var columns = Math.Max(1, rows.Max(row => row.Count));
        var widths = new int[columns];
        for (var column = 0; column < columns; column++)
        {
            var longest = 3;
            foreach (var row in rows)
                if (column < row.Count) longest = Math.Max(longest, DisplayWidth(Plain(row[column])));
            widths[column] = Math.Min(28, longest);
        }
        var gridWidth = widths.Sum() + columns * 3 + 1;
        if (gridWidth > MaximumTableWidth)
        {
            // Very wide tables stay readable as labeled lines instead of a compressed grid.
            var header = rows[0];
            foreach (var row in rows.Skip(1))
            {
                for (var column = 0; column < columns; column++)
                {
                    var label = column < header.Count ? Plain(header[column]) : "列 " + (column + 1);
                    var value = column < row.Count ? Plain(row[column]) : "";
                    draw(label + "：" + value, 10.5f, typeface, 0, 2, 0, 2);
                }
            }
            return;
        }
        void Rule() => draw("┌" + new string('─', Math.Max(1, widths.Sum() + columns * 3 - 1)) + "┐", 9, typeface, 0, 2, 0, 2);
        Rule();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var padded = new string[columns];
            for (var column = 0; column < columns; column++)
            {
                var text = column < row.Count ? Plain(row[column]) : "";
                padded[column] = " " + text + new string(' ', Math.Max(0, widths[column] - DisplayWidth(text))) + " ";
            }
            draw("│" + string.Join("│", padded) + "│", rowIndex == 0 ? 9 : 9, typeface, 0, 2, 0, 2);
            if (rowIndex == 0) Rule();
        }
        draw("└" + new string('─', Math.Max(1, widths.Sum() + columns * 3 - 1)) + "┘", 9, typeface, 0, 2, 0, 2);
    }

    private static bool IsTableRow(string line) => line.StartsWith('|') && line.EndsWith('|') && line.Length > 1;

    private static IReadOnlyList<string> TableCells(string line)
    {
        var body = line[1..^1];
        return body.Split('|').Select(cell => cell.Trim()).ToList();
    }

    private static bool IsTableSeparator(IReadOnlyList<string> cells) =>
        cells.Count > 0 && cells.All(cell => cell.Length > 0
            && cell.All(character => character is '-' or ':' or ' '));

    /// <summary>SessionDocumentRenderer.displayWidth: CJK and full-width characters count as two cells.</summary>
    private static int DisplayWidth(string text) => text.EnumerateRunes().Sum(rune => rune.Value >= 0x2E80 ? 2 : 1);

    private sealed record InlineRun(string Text, bool Bold = false, bool Italic = false, bool Strike = false,
        bool Code = false);

    /// <summary>Minimal inline Markdown splitter following SessionDocumentRenderer.inlineRuns.</summary>
    private static IReadOnlyList<InlineRun> InlineRuns(string markdown)
    {
        var runs = new List<InlineRun>();
        var pending = new System.Text.StringBuilder();
        var index = 0;
        void Flush()
        {
            if (pending.Length == 0) return;
            runs.Add(new InlineRun(pending.ToString()));
            pending.Clear();
        }
        while (index < markdown.Length)
        {
            if (markdown[index] == '[')
            {
                var labelEnd = markdown.IndexOf(']', index + 1);
                if (labelEnd > index + 1 && labelEnd + 1 < markdown.Length && markdown[labelEnd + 1] == '(')
                {
                    var urlEnd = markdown.IndexOf(')', labelEnd + 2);
                    if (urlEnd > labelEnd)
                    {
                        Flush();
                        runs.Add(new InlineRun(markdown[(index + 1)..labelEnd] + " (" + markdown[(labelEnd + 2)..urlEnd] + ")"));
                        index = urlEnd + 1;
                        continue;
                    }
                }
            }
            if (Markup(markdown, index) is { } markup)
            {
                var contentStart = index + markup.Marker.Length;
                var close = markdown.IndexOf(markup.Marker, contentStart, StringComparison.Ordinal);
                if (close > contentStart)
                {
                    Flush();
                    runs.Add(new InlineRun(markdown[contentStart..close], markup.Bold, markup.Italic, markup.Strike,
                        markup.Code));
                    index = close + markup.Marker.Length;
                    continue;
                }
            }
            pending.Append(markdown[index]);
            index++;
        }
        Flush();
        return runs.Count == 0 ? [new InlineRun("")] : runs;
    }

    private static (string Marker, bool Bold, bool Italic, bool Strike, bool Code)? Markup(string text, int start)
    {
        if (text.AsSpan(start).StartsWith("**", StringComparison.Ordinal)) return ("**", true, false, false, false);
        if (text.AsSpan(start).StartsWith("__", StringComparison.Ordinal)) return ("__", true, false, false, false);
        if (text.AsSpan(start).StartsWith("~~", StringComparison.Ordinal)) return ("~~", false, false, true, false);
        if (text[start] == '`') return ("`", false, false, false, true);
        if (text[start] == '*') return ("*", false, true, false, false);
        if (text[start] == '_') return ("_", false, true, false, false);
        return null;
    }

    private static SKTypeface LoadMonoFont()
    {
        foreach (var family in new[] { "Cascadia Mono", "Consolas", "Courier New" })
            if (SKTypeface.FromFamilyName(family) is { } typeface) return typeface;
        return SKTypeface.Default;
    }

    private static SKTypeface LoadCjkFont()
    {
        // SimHei is smaller than YaHei; embed it when present so exported PDFs stay moderate.
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var name in new[] { "simhei.ttf", "msyh.ttc", "simsun.ttc" })
        {
            var path = Path.Combine(fonts, name);
            if (File.Exists(path))
            {
                try { return SKTypeface.FromFile(path); } catch { }
            }
        }
        return SKTypeface.FromFamilyName("Microsoft YaHei") ?? SKTypeface.Default;
    }

    private static IEnumerable<string> Wrap(string text, SKFont font, float width)
    {
        var current = "";
        foreach (var rune in text.EnumerateRunes())
        {
            var candidate = current + rune;
            if (font.MeasureText(candidate) > width && current.Length > 0)
            {
                yield return current;
                current = rune.ToString();
            }
            else current = candidate;
        }
        if (current.Length > 0) yield return current;
    }

    private static string Plain(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '`') continue;
            if (text[index] == '*' && index + 1 < text.Length && text[index + 1] == '*') { index++; continue; }
            if (text[index] == '[')
            {
                var close = text.IndexOf(']', index + 1);
                if (close > 0 && close + 1 < text.Length && text[close + 1] == '(')
                {
                    var end = text.IndexOf(')', close + 2);
                    if (end > 0)
                    {
                        builder.Append(text[(index + 1)..close]).Append(" (").Append(text[(close + 2)..end]).Append(')');
                        index = end;
                        continue;
                    }
                }
            }
            builder.Append(text[index]);
        }
        return builder.ToString();
    }
}
