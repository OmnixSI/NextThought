using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E14: the web-verification cache semantics — keyed by question + provider, 30-minute lifetime,
/// invalidated by a new key, and never caching an empty result set.
/// </summary>
public sealed class WebVerificationCacheTests
{
    private static WebResult Result(string title = "Kubernetes 1.33") =>
        new(title, "https://kubernetes.io/blog", "发布说明", "2026-01-01", null);

    private static WebVerificationEntry Entry(string question, string provider, DateTimeOffset at,
        IReadOnlyList<WebResult>? results = null) =>
        new(question, provider, question, results ?? [Result()], at);

    [Fact]
    public void FreshEntryHitsAndExpiresAfterThirtyMinutes()
    {
        var cache = new WebVerificationCache();
        var now = DateTimeOffset.UtcNow;
        cache.Store(Entry("Kubernetes 最新版本？", "tavily", now));

        Assert.True(cache.TryGet("Kubernetes 最新版本？", "tavily", now.AddMinutes(10), out var hit));
        Assert.Single(hit.Results);
        Assert.False(cache.TryGet("Kubernetes 最新版本？", "tavily", now.AddMinutes(31), out _));
        Assert.Equal(0, cache.Count); // the expired entry is dropped on read
    }

    [Fact]
    public void KeyUsesNormalisedQuestionAndProvider()
    {
        var cache = new WebVerificationCache();
        var now = DateTimeOffset.UtcNow;
        cache.Store(Entry("  Kubernetes   最新版本？ ", "Tavily", now));

        Assert.True(cache.TryGet("kubernetes 最新版本？", "tavily", now, out _));
        Assert.False(cache.TryGet("Kubernetes 最新版本？", "codex", now, out _));
        Assert.False(cache.TryGet("Kubernetes 版本？", "tavily", now, out _));
    }

    [Fact]
    public void SavingANewKeyInvalidatesEverything()
    {
        var cache = new WebVerificationCache();
        cache.Store(Entry("问题", "tavily", DateTimeOffset.UtcNow));
        Assert.Equal(1, cache.Count);

        cache.Invalidate();
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("问题", "tavily", DateTimeOffset.UtcNow, out _));
    }

    [Fact]
    public void EmptyResultsAreNotCached()
    {
        var cache = new WebVerificationCache();
        cache.Store(Entry("失败的问题", "tavily", DateTimeOffset.UtcNow, []));

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("失败的问题", "tavily", DateTimeOffset.UtcNow, out _));
    }

    [Fact]
    public void OldestEntriesFallOutAtCapacity()
    {
        var cache = new WebVerificationCache();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index <= WebVerificationCache.Capacity; index++)
            cache.Store(Entry("问题 " + index, "tavily", now.AddSeconds(index)));

        Assert.Equal(WebVerificationCache.Capacity, cache.Count);
        Assert.False(cache.TryGet("问题 0", "tavily", now.AddMinutes(1), out _));
        Assert.True(cache.TryGet("问题 " + WebVerificationCache.Capacity, "tavily", now.AddMinutes(1), out _));
    }

    [Fact]
    public void AgeLabelIsHumanReadable()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("刚刚", Entry("q", "tavily", now).AgeLabel(now.AddSeconds(20)));
        Assert.Equal("5 分钟前", Entry("q", "tavily", now).AgeLabel(now.AddMinutes(5)));
        Assert.Equal("2 小时前", Entry("q", "tavily", now).AgeLabel(now.AddHours(2)));
    }
}
