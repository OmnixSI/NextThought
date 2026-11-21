using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B19: translated from LiveInsightsTests.swift excerpt budget and prompt tests.</summary>
public sealed class LiveInsightsPromptParityTests
{
    [Fact]
    public void ExcerptBudgetKeepsWholeChunksWithinFourThousandCharactersAndFiveItems()
    {
        var excerpts = Enumerable.Range(0, 8)
            .Select(index => new ContextExcerpt("材料.pdf", index + 1, new string('字', 1_000))).ToList();
        var budgeted = ContextExcerpt.Budget(excerpts, 4_000, 5);
        Assert.Equal(4, budgeted.Count);
        Assert.All(budgeted, excerpt => Assert.Equal(1_000, excerpt.Text.Length));

        var tooLong = new[] { new ContextExcerpt("材料.pdf", 1, new string('字', 4_001)) };
        Assert.Empty(ContextExcerpt.Budget(tooLong, 4_000, 5));
        Assert.Equal("材料.pdf 第 3 段", new ContextExcerpt("材料.pdf", 3, "片段").Citation);
    }

    [Fact]
    public void SlidePromptCarriesTheBudgetedExcerptsWithCitations()
    {
        var page = new SlidePage
        {
            Page = 2, PageCount = 5, DocumentName = "报告.pdf", Text = "这一页讲缓存一致性。"
        };
        var excerpts = new[]
        {
            new ContextExcerpt("论文.pdf", 3, new string('甲', 2_000)),
            new ContextExcerpt("论文.pdf", 4, new string('乙', 2_000)),
            new ContextExcerpt("论文.pdf", 5, new string('丙', 2_000))
        };
        var prompt = SlideNotes.Prompt(MeetingMode.Meeting, page, null, null, excerpts, "");
        Assert.Contains("[论文.pdf 第 3 段] " + new string('甲', 2_000), prompt);
        Assert.Contains("[论文.pdf 第 4 段] " + new string('乙', 2_000), prompt);
        Assert.DoesNotContain("丙", prompt);
        Assert.Contains("当前第 2 / 5 页（报告.pdf）", prompt);
    }
}
