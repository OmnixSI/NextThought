using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Translated from Tests/NextThoughtCoreTests/LiveInsightsTests.swift.</summary>
public sealed class LiveInsightsTestsParityTests
{
    private static readonly Guid Deck = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static SlidePage Page(int page, string text, string notes = "", int pageCount = 5) => new()
    {
        DocumentId = Deck,
        DocumentName = "talk.pptx",
        Page = page,
        PageCount = pageCount,
        Text = text,
        Notes = notes
    };

    private static List<SlidePage> Pages(IReadOnlyList<string> texts) =>
        texts.Select((text, index) => Page(index + 1, text, pageCount: texts.Count)).ToList();

    private static List<SlidePage> Talk() => Pages(
    [
        "基于稀疏注意力的长文本建模 张三 清华大学 2026",
        "研究背景 长文本推理的显存瓶颈 注意力复杂度随长度平方增长 现有方法精度下降",
        "方法 动态稀疏注意力 局部窗口加全局令牌 可学习的路由选择关键块",
        "实验结果 在 LongBench 上平均提升 3.2 分 推理速度提升 2.1 倍 显存降低 40%",
        "总结与未来工作 稀疏注意力兼顾精度与效率 未来扩展到多模态"
    ]);

    [Fact]
    public void FindsTheSlideOnScreen()
    {
        var screen = "PowerPoint 幻灯片放映 实验结果 在 LongBench 上平均提升 3.2 分 推理速度提升 2.1 倍 显存降低 40% 4 / 5";
        var talk = Talk();
        var ranked = SlideMatcher.Rank(screen, talk);
        Assert.Equal(4, ranked.First().Page.Page);
        var decision = SlideMatcher.Decide(ranked);
        Assert.Equal(SlideDecisionKind.Page, decision.Kind);
        Assert.Equal(talk[3], decision.Page);
    }

    [Fact]
    public void PresenterViewWithNextSlidePreviewPicksCurrent()
    {
        // Presenter view: the current slide (3) and the next one (4) are both on screen.
        var screen = "当前幻灯片 方法 动态稀疏注意力 局部窗口加全局令牌 可学习的路由选择关键块 下一张 实验结果 在 LongBench 上平均提升 3.2 分 推理速度提升 2.1 倍 显存降低 40% 备注 讲清楚路由";
        var talk = Talk();
        var outcome = SlideMatcher.Decide(SlideMatcher.Rank(screen, talk));
        Assert.Equal(SlideDecisionKind.Page, outcome.Kind);
        Assert.Equal(talk[2], outcome.Page);
    }

    [Fact]
    public void UnrelatedScreenMatchesNothing()
    {
        var screen = "微信 文件传输助手 今天下午三点开会 收到";
        var outcome = SlideMatcher.Decide(SlideMatcher.Rank(screen, Talk()));
        Assert.Equal(SlideDecisionKind.None, outcome.Kind);
    }

    [Fact]
    public void DecksAreRecognisedByName()
    {
        Assert.True(SlideMatcher.IsDeck("talk.pptx", 1));
        Assert.True(SlideMatcher.IsDeck("slides.pdf", 12));
        Assert.False(SlideMatcher.IsDeck("resume.pdf", 1));
        Assert.False(SlideMatcher.IsDeck("notes.md", 5));
    }

    [Fact]
    public void SameScreenIsNotJudgedTwice()
    {
        var a = "def two_sum(nums, target):\n    seen = {}\n    for i, n in enumerate(nums):\n        if target - n in seen:\n            return [seen[target - n], i]\n        seen[n] = i";
        Assert.True(ScreenText.IsSameScreen(a, a + " 10:42"));
        Assert.False(ScreenText.IsSameScreen(a, "class LRUCache: def get(self, key): def put(self, key, value):"));
    }

    [Fact]
    public void HeuristicRelevanceFollowsTheConversation()
    {
        var screen = "LeetCode 146. LRU 缓存 请你设计并实现一个满足 LRU (最近最少使用) 缓存约束的数据结构";
        Assert.True(ScreenText.HeuristicRelevance(screen, "你看一下屏幕上这道 LRU 缓存的题，设计一个数据结构"));
        Assert.False(ScreenText.HeuristicRelevance(screen, "先简单做个自我介绍吧，讲讲你最近的项目"));
    }

