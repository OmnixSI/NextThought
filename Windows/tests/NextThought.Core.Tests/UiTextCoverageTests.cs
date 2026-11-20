using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>G7: the generated table carries every Mac catalogue string (2,607).</summary>
public sealed class UiTextCoverageTests
{
    [Fact]
    public void GeneratedTableHasEveryMacCatalogueString()
    {
        Assert.Equal(2607, UiText.MacStringCount);
    }

    [Fact]
    public void EnglishQuestionsRenderFromTheMacTable()
    {
        var english = new AppSettings { Language = "en" };
        Assert.True(UiText.IsEnglish(english.Language));
        Assert.Equal("Cancel", UiText.Translate("取消", true));
    }
}
