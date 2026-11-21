using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D8: every focusable control draws the Mac 2 px Ink.focus ring, hover/pressed use the Mac
/// hover/pressed tokens, and the control sample page still renders every Mac component state.
/// </summary>
public sealed class ControlStateTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Controls() =>
        File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));

    private static string Sample() =>
        File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "ControlSampleWindow.axaml"));

    /// <summary>The block of one style selector (a top-level Style or a nested one with ^).</summary>
    private static string StyleBlock(string controls, string selector)
    {
        var marker = $"<Style Selector=\"{selector}\">";
        var start = controls.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "missing style: " + selector);
        start += marker.Length;
        var end = controls.IndexOf("<Style Selector=", start, StringComparison.Ordinal);
        return controls[start..(end < 0 ? controls.Length : end)];
    }

    [Fact]
    public void EveryFocusableControlDrawsTheTwoPixelInkFocusRing()
    {
        var controls = Controls();
        // Buttons, TextBox and ComboBox draw the ring on their own template border.
        Assert.Contains("BorderBrush", StyleBlock(controls, "Button:focus-visible /template/ Border#Root"));
        Assert.Contains("Value=\"2\"", StyleBlock(controls, "Button:focus-visible /template/ Border#Root"));
        Assert.Contains("Ai", StyleBlock(controls, "Button:focus-visible /template/ Border#Root"));
        Assert.Contains("Value=\"2\"", StyleBlock(controls, "TextBox:focus-visible"));
        Assert.Contains("Value=\"2\"", StyleBlock(controls, "ComboBox:focus-visible"));
        // CheckBox / navigation row / filter chip / list row use a focus adorner with the same ring.
        foreach (var selector in new[] { "CheckBox", "RadioButton.nav", "RadioButton.chip", "ListBoxItem" })
        {
            var block = StyleBlock(controls, selector);
            Assert.Contains("FocusAdorner", block);
            Assert.Contains("Ai", block);
            Assert.Contains("BorderThickness=\"2\"", block);
        }
        Assert.True(CountOf(controls, "FocusAdorner") >= 4);
        // Avalonia 12 expects an ITemplate (FocusAdornerTemplate) here; a ControlTemplate throws
        // InvalidCastException only at runtime, so pin the element name.
        Assert.True(CountOf(controls, "<FocusAdornerTemplate>") >= 4);
        var index = 0;
        while ((index = controls.IndexOf("<FocusAdornerTemplate>", index, StringComparison.Ordinal)) >= 0)
        {
            var close = controls.IndexOf("</", index, StringComparison.Ordinal);
            Assert.True(close > 0);
            Assert.StartsWith("</FocusAdornerTemplate>", controls[close..]);
            index = close;
        }
    }

    [Fact]
    public void TheControlsSelfTestBuildsTheFocusAdornersForReal()
    {
        // A build or a pure unit test cannot catch a FocusAdorner template type mistake: the shell
        // crashed at startup. --self-test=controls instantiates the styled controls and moves focus.
        var check = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop",
            "SelfTest", "Checks", "RunControlsCheckAsync.cs"));
        Assert.Contains("RunControlsCheckAsync", check);
        Assert.Contains("new CheckBox", check);
        Assert.Contains("Classes.Add(\"chip\")", check);
        Assert.Contains("Classes.Add(\"nav\")", check);
        Assert.Contains("new ListBox", check);
        Assert.Contains(".Focus()", check);
        Assert.Contains("controls-check: passed", check);
        var registry = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop",
            "SelfTest", "Checks", "SelfTestRegistry.cs"));
        Assert.Contains("\"Run\" + string.Concat", registry); // --self-test=controls -> RunControlsCheckAsync
    }

    [Fact]
    public void HoverAndPressedUseTheMacTokens()
    {
        var controls = Controls();
        Assert.Contains("Hover", StyleBlock(controls, "Button:pointerover /template/ Border#Root"));
        Assert.Contains("Pressed", StyleBlock(controls, "Button:pressed /template/ Border#Root"));
        Assert.Contains("Hover", StyleBlock(controls, "RadioButton.nav") + StyleBlock(controls, "^:pointerover /template/ Border#Root"));
        Assert.Contains("Pressed", StyleBlock(controls, "^:pressed /template/ Border#Root"));
        Assert.Contains("Hover", StyleBlock(controls, "ListBoxItem:pointerover /template/ ContentPresenter"));
        Assert.Contains("Pressed", StyleBlock(controls, "ListBoxItem:pressed /template/ ContentPresenter"));
        Assert.Contains("Hover", StyleBlock(controls, "CheckBox:pointerover"));
        Assert.Contains("Pressed", StyleBlock(controls, "CheckBox:pressed"));
    }

    [Fact]
    public void ClickTargetsFollowTheMacHitSizes()
    {
        var controls = Controls();
        // Mac HitTargets.swift: minHeight 32, icon 36, row 36; this task only settles the row control.
        Assert.Contains("Value=\"{DynamicResource Control}\"", StyleBlock(controls, "CheckBox"));
        Assert.Contains("Mac Hit.minHeight", controls);
    }

    [Fact]
    public void ControlSamplePageStillShowsEveryMacComponentState()
    {
        var sample = Sample();
        foreach (var section in new[]
                 {
                     "ActionButton", "DataStateView", "SourceChip", "TranscriptRow", "TaskRow",
                     "RevisionDiff", "InlineNotice", "NewContentPill", "ComposerDock"
                 })
            Assert.Contains(section, sample);
        foreach (var copy in new[]
                 {
                     "原生控件样板", "正在加载本场资料…", "没有匹配到「数据库迁移」", "清除查询",
                     "译文 · 当前", "译文待更新", "过期状态来自版本比较；旧内容仍可读。",
                     "回答正在生成…", "停止生成", "保存受阻 · 有 3 项待保存 · 重试", "已保存 · 修订 12 · 03:05",
                     "旧原话：缓存几穿", "手动校正：缓存击穿", "旧译文基于修订前原话；手动校正仍保留。",
                     "回答暂时不可用；记录仍在继续。", "新增 6 句 · 回到现场", "立即回答"
                 })
            Assert.Contains(copy, sample);
        // No literal icons left in the sample: every glyph is a resource reference (D7 invariant).
        Assert.DoesNotContain("&#xE", sample);
    }

    [Fact]
    public void TheFourEmphasisStylesExist()
    {
        var controls = Controls();
        Assert.Contains("<Style Selector=\"Button\">", controls); // secondary (outline)
        Assert.Contains("<Style Selector=\"Button.primary\">", controls);
        Assert.Contains("<Style Selector=\"Button.quiet\">", controls);
        Assert.Contains("<Style Selector=\"Button.destructive\">", controls);
        Assert.Contains("ControlMetrics", File.ReadAllText(Path.Combine(Root(), "Sources", "NextThought", "ControlComponents.swift")));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
