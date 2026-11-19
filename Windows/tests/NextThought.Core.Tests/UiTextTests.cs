using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

public sealed class UiTextTests
{
    [Fact]
    public void TranslateReplacesKnownLabelsAndLeavesUnknownText()
    {
        Assert.Equal("Home", UiText.Translate("首页", true));
        Assert.Equal("Settings", UiText.Translate("设置", true));
        Assert.Equal("首页", UiText.Translate("首页", false));
        Assert.Equal("首页", UiText.Translate("Home", false));
        Assert.Equal("设置", UiText.Translate("Settings", false));
        Assert.Equal("会议标题", UiText.Translate("会议标题", true));
        Assert.All(UiText.EnglishStrings, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("en-US", true)]
    [InlineData("EN", true)]
    [InlineData("zh", false)]
    [InlineData("", false)]
    public void IsEnglishAcceptsLanguageTags(string language, bool expected) =>
        Assert.Equal(expected, UiText.IsEnglish(language));
}
