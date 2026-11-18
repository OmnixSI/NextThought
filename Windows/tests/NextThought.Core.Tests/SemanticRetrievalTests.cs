using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for chunking, the semantic index and rank fusion (SemanticRetrieval.swift).</summary>
public sealed class SemanticRetrievalTests
{
    private static MaterialDocument Document(string name, params DocumentBlock[] blocks) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        Blocks = blocks.ToList()
    };

    [Fact]
    public void FingerprintsAreStableAndChangeSensitive()
    {
        Assert.Equal("cbf29ce484222325", ContextFingerprint.Hash(""));
        Assert.Equal(ContextFingerprint.Hash("abc"), ContextFingerprint.Hash("abc"));
        Assert.NotEqual(ContextFingerprint.Hash("abc"), ContextFingerprint.Hash("abd"));
        Assert.Equal(ContextFingerprint.Hash("中文"), ContextFingerprint.Hash("中文"));
    }

    [Fact]
    public void ShortParagraphsMergeUnderTheirHeading()
    {
        var document = Document("report.pdf", new DocumentBlock("p.1", "# 方法\n短一\n短二", 1));
        var chunks = ContextChunker.Chunks([document]);
        var chunk = Assert.Single(chunks);
        Assert.Contains("report.pdf > 方法", chunk.Text);
        Assert.Contains("短一", chunk.Text);
        Assert.Contains("短二", chunk.Text);
        Assert.Equal(1, chunk.Page);
        Assert.Equal(0, chunk.Order);
    }

    [Fact]
    public void LongParagraphsSplitAndTablesStayWhole()
    {
        var longDocument = Document("long.pdf", new DocumentBlock("p.2", new string('字', 2000), 2));
        var tableDocument = Document("table.pdf", new DocumentBlock("p.3", "| a | b |\n|---|---|\n| 1 | 2 |", 3));
        var chunks = ContextChunker.Chunks([longDocument, tableDocument]);
        Assert.Equal(4, chunks.Count);
        Assert.All(chunks.Take(3), chunk => Assert.Equal(2, chunk.Page));
        Assert.Equal(900 + 900 + 440 + 3 * "long.pdf\n".Length, chunks.Take(3).Sum(chunk => chunk.Text.Length));
        Assert.Equal(3, chunks[3].Page);
        Assert.Contains("| 1 | 2 |", chunks[3].Text);
        Assert.Equal([0, 1, 2, 3], chunks.Select(chunk => chunk.Order));
        Assert.Equal(4, chunks.Select(chunk => chunk.Fingerprint).Distinct().Count());
    }

    [Fact]
    public void ChangedTextChangesTheFingerprint()
    {
        var first = ContextChunker.Chunks([Document("a.pdf", new DocumentBlock("p.1", "第一版", 1))]);
        var second = ContextChunker.Chunks([Document("a.pdf", new DocumentBlock("p.1", "第二版", 1))]);
        Assert.NotEqual(first[0].Fingerprint, second[0].Fingerprint);
    }

    [Fact]
    public void SemanticIndexRemembersWhichTextAVectorBelongsTo()
    {
        var index = new SemanticIndex();
        Assert.True(index.IsEmpty);
        index.SetVector([1f, 0f], "fp0", 0);
        index.SetVector([0f, 1f], "fp1", 1);
        Assert.False(index.IsEmpty);
        Assert.Equal(2, index.EmbeddedCount);
        Assert.Equal(2, index.Dimension);
        Assert.Equal([1f, 0f], index.Vector(0, "fp0")!);
        Assert.Null(index.Vector(0, "other"));
        Assert.Null(index.Vector(5, "fp0"));
        index.RemoveAll();
        Assert.True(index.IsEmpty);
    }

    [Fact]
    public void SemanticRankingSkipsStaleOrMismatchedVectors()
    {
        var index = new SemanticIndex();
        index.SetVector([1f, 0f], "a", 0);
        index.SetVector([0.6f, 0.8f], "b", 1);
        index.SetVector([0f, 1f], "old", 2);
        var ranking = index.RankedOrders([1f, 0f], ["a", "b", "new"]);
        Assert.Equal(2, ranking.Count);
        Assert.Equal((0, 1.0), (ranking[0].Order, ranking[0].Score));
        Assert.Equal(1, ranking[1].Order);
        Assert.Equal(0.6, ranking[1].Score, 5);
        Assert.Empty(index.RankedOrders([], ["a"]));
        Assert.Empty(index.RankedOrders([float.NaN], ["a"]));
    }

    [Fact]
    public void DotProductAndFusionPreferAgreement()
    {
        Assert.Equal(0.6, SemanticIndex.Dot([1f, 0f], [0.6f, 0.8f]), 5);
        Assert.Equal(0, SemanticIndex.Dot([], [1f]));

        var fused = HybridRanker.Fuse([2, 0], [0, 2]);
        Assert.Equal(2, fused.Count);
        Assert.Equal(1.0 / 61 + 1.0 / 62, fused[0].Score, 10);
        Assert.Equal(1.0 / 61 + 1.0 / 62, fused[1].Score, 10);
        Assert.Equal([0, 2], fused.Select(item => item.Order));
        Assert.Equal(0.43, RetrievalPolicy.MinimumSemanticScore);
    }
}
