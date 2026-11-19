using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for search decisions, grounding rules and the answer state bundle.</summary>
public sealed class AnswerEvidenceTests
{
    [Fact]
    public void EvidenceBundleUsesTheMacRequestKey()
    {
        var id = Guid.NewGuid();
        var json = System.Text.Json.JsonSerializer.Serialize(new EvidenceBundle(id), JsonDefaults.Options);
        Assert.Contains("\"requestID\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<EvidenceBundle>(json, JsonDefaults.Options)!;
        Assert.Equal(id, back.RequestId);
    }

    [Fact]
    public void ContextExcerptUsesTheMacChunkKeys()
    {
        var bundle = new EvidenceBundle(Guid.NewGuid())
        {
            Materials = [new ContextExcerpt("doc", 1, "正文", ChunkId: "chunk", BlockId: "block")]
        };
        var json = System.Text.Json.JsonSerializer.Serialize(bundle, JsonDefaults.Options);
        Assert.Contains("\"chunkID\"", json);
        Assert.Contains("\"blockID\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<EvidenceBundle>(json, JsonDefaults.Options)!;
        Assert.Equal("chunk", back.Materials[0].ChunkId);
        Assert.Equal("block", back.Materials[0].BlockId);
    }

    [Fact]
    public void SearchDecisionParsesTheOutermostJsonObject()
    {
        var decision = SearchDecision.Parse("""
            好的，结果如下：{"needsSearch": true, "reason": "需要最新数据", "query": "2026 缓存基准"}
            希望有帮助。
            """, "缓存怎么样？");
        Assert.NotNull(decision);
        Assert.True(decision!.Search);
        Assert.Equal("需要最新数据", decision.Reason);
        Assert.Equal("2026 缓存基准", decision.Query);
        Assert.Equal("model", decision.Mechanism);
    }

    [Fact]
    public void SearchDecisionFallsBackToTheQuestionAndTruncates()
    {
        var reason = new string('理', 260);
        var decision = SearchDecision.Parse($$"""{"needsSearch": false, "reason": "{{reason}}", "query": "  "}""", "原问题");
        Assert.NotNull(decision);
        Assert.False(decision!.Search);
        Assert.Equal(200, decision.Reason.Length);
        Assert.Equal("原问题", decision.Query);
    }

    [Fact]
    public void SearchDecisionRejectsMalformedOrIncompleteJson()
    {
        Assert.Null(SearchDecision.Parse("no json here", "q"));
        Assert.Null(SearchDecision.Parse("{\"reason\":\"x\"}", "q"));
        Assert.Null(SearchDecision.Parse("{\"needsSearch\":\"yes\",\"reason\":\"x\"}", "q"));
        Assert.Null(SearchDecision.Parse("{\"needsSearch\":true}", "q"));
    }

    [Fact]
    public void GroundingRulesRecogniseRewritesAndExplicitSearches()
    {
        Assert.True(AnswerGrounding.PureRewrite("把上一条回答压缩到三点"));
        Assert.False(AnswerGrounding.PureRewrite("解释一下缓存一致性"));
        Assert.True(AnswerGrounding.NeedsVerification("这个价格可能已过时。"));
        Assert.False(AnswerGrounding.NeedsVerification("这是通用原理。"));
        Assert.True(AnswerGrounding.ExplicitSearch("帮我联网查一下最新版本"));
        Assert.True(AnswerGrounding.ExplicitSearch("search the web for the current release"));
        Assert.False(AnswerGrounding.ExplicitSearch("解释一下这个算法"));
    }

    [Fact]
    public void EvidenceBundleDefaultsAndCarriesItsSources()
    {
        var id = Guid.NewGuid();
        var bundle = new EvidenceBundle(id);
        Assert.Equal(id, bundle.RequestId);
        Assert.Empty(bundle.Materials);
        Assert.Null(bundle.Screen);
        Assert.False(bundle.ScreenImageAttached);
        Assert.Equal("关键词查找", bundle.Retrieval);
        Assert.Null(bundle.Search);
        Assert.Empty(bundle.WebSources);
        Assert.Empty(bundle.Notes);

        var filled = bundle with
        {
            Materials = [new ContextExcerpt("论文", 2, "相关内容")],
            Retrieval = "语义检索",
            Search = new SearchDecision(true, "r", "q", "model"),
            WebSources = [new WebResult("标题", "https://example.com", "摘要")],
            Notes = ["备注"]
        };
        Assert.Single(filled.Materials);
        Assert.Equal("语义检索", filled.Retrieval);
        Assert.Single(filled.WebSources);
        Assert.Single(filled.Notes);
    }

    [Fact]
    public void AnswerStateStartsWaitingWithMatchingEvidence()
    {
        var id = Guid.NewGuid();
        var state = new AnswerState(id, "为什么？");
        Assert.Equal(id, state.Id);
        Assert.Equal("为什么？", state.Question);
        Assert.Equal("", state.Text);
        Assert.Equal(AnswerState.PhaseValue.Waiting, state.Phase);
        Assert.Equal(0, state.Revision);
        Assert.Null(state.Error);
        Assert.Equal(id, state.Evidence.RequestId);

        var generating = state with { Phase = AnswerState.PhaseValue.Generating, Text = "因为", Revision = 1 };
        Assert.Equal(AnswerState.PhaseValue.Generating, generating.Phase);
        Assert.Equal(1, generating.Revision);
        var failed = generating with { Phase = AnswerState.PhaseValue.Failed, Error = "服务不可用" };
        Assert.Equal("服务不可用", failed.Error);
    }

    [Fact]
    public void ParameterlessAnswerStateStillGetsAnEvidenceBundle()
    {
        var state = new AnswerState();
        Assert.Equal(state.Id, state.Evidence.RequestId);
        Assert.Equal(AnswerState.PhaseValue.Waiting, state.Phase);
    }
}
