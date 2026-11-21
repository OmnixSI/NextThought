using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// Stage 3 §6.1: the Mac design-system components exist as same-named reusable Avalonia controls,
/// the sample page shows every state on one page, and MarkdownText actually parses bold/bullets/
/// inline code. These are source-contract tests: the controls themselves are visual, so the state
/// coverage is asserted on the sample page and the style dictionary.
/// </summary>
public sealed class DesignSystemComponentsTests
{
    private static readonly string[] Components =
    [
        "ControlButtonBody", "BusyActionLabel", "AIHeading", "InkSegmented", "InteractiveRowBody",
        "ShortcutHint", "StatusDot", "InkSwitch", "WideToggleRow", "AppBackdrop", "GlassGroup",
        "MenuPicker", "PillTabs", "ProminentFill", "ScrollEdgeFade", "OperationFeedback",
        "PaperFieldBody", "Lift", "VoiceWave", "MetricPill", "RefinementBar",
        "UnderlineTabs"
    ];

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void EveryMacComponentHasASameNamedReusableControl()
    {
        var code = Read("Controls", "DesignSystemComponents.cs");
        foreach (var component in Components)
            Assert.Contains("class " + component, code);
        // MarkdownText already existed as the reusable renderer; §6.1 only requires it to render emphasis.
        Assert.Contains("class MarkdownText", Read("Views", "MarkdownText.cs"));
        // No page implementation may own a component: they live in Controls/ only.
        Assert.Contains("namespace NextThought.Desktop.Controls", code);
    }

    [Fact]
    public void DesignSystemStylesCoverEveryStateAndMacValue()
    {
        var styles = Read("Styles", "DesignSystem.axaml");
        foreach (var state in new[] { ":pointerover", ":pressed", ":disabled", ":focus-visible" })
            Assert.Contains(state, styles);
        Assert.Contains("selected", styles);
        // ControlMetrics.radius = 9, height = 32, focusWidth = 2 from ControlComponents.swift.
        Assert.Contains("ControlRadius", styles);
        Assert.Contains("9", styles);
        // Token colours come from Theme.axaml (Ink palette).
        foreach (var brush in new[] { "InkBrush", "Secondary", "Hairline", "Sunken", "OnInk", "SurfaceRaised", "Ai" })
            Assert.Contains(brush, styles);
    }

    [Fact]
    public void SamplePageShowsAllSixStatesForEveryComponent()
    {
        var samples = Read("Views", "ComponentSamplesWindow.axaml");
        foreach (var component in Components)
            Assert.Contains(component, samples);
        foreach (var state in new[] { "常态", "悬停", "按下", "选中", "禁用", "聚焦" })
            Assert.Contains(state, samples);
        Assert.Contains("浅色", samples);
        Assert.Contains("深色", samples);
    }

    [Fact]
    public void MarkdownTextRendersBoldBulletsAndInlineCode()
    {
        var code = Read("Views", "MarkdownText.cs");
        Assert.Contains("FontWeight", code);
        Assert.Contains("FontStyle", code);
        Assert.Contains("Run(", code);
        Assert.Contains("MarkdownReader", code + File.ReadAllText(Path.Combine(Root(), "src", "NextThought.Core", "MarkdownReader.cs")));
    }

    // ---- MarkdownReader (Core): the actual parsing behaviour behind MarkdownText --------

    [Fact]
    public void MarkdownBoldListAndInlineCodeAreParsed()
    {
        var blocks = MarkdownReader.Blocks("## 结论\n- 先给 **答案**，再给 `code`\n1. 第一条");
        Assert.Equal(MarkdownBlockKind.Heading, blocks[0].Kind);
        Assert.Equal("结论", blocks[0].Text);
        Assert.Equal(MarkdownBlockKind.Bullet, blocks[1].Kind);
        Assert.Equal(MarkdownBlockKind.Bullet, blocks[2].Kind);

        var runs = MarkdownReader.Inline(blocks[1].Text);
        Assert.Contains(runs, run => run.Kind == MarkdownInlineKind.Bold && run.Text == "答案");
        Assert.Contains(runs, run => run.Kind == MarkdownInlineKind.Code && run.Text == "code");
    }

    [Fact]
    public void MarkdownCodeFenceAndItalicAreParsed()
    {
        var blocks = MarkdownReader.Blocks("```\nlet x = 1\n```\n*强调*");
        Assert.Equal(MarkdownBlockKind.Code, blocks[0].Kind);
        Assert.Equal("let x = 1", blocks[0].Text);
        Assert.Contains(MarkdownReader.Inline("*强调*"),
            run => run.Kind == MarkdownInlineKind.Italic && run.Text == "强调");
    }
    [Fact]
    public void MarkdownTextResolvesInkFromTheActualThemeVariant()
    {
        var code = File.ReadAllText(Path.Combine(WindowsRoot(), "src", "NextThought.Desktop", "Views", "MarkdownText.cs"));
        // D6: the control's own theme scope must win over Application.TryFindResource (which is light-only
        // when the app theme differs from the window's ThemeVariantScope).
        Assert.Contains("ActualThemeVariant", code);
        Assert.Contains("TryFindResource(key, origin.ActualThemeVariant", code);
        Assert.Contains("ActualThemeVariantChanged", code);
    }

    private static string WindowsRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }
}