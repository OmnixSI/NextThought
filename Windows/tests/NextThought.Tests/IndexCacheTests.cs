using System.Security.Cryptography;
using System.Text;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class IndexCacheTests
{
    [Fact]
    public void ChunkIdentityIsStableAndChangesWithContentSourceAndLocation()
    {
        var document = new MaterialDocument { Id = "a", Sha256 = "original", Blocks = [new("第 1 页", "桥面巡检", 1)] };
        var first = DocumentChunks.Build([document]).Single(); Assert.Equal(first.Id, DocumentChunks.Build([document]).Single().Id);
        Assert.NotEqual(first.Id, DocumentChunks.Build([document with { Sha256 = "new-source" }]).Single().Id);
        Assert.NotEqual(first.Id, DocumentChunks.Build([document with { Blocks = [new("第 2 页", "桥面巡检", 2)] }]).Single().Id);
        Assert.NotEqual(first.Id, DocumentChunks.Build([document with { Blocks = [new("第 1 页", "每周巡检", 1)] }]).Single().Id);
    }
    [Fact]
    public void SpreadsheetRowsAreIndexedOnceAndKeepCellLocators()
    {
        var blocks = new List<DocumentBlock>(); for (int row = 1; row <= 2000; row++) { blocks.Add(new($"计划!A{row}", "巡检")); blocks.Add(new($"计划!B{row}", "7")); }
        var chunks = DocumentChunks.Build([new MaterialDocument { Extension = ".xlsx", Blocks = blocks }]);
        Assert.Equal(2000, chunks.Count); Assert.Contains("计划!B1：7", chunks[0].Text);
    }
    [Fact]
    public async Task VectorCacheRejectsTamperingAndKeepsLastFileOnCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-vector-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "vectors.bin"); var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("fixture")));
            await VectorCache.SaveAsync(file, "model-a", 2, new Dictionary<string, float[]> { [key] = [1, 0] }, default);
            Assert.Equal(new float[] { 1, 0 }, VectorCache.Load(file, "model-a", 2)[key]); Assert.Throws<InvalidDataException>(() => VectorCache.Load(file, "model-b", 2));
            var old = File.ReadAllBytes(file); using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VectorCache.SaveAsync(file, "model-a", 2, new Dictionary<string, float[]> { [key] = [0, 1] }, cancel.Token)); Assert.Equal(old, File.ReadAllBytes(file));
            old[20] ^= 1; File.WriteAllBytes(file, old); Assert.Throws<InvalidDataException>(() => VectorCache.Load(file, "model-a", 2));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void TokenBudgetSplitsLongBlocksWithoutChangingQuotedSourceOrBreakingUnicode()
    {
        var original = string.Concat(Enumerable.Repeat("𠮷桥🌉复核123", 150));
        var initial = DocumentChunks.Build([new MaterialDocument { Id = "a", Blocks = [new("第 1 页", original, 1)] }]);
        int Count(string text) { _ = text.Normalize(); return Encoding.UTF8.GetByteCount(text) + 1; }
        var fitted = DocumentChunks.FitTokenBudget(initial, Count, 100);
        Assert.True(fitted.Count > initial.Count);
        foreach (var chunk in fitted) { Assert.True(Count(chunk.Text) <= 100); Assert.Equal(chunk.Text, original.Substring(chunk.StartCharacter, chunk.Text.Length)); Assert.Equal(1, chunk.Page); }
        Assert.Equal(fitted.Count, fitted.Select(c => c.Id).Distinct().Count());
    }
    [Fact]
    public async Task KeywordCorpusCacheRefreshesAfterImportWithoutLoadingSemanticModel()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-corpus-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var paths = new AppPaths(Path.Combine(root, "data")); var input = Path.Combine(root, "资料.md"); await File.WriteAllTextAsync(input, "桥面巡检每七天一次"); var store = new MaterialStore(paths); await store.ImportAsync(input);
            using var service = new RetrievalService(paths); await service.SearchAsync("桥面巡检", false); await service.SearchAsync("七天", false); Assert.Equal(1, service.CorpusBuildCount);
            await File.WriteAllTextAsync(input, "支座标高复核负责人是陈工"); await store.ImportAsync(input); var found = await service.SearchAsync("支座标高", false);
            Assert.Equal(2, service.CorpusBuildCount); Assert.NotEmpty(found.Sources); Assert.Equal(0, service.DocumentEmbeddingCount);
        }
        finally { Directory.Delete(root, true); }
    }
}
