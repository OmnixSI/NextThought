using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class SlideDeckTests
{
    [Fact]
    public void FirstPptxBecomesAnOrderedPageList()
    {
        var deck = new MaterialDocument
        {
            Name = "答辩.pptx", Extension = ".pptx",
            Blocks = [
                new DocumentBlock("幻灯片 2", "第二页内容", 2),
                new DocumentBlock("幻灯片 1", "第一页内容", 1)
            ]
        };
        var other = new MaterialDocument { Name = "论文.pdf", Extension = ".pdf", Blocks = [new DocumentBlock("第 1 页", "x", 1)] };
        var pages = SlideDecks.FromDocuments([other, deck]);
        Assert.Equal(2, pages.Count);
        Assert.Equal(1, pages[0].Number);
        Assert.Equal("第一页内容", pages[0].Text);
        Assert.Equal("答辩.pptx", pages[0].DocumentName);
        Assert.Equal(2, pages[1].Number);
    }

    [Fact]
    public void NoPptxMeansAnEmptyDeck()
    {
        Assert.Empty(SlideDecks.FromDocuments([new MaterialDocument { Name = "a.pdf", Extension = ".pdf" }]));
    }
}
