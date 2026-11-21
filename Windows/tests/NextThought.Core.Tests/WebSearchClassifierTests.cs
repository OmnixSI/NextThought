using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E14 (决议 §3.3 web-search): the Mac decision pipeline is settings → pure rewrite → rules →
/// model classifier with a three-second deadline and a rules fallback that records why; the
/// background lookup state machine is per answer id with generations and Mac's twelve-entry cap.
/// </summary>
public sealed class WebSearchClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(8));

    private static Task<string> Never(string system, string payload, CancellationToken ct) =>
        throw new InvalidOperationException("the model must not be asked in this case");

    private static WebSearchAssessment Rules(double score, string reason = "规则") => new(score, reason);

    [Fact]
    public async Task OffAndAlwaysAreTheSettingDecision()
    {
        var off = await WebSearchClassifier.AssessAsync(WebSearchMode.Off, "现在比特币多少钱", "", null,
            Rules(0.9), pureRewrite: false, explicitSearch: false, Never);
        Assert.False(off.Search);
        Assert.Equal("setting", off.Mechanism);

        var always = await WebSearchClassifier.AssessAsync(WebSearchMode.Always, "Redis 和 Memcached 的区别", "", null,
            Rules(0), pureRewrite: false, explicitSearch: false, Never);
        Assert.True(always.Search);
        Assert.Equal("每次都查", always.Reason);
        Assert.Equal("setting", always.Mechanism);
    }

    [Fact]
    public async Task PureRewriteReusesThePreviousGroundingAndRulesDecideImmediately()
    {
        var rewrite = await WebSearchClassifier.AssessAsync(WebSearchMode.Auto, "把上一条回答压缩到三句", "", null,
            Rules(0), pureRewrite: true, explicitSearch: false, Never);
        Assert.False(rewrite.Search);
        Assert.Equal("reuse", rewrite.Mechanism);

        var rule = await WebSearchClassifier.AssessAsync(WebSearchMode.Auto, "今天北京天气怎么样", "", null,
            Rules(0.9, "时间词“今天”"), pureRewrite: false, explicitSearch: false, Never);
        Assert.True(rule.Search);
        Assert.Equal("rule", rule.Mechanism);
        Assert.Equal("时间词“今天”", rule.Reason);
    }

    [Fact]
    public async Task TheModelDecisionWinsWhenItArrivesWithinThreeSeconds()
    {
        var decision = await WebSearchClassifier.AssessAsync(WebSearchMode.Auto, "解释一下 TCP 三次握手", "", null,
            Rules(0), pureRewrite: false, explicitSearch: false,
            (system, payload, ct) =>
            {
                Assert.Contains("Return only compact JSON", system, StringComparison.Ordinal);
                Assert.Contains("TCP", payload, StringComparison.Ordinal);
                Assert.Contains("\"question\"", payload, StringComparison.Ordinal);
                return Task.FromResult("{\"needsSearch\":false,\"reason\":\"稳定概念\",\"query\":\"\"}");
            }, now: Now);
        Assert.False(decision.Search);
        Assert.Equal("稳定概念", decision.Reason);
        Assert.Equal("model", decision.Mechanism);
    }

    [Fact]
    public async Task ASlowOrBrokenClassifierFallsBackToTheRulesAndRecordsTheFailure()
    {
        var slow = await WebSearchClassifier.AssessAsync(WebSearchMode.Auto, "哪个方案更好", "", null,
            Rules(0.4, "比较问题"), pureRewrite: false, explicitSearch: false,
            async (_, _, ct) => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return "{}"; },
            timeout: TimeSpan.FromMilliseconds(50), now: Now);
        Assert.False(slow.Search);
        Assert.Contains("模型复核未完成（三秒超时）", slow.Reason, StringComparison.Ordinal);
        Assert.Contains("沿用规则：比较问题", slow.Reason, StringComparison.Ordinal);
        Assert.Equal("fallback", slow.Mechanism);

        var broken = await WebSearchClassifier.AssessAsync(WebSearchMode.Auto, "哪个方案更好", "", null,
            Rules(0), pureRewrite: false, explicitSearch: false,
            (_, _, _) => Task.FromResult("not json"), now: Now);
        Assert.False(broken.Search);
        Assert.Contains("未取得有效 JSON", broken.Reason, StringComparison.Ordinal);
        Assert.Equal("fallback", broken.Mechanism);
    }

    [Fact]
    public async Task ACancelledDecisionIsRecordedAsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var decision = await WebSearchClassifier.AssessAsync(WebSearchMode.Auto, "哪个方案更好", "", null,
            Rules(0), pureRewrite: false, explicitSearch: false,
            (_, _, ct) => Task.Delay(1000, ct).ContinueWith(_ => "{}", ct), ct: cancellation.Token, now: Now);
        Assert.False(decision.Search);
        Assert.Equal("cancelled", decision.Mechanism);
        Assert.Contains("已取消", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLookupStoreIsPerAnswerChecksGenerationsAndKeepsTwelve()
    {
        var store = new WebLookupStore();
        var first = store.Begin(Guid.NewGuid(), "问题一", "tavily", Now);
        Assert.True(store.Find(first.AnswerId)!.IsSearching);
        Assert.True(store.UpdateAnswer(first.AnswerId, first.Generation, "部分"));
        Assert.False(store.UpdateAnswer(first.AnswerId, Guid.NewGuid(), "旧请求不能覆盖新答案"));
        Assert.True(store.Finish(first.AnswerId, first.Generation, "联网版答案",
            [new WebResult("标题", "https://example.com", "摘要")], 1.2));
        var done = store.Find(first.AnswerId)!;
        Assert.True(done.HasAnswer);
        Assert.Equal("联网版答案", done.Answer);
        Assert.Single(done.Sources!);

        for (var index = 0; index < 15; index++)
            store.Begin(Guid.NewGuid(), "问题" + index, "tavily", Now.AddSeconds(index));
        Assert.Equal(WebLookupStore.Capacity, store.Count);
        store.Clear();
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void WebSearchModeIsTheMacThreeStateString()
    {
        var settings = new AppSettings();
        Assert.Equal("auto", settings.WebSearchMode);
        var json = MacPreferenceMap.ToJsonObject(settings with { WebSearchMode = "always" });
        Assert.Equal("always", json["webSearchMode"]!.GetValue<string>());
        MacPreferenceMap.Apply(settings, "webSearchMode", "off");
        Assert.Equal("off", settings.WebSearchMode);
        Assert.False(settings.WebSearch);
        MacPreferenceMap.Apply(settings, "webSearchMode", "always");
        Assert.Equal("always", settings.WebSearchMode);
        Assert.True(settings.WebSearch);
        // Legacy Windows files stored the bool mapping: migrate it once.
        MacPreferenceMap.Apply(settings, "webSearchMode", false);
        Assert.Equal("off", settings.WebSearchMode);
    }
}
