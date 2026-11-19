using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for DocumentStructure parsing and chunking.</summary>
public sealed class DocumentStructureTests
{
    [Fact]
    public void ParsesHeadingsParagraphsAndPaths()
    {
        var blocks = DocumentStructure.Parse("# A\npara one\n\n## B\npara two");
        Assert.Equal(["heading", "paragraph", "heading", "paragraph"], blocks.Select(block => block.Kind));
        Assert.Equal("A", blocks[0].Locator.HeadingPath[0]);
        Assert.Equal(["A", "B"], blocks[2].Locator.HeadingPath);
        Assert.Equal("A > B", blocks[2].Locator.Label);
        Assert.Equal("para two", blocks[3].Markdown);
    }

    [Fact]
    public void DeepHeadingKeepsTheAncestors()
    {
        var blocks = DocumentStructure.Parse("## B\n### Deep\ntext");
        Assert.Equal(["B", "Deep"], blocks[1].Locator.HeadingPath);
    }

    [Fact]
    public void CodeAndFormulaBlocksAreAtomic()
    {
        var blocks = DocumentStructure.Parse("```\ncode line\n```\nafter\n\n$$\nx^2\n$$\nend");
        Assert.Equal("code", blocks[0].Kind);
        Assert.Contains("code line", blocks[0].Markdown);
        Assert.Equal("paragraph", blocks[1].Kind);
        Assert.Equal("formula", blocks[2].Kind);
        Assert.Contains("x^2", blocks[2].Markdown);
        Assert.Equal("end", blocks[3].Markdown);
    }

    [Fact]
    public void PipeTablesStayTogether()
    {
        var blocks = DocumentStructure.Parse("| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\npara");
        Assert.Equal("table", blocks[0].Kind);
        Assert.Contains("| 3 | 4 |", blocks[0].Markdown);
        Assert.Equal("para", blocks[1].Markdown);
    }

    [Fact]
    public void TablePiecesRepeatTheHeader()
    {
        var header = "| name | value |\n|---|---|";
        var rows = string.Join("\n", Enumerable.Range(0, 20).Select(index => $"| row{index} | {new string('x', 20)} |"));
        var block = new StructuredDocumentBlock { Kind = "table", Markdown = header + "\n" + rows };
        var pieces = DocumentStructure.Pieces(block, target: 150);
        Assert.True(pieces.Count > 1);
        Assert.All(pieces, piece => Assert.StartsWith(header, piece));
        Assert.Equal(20, pieces.Sum(piece => piece.Split('\n').Length - 2));
    }

    [Fact]
    public void HtmlTablePiecesKeepTheHeaderInEveryGroup()
    {
        var text = "<table>\n<tr><th>H</th></tr>\n<tr><td>1</td></tr>\n<tr><td>2</td></tr>\n<tr><td>3</td></tr>\n</table>";
        var pieces = DocumentStructure.HtmlTablePieces(text, target: 40);
        Assert.True(pieces.Count > 1);
        Assert.All(pieces, piece => Assert.Contains("<th>H</th>", piece));
    }

    [Fact]
    public void LongParagraphsSplitWithOverlap()
    {
        var text = new string('字', 2000);
        var block = new StructuredDocumentBlock { Kind = "paragraph", Markdown = text };
        var pieces = DocumentStructure.Pieces(block, target: 900);
        Assert.Equal(3, pieces.Count);
        Assert.Equal(900, pieces[0].Length);
        Assert.Equal(900, pieces[1].Length);
        Assert.Equal(440, pieces[2].Length);
    }

    [Fact]
    public void ShortAndAtomicBlocksAreNotSplit()
    {
        var shortBlock = new StructuredDocumentBlock { Kind = "paragraph", Markdown = "short" };
        Assert.Single(DocumentStructure.Pieces(shortBlock));
        var formula = new StructuredDocumentBlock { Kind = "formula", Markdown = new string('x', 2000) };
        Assert.Single(DocumentStructure.Pieces(formula, target: 10));
    }

    [Fact]
    public void LocatorLabelCoversEveryPlaceKind()
    {
        Assert.Equal("p.7", new DocumentLocator { Page = 7 }.Label);
        Assert.Equal("幻灯片 3", new DocumentLocator { Slide = 3 }.Label);
        Assert.Equal("工作表 Sheet1", new DocumentLocator { Sheet = "Sheet1" }.Label);
        Assert.Equal("第 2 段", new DocumentLocator { Section = 2 }.Label);
        Assert.Equal("p.7 · 幻灯片 3 · A > B", new DocumentLocator { Page = 7, Slide = 3, HeadingPath = ["A", "B"] }.Label);
    }

    [Fact]
    public void BlockIdsAreStableAndContentAddressed()
    {
        var first = DocumentStructure.Parse("# A\ntext");
        var second = DocumentStructure.Parse("# A\ntext");
        Assert.Equal(first.Select(block => block.Id), second.Select(block => block.Id));
        var changed = DocumentStructure.Parse("# A\nother");
        Assert.NotEqual(first[1].Id, changed[1].Id);
    }

    [Fact]
    public void StructuredDocumentExposesMarkdownAndStatus()
    {
        var document = new StructuredDocument
        {
            Pipeline = "test",
            Blocks = [new StructuredDocumentBlock { Markdown = "one" }, new StructuredDocumentBlock { Markdown = "two" }]
        };
        Assert.Equal("one\n\ntwo", document.Markdown);
        Assert.Equal("complete", document.Status);
        document.Warnings.Add("warn");
        Assert.Equal("degraded", document.Status);
    }
}
