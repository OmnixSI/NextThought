using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the small self-contained Core ports (language, grounding, terms, shortcuts, Codex, context).</summary>
public sealed class SmallPortsTests
{
    [Fact]
    public void EnglishQuestionWithAChineseTermStaysEnglish()
    {
        Assert.Equal("English", AnswerLanguage.Spoken("How does the XL-MIMO testbed work in practice?"));
    }

    [Fact]
    public void ChineseQuestionNamingATermStaysChinese()
    {
        Assert.Equal("简体中文", AnswerLanguage.Spoken("XL-MIMO 的测试床后来怎么搭的？"));
    }

    [Fact]
    public void ShortOrAmbiguousQuestionsDoNotDecide()
    {
        Assert.Null(AnswerLanguage.Spoken("OK"));
        Assert.Null(AnswerLanguage.Spoken("？"));
    }

    [Fact]
    public void AnswerLanguageFieldsFollowChoiceAndGloss()
    {
        Assert.Equal(("English", null), AnswerLanguage.Fields("en", false, "中文问题"));
        Assert.Equal(("简体中文", null), AnswerLanguage.Fields("zh", true, "English question"));
        Assert.Equal(("English", "简体中文"), AnswerLanguage.Fields("auto", true, "Why does the cache matter here?"));
        Assert.Equal((null, "简体中文"), AnswerLanguage.Fields("auto", true, "短"));
    }

    [Fact]
    public void RewriteAndSearchDetectionUseTheSamePhrases()
    {
        Assert.True(AnswerGrounding.PureRewrite("把上一条回答压缩到三句话"));
        Assert.True(AnswerGrounding.PureRewrite("Rewrite the previous answer shorter"));
        Assert.False(AnswerGrounding.PureRewrite("为什么用 Redis"));

        Assert.True(AnswerGrounding.NeedsVerification("这个数字可能已过时，建议核实。"));
        Assert.True(AnswerGrounding.NeedsVerification("The figure may be outdated."));
        Assert.False(AnswerGrounding.NeedsVerification("数据来自会议转写。"));

        Assert.True(AnswerGrounding.ExplicitSearch("帮我搜索一下最新版本"));
        Assert.True(AnswerGrounding.ExplicitSearch("Can you search the web for it?"));
        Assert.False(AnswerGrounding.ExplicitSearch("继续解释一下"));
    }

    [Fact]
    public void SearchDecisionParsesFencedJsonAndTrims()
    {
        var decision = SearchDecision.Parse(
            "```json\n{\"needsSearch\": true, \"reason\": \"需要最新数据\", \"query\": \"  NextThought 0.25  \"}\n```",
            "原始问题");
        Assert.NotNull(decision);
        Assert.True(decision!.Search);
        Assert.Equal("需要最新数据", decision.Reason);
        Assert.Equal("NextThought 0.25", decision.Query);
        Assert.Equal("model", decision.Mechanism);
    }

    [Fact]
    public void SearchDecisionFallsBackToTheQuestionAndRejectsMalformedReplies()
    {
        var decision = SearchDecision.Parse("""{"needsSearch": false, "reason": "会议里已经说过"}""", "原始问题");
        Assert.NotNull(decision);
        Assert.Equal("原始问题", decision!.Query);
        Assert.Null(SearchDecision.Parse("""{"reason": "missing flag"}""", "q"));
        Assert.Null(SearchDecision.Parse("not json", "q"));
    }

    [Fact]
    public void MaterialTermsKeepAcronymsAndInternalCapitals()
    {
        var terms = MaterialTerms.Extract(
        [
            "The XL-MIMO testbed uses TSIR and DeepMIMO data; RQ2 is open.",
            "Again, XL-MIMO and TSIR; RQ2 is answered by the DeepMIMO run."
        ]);
        Assert.Equal(new[] { "XL-MIMO", "TSIR", "DeepMIMO" }, terms.Take(3));
        Assert.Contains("RQ2", terms);
    }

    [Fact]
    public void MaterialTermsSkipOrdinaryWordsAndOneOffs()
    {
        Assert.Empty(MaterialTerms.Extract(["PDF only, once."]));
        // A term said once is still the only candidate when nothing repeats (Mac behaviour).
        Assert.Equal(new[] { "XL-MIMO" }, MaterialTerms.Extract(["XL-MIMO"]));
    }

