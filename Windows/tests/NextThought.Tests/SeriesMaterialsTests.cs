using Xunit;
using NextThought.Core;

namespace NextThought.Tests;

/// <summary>
/// E20: importing library materials into a series, removing them again and counting what answers
/// can actually use. These are the pure parts of WorkspaceDocumentsTab, so they need no window.
/// </summary>
public sealed class SeriesMaterialsTests
{
    private static MaterialDocument Material(string name = "简历.pdf", string extension = ".pdf") => new()
    {
        Name = name,
        Extension = extension,
        Blocks =
        [
            new DocumentBlock("p1", "2019–2023 在字节跳动做后端"),
            new DocumentBlock("p2", "负责缓存一致性与订单链路")
        ]
    };

    [Fact]
    public void AttachKeepsDocumentAndLibraryIdTogether()
    {
        var material = Material();
        var series = SeriesMaterials.Attach(WorkspaceSeries.Create("面试准备"), material);

        Assert.True(SeriesMaterials.IsAttached(series, material.Id));
        Assert.Single(series.Documents);
        Assert.Single(series.DocumentIds);
        var document = series.Documents[0];
        Assert.Equal(material.Id, document.Id);
        Assert.Equal("简历.pdf", document.Name);
        Assert.Equal(2, document.Pages.Count);
        Assert.Contains("缓存一致性", document.Pages[1]);
        Assert.Equal("PDF", document.SourceFormat);
        Assert.True(document.Enabled);
    }

    [Fact]
    public void AttachingTheSameLibraryDocumentTwiceDoesNotDuplicateIt()
    {
        var material = Material();
        var series = SeriesMaterials.Attach(SeriesMaterials.Attach(WorkspaceSeries.Create("系列"), material), material);

        Assert.Single(series.Documents);
        Assert.Single(series.DocumentIds);
    }

    [Fact]
    public void DetachRemovesTheEmbeddedCopyAndTheLibraryId()
    {
        var material = Material();
        var series = SeriesMaterials.Attach(WorkspaceSeries.Create("系列"), material);
        var detached = SeriesMaterials.Detach(series, material.Id);

        Assert.False(SeriesMaterials.IsAttached(detached, material.Id));
        Assert.Empty(detached.Documents);
        Assert.Empty(detached.DocumentIds);
    }

    [Fact]
    public void UnknownCategoryFallsBackToOther()
    {
        var document = SeriesMaterials.DocumentOf(Material());

        Assert.Equal(SeriesMaterials.OtherCategory, SeriesMaterials.CategoryOf(document));
        Assert.Equal("简历", SeriesMaterials.CategoryOf(document with { Category = "简历" }));
    }

    [Fact]
    public void EnabledCountOnlyCountsUsableMaterials()
    {
        var first = Material("简历.pdf");
        var second = Material("项目资料.docx", ".docx");
        var series = SeriesMaterials.Attach(SeriesMaterials.Attach(WorkspaceSeries.Create("系列"), first), second);
        series = series with
        {
            Documents = series.Documents
                .Select(document => document.Id == second.Id ? document with { Enabled = false } : document)
                .ToList()
        };

        Assert.Equal(1, SeriesMaterials.EnabledCount(series));
    }
}