    [Fact]
    public void SlideNotesParse()
    {
        var text = """
        【要点】
        - 这一页我想讲清楚 **动态稀疏注意力** 的核心思路。
        - 我们用局部窗口加全局令牌。
        2. 路由是可学习的，会挑出关键块。
        【过渡】
        那这个方法效果怎么样？我们看实验。
        【可能被问】
        - 路由的开销有多大？
        - 和 Longformer 比有什么不同？
        """;
        var notes = SlideNotes.Parse(text);
        Assert.NotNull(notes);
        Assert.Equal(3, notes!.Points.Count);
        Assert.Equal("这一页我想讲清楚 动态稀疏注意力 的核心思路。", notes.Points[0]);
        Assert.Equal("那这个方法效果怎么样？我们看实验。", notes.Transition);
        Assert.Equal(2, notes.Questions.Count);
        Assert.Null(SlideNotes.Parse(""));
    }

    [Fact]
    public void SlideNotesPromptCarriesTheSlide()
    {
        var talk = Talk();
        var prompt = SlideNotes.Prompt(MeetingMode.Academic, talk[2], talk[1].Text, talk[3].Text, [], "");
        Assert.Contains("当前第 3 / 5 页", prompt);
        Assert.Contains("下一页", prompt);
        Assert.Contains("【要点】", SlideNotes.SystemPrompt);
    }

    [Fact]
    public void AnswerSplitsIntoSpokenSentences()
    {
        var answer = """
        我觉得核心是**一致性和可用性的取舍**。比如说：
        - 写多读少的场景，我会先更新数据库再删缓存；
        - 读多写少的时候，可以加一个短的过期时间。
        ```python
        cache.delete(key)
        ```
        最后要监控缓存命中率！
        """;
        var points = AnswerPoints.Split(answer);
        Assert.Equal("我觉得核心是一致性和可用性的取舍。", points.First());
        Assert.Contains(points, point => point.StartsWith("比如说：写多读少", StringComparison.Ordinal));
        Assert.DoesNotContain(points, point => point.Contains("cache.delete", StringComparison.Ordinal));
        Assert.Equal("最后要监控缓存命中率！", points.Last());
    }

    [Fact]
    public void HeuristicCoverage()
    {
        Assert.True(AnswerPoints.HeuristicCovered("写多读少的场景，我会先更新数据库再删缓存",
            "嗯我觉得写多读少的场景的话我会先更新数据库然后再删缓存"));
        Assert.False(AnswerPoints.HeuristicCovered("最后要监控缓存命中率", "我会先更新数据库再删缓存"));
        Assert.Equal(2, AnswerPoints.Next(new HashSet<int> { 0, 1 }, 4));
        Assert.Null(AnswerPoints.Next(new HashSet<int> { 0, 1 }, 2));
    }

    [Fact]
    public void PromptCarriesScreenAndSlide()
    {
        var parts = AnswerPromptBuilder.MakeParts("路由开销有多大？", "", "", [], false,
            mode: MeetingMode.Academic, screen: "方法 动态稀疏注意力",
            currentSlide: new PromptExcerpt("方法", "talk.pptx", new DocumentLocator { Page = 3 }));
        Assert.Contains("\"screen_text\"", parts.User);
        Assert.Contains("\"current_slide\"", parts.User);
        var plain = AnswerPromptBuilder.MakeParts("q", "", "", [], false);
        Assert.DoesNotContain("screen_text", plain.User);
    }

    [Fact]
    public void AReportIsNotADeck()
    {
        Assert.True(SlideMatcher.IsDeck("slides.pdf", 30, 420));
        Assert.False(SlideMatcher.IsDeck("report.pdf", 27, 3_000));
        Assert.True(SlideMatcher.IsDeck("talk.pptx", 40, 3_000));
    }

