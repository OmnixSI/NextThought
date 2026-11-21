using System.IO.Compression;
using System.Text;

namespace NextThought.Core;

/// <summary>
/// E19: dependency-free Word export, ported from SessionDocumentRenderer.swift. The Markdown the
/// notes page shows becomes a DOCX package with the Mac styles.xml (heading 1–6 sizes/spacing,
/// body 10.5 pt, quote indent), numbering.xml (multilevel bullet and decimal lists) and inline runs
/// (bold/italic/strike/inline code in a monospace font, links written as 文字 (URL)).
/// </summary>
public static class DocxWriter
{
    private enum BlockKind { Normal, Heading1, Heading2, Heading3, Heading4, Heading5, Heading6, Quote, Code, List }

    private sealed record Block(BlockKind Kind, string Text, int Level = 0, int NumberId = 0,
        IReadOnlyList<IReadOnlyList<string>>? Rows = null);

    private sealed record ListNumbering(int NumberId, int AbstractNumberId);

    private sealed record InlineStyle(bool Bold = false, bool Italic = false, bool Strike = false, bool Code = false);

    public static void Write(string markdown, string path)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var (blocks, numbering) = Parse(markdown);
        var temporary = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                Add(archive, "[Content_Types].xml", ContentTypes);
                Add(archive, "_rels/.rels", PackageRelationships);
                Add(archive, "word/_rels/document.xml.rels", DocumentRelationships);
                Add(archive, "word/styles.xml", Styles);
                Add(archive, "word/numbering.xml", NumberingXml(numbering));
                Add(archive, "word/document.xml", Document(blocks));
            }
            File.Move(temporary, full, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private static void Add(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }

    // ---- Markdown parsing (Swift parse/heading/listItem/tableCells/isTableSeparator) ----

    private static (IReadOnlyList<Block> Blocks, IReadOnlyList<ListNumbering> Numbering) Parse(string markdown)
    {
        var blocks = new List<Block>();
        var numbering = new List<ListNumbering>();
        var orderedByNumberId = new Dictionary<int, bool>();
        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var code = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                code = !code;
                continue;
            }
            if (code)
            {
                blocks.Add(new Block(BlockKind.Code, line));
                continue;
            }
            if (line.Length == 0) continue;
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
                if (rows.Count > 0) blocks.Add(new Block(BlockKind.Normal, "", Rows: rows));
                continue;
            }
            if (Heading(line) is { } heading)
            {
                var kind = heading.Level switch
                {
                    1 => BlockKind.Heading1,
                    2 => BlockKind.Heading2,
                    3 => BlockKind.Heading3,
                    4 => BlockKind.Heading4,
                    5 => BlockKind.Heading5,
                    _ => BlockKind.Heading6
                };
                blocks.Add(new Block(kind, heading.Text));
                continue;
            }
            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                blocks.Add(new Block(BlockKind.Quote, line[2..]));
                continue;
            }
            if (ListItem(line) is { } item)
            {
                var previous = blocks.Count > 0 ? blocks[^1] : null;
                int numberId;
                if (previous is { Kind: BlockKind.List } current
                    && orderedByNumberId.TryGetValue(current.NumberId, out var previousOrdered)
                    && previousOrdered == item.Ordered)
                {
                    numberId = current.NumberId;
                }
                else
                {
                    numberId = numbering.Count + 1;
                    numbering.Add(new ListNumbering(numberId, item.Ordered ? 1 : 0));
                    orderedByNumberId[numberId] = item.Ordered;
                }
                blocks.Add(new Block(BlockKind.List, item.Text, item.Level, numberId));
                continue;
            }
            if (line.Trim() is "---" or "***" or "___") continue;
            blocks.Add(new Block(BlockKind.Normal, line));
        }
        return (blocks, numbering);
    }

    private static (int Level, string Text)? Heading(string line)
    {
        var level = 0;
        while (level < line.Length && level < 6 && line[level] == '#') level++;
        if (level == 0 || line.Length <= level + 1 || line[level] != ' ') return null;
        return (level, line[(level + 1)..]);
    }

    /// <summary>Swift listItem: "- ", "* ", "+ " or "N. " with two spaces per depth level.</summary>
    private static (string Text, int Level, bool Ordered)? ListItem(string line)
    {
        var spaces = 0;
        while (spaces < line.Length && line[spaces] == ' ') spaces++;
        var body = line[spaces..];
        if (body.Length < 2) return null;
        var ordered = false;
        var textStart = -1;
        if (body.StartsWith("- ", StringComparison.Ordinal) || body.StartsWith("* ", StringComparison.Ordinal)
            || body.StartsWith("+ ", StringComparison.Ordinal)) textStart = 2;
        else
        {
            var digits = 0;
            while (digits < body.Length && char.IsDigit(body[digits])) digits++;
            if (digits > 0 && digits + 1 < body.Length && body[digits] == '.' && body[digits + 1] == ' ')
            {
                ordered = true;
                textStart = digits + 2;
            }
        }
        if (textStart < 0) return null;
        return (body[textStart..], Math.Clamp(spaces / 2, 0, 8), ordered);
    }

    private static bool IsTableRow(string line) => line.StartsWith('|') && line.EndsWith('|') && line.Length > 1;

    private static IReadOnlyList<string> TableCells(string line) =>
        line[1..^1].Split('|').Select(cell => cell.Trim()).ToList();

    private static bool IsTableSeparator(IReadOnlyList<string> cells) =>
        cells.Count > 0 && cells.All(cell => cell.Length > 0 && cell.All(character => character is '-' or ':' or ' '));

    // ---- document.xml ----

    private static string Document(IReadOnlyList<Block> blocks)
    {
        var body = new StringBuilder();
        foreach (var block in blocks)
        {
            if (block.Rows is { } rows)
            {
                body.Append(Table(rows));
                continue;
            }
            switch (block.Kind)
            {
                case BlockKind.List:
                    body.Append("<w:p><w:pPr><w:numPr><w:ilvl w:val=\"").Append(block.Level)
                        .Append("\"/><w:numId w:val=\"").Append(block.NumberId)
                        .Append("\"/></w:numPr><w:spacing w:after=\"80\"/></w:pPr>")
                        .Append(InlineRuns(block.Text, new InlineStyle())).Append("</w:p>");
                    break;
                default:
                    body.Append("<w:p><w:pPr>").Append(ParagraphProperties(block.Kind)).Append("</w:pPr>")
                        .Append(InlineRuns(block.Text, StyleFor(block.Kind))).Append("</w:p>");
                    break;
            }
        }
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>"
            + body
            + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>"
            + "<w:pgMar w:top=\"1134\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\" "
            + "w:header=\"567\" w:footer=\"567\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>";
    }

    private static string ParagraphProperties(BlockKind kind) => kind switch
    {
        BlockKind.Heading1 => "<w:pStyle w:val=\"Heading1\"/><w:keepNext/>",
        BlockKind.Heading2 => "<w:pStyle w:val=\"Heading2\"/><w:keepNext/>",
        BlockKind.Heading3 => "<w:pStyle w:val=\"Heading3\"/><w:keepNext/>",
        BlockKind.Heading4 => "<w:pStyle w:val=\"Heading4\"/><w:keepNext/>",
        BlockKind.Heading5 => "<w:pStyle w:val=\"Heading5\"/><w:keepNext/>",
        BlockKind.Heading6 => "<w:pStyle w:val=\"Heading6\"/><w:keepNext/>",
        BlockKind.Quote => "<w:ind w:left=\"360\"/><w:spacing w:after=\"120\"/>",
        BlockKind.Code => "<w:ind w:left=\"240\"/><w:spacing w:after=\"100\"/>",
        _ => "<w:spacing w:after=\"120\"/>"
    };

    private static InlineStyle StyleFor(BlockKind kind) => kind switch
    {
        BlockKind.Heading1 or BlockKind.Heading2 or BlockKind.Heading3 or BlockKind.Heading4
            or BlockKind.Heading5 or BlockKind.Heading6 => new InlineStyle(Bold: true),
        BlockKind.Quote => new InlineStyle(Italic: true),
        BlockKind.Code => new InlineStyle(Code: true),
        _ => new InlineStyle()
    };

    // ---- inline runs (Swift inlineRuns/markdownLinkPrefix/markupPrefix/run) ----

    private static string InlineRuns(string markdown, InlineStyle defaultStyle)
    {
        var result = new StringBuilder();
        var pending = new StringBuilder();
        var index = 0;
        while (index < markdown.Length)
        {
            if (LinkPrefix(markdown, index) is { } link)
            {
                Flush();
                result.Append(Run(link.Label + " (" + link.Url + ")", defaultStyle));
                index += link.Length;
                continue;
            }
            if (MarkupPrefix(markdown, index) is { } markup)
            {
                var contentStart = index + markup.Marker.Length;
                var close = markdown.IndexOf(markup.Marker, contentStart, StringComparison.Ordinal);
                if (close > contentStart)
                {
                    Flush();
                    result.Append(Run(markdown[contentStart..close], markup.Style));
                    index = close + markup.Marker.Length;
                    continue;
                }
            }
            pending.Append(markdown[index]);
            index++;
        }
        Flush();
        return result.Length == 0 ? "<w:r><w:t></w:t></w:r>" : result.ToString();

        void Flush()
        {
            if (pending.Length == 0) return;
            result.Append(Run(pending.ToString(), defaultStyle));
            pending.Clear();
        }
    }

    private static (string Label, string Url, int Length)? LinkPrefix(string text, int start)
    {
        if (start >= text.Length || text[start] != '[') return null;
        var labelEnd = text.IndexOf(']', start + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(') return null;
        var urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0) return null;
        return (text[(start + 1)..labelEnd], text[(labelEnd + 2)..urlEnd], urlEnd - start + 1);
    }

    private static (string Marker, InlineStyle Style)? MarkupPrefix(string text, int start)
    {
        if (start >= text.Length) return null;
        if (text.AsSpan(start).StartsWith("**", StringComparison.Ordinal)) return ("**", new InlineStyle(Bold: true));
        if (text.AsSpan(start).StartsWith("__", StringComparison.Ordinal)) return ("__", new InlineStyle(Bold: true));
        if (text.AsSpan(start).StartsWith("~~", StringComparison.Ordinal)) return ("~~", new InlineStyle(Strike: true));
        if (text[start] == '`') return ("`", new InlineStyle(Code: true));
        if (text[start] == '*') return ("*", new InlineStyle(Italic: true));
        if (text[start] == '_') return ("_", new InlineStyle(Italic: true));
        return null;
    }

    private static string Run(string text, InlineStyle style)
    {
        var properties = new StringBuilder();
        if (style.Bold) properties.Append("<w:b/>");
        if (style.Italic) properties.Append("<w:i/>");
        if (style.Strike) properties.Append("<w:strike/>");
        if (style.Code)
            properties.Append("<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\" w:eastAsia=\"Microsoft YaHei\" ")
                .Append("w:cs=\"Consolas\"/><w:shd w:val=\"clear\" w:fill=\"F3F4F6\"/>");
        var prefix = properties.Length == 0 ? "" : "<w:rPr>" + properties + "</w:rPr>";
        return "<w:r>" + prefix + "<w:t xml:space=\"preserve\">" + Escape(text) + "</w:t></w:r>";
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is not ('\t' or '\n' or '\r') && character < 0x20) continue;
            builder.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => character.ToString()
            });
        }
        return builder.ToString();
    }

    // ---- tables (Swift table) ----

    private static string Table(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0) return "";
        var columnCount = Math.Max(1, rows.Max(row => row.Count));
        var columnWidth = Math.Max(720, 9360 / columnCount);
        var grid = new StringBuilder();
        for (var column = 0; column < columnCount; column++)
            grid.Append("<w:gridCol w:w=\"").Append(columnWidth).Append("\"/>");
        const string border = "<w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/>"
            + "<w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/>"
            + "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/>"
            + "<w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/>"
            + "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/>"
            + "<w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8C0CC\"/></w:tblBorders>";
        var body = new StringBuilder();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var cells = new StringBuilder();
            for (var column = 0; column < columnCount; column++)
            {
                var value = column < row.Count ? row[column] : "";
                var shading = rowIndex == 0 ? "<w:shd w:val=\"clear\" w:fill=\"EEF1F5\"/>" : "";
                cells.Append("<w:tc><w:tcPr><w:tcW w:w=\"").Append(columnWidth).Append("\" w:type=\"dxa\"/>")
                    .Append(shading).Append("<w:vAlign w:val=\"center\"/></w:tcPr><w:p><w:pPr>")
                    .Append("<w:spacing w:after=\"40\"/></w:pPr>")
                    .Append(InlineRuns(value, new InlineStyle(Bold: rowIndex == 0)))
                    .Append("</w:p></w:tc>");
            }
            var headerRow = rowIndex == 0 ? "<w:trPr><w:tblHeader w:val=\"true\"/></w:trPr>" : "";
            body.Append("<w:tr>").Append(headerRow).Append(cells).Append("</w:tr>");
        }
        return "<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"0\" w:type=\"auto\"/>"
            + border + "</w:tblPr><w:tblGrid>" + grid + "</w:tblGrid>" + body + "</w:tbl>";
    }

    // ---- numbering.xml (Swift numberingXML/numberingLevels) ----

    private static string NumberingXml(IReadOnlyList<ListNumbering> listNumbering)
    {
        var numbers = new StringBuilder();
        foreach (var entry in listNumbering)
            numbers.Append("<w:num w:numId=\"").Append(entry.NumberId).Append("\"><w:abstractNumId w:val=\"")
                .Append(entry.AbstractNumberId).Append("\"/></w:num>");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<w:numbering xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
            + "<w:abstractNum w:abstractNumId=\"0\"><w:multiLevelType w:val=\"multilevel\"/>"
            + NumberingLevels(ordered: false) + "</w:abstractNum>"
            + "<w:abstractNum w:abstractNumId=\"1\"><w:multiLevelType w:val=\"multilevel\"/>"
            + NumberingLevels(ordered: true) + "</w:abstractNum>" + numbers + "</w:numbering>";
    }

    private static string NumberingLevels(bool ordered)
    {
        var levels = new StringBuilder();
        for (var level = 0; level <= 8; level++)
        {
            var indent = 720 * (level + 1);
            var marker = ordered ? "%" + (level + 1) + "." : new[] { "•", "◦", "▪" }[level % 3];
            var format = ordered ? "decimal" : "bullet";
            var font = ordered
                ? ""
                : "<w:rPr><w:rFonts w:ascii=\"Symbol\" w:hAnsi=\"Symbol\" w:eastAsia=\"Microsoft YaHei\"/></w:rPr>";
            levels.Append("<w:lvl w:ilvl=\"").Append(level).Append("\"><w:start w:val=\"1\"/><w:numFmt w:val=\"")
                .Append(format).Append("\"/><w:lvlText w:val=\"").Append(marker)
                .Append("\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:tabs><w:tab w:val=\"num\" w:pos=\"")
                .Append(indent).Append("\"/></w:tabs><w:ind w:left=\"").Append(indent)
                .Append("\" w:hanging=\"360\"/></w:pPr>").Append(font).Append("</w:lvl>");
        }
        return levels.ToString();
    }

    // ---- package parts (Swift contentTypesXML/relationships/stylesXML) ----

    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
        + "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>"
        + "<Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/>"
        + "</Types>";

    private const string PackageRelationships =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>"
        + "</Relationships>";

    private const string DocumentRelationships =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
        + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering\" Target=\"numbering.xml\"/>"
        + "</Relationships>";

    private const string Styles =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
        + "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:eastAsia=\"Microsoft YaHei\" w:cs=\"Arial\"/><w:sz w:val=\"21\"/><w:szCs w:val=\"21\"/></w:rPr></w:rPrDefault>"
        + "<w:pPrDefault><w:pPr><w:spacing w:after=\"120\" w:line=\"300\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>"
        + "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before=\"360\" w:after=\"180\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"36\"/><w:szCs w:val=\"36\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before=\"300\" w:after=\"140\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"30\"/><w:szCs w:val=\"30\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading3\"><w:name w:val=\"heading 3\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before=\"240\" w:after=\"100\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"25\"/><w:szCs w:val=\"25\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading4\"><w:name w:val=\"heading 4\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/><w:rPr><w:b/><w:sz w:val=\"23\"/><w:szCs w:val=\"23\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading5\"><w:name w:val=\"heading 5\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/><w:rPr><w:b/><w:sz w:val=\"22\"/><w:szCs w:val=\"22\"/></w:rPr></w:style>"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Heading6\"><w:name w:val=\"heading 6\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/></w:style>"
        + "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:tblPr><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\"/><w:left w:val=\"single\" w:sz=\"4\"/><w:bottom w:val=\"single\" w:sz=\"4\"/><w:right w:val=\"single\" w:sz=\"4\"/><w:insideH w:val=\"single\" w:sz=\"4\"/><w:insideV w:val=\"single\" w:sz=\"4\"/></w:tblBorders></w:tblPr></w:style>"
        + "</w:styles>";
}
