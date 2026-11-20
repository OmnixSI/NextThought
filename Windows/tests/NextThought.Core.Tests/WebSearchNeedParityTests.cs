using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from WebSearchNeedTests.swift: time-sensitive questions need a search, everyday
/// questions stay offline, the reason names the cue, and a word inside another word is not a topic.
/// </summary>
public sealed class WebSearchNeedParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    // Swift: @Test(arguments: WebSearchNeed.evalSamples.filter(\.needsSearch).map(\.question))
    [Theory]
    [InlineData("现在比特币多少钱")]
    [InlineData("最近 OpenAI 发布了什么新模型")]
    [InlineData("今天北京天气怎么样")]
    [InlineData("英伟达最新一季的财报营收是多少")]
    [InlineData("苹果现任 CEO 是谁")]
    [InlineData("2026 年诺贝尔物理学奖颁给了谁")]
    [InlineData("What's the latest version of Swift?")]
    [InlineData("What is the current exchange rate of USD to CNY")]
    [InlineData("这周 A 股的行情怎么样，股价跌了吗")]
    public void TimeSensitiveQuestionsNeedSearch(string question)
    {
        var result = WebSearchNeed.Assess(question, Now);
        Assert.True(result.NeedsSearch, $"{question} → score {result.Score} ({result.Reason})");
    }

    // Swift: @Test(arguments: WebSearchNeed.evalSamples.filter { !$0.needsSearch }.map(\.question))
    [Theory]
    [InlineData("你最近在做什么项目")]
    [InlineData("介绍一下你目前负责的工作")]
    [InlineData("Redis 和 Memcached 有什么区别")]
    [InlineData("数据库和缓存怎么保证一致性")]
    [InlineData("为什么选择 Kafka 而不是 RabbitMQ")]
    [InlineData("讲一下 TCP 三次握手的原理")]
    [InlineData("Can you walk me through your current project?")]
    [InlineData("目前我们团队用的是微服务架构，你怎么看")]
    public void EverydayQuestionsStayOffline(string question)
    {
        var result = WebSearchNeed.Assess(question, Now);
        Assert.False(result.NeedsSearch, $"{question} → score {result.Score} ({result.Reason})");
    }

    [Fact]
    public void ReasonNamesTheCue()
    {
        var result = WebSearchNeed.Assess("最近的油价是多少", Now);
        Assert.Contains("最近", result.Reason);
        Assert.Contains("油价", result.Reason);
    }

    [Fact]
    public void AWordInsideAnotherIsNotATopic()
    {
        Assert.False(WebSearchNeed.Mentions("won", "i was wondering whether the site map needs to be updated"));
        Assert.True(WebSearchNeed.Mentions("won", "who won the election"));
        Assert.True(WebSearchNeed.Mentions("launch", "when it launches"));
        Assert.True(WebSearchNeed.Mentions("股价", "苹果的股价是多少"));
        Assert.True(WebSearchNeed.Assess(
            "I was wondering whether the site map in your third research question needs to be updated when the environment changes.",
            Now).Score < 0.5);
    }
}