    private static List<DocumentBlock> ParseBlocks(string markdown, int slide) =>
        DocumentStructure.Parse(markdown, new DocumentLocator { Slide = slide })
            .Select(block => new DocumentBlock(block.Locator.Label, block.Markdown, Kind: block.Kind))
            .ToList();

    [Fact]
    public void SpeakerNotesAreKeptApartFromTheSlide()
    {
        var slide = 3;
        var blocks = ParseBlocks("Scenario\nTwo co-sited arrays", slide)
            .Concat(ParseBlocks("### 演讲备注\n这一页是我们的通信场景。灯杆上装了两块阵列。", slide))
            .ToList();
        var parts = SlidePage.Split(blocks);
        Assert.Equal("Scenario\nTwo co-sited arrays", parts.Text);
        Assert.Equal("这一页是我们的通信场景。灯杆上装了两块阵列。", parts.Notes);
    }

    [Fact]
    public void AScriptIsTheSpeakersOwnWords()
    {
        var next = Page(4, "# Signals the base station keeps\nSRS, probes, pilots", pageCount: 9);
        var page = Page(3, "Scenario",
            "这一页是我们的通信场景。灯杆上装了两块阵列。右上角的小图是俯视图。", pageCount: 9);
        var script = SlideNotes.Script(page, next);
        Assert.NotNull(script);
        Assert.True(script!.IsScript);
        Assert.Equal(["这一页是我们的通信场景。", "灯杆上装了两块阵列。", "右上角的小图是俯视图。"], script.Points);
        Assert.Equal("Signals the base station keeps", script.Transition);
        // A stub ("TODO") is not a script.
        Assert.Null(SlideNotes.Script(Page(1, "Title", "TODO", pageCount: 9), null));
        // Notes written before `isScript` existed still decode.
        var old = JsonSerializer.Deserialize<SlideNotes>(
            """{"points":["a"],"transition":"","questions":[]}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(old);
        Assert.False(old!.IsScript);
    }

    [Fact]
    public void AFooterOnEverySlideDoesNotDecideThePage()
    {
        const string footer = "Adaptive Beam Prediction Across Bands · A. Speaker · Example University 2026";
        var bodies = new[]
        {
            "Scenario: a sub-6 GHz aided mmWave site with two co-sited arrays",
            "Signals the base station keeps: fingerprints, probing beams and received pilots",
            "Label-free adaptive prior learns the cross-band map from service pilots",
            "Result: the harder the site, the larger the gain over the analytic map"
        };
        var pages = Pages(bodies.Select(body => body + "\n" + footer).ToList());
        // The screen shows slide 3 and, like every slide, the footer.
        var ranked = SlideMatcher.Rank(bodies[2] + " " + footer, pages);
        Assert.Equal(3, ranked.First().Page.Page);
        var decision = SlideMatcher.Decide(ranked);
        Assert.Equal(SlideDecisionKind.Page, decision.Kind);
        Assert.Equal(pages[2], decision.Page);
        // Only the footer is readable (a slide that is one picture): no page is claimed.
        var footerOnly = SlideMatcher.Decide(SlideMatcher.Rank(footer, pages));
        Assert.False(footerOnly.Kind == SlideDecisionKind.Page && footerOnly.Page == pages[0]);
    }

    [Fact]
    public void TheNextSlideNeedsLessProofThanAJump()
    {
        var pages = Pages(
        [
            "alpha beta gamma delta epsilon zeta eta theta",
            "iota kappa lambda mu nu xi omicron pi rho",
            "sigma tau upsilon phi chi psi omega end"
        ]);
        var weak = new List<SlideMatch>
        {
            new(pages[1], 0.38),
            new(pages[2], 0.2)
        };
        var asNext = SlideMatcher.Decide(weak, pages[0]);
        Assert.Equal(SlideDecisionKind.Page, asNext.Kind);
        Assert.Equal(pages[1], asNext.Page);
        var asJump = SlideMatcher.Decide(weak, null);
        Assert.False(asJump.Kind == SlideDecisionKind.Page && asJump.Page == pages[1]);
    }

    [Fact]
    public void TheScriptBeingSpokenFindsItsSlide()
    {
        var scripts = new[]
        {
            "First, the background. Wireless systems increasingly rely on learned models for channel estimation.",
            "This slide shows the scenario. Two arrays are installed on one lamp post, and their normals differ by a yaw angle.",
            "Now the results. The harder the deployment condition, the larger the gain over the analytic mapping."
        };
        var pages = scripts.Select((script, index) =>
            Page(index + 1, "Slide " + (index + 1), script, pageCount: 3)).ToList();
        // Reading slide 2's script, as recognised (a word lost here and there), moves to slide 2 and not beyond.
        const string heard = "this slide shows the scenario two arrays are installed on lamp post and their normals differ by a yaw angle";
        Assert.Equal(2, SlideMatcher.SpokenPage(pages.Skip(1).ToList(), heard)?.Page);
        Assert.Null(SlideMatcher.SpokenPage([pages[2]], heard));
        // English is told by its words: sharing letters with the script is not saying it.
        Assert.False(AnswerPoints.HeuristicCovered("The harder the deployment condition, the larger the gain",
            "there is a garden behind the old theatre and children play in it together"));
        Assert.True(AnswerPoints.HeuristicCovered("The harder the deployment condition, the larger the gain",
            "so the harder the deployment gets the larger our gain"));
    }

    [Fact]
    public void AnEnglishAnswerCarriesItsMeaning()
    {
        // As the answer model writes it: the question first, a meaning under each sentence,
        // with or without the "译：" mark.
        var answer = """
        > 问：没有真实信道作标签，怎么确认学到的先验是对的？

        The prior is not guaranteed to be exactly correct without ground-truth labels.
        > 这个先验在没有真实标签时不能保证完全正确。

        I assess it through held-out service pilots and compare it with simpler baselines.
        > 译：我用留出的服务导频评估，并和更简单的基线比较。
        """;
        Assert.Equal("没有真实信道作标签，怎么确认学到的先验是对的？", BilingualAnswer.Question(answer));
        var lines = CueScript.Lines(answer, CueStyle.KeyPoints);
        Assert.Equal(
            ["The prior is not guaranteed to be exactly correct without ground-truth labels.",
             "I assess it through held-out service pilots and compare it with simpler baselines."],
            lines.Select(line => line.Text));
        Assert.Equal(["这个先验在没有真实标签时不能保证完全正确。", "我用留出的服务导频评估，并和更简单的基线比较。"],
            lines.Select(line => line.Gloss));
        // Only the English is to be said: coverage and the read-aloud script leave the Chinese out.
        Assert.Equal(2, AnswerPoints.Split(answer).Count);
        Assert.DoesNotContain("先验", BilingualAnswer.Spoken(answer));
        // While it streams, the question's meaning is there before the first sentence.
        Assert.Empty(CueScript.Lines("> 问：怎么确认先验是对的？\n", CueStyle.Script));
    }

    [Fact]
    public void AnOrdinaryQuoteIsNotAGloss()
    {
        var answer = "这句话的出处是：\n> Premature optimization is the root of all evil.\n它提醒我们先度量。";
        Assert.False(BilingualAnswer.IsBilingual(answer));
        Assert.Equal(3, CueScript.Lines(answer, CueStyle.KeyPoints).Count);
        Assert.Equal(answer, BilingualAnswer.Spoken(answer));
    }

    [Fact]
    public void TermsComeFromTheMaterials()
    {
        var pages = new[]
        {
            "RQ1 studies beam management for XL-MIMO. TSIR reuses forecasts. In RQ1 the XL-MIMO array is hybrid.",
            "DeepMIMO O1 at mmWave: TSIR gains 9 pp. DeepMIMO gives the channels, and mmWave pilots are scarce.",
            "González-Prelcic and Nimier-David report 3.5 bit/s/Hz in 2026. THE RESULTS ARE PRELIMINARY AND UNPUBLISHED."
        };
        var terms = MaterialTerms.Extract(pages);
        Assert.Equal(new HashSet<string> { "RQ1", "XL-MIMO", "TSIR", "DeepMIMO", "mmWave" },
            terms.ToHashSet(StringComparer.Ordinal));
        // Ordinary words, years, units, surnames and shouting are not terms.
        foreach (var word in new[] { "Results", "2026", "bit/s/Hz", "Nimier-David", "lez-Prelcic", "UNPUBLISHED", "PDF" })
            Assert.False(MaterialTerms.IsTerm(word), word);
    }

    [Fact]
    public void NotesReadWithTheNamesGivenLater()
    {
        var names = new Dictionary<int, string>
        {
            [1] = "Kai Wu（主导师）",
            [2] = "Ren Ping Liu",
            [12] = "Wei Ni"
        };
        Assert.Equal("Kai Wu 要求补充基线，Ren Ping Liu 同意；Wei Ni 没有发言。",
            SpeakerLabels.Naming("说话人 1 要求补充基线，说话人2 同意；说话人 12 没有发言。", names));
        Assert.Equal("Ren Ping Liu asked about RQ2.", SpeakerLabels.Naming("Speaker 2 asked about RQ2.", names));
        Assert.Equal("说话人 3 没有名字。", SpeakerLabels.Naming("说话人 3 没有名字。", names));
        Assert.Equal("Kai Wu", SpeakerLabels.ShortName("Kai Wu (advisor)"));
        var notes = new StructuredNotesDoc("general", "通用", "zh", "与说话人 1 的讨论",
        [
            new NotesSectionResult("actions", "待办", NotesSectionKind.Actions, false,
            [
                new NotesItem { Text = "补充基线对比", Owner = "说话人 1", Quote = "说话人 1：补一下基线" }
            ])
        ]);
        notes = notes.Named(names);
        Assert.Contains(notes.Title, new[] { "与Kai Wu 的讨论", "与Kai Wu的讨论" });
        Assert.Equal("Kai Wu", notes.Sections[0].Items[0].Owner);
        // The quote is what was said: untouched.
        Assert.Equal("说话人 1：补一下基线", notes.Sections[0].Items[0].Quote);
    }

    [Fact]
    public void QuestionRepeatedInEnglishGivesItsMeaning()
    {
        var answer = "> 问：How does reusing the prediction reduce pilots?\n> 译：复用上一次的预测，如何降低导频开销？\n\nIt improves the beam at the same pilot cost.\n> 译：它在同样的导频开销下改善波束。";
        Assert.Equal("复用上一次的预测，如何降低导频开销？", BilingualAnswer.Question(answer));
        var lines = CueScript.Lines(answer, CueStyle.Script);
        Assert.Equal(["It improves the beam at the same pilot cost."], lines.Select(line => line.Text));
        Assert.Equal("它在同样的导频开销下改善波束。", lines.First().Gloss);
    }

    [Fact]
    public void MeaningsGivenTogetherAtTheEndGoToTheirSentences()
    {
        var answer = "> 问：信道模型是否考虑近场？\n\nYes. My model includes the near-field effect.\n\nI use ray-traced channels at 3.5 and 28 GHz. (报告第 15 页)\n\n> 译：是的，模型考虑了近场效应。\n> 译：我用 3.5 和 28 GHz 的射线追踪信道。（报告第 15 页）";
        var lines = CueScript.Lines(answer, CueStyle.Script);
        Assert.Equal(["Yes. My model includes the near-field effect.", "I use ray-traced channels at 3.5 and 28 GHz."],
            lines.Select(line => line.Text));
        Assert.Equal(["是的，模型考虑了近场效应。", "我用 3.5 和 28 GHz 的射线追踪信道。（报告第 15 页）"],
            lines.Select(line => line.Gloss));
    }

    [Fact]
    public void SourceNotesAreNotSpoken()
    {
        Assert.Equal("It raised the mean by about ten percent.",
            BilingualAnswer.WithoutSource("It raised the mean by about ten percent. (第 1 页，报告第 5.1 节)"));
        Assert.Equal("The array response depends on angle and range (near field).",
            BilingualAnswer.WithoutSource("The array response depends on angle and range (near field)."));
    }
}
