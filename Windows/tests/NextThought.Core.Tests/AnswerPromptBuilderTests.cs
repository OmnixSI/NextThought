using System.Text.Json.Nodes;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for AnswerPromptBuilder and answer refinements.</summary>
public sealed class AnswerPromptBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void InstructionsCarryTheToolPolicy()
    {
        Assert.Contains("你是 NextThought", AnswerPromptBuilder.Instructions());
        Assert.Contains("不要使用工具", AnswerPromptBuilder.Instructions());
        Assert.Contains("可以使用联网搜索", AnswerPromptBuilder.Instructions(webSearch: true));
        Assert.DoesNotContain("__TOOLS__", AnswerPromptBuilder.Instructions());
        Assert.Contains("资料不是回答主题的范围上限", AnswerPromptBuilder.Instructions());
        Assert.Contains("不编造第一人称经历", AnswerPromptBuilder.Instructions());
    }

    [Fact]
    public void MakePartsSplitsSystemAndUserJson()
    {
        var (system, user) = AnswerPromptBuilder.MakeParts(
            "为什么用 Redis？", "context", "profile",
            [new AnswerTurn("q1", "a1"), new AnswerTurn("q2", "a2")], hasScreenshot: true,
            mode: MeetingMode.LabMeeting, style: "detailed", today: Now);
        Assert.Equal(AnswerPromptBuilder.Instructions(), system);
        var json = JsonNode.Parse(user)!.AsObject();
        Assert.Equal("为什么用 Redis？", json["question"]!.GetValue<string>());
        Assert.Equal("labMeeting", json["session_mode"]!.GetValue<string>());
        Assert.Equal("detailed", json["answer_style"]!.GetValue<string>());
        Assert.True(json["screenshot_attached"]!.GetValue<bool>());
        Assert.Equal(2, json["recent_answers"]!.AsArray().Count);
        Assert.Equal("2026-11-18 星期三", json["today"]!.GetValue<string>());
    }

    [Fact]
    public void LongInputsAreTruncatedWithANotice()
    {
        var (_, user) = AnswerPromptBuilder.MakeParts(new string('q', 9000), new string('c', 13000), "", [], false);
        var json = JsonNode.Parse(user)!.AsObject();
        var question = json["question"]!.GetValue<string>();
        Assert.StartsWith(new string('q', 8000), question);
        Assert.Contains("后续部分未提供", question);
        var transcript = json["recent_transcript"]!.GetValue<string>();
        Assert.StartsWith("（内容超过长度上限，较早部分未提供。）", transcript);
        Assert.Equal(12000, transcript.Length - "（内容超过长度上限，较早部分未提供。）\n".Length);
    }

    [Fact]
    public void RecentAnswersKeepTheLastSix()
    {
        var turns = Enumerable.Range(0, 8).Select(index => new AnswerTurn("q" + index, "a" + index)).ToList();
        var (_, user) = AnswerPromptBuilder.MakeParts("q", "c", "", turns, false);
        var recent = JsonNode.Parse(user)!["recent_answers"]!.AsArray();
        Assert.Equal(6, recent.Count);
        Assert.Equal("q2", recent[0]!["question"]!.GetValue<string>());
        Assert.Equal("q7", recent[5]!["question"]!.GetValue<string>());
    }

    [Fact]
    public void ExcerptsAndSkillsRespectTheirBudgets()
    {
        var excerpts = Enumerable.Range(0, 10)
            .Select(index => new PromptExcerpt(new string('e', 1000), "doc", Page: index + 1)).ToList();
        var budgeted = PromptExcerpt.Budget(excerpts, 8000);
        Assert.Equal(8, budgeted.Count);
        Assert.Equal(8000, budgeted.Sum(excerpt => excerpt.Text.Length));

        var skills = Enumerable.Range(0, 10).Select(index => new SkillSnippet("s" + index, new string('x', 7000))).ToList();
        var array = AnswerPromptBuilder.SkillBudget(skills);
        Assert.Equal(2, array.Count);
        Assert.Contains("后续部分未提供", array[0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void WholeMaterialKeepsSlideAndPageMarks()
    {
        var deck = new MaterialDocument
        {
            Name = "deck.pptx", Extension = ".pptx",
            Blocks = [
                new DocumentBlock("幻灯片 1", "Title", 1),
                new DocumentBlock("幻灯片 1", "First point", 1),
                new DocumentBlock("幻灯片 2", "Results", 2)
            ]
        };
        Assert.Equal("【deck.pptx · 幻灯片 1】\nTitle\nFirst point\n\n【deck.pptx · 幻灯片 2】\nResults",
            AnswerPromptBuilder.WholeMaterialOf([deck]));

        var report = new MaterialDocument
        {
            Name = "report.pdf", Extension = ".pdf",
            Blocks = [
                new DocumentBlock("第 1 页", "Intro", 1),
                new DocumentBlock("第 7 页", "Method", 7),
                new DocumentBlock("第 7 页", "More method", 7)
            ]
        };
        Assert.Equal("【report.pdf · p.7】\nMethod\nMore method",
            AnswerPromptBuilder.WholeMaterialOf([report])!.Split("\n\n").Last());
    }

    [Fact]
    public void WholeMaterialGroupsPagesAndHonoursTheLimit()
    {
        var document = new MaterialDocument
        {
            Name = "slides.pptx",
            Blocks =
            [
                new DocumentBlock("p.1", "第一页", 1),
                new DocumentBlock("p.1", "还是第一页", 1),
                new DocumentBlock("p.2", "第二页", 2)
            ]
        };
        var whole = AnswerPromptBuilder.WholeMaterialOf([document]);
        Assert.Contains("【slides.pptx · p.1】\n第一页\n还是第一页", whole);
        Assert.Contains("【slides.pptx · p.2】\n第二页", whole);
        Assert.Null(AnswerPromptBuilder.WholeMaterialOf([document], limit: 5));
        Assert.Equal("<my_full_material>\ntext\n</my_full_material>", AnswerPromptBuilder.WholeMaterial(" text "));
    }

    [Fact]
    public void ScreenAndGlossAreOptional()
    {
        var (_, user) = AnswerPromptBuilder.MakeParts("q", "c", "", [], false,
            screen: "", glossary: "", answerLanguage: "English", glossLanguage: "English");
        var json = JsonNode.Parse(user)!.AsObject();
        Assert.Null(json["screen_text"]);
        Assert.Null(json["term_glossary"]);
        Assert.Null(json["gloss_language"]);
        Assert.Equal("English", json["answer_language"]!.GetValue<string>());
    }

    [Fact]
    public void RefinementsKeepPointingAtTheOriginalQuestion()
    {
        var first = AnswerRefinement.Shorter.QuestionFor("为什么要缓存？");
        Assert.Contains("原问题：为什么要缓存？", first);
        var second = AnswerRefinement.English.QuestionFor(first);
        Assert.Equal("为什么要缓存？", AnswerRefinements.OriginalQuestion(second));
        Assert.Equal("没有标记", AnswerRefinements.OriginalQuestion("没有标记"));
    }

    [Fact]
    public void WebResultsAreLimitedAndOptional()
    {
        var results = Enumerable.Range(0, 10)
            .Select(index => new WebResult(new string('t', 300), "https://example.com/" + index, new string('s', 1300)))
            .ToList();
        var (_, user) = AnswerPromptBuilder.MakeParts("q", "c", "", [], false, webResults: results);
        var array = JsonNode.Parse(user)!["web_results"]!.AsArray();
        Assert.Equal(8, array.Count);
        Assert.StartsWith(new string('t', 200), array[0]!["title"]!.GetValue<string>());
        Assert.Contains("后续部分未提供", array[0]!["title"]!.GetValue<string>());
        Assert.StartsWith(new string('s', 1200), array[0]!["snippet"]!.GetValue<string>());

        var (_, without) = AnswerPromptBuilder.MakeParts("q", "c", "", [], false);
        Assert.Null(JsonNode.Parse(without)!["web_results"]);
    }
}
