using System.IO.Compression;
using System.Text;

namespace NextThought.Core;

/// <summary>
/// Minimal, dependency-free Word export: a valid DOCX package with heading, list and
/// quote paragraphs. The input is the same Markdown the Markdown exporter writes.
/// </summary>
public static class DocxWriter
{
    public static void Write(string markdown, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(archive, "[Content_Types].xml", ContentTypes);
        Add(archive, "_rels/.rels", RootRelationships);
        Add(archive, "word/document.xml", Document(markdown));
        Add(archive, "word/styles.xml", Styles);
        Add(archive, "word/_rels/document.xml.rels", DocumentRelationships);
    }

    private static void Add(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string Document(string markdown)
    {
        var body = new StringBuilder();
        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { body.Append("<w:p/>"); continue; }
            var style = "";
            var prefix = "";
            var italic = false;
            if (line.StartsWith("# ", StringComparison.Ordinal)) { style = "Heading1"; line = line[2..]; }
            else if (line.StartsWith("## ", StringComparison.Ordinal)) { style = "Heading2"; line = line[3..]; }
            else if (line.StartsWith("### ", StringComparison.Ordinal)) { style = "Heading3"; line = line[4..]; }
            else if (line.StartsWith("> ", StringComparison.Ordinal)) { prefix = "“"; line = line[2..] + "”"; italic = true; }
            else if (line.StartsWith("  - ", StringComparison.Ordinal)) { prefix = "◦ "; line = line[4..]; }
            else if (line.StartsWith("- ", StringComparison.Ordinal)) { prefix = "• "; line = line[2..]; }
            var text = Escape(Inline(line));
            body.Append("<w:p>");
            if (style.Length > 0) body.Append("<w:pPr><w:pStyle w:val=\"").Append(style).Append("\"/></w:pPr>");
            body.Append("<w:r>");
            if (italic) body.Append("<w:rPr><w:i/></w:rPr>");
            body.Append("<w:t xml:space=\"preserve\">").Append(Escape(prefix)).Append(text).Append("</w:t></w:r></w:p>");
        }
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>"
            + body
            + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>"
            + "<w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\" w:header=\"720\" w:footer=\"720\" w:gutter=\"0\"/>"
            + "</w:sectPr></w:body></w:document>";
    }

    private static string Inline(string text)
    {
        // Keep the words, drop the markup that a plain Word run cannot carry.
        var builder = new StringBuilder(text.Length);
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
                        builder.Append(text[(index + 1)..close]);
                        index = end;
                        continue;
                    }
                }
            }
            builder.Append(text[index]);
        }
        return builder.ToString();
    }

    private static string Escape(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
        </Types>
        """;

    private const string RootRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
          <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="32"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="28"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:outlineLvl w:val="2"/></w:pPr><w:rPr><w:b/><w:sz w:val="24"/></w:rPr></w:style>
        </w:styles>
        """;
}
