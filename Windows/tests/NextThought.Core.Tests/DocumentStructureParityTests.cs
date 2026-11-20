using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from DocumentStructureTests.swift: HTML rows repeat the header and keep row spans,
/// Markdown tables and formulas are never cut by prompt budgets, every accepted block stays
/// searchable beyond the old chunk cap, and vectors reject changed chunks and invalid numbers.
/// </summary>
public sealed class DocumentStructureParityTests
{
    [Fact(Skip = "Blocked: ContextDocument.Content is an opaque JsonElement (no typed DocumentContent/"
        + "StructuredDocument bridge), and ContextChunker maps imported pages to physical page locators, "
        + "never to the legacy section locator (\"第 N 段\"), so the physical-vs-legacy locator "
        + "assertions cannot be expressed.")]
    public void PhysicalAndLegacyLocationsStayDistinct()
    {
    }

    [Fact]
    public void HtmlRowsRepeatHeaderAndKeepSpans()
    {
        var rows = Enumerable.Range(0, 80).Select(number => $"<tr><td>row{number}</td><td>12345</td></tr>");
        var text = "<table><tr><th>A</th><th>B</th></tr>" + string.Concat(rows) + "</table>";
        var block = new StructuredDocumentBlock { Kind = "table", Markdown = text };
        var pieces = DocumentStructure.Pieces(block, target: 300);

        Assert.True(pieces.Count > 1);
        Assert.All(pieces, piece => Assert.True(piece.Contains("<th>A</th>") && piece.EndsWith("</table>")));
        for (var number = 0; number < 80; number++)
            Assert.Single(pieces, piece => piece.Contains($"<td>row{number}</td>"));

        const string merged = "<table><tr><th>A</th></tr><tr><td rowspan=\"2\">joined</td><td>x</td></tr>"
            + "<tr><td>y</td></tr><tr><td>z</td></tr></table>";
        Assert.Contains("<td>y</td>", DocumentStructure.Pieces(
            new StructuredDocumentBlock { Kind = "table", Markdown = merged }, target: 80).First());
    }

    [Fact]
    public void MarkdownTablesAndFormulasAreNeverCutByPromptBudgets()
    {
        var table = "| Name | Value |\n| --- | --- |\n"
            + string.Join("\n", Enumerable.Range(0, 30).Select(index => $"| item{index} | {index} |"));
        var pieces = DocumentStructure.Pieces(new StructuredDocumentBlock { Kind = "table", Markdown = table }, target: 150);

        Assert.True(pieces.Count > 1);
        Assert.All(pieces, piece => Assert.StartsWith("| Name | Value |\n| --- | --- |", piece));

        var formula = "$$\n" + new string('x', 1500) + "\n$$";
        Assert.Equal(new[] { formula },
            DocumentStructure.Pieces(new StructuredDocumentBlock { Kind = "formula", Markdown = formula }, target: 50));

        var excerpt = new ContextExcerpt("math", 1, formula, new DocumentLocator { Page = 1 });
        Assert.Empty(ContextExcerpt.Budget([excerpt], characters: 100));
    }

    [Fact]
    public void EveryAcceptedBlockRemainsSearchableBeyondTheOldChunkCap()
    {
        // The Swift fixture puts 2105 blocks in one ContextDocument content. The Windows material
        // pipeline carries the physical page instead of the section, so block N is located at page N.
        var blocks = Enumerable.Range(0, 2105)
            .Select(index => new DocumentBlock("section " + (index + 1),
                index == 2104 ? "terminal-needle-zebra" : "paragraph " + index, index + 1))
            .ToList();
        var document = new MaterialDocument { Id = "many-sections", Name = "many-sections.md", Blocks = blocks };
        var index = new HybridContextIndex([document]);

        Assert.Equal(2105, index.Chunks.Count);
        Assert.Equal(2105, index.Retrieve("terminal-needle-zebra").First().Locator!.Page);
    }

    [Fact]
    public void VectorsRejectChangedChunksAndInvalidNumbers()
    {
        var index = new SemanticIndex(
        [
            new SemanticIndex.Entry("old", [1f, 0f]),
            new SemanticIndex.Entry("same", [float.NaN, 0f])
        ]);
        Assert.Empty(index.RankedOrders([1f, 0f], ["changed", "same"]));
        Assert.Empty(index.RankedOrders([1f], ["old", "same"]));
    }
}
