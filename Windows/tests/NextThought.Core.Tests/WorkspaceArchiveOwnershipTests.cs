using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for series-owned extracted document archives.</summary>
public sealed class WorkspaceArchiveOwnershipTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-archive-owner-").FullName;
    private readonly WorkspaceSeries series = new("series-1", "系列", DateTimeOffset.UtcNow, [], []);

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static MaterialDocument Document(string id = "doc-1", params string[] blocks) => new()
    {
        Id = id,
        Name = "资料.pdf",
        Blocks = blocks.Select(text => new DocumentBlock("", text)).ToList()
    };

    [Fact]
    public void RepairCreatesMarkdownAndMetadataUnderTheSeries()
    {
        var archived = Assert.Single(WorkspaceArchiveOwnership.Repair(series, [Document("doc-1", "第一段", "第二段")], root));
        var owner = Path.Combine(root, "Materials", "series-1");
        Assert.Equal(Path.Combine(owner, "doc-1.md"), archived.MarkdownPath);
        Assert.Equal(Path.Combine(owner, "doc-1.json"), archived.MetadataPath);
        Assert.True(archived.Created);
        Assert.Equal("第一段\n\n第二段", File.ReadAllText(archived.MarkdownPath));
        Assert.Contains("\"id\": \"doc-1\"", File.ReadAllText(archived.MetadataPath));
    }

    [Fact]
    public void RepairIsIdempotentAndKeepsExistingFiles()
    {
        var first = Assert.Single(WorkspaceArchiveOwnership.Repair(series, [Document("doc-1", "原文")], root));
        File.WriteAllText(first.MarkdownPath, "人工改过");
        var second = Assert.Single(WorkspaceArchiveOwnership.Repair(series, [Document("doc-1", "新原文")], root));
        Assert.False(second.Created);
        Assert.Equal("人工改过", File.ReadAllText(first.MarkdownPath));
    }

    [Fact]
    public void DocumentsWithoutBlocksStillGetFiles()
    {
        var archived = Assert.Single(WorkspaceArchiveOwnership.Repair(series, [Document("doc-2")], root));
        Assert.Equal("", File.ReadAllText(archived.MarkdownPath));
        Assert.True(File.Exists(archived.MetadataPath));
    }

    [Fact]
    public void EveryDocumentIsArchivedInOrder()
    {
        var archived = WorkspaceArchiveOwnership.Repair(series, [Document("a", "A"), Document("b", "B")], root);
        Assert.Equal(["a", "b"], archived.Select(item => item.DocumentId));
        Assert.All(archived, item => Assert.True(File.Exists(item.MarkdownPath)));
    }

    [Fact]
    public void UnsafeIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => WorkspaceArchiveOwnership.Repair(series, [Document("../escape")], root));
        Assert.Throws<ArgumentException>(() => WorkspaceArchiveOwnership.Repair(series, [Document("a/b")], root));
        Assert.Throws<ArgumentException>(() => WorkspaceArchiveOwnership.Repair(series, [Document("..")], root));
    }

    [Fact]
    public void NoDocumentsMeansNoFiles()
    {
        Assert.Empty(WorkspaceArchiveOwnership.Repair(series, [], root));
        Assert.False(Directory.Exists(Path.Combine(root, "Materials", "series-1")));
    }
}
