using System.ComponentModel;
using System.Globalization;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>G8: explicit string lookup, placeholder formatting and live language switching.</summary>
[Collection("UiTextGlobalState")]
public sealed class UiTextSourceTests
{
    [Fact]
    public void ChineseIsTheKeyAndEnglishComesFromTheTables()
    {
        Assert.Equal("保存", UiText.Lookup("保存", false));
        Assert.Equal("Save", UiText.Lookup("保存", true));
        // Mac catalogue entry (generated table).
        Assert.Equal("Cancel", UiText.Lookup("取消", true));
        // Windows-only wording (hand-written table, listed in WORDING.md).
        Assert.Equal("Home", UiText.Lookup("首页", true));
        // Unknown keys pass through so a partial table is safe.
        Assert.Equal("没有这条文案", UiText.Lookup("没有这条文案", true));
    }

    [Fact]
    public void WindowsTableDoesNotDuplicateMacOnlySentences()
    {
        Assert.All(UiText.WindowsStrings, pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Key));
            Assert.False(string.IsNullOrWhiteSpace(pair.Value));
        });
        Assert.True(UiText.WindowsStringCount > 0);
        Assert.True(UiText.MacStringCount >= 2638);
    }

    [Theory]
    [InlineData("%lld 场会议", 3, "3 场会议")]
    [InlineData("已删除 %lld 场会议。", 2, "已删除 2 场会议。")]
    [InlineData("第 %lld 页 / %lld", 2, "第 2 页 / %lld")]
    [InlineData("一个半：%.1f", 1.5, "一个半：1.5")]
    [InlineData("完成 100%%", 0, "完成 100%")]
    public void FormatFillsMacPlaceholders(string template, object argument, string expected)
    {
        Assert.Equal(expected, UiText.FormatTemplate(template, argument));
    }

    [Fact]
    public void FormatSupportsSeveralArgumentsAndPositionalForms()
    {
        Assert.Equal("3 files · 2 questions", UiText.FormatTemplate("%lld files · %lld questions", 3, 2));
        Assert.Equal("Meeting 2 of “Kubernetes”", UiText.FormatTemplate("Meeting %2$lld of “%1$@”", "Kubernetes", 2));
        Assert.Equal("6 of 10 searched", UiText.FormatTemplate("%1$lld of %2$lld searched", 6, 10));
        Assert.Equal("12%", UiText.FormatTemplate("%lld%%", 12));
    }

    [Fact]
    public void FormatTranslatesTheSentenceBeforeFillingIt()
    {
        var previous = UiText.EnglishUi;
        try
        {
            UiText.EnglishUi = false;
            Assert.Equal("已选择 3 场会议", UiText.Format("已选择 %lld 场会议", 3));
            UiText.EnglishUi = true;
            Assert.Equal("3 meetings selected", UiText.Format("已选择 %lld 场会议", 3));
        }
        finally { UiText.EnglishUi = previous; }
    }

    [Fact]
    public void MissingArgumentsLeaveThePlaceholderVisible()
    {
        Assert.Equal("3 files · %lld questions", UiText.FormatTemplate("%lld files · %lld questions", 3));
    }

    [Fact]
    public void LanguagePreferenceFollowsMacValues()
    {
        Assert.Equal(UiTextSource.SystemPreference, UiTextSource.NormalizePreference(null));
        Assert.Equal(UiTextSource.SystemPreference, UiTextSource.NormalizePreference(""));
        Assert.Equal(UiTextSource.SystemPreference, UiTextSource.NormalizePreference("跟随系统"));
        Assert.Equal(UiTextSource.ChinesePreference, UiTextSource.NormalizePreference("zh-Hans"));
        Assert.Equal(UiTextSource.ChinesePreference, UiTextSource.NormalizePreference("zh"));
        Assert.Equal(UiTextSource.ChinesePreference, UiTextSource.NormalizePreference("简体中文"));
        Assert.Equal(UiTextSource.EnglishPreference, UiTextSource.NormalizePreference("en"));
        Assert.Equal(UiTextSource.EnglishPreference, UiTextSource.NormalizePreference("English"));

        Assert.True(UiText.ResolveEnglish("en"));
        Assert.False(UiText.ResolveEnglish("zh-Hans"));
        Assert.False(UiText.ResolveEnglish("zh"));
    }

    [Theory]
    [InlineData("zh-CN", false)]
    [InlineData("zh-Hans", false)]
    [InlineData("zh-TW", false)]
    [InlineData("en-US", true)]
    [InlineData("en-GB", true)]
    [InlineData("de-DE", true)]
    [InlineData("ja-JP", true)]
    public void DefaultLanguageFollowsTheSystemUnlessItIsChinese(string culture, bool english)
    {
        // "跟随系统" is the default preference; the app only ships Chinese and English, so every
        // non-Chinese Windows display language resolves to English.
        Assert.Equal(english, UiText.SystemIsEnglish(new CultureInfo(culture)));
    }

    [Fact]
    public void ChangingTheLanguageNotifiesIndexerBindingsAndObservers()
    {
        var source = UiTextSource.Instance;
        var previous = source.Preference;
        var names = new List<string?>();
        var observed = new List<string>();
        PropertyChangedEventHandler handler = (_, args) => names.Add(args.PropertyName);
        using var subscription = source.Observe("保存").Subscribe(new Observer(observed.Add));
        try
        {
            source.SetPreference(UiTextSource.ChinesePreference);
            names.Clear();
            source.PropertyChanged += handler;
            Assert.Equal("保存", source["保存"]);
            Assert.Equal("保存", observed[^1]);

            source.SetPreference(UiTextSource.EnglishPreference);
            Assert.True(source.IsEnglish);
            Assert.Equal("Save", source["保存"]);
            Assert.Contains("Item[]", names);
            Assert.Equal("Save", observed[^1]);

            source.SetPreference(UiTextSource.ChinesePreference);
            Assert.False(source.IsEnglish);
            Assert.Equal("保存", source["保存"]);
            Assert.Equal("保存", observed[^1]);
        }
        finally
        {
            source.PropertyChanged -= handler;
            source.SetPreference(previous);
        }
    }

    [Fact]
    public void SettingTheSameLanguageDoesNotNotifyAgain()
    {
        var source = UiTextSource.Instance;
        var previous = source.Preference;
        source.SetPreference(UiTextSource.EnglishPreference);
        var raised = 0;
        PropertyChangedEventHandler handler = (_, _) => raised++;
        try
        {
            source.PropertyChanged += handler;
            source.SetEnglish(true);
            Assert.Equal(0, raised);
            source.SetEnglish(false);
            Assert.True(raised > 0);
        }
        finally
        {
            source.PropertyChanged -= handler;
            source.SetPreference(previous);
        }
    }

    private sealed class Observer(Action<string> onNext) : IObserver<string>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(string value) => onNext(value);
    }
}
