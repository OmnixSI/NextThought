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
        Assert.Equal("Meeting title", UiText.Translate("会议标题", true));
        Assert.Equal("没有这条文案", UiText.Translate("没有这条文案", true));
        Assert.All(UiText.EnglishStrings, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    [Fact]
    public void CountHelpersFollowTheGlobalLanguage()
    {
        var previous = UiText.EnglishUi;
        try
        {
            UiText.EnglishUi = false;
            Assert.Equal("3 场会议", UiText.Meetings(3));
            Assert.Equal("1 条转写", UiText.Transcripts(1));
            Assert.Equal("2 个系列", UiText.SeriesCount(2));
            UiText.EnglishUi = true;
            Assert.Equal("3 meetings", UiText.Meetings(3));
            Assert.Equal("1 line", UiText.Transcripts(1));
            Assert.Equal("2 series", UiText.SeriesCount(2));
            Assert.Equal("2 answers", UiText.Answers(2));
            Assert.Equal("1 lesson", UiText.Lessons(1));
        }
        finally { UiText.EnglishUi = previous; }
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
