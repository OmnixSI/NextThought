using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Swift parity: Tests/NextThoughtCoreTests/ScreenEvidenceTests.swift.</summary>
public sealed class ScreenEvidenceParityTests
{
    [Fact]
    public void DeicticQuestionDoesNotRequireTextOverlap()
    {
        var screen = new ScreenContext
        {
            FrameId = "page",
            TargetId = "window:7",
            Source = "PPT",
            CapturedAt = DateTimeOffset.UtcNow,
            Regions = [new ScreenTextRegion("Throughput 92.7%", [0.1, 0.2, 0.3, 0.1], 0.95)]
        };
        Assert.True(ScreenReference.IsRelated("你这一页的这个地方为什么这样画？", screen));
        Assert.True(ScreenReference.NeedsImage("右边这个图说明了什么？"));
        Assert.False(ScreenReference.IsRelated("介绍一下你最近负责的项目", screen));
    }

    [Fact]
    public void OldOrFutureFrameCannotMasqueradeAsCurrent()
    {
        var now = DateTimeOffset.UtcNow;
        var old = new ScreenContext
        {
            FrameId = "old",
            TargetId = "display:1",
            Source = "screen",
            CapturedAt = DateTimeOffset.UtcNow.AddSeconds(-3),
            Regions = []
        };
        Assert.False(old.IsFresh(now));

        var future = new ScreenContext
        {
            FrameId = "future",
            TargetId = "display:1",
            Source = "screen",
            CapturedAt = now.AddSeconds(3),
            Regions = []
        };
        Assert.False(future.IsFresh(now));
    }

    [Fact]
    public void SearchDecisionRequiresStructuredVerdictAndKeepsSubject()
    {
        var parsed = SearchDecision.Parse(
            """{"needsSearch":true,"reason":"当前规格","query":"Claude context window"}""",
            question: "它支持多长？");
        Assert.True(parsed?.Search);
        Assert.Equal("Claude context window", parsed?.Query);
        Assert.Null(SearchDecision.Parse("yes", question: "q"));
        Assert.False(AnswerGrounding.PureRewrite("补充这个方法与最新 SOTA 的差异"));
        Assert.True(AnswerGrounding.PureRewrite("把上一条回答压缩成两句"));
    }

    [Fact]
    public void LowSimilarityCanReturnNoMaterialsAndPhysicalLocatorWins()
    {
        var doc = new MaterialDocument
        {
            Name = "paper.pdf",
            Blocks = [new DocumentBlock("p.7", "cache consistency", 7)]
        };
        var chunks = ContextChunker.Chunks([doc]);
        var semantic = new SemanticIndex();
        semantic.SetVector([1f, 0f], chunks[0].Fingerprint, 0);
        var index = new HybridContextIndex([doc], semantic);
        Assert.Empty(index.Retrieve(question: "unrelated astronomy", queryVector: [0f, 1f]));
        Assert.Equal(7, index.Retrieve(question: "paper.pdf 第7页", queryVector: [0f, 1f]).First().Locator?.Page);
    }

    [Fact]
    public void PromptAllowsGeneralScienceWithoutInventingResearchResults()
    {
        var prompt = AnswerPromptBuilder.MakeParts(
            question: "什么是近场波束成形？", context: "", profile: "", turns: [], hasScreenshot: false,
            mode: MeetingMode.Academic);
        Assert.Contains("资料不是回答主题的范围上限", prompt.System);
        Assert.Contains("不编造第一人称经历", prompt.System);
    }
}
