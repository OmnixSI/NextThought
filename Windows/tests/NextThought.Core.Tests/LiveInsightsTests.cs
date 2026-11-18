using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for screen similarity, slide matching and answer progress.</summary>
public sealed class LiveInsightsTests
{
    private static SlidePage Page(int page, string text, string notes = "", Guid? document = null) => new()
    {
        DocumentId = document ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
        DocumentName = "talk.pptx",
        Page = page,
        PageCount = 10,
        Text = text,
        Notes = notes
    };

    [Fact]
    public void SimilarityWorksForChineseAndEnglish()
    {
        Assert.Equal(1, Math.Round(TextSimilarity.Jaccard("缓存一致性方案", "缓存一致性方案"), 2));
        Assert.True(TextSimilarity.Jaccard("the quick brown fox", "the quick brown cat") > 0.5);
        Assert.True(TextSimilarity.Containment("缓存一致性", "我们讨论缓存一致性方案") > 0.6);
        Assert.False(ScreenText.IsSameScreen("完全不同的屏幕", "另一块屏幕的内容"));
        Assert.True(ScreenText.IsSameScreen("同样的屏幕内容", "同样的屏幕内容。"));
    }

    [Fact]
    public void TermsUseWordsForLatinAndPairsForCjk()
    {
        var terms = TextSimilarity.Terms("Kubernetes 缓存 一致性 cache");
        Assert.Contains("kubernetes", terms);
        Assert.Contains("cache", terms);
        Assert.Contains("缓存", terms);
        Assert.Contains("一致", terms);
        Assert.DoesNotContain("th", terms);
    }

    [Fact]
    public void ScreenRelevanceNeedsEnoughSpeechOnScreen()
    {
        var speech = "我们今天讨论缓存一致性和读写时序的问题";
        Assert.True(ScreenText.HeuristicRelevance("缓存一致性：读写时序图", speech));
        Assert.False(ScreenText.HeuristicRelevance("完全不相关的幻灯片", speech));
        Assert.False(ScreenText.HeuristicRelevance("任何屏幕", "太短"));
    }

    [Fact]
    public void DeckDetectionFollowsExtensionAndPageDensity()
    {
        Assert.True(SlideMatcher.IsDeck("talk.pptx", 10));
        Assert.True(SlideMatcher.IsDeck("keynote.key", 2));
        Assert.True(SlideMatcher.IsDeck("slides.pdf", 10, 800));
        Assert.False(SlideMatcher.IsDeck("slides.pdf", 10, 3000));
        Assert.False(SlideMatcher.IsDeck("slides.pdf", 2, 800));
        Assert.False(SlideMatcher.IsDeck("paper.docx", 10));
    }

    [Fact]
    public void RankingPrefersPagesWithRareMatchingTerms()
    {
        var pages = new[]
        {
            Page(1, "会议标题 页脚 缓存"),
            Page(2, "缓存一致性 读写时序 方案")
        };
        var ranked = SlideMatcher.Rank("缓存一致性 读写时序", pages);
        Assert.NotEmpty(ranked);
        Assert.Equal(2, ranked[0].Page.Page);
    }

    [Fact]
    public void DecideHandlesClearWinsAndPreviousPageTies()
    {
        var page = Page(3, "内容");
        Assert.Equal(SlideDecisionKind.Page, SlideMatcher.Decide([new SlideMatch(page, 0.6)]).Kind);
        Assert.Equal(SlideDecisionKind.Ambiguous, SlideMatcher.Decide([new SlideMatch(page, 0.3)]).Kind);
        Assert.Equal(SlideDecisionKind.None, SlideMatcher.Decide([]).Kind);

        var previous = Page(2, "上一页");
        var tied = SlideMatcher.Decide([new SlideMatch(previous, 0.3), new SlideMatch(page, 0.32)], previous);
        Assert.Equal(SlideDecisionKind.Page, tied.Kind);
        Assert.Equal(previous, tied.Page);
    }

    [Fact]
    public void NextSlideNeedsLessProofThanAJump()
    {
        var previous = Page(4, "上一页");
        var next = Page(5, "下一页");
        var decision = SlideMatcher.Decide([new SlideMatch(next, 0.36)], previous);
        Assert.Equal(SlideDecisionKind.Page, decision.Kind);
        Assert.Equal(next, decision.Page);
    }

    [Fact]
    public void SpokenPageMatchesTheSpeakerScript()
    {
        var page = Page(1, "标题", "第一句完整的话包含足够多的内容。第二句也包含足够多的内容。");
        var speech = "第一句完整的话包含足够多的内容。第二句也包含足够多的内容。";
        Assert.Equal(page, SlideMatcher.SpokenPage([page], speech));
        Assert.Null(SlideMatcher.SpokenPage([page], "太短"));
    }

    [Fact]
    public void SlideTitleSkipsDecorationsAndSplitSeparatesNotes()
    {
        var page = Page(1, "# 缓存一致性\n正文内容");
        Assert.Equal("缓存一致性", page.Title);
        Assert.Equal("11111111-1111-1111-1111-111111111111#1", page.Key);

        var (text, notes) = SlidePage.Split(
        [
            new DocumentBlock("正文", "观众看到的内容"),
            new DocumentBlock("演讲备注", "只给讲者看"),
            new DocumentBlock("", "演讲备注", Kind: "heading")
        ]);
        Assert.Equal("观众看到的内容", text);
        Assert.Equal("只给讲者看", notes);
    }

    [Fact]
    public void AnswerSentencesStripMarkupAndCoverageFindsUnsaidPoints()
    {
        var points = AnswerPoints.Split("**第一点。**\n- 第二点。\n第三点。", 10);
        Assert.Contains("第一点。第二点。", points);
        Assert.Equal(0, AnswerPoints.Next([], 3));
        Assert.Equal(1, AnswerPoints.Next([0], 3));
        Assert.Null(AnswerPoints.Next([0, 1, 2], 3));
        Assert.True(AnswerPoints.HeuristicCovered("缓存一致性方案", "我们讨论缓存一致性方案"));
    }

    [Fact]
    public void SlideNotesParseSectionsAndBuildScripts()
    {
        var notes = SlideNotes.Parse("【要点】\n- 第一点\n- 第二点\n【过渡】\n下一页见\n【可能被问】\n- 为什么？");
        Assert.NotNull(notes);
        Assert.Equal(["第一点", "第二点"], notes!.Points);
        Assert.Equal("下一页见", notes.Transition);
        Assert.Equal(["为什么？"], notes.Questions);

        var page = Page(1, "标题", "第一句完整的话包含足够多的内容。第二句也包含足够多的内容。");
        var next = Page(2, "下一页");
        var script = SlideNotes.Script(page, next);
        Assert.NotNull(script);
        Assert.True(script!.IsScript);
        Assert.Equal("下一页", script.Transition);
        Assert.Null(SlideNotes.Script(Page(1, "标题", "很短"), null));
    }

    [Fact]
    public void SlidePromptCarriesPageTextAndRole()
    {
        var page = Page(3, "缓存一致性设计", "备注");
        var prompt = SlideNotes.Prompt(MeetingMode.Academic, page, "上一页内容", "下一页内容",
            [new ContextExcerpt("论文", 2, "相关资料")], "回答简短");
        Assert.Contains("正在讲这一页幻灯片", prompt);
        Assert.Contains("当前第 3 / 10 页（talk.pptx）", prompt);
        Assert.Contains("缓存一致性设计", prompt);
        Assert.Contains("上一页：", prompt);
        Assert.Contains("[论文 p.2] 相关资料", prompt);
        Assert.Contains("回答简短", prompt);
    }
}
