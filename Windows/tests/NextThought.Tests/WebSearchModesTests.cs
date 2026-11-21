using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>E4: the three-state web-search mode (Mac webSearchMode) and its decision policy.</summary>
public sealed class WebSearchModesTests
{
    [Fact]
    public void OffNeverSearchesEvenWhenTheRulesAsk()
    {
        Assert.False(WebSearchMode.Off.ShouldSearch(allowed: true, rulesNeedSearch: true));
        Assert.False(WebSearchMode.Off.ShouldSearch(allowed: false, rulesNeedSearch: true));
    }

    [Fact]
    public void AutoFollowsTheLocalRules()
    {
        Assert.True(WebSearchMode.Auto.ShouldSearch(allowed: true, rulesNeedSearch: true));
        Assert.False(WebSearchMode.Auto.ShouldSearch(allowed: true, rulesNeedSearch: false));
        Assert.False(WebSearchMode.Auto.ShouldSearch(allowed: false, rulesNeedSearch: true));
    }

    [Fact]
    public void AlwaysSearchesWhenAProviderIsConfigured()
    {
        Assert.True(WebSearchMode.Always.ShouldSearch(allowed: true, rulesNeedSearch: false));
        Assert.False(WebSearchMode.Always.ShouldSearch(allowed: false, rulesNeedSearch: false));
    }

    [Fact]
    public void ParseAndValueRoundTripThePreferencesString()
    {
        Assert.Equal(WebSearchMode.Off, WebSearchModes.Parse("off"));
        Assert.Equal(WebSearchMode.Always, WebSearchModes.Parse(" Always "));
        Assert.Equal(WebSearchMode.Auto, WebSearchModes.Parse(null));
        Assert.Equal(WebSearchMode.Auto, WebSearchModes.Parse("nonsense"));
        Assert.Equal("off", WebSearchMode.Off.Value());
        Assert.Equal("always", WebSearchMode.Always.Value());
        Assert.Equal("auto", WebSearchMode.Auto.Value());
    }

    [Fact]
    public void LabelsFollowTheMode()
    {
        Assert.Equal("关闭", WebSearchMode.Off.Title());
        Assert.Equal("自动判断", WebSearchMode.Auto.Title());
        Assert.Equal("每次都查", WebSearchMode.Always.Title());
    }
}
