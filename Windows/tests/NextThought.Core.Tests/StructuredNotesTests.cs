using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Ported from the Mac Core tests for StructuredNotes.swift.</summary>
public sealed class StructuredNotesTests
{
    [Fact]
    public void NotesTimeFormatsAndParses()
    {
        Assert.Equal("00:00", NotesTime.Text(0));
        Assert.Equal("12:41", NotesTime.Text(761));
        Assert.Equal("1:02:05", NotesTime.Text(3725));
        Assert.Equal(761, NotesTime.Seconds("12:41"));
        Assert.Equal(761, NotesTime.Seconds("[12:41]"));
        Assert.Equal(3725, NotesTime.Seconds("1:02:05"));
        Assert.Equal(761, NotesTime.Seconds("12：41"));
        Assert.Null(NotesTime.Seconds("abc"));
        Assert.Null(NotesTime.Seconds("12:99"));
        Assert.Null(NotesTime.Seconds("12"));
    }

    [Fact]
    public void FishboneParsesFencesTrailingCommasAndPlainCauses()
    {
        var reply = """
            Here is the diagram:
            ```json
            {
              "fishbone": {
                "head": "为什么残差偏大",
                "headTime": "02:10",
                "categories": [
                  {"name": "数据", "causes": ["支座数据缺失 [03:05]", {"text": "温度补偿不完整", "time": "04:20"}],},
                  {"label": "流程", "detail": "没有做敏感性分析", "time": "06:00"}
                ],
              }
            }
            ```
            """;
        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Fishbone, fallbackTitle: "结构组会")!;
        Assert.NotNull(payload);
        Assert.Equal(NotesFormat.Fishbone, payload.Format);
        Assert.Equal("为什么残差偏大", payload.Fishbone!.Head);
        Assert.Equal(130, payload.Fishbone.HeadAt);
        Assert.Equal(2, payload.Fishbone.Categories.Count);
        Assert.Equal("支座数据缺失", payload.Fishbone.Categories[0].Causes[0].Text);
        Assert.Equal(185, payload.Fishbone.Categories[0].Causes[0].At);
        Assert.Equal("温度补偿不完整", payload.Fishbone.Categories[0].Causes[1].Text);
        Assert.Equal(260, payload.Fishbone.Categories[0].Causes[1].At);
        Assert.Equal("流程", payload.Fishbone.Categories[1].Name);
        Assert.Equal("没有做敏感性分析", payload.Fishbone.Categories[1].Causes[0].Text);
    }

    [Fact]
    public void OutlineParsesSectionsChildrenConceptsAndReview()
    {
        var reply = """
            {"outline": {"sections": [
              {"title": "温度补偿", "time": "00:30", "points": [
                {"text": "小波分解", "time": "01:10", "children": [{"text": "分离日温差", "time": "01:40"}]},
                "残差约 12% [02:20]"
              ]}
            ], "concepts": [{"term": "残差", "meaning": "原始信号与模型之差", "time": "03:00"}],
            "review": ["补做敏感性分析 [20:00]"]}}
            """;
        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Outline)!;
        Assert.NotNull(payload.Outline);
        var section = payload.Outline!.Sections[0];
        Assert.Equal("温度补偿", section.Title);
        Assert.Equal(30, section.At);
        Assert.Equal(2, section.Points.Count);
        Assert.Equal("小波分解", section.Points[0].Text);
        Assert.Equal("分离日温差", section.Points[0].Children[0].Text);
        Assert.Equal(100, section.Points[0].Children[0].At);
        Assert.Equal("残差约 12%", section.Points[1].Text);
        Assert.Equal(140, section.Points[1].At);
        Assert.Equal("残差", payload.Outline.Concepts[0].Term);
        Assert.Equal(1200, payload.Outline.Review[0].At);
    }

    [Fact]
    public void ViewpointsParsePartiesIssuesConsensusOwners()
    {
        var reply = """
            {"viewpoints": {"topic": "监测方案怎么定", "parties": ["我", "导师"],
             "issues": [{"title": "支座刚度", "positions": [
               {"party": "我", "text": "先做敏感性分析", "time": "10:00"},
               {"party": "导师", "text": "补一次静载试验"}]}],
             "consensus": [{"text": "本周先跑 20 组", "time": "12:30"}],
             "disagreements": [{"text": "是否现场试验"}],
             "owners": [{"who": "我", "task": "跑敏感性分析", "due": "周末", "time": "13:00"}]}}
            """;
        var payload = StructuredNotesParser.Parse(reply, NotesFormat.Viewpoints)!;
        var matrix = payload.Viewpoints!;
        Assert.Equal("监测方案怎么定", matrix.Topic);
        Assert.Equal(["我", "导师"], matrix.Parties);
        Assert.Equal("支座刚度", matrix.Issues[0].Title);
        Assert.Equal(2, matrix.Issues[0].Positions.Count);
        Assert.Equal(600, matrix.Issues[0].Positions[0].At);
        Assert.Equal(750, matrix.Consensus[0].At);
        Assert.Single(matrix.Disagreements);
        Assert.Equal("跑敏感性分析", matrix.Owners[0].Task);
        Assert.Equal("周末", matrix.Owners[0].Due);
        Assert.Single(matrix.PositionsOf(matrix.Issues[0], "我"));
    }

    [Fact]
    public void MalformedOrEmptyRepliesReturnNull()
    {
        Assert.Null(StructuredNotesParser.Parse("no json here", NotesFormat.Fishbone));
        Assert.Null(StructuredNotesParser.Parse("{\"head\": \"x\"}", NotesFormat.Fishbone));
        Assert.Null(StructuredNotesParser.Parse("{}", NotesFormat.Outline));
        Assert.Null(StructuredNotesParser.Parse("{\"a\":1}", NotesFormat.Standard));
    }

    [Fact]
    public void MarkdownRendersTheThreeFormats()
    {
        var fishbone = StructuredNotesParser.Parse(
            """{"head":"残差偏大","categories":[{"name":"数据","causes":[{"text":"支座缺失","time":"03:05"}]}]}""",
            NotesFormat.Fishbone)!;
        var markdown = StructuredNotesMarkdown.Render(fishbone);
        Assert.Contains("## 因果鱼骨图：残差偏大", markdown);
        Assert.Contains("### 数据", markdown);
        Assert.Contains("- 支座缺失 [03:05]", markdown);

        var outline = StructuredNotesParser.Parse(
            """{"sections":[{"title":"开场","points":[{"text":"目标"}]}],"concepts":[{"term":"残差","meaning":"差值"}]}""",
            NotesFormat.Outline)!;
        var outlineMarkdown = StructuredNotesMarkdown.Render(outline);
        Assert.Contains("## 要点大纲", outlineMarkdown);
        Assert.Contains("### 1. 开场", outlineMarkdown);
        Assert.Contains("- **残差**：差值", outlineMarkdown);

        var viewpoints = StructuredNotesParser.Parse(
            """{"topic":"方案","parties":["我"],"issues":[{"title":"刚度","positions":[{"party":"我","text":"先分析"}]}],"consensus":[{"text":"先跑"}],"owners":[{"who":"我","task":"跑分析"}]}""",
            NotesFormat.Viewpoints)!;
        var viewpointsMarkdown = StructuredNotesMarkdown.Render(viewpoints);
        Assert.Contains("## 观点对照：方案", viewpointsMarkdown);
        Assert.Contains("- **我**：先分析", viewpointsMarkdown);
        Assert.Contains("### 共识", viewpointsMarkdown);
        Assert.Contains("- 我 · 跑分析", viewpointsMarkdown);
    }

    [Fact]
    public void PromptCarriesLimitsAndSchema()
    {
        var prompt = StructuredNotesPrompt.System(NotesFormat.Fishbone, english: false);
        Assert.Contains("因果鱼骨图", prompt);
        Assert.Contains($"2 到 {StructuredNotesLimits.Categories} 个类别", prompt);
        Assert.Contains("只输出一个 JSON 对象", prompt);
        Assert.Contains("\"categories\"", prompt);
        Assert.Equal("", StructuredNotesPrompt.System(NotesFormat.Standard, false));

        var english = StructuredNotesPrompt.System(NotesFormat.Outline, english: true);
        Assert.Contains("Outline", english);
        Assert.Contains("\"sections\"", english);
    }

    [Fact]
    public void RecommendedFormatsFollowTheScene()
    {
        Assert.Equal(NotesFormat.Outline, NotesFormats.Recommended(MeetingMode.Lesson));
        Assert.Equal(NotesFormat.Fishbone, NotesFormats.Recommended(MeetingMode.LabMeeting));
        Assert.Equal(NotesFormat.Viewpoints, NotesFormats.Recommended(MeetingMode.Discussion));
        Assert.Equal(NotesFormat.Standard, NotesFormats.Recommended(MeetingMode.Meeting));
        Assert.Equal(NotesFormat.Outline, NotesFormats.Resolve(MeetingMode.Meeting,
            new Dictionary<string, string> { ["meeting"] = "outline" }));
        Assert.Equal(NotesFormat.Fishbone, NotesFormats.Resolve(MeetingMode.LabMeeting, null));
    }

    [Fact]
    public void PayloadKeepsUnknownMacFields()
    {
        const string json = """
            {"format":"outline","outline":{"sections":[{"title":"结论","points":[{"text":"灰度发布","at":12}]}]},
             "language":"zh","droppedCount":1,"model":"m","futureNotes":"x"}
            """;
        var payload = System.Text.Json.JsonSerializer.Deserialize<StructuredNotesPayload>(
            json, NextThought.Core.JsonDefaults.Options)!;
        Assert.Equal(NotesFormat.Outline, payload.Format);
        Assert.Equal("结论", payload.Outline!.Sections[0].Title);
        Assert.Equal("灰度发布", payload.Outline.Sections[0].Points[0].Text);
        var saved = System.Text.Json.JsonSerializer.Serialize(payload, NextThought.Core.JsonDefaults.Options);
        Assert.Contains("\"futureNotes\"", saved);
        Assert.Contains("\"format\"", saved);
    }
}
