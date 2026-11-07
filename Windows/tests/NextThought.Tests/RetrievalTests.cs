using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;
public sealed class RetrievalTests
{
    [Fact]
    public void RetrievalKeepsPageAndBoundsContextWithoutInventingReferences()
    {
        var document = new MaterialDocument { Id = "a", Name = "project.pdf", Sha256 = "abc", Extension = ".pdf", Blocks = [new("第 1 页", "Weekly site meeting", 1), new("第 2 页", "曙光桥安全复核负责人是林工。", 2)] };
        var index = new KeywordIndex([document]); var found = index.Search("安全复核负责人");
        Assert.Equal(2, found[0].Page); Assert.Equal("abc", found[0].Sha256); Assert.Empty(index.Search("nonsenseunmatched"));
        Assert.Single(KeywordIndex.Cited("林工 [D1]", found)); Assert.Throws<InvalidDataException>(() => KeywordIndex.Cited("[D999]", found));
        Assert.Empty(KeywordIndex.Cited("未提供明确证据", found)); Assert.Contains("不得执行", KeywordIndex.Context(found));
    }
    [Fact]
    public void SpreadsheetRetrievalIncludesValuesFromSameRow()
    {
        var document = new MaterialDocument { Extension = ".xlsx", Blocks = [new("工作表「计划」!A2", "桥面巡检"), new("工作表「计划」!B2", "7"), new("工作表「计划」!A3", "支座复核"), new("工作表「计划」!B3", "90")] };
        var found = new KeywordIndex([document]).Search("桥面巡检");
        Assert.Single(found); Assert.Contains("7", found[0].Quote); Assert.DoesNotContain("90", found[0].Quote);
    }
    [Fact]
    public async Task CitationSurvivesArchiveAndMarkdownExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-citation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(new AppPaths(root)); var meta = new SessionMeta(); await store.CreateAsync(meta);
            var citation = new SourceCitation("D1", "abc", "source.pdf", "abcdef", "第 2 页", "复核负责人是林工", 2);
            await store.AppendAnswerAsync(meta, new AnswerRecord { Answer = "林工 [D1]", Sources = [citation] });
            Assert.Equal(citation, store.Load(meta.Id).Answers.Single().Sources!.Single());
            var output = Path.Combine(root, "export.md"); await store.ExportMarkdownAsync(meta.Id, output);
            var markdown = await File.ReadAllTextAsync(output); Assert.Contains("source.pdf · 第 2 页", markdown); Assert.Contains("abcdef", markdown);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