    [Fact]
    public void MaterialTermsRespectTheLimit()
    {
        var terms = MaterialTerms.Extract(["AA1 BB2 CC3 DD4 AA1 BB2 CC3 DD4"], limit: 2);
        Assert.Equal(2, terms.Count);
    }

    [Theory]
    [InlineData("XL-MIMO", true)]
    [InlineData("DeepMIMO", true)]
    [InlineData("RQ2", true)]
    [InlineData("mmWave", true)]
    [InlineData("PDF", false)]
    [InlineData("Nimier-David", false)]
    [InlineData("2024", false)]
    [InlineData("HELLOWORLDWIDE", false)]
    [InlineData("A", false)]
    public void MaterialTermsRecogniseTerms(string word, bool expected)
    {
        Assert.Equal(expected, MaterialTerms.IsTerm(word));
    }

    [Fact]
    public void ShortcutLabelsAndSafety()
    {
        Assert.Equal("⇧⌘R", new ShortcutBinding(15).Label);
        Assert.Equal("⌃⌥⇧⌘R", new ShortcutBinding(15, Control: true, Option: true).Label);
        Assert.True(new ShortcutBinding(15).IsSafe);
        Assert.False(new ShortcutBinding(15, Command: false, Shift: false).IsSafe);
        Assert.False(new ShortcutBinding(200).IsSafe);
    }

    [Fact]
    public void ShortcutValidationRejectsUnsafeAndDuplicateBindings()
    {
        Assert.Null(ShortcutBinding.Validate(new Dictionary<string, ShortcutBinding>
        {
            ["record"] = new(15),
            ["island"] = new(31)
        }));
        Assert.NotNull(ShortcutBinding.Validate(new Dictionary<string, ShortcutBinding>
        {
            ["record"] = new(15, Command: false, Shift: false)
        }));
        Assert.NotNull(ShortcutBinding.Validate(new Dictionary<string, ShortcutBinding>
        {
            ["record"] = new(15),
            ["island"] = new(15)
        }));
    }

    [Fact]
    public void CodexParserReadsAgentMessages()
    {
        var message = CodexOutputParser.Parse("""{"type":"item.completed","item":{"type":"agent_message","text":"结论是缓存一致"}}""");
        Assert.NotNull(message);
        Assert.True(message!.IsMessage);
        Assert.Equal("结论是缓存一致", message.Text);
        Assert.Null(CodexOutputParser.Parse("""{"type":"item.completed","item":{"type":"reasoning","text":"x"}}"""));
        Assert.Null(CodexOutputParser.Parse("""{"type":"item.completed","item":{"type":"agent_message","text":""}}"""));
    }

    [Fact]
    public void CodexParserReadsErrors()
    {
        Assert.Equal("bad", CodexOutputParser.Parse("""{"type":"error","message":"bad"}""")!.Text);
        Assert.Equal("nested", CodexOutputParser.Parse("""{"type":"turn.failed","error":{"message":"nested"}}""")!.Text);
        Assert.Equal("Codex CLI 报告了未知错误。", CodexOutputParser.Parse("""{"type":"error"}""")!.Text);
        Assert.Null(CodexOutputParser.Parse("plain text"));
    }

    [Fact]
    public void TranscriptContextKeepsTheLastFiveMinutes()
    {
        var now = DateTimeOffset.UtcNow;
        var text = TranscriptContextBuilder.Recent(
        [
            (now.AddMinutes(-6), "六分钟前"),
            (now.AddMinutes(-2), "两分钟前"),
            (now.AddMinutes(1), "未来")
        ], now);
        Assert.Equal("两分钟前", text);
    }

    [Fact]
    public void OnboardingRepeatsForFeatureReleasesOnly()
    {
        Assert.Equal("2.3", OnboardingVersion.GuideVersion("2.3.1"));
        Assert.Equal("2.3", OnboardingVersion.GuideVersion("2.3"));
        Assert.True(OnboardingVersion.NeedsGuide("2.3.1", "2.2"));
        Assert.False(OnboardingVersion.NeedsGuide("2.3.1", "2.3"));
        Assert.False(OnboardingVersion.NeedsGuide("2.3.1", "2.3"));
        Assert.True(OnboardingVersion.IsFeatureUpgrade("2.3.1", "2.2", 1, 1));
        Assert.True(OnboardingVersion.IsFeatureUpgrade("2.3.1", "2.3", 1, 2));
        Assert.False(OnboardingVersion.IsFeatureUpgrade("2.3.1", "2.3", 2, 2));
    }
}
