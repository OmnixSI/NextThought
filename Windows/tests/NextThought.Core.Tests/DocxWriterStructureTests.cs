using System.IO.Compression;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E19 (decisions §3.4 export/SessionExportSheet 2/3): the Word export carries the Mac styles
/// (heading 1–6, 1134 twip margins), the multilevel numbering part, inline code in a monospace
/// font and links written as 文字 (URL).
/// </summary>
public sealed class DocxWriterStructureTests
{
    private const string Sample = """
        # 会议标题

        正文有 **粗体**、*斜体*、`内联代码` 和 [链接示例](https://example.com/doc)。

        ## 二级标题

        - 第一项
          - 子项
        - 第二项

        1. 步骤一
        2. 步骤二

        > 一句引用

        | 列A | 列B |
        |---|---|
        | 值1 | 值2 |
        """;

    private static Dictionary<string, string> WriteAndRead()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-docx-" + Guid.NewGuid().ToString("N") + ".docx");
        try
        {
            DocxWriter.Write(Sample, path);
            using var archive = ZipFile.OpenRead(path);
            return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
            {
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            });
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void ThePackageHasTheMacStylesNumberingAndMargins()
    {
        var entries = WriteAndRead();
        Assert.Contains("word/styles.xml", entries.Keys);
        Assert.Contains("word/numbering.xml", entries.Keys);
        var styles = entries["word/styles.xml"];
        foreach (var heading in new[] { "Heading1", "Heading2", "Heading3", "Heading4", "Heading5", "Heading6" })
            Assert.Contains("w:styleId=\"" + heading + "\"", styles, StringComparison.Ordinal);
        Assert.Contains("w:eastAsia=\"Microsoft YaHei\"", styles, StringComparison.Ordinal);
        var document = entries["word/document.xml"];
        Assert.Contains("w:top=\"1134\"", document, StringComparison.Ordinal);
        Assert.Contains("w:pStyle w:val=\"Heading1\"", document, StringComparison.Ordinal);
        Assert.Contains("<w:numPr>", document, StringComparison.Ordinal);
        Assert.Contains("<w:tbl>", document, StringComparison.Ordinal);
        var numbering = entries["word/numbering.xml"];
        Assert.Contains("w:abstractNumId=\"0\"", numbering, StringComparison.Ordinal);
        Assert.Contains("w:abstractNumId=\"1\"", numbering, StringComparison.Ordinal);
        Assert.Contains("w:numFmt w:val=\"decimal\"", numbering, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineCodeIsMonospacedAndLinksShowTheirAddress()
    {
        var document = WriteAndRead()["word/document.xml"];
        Assert.Contains("w:ascii=\"Consolas\"", document, StringComparison.Ordinal);
        Assert.Contains("<w:shd w:val=\"clear\" w:fill=\"F3F4F6\"/>", document, StringComparison.Ordinal);
        Assert.Contains("链接示例 (https://example.com/doc)", document, StringComparison.Ordinal);
        Assert.Contains("<w:b/>", document, StringComparison.Ordinal);
        Assert.Contains("<w:i/>", document, StringComparison.Ordinal);
        Assert.DoesNotContain("`", document, StringComparison.Ordinal);
        Assert.DoesNotContain("**", document, StringComparison.Ordinal);
    }
}
