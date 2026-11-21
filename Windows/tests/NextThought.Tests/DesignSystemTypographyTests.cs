using System.Text.RegularExpressions;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D5 §5.1: the type scale has to render at the Mac's weights. Variable fonts fall back to their
/// default instance inside Avalonia (the bundled Noto CJK faces default to Thin/ExtraLight), so the
/// app bundles one static face per weight and every style names the family for that weight. The
/// tests read the shipped resources so a future font swap cannot silently make the text thin again.
/// </summary>
public sealed class DesignSystemTypographyTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");
    private static string Theme => File.ReadAllText(Path.Combine(Desktop, "Styles", "Theme.axaml"));
    private static string Program => File.ReadAllText(Path.Combine(Desktop, "Program.cs"));
    private static string Project => File.ReadAllText(Path.Combine(Desktop, "NextThought.Desktop.csproj"));
    private static string FontsDirectory => Path.Combine(Desktop, "Assets", "Fonts");

    /// <summary>The static faces the type scale needs: sans regular/medium/semibold, serif medium.</summary>
    private static readonly string[] StaticFaces =
    [
        "Inter-Regular.ttf", "Inter-Medium.ttf", "Inter-SemiBold.ttf",
        "NotoSansSC-Regular.otf", "NotoSansSC-Medium.otf", "NotoSansSC-SemiBold.otf",
        "SourceSerif4-Medium.ttf", "NotoSerifSC-Medium.otf"
    ];

    [Fact]
    public void EveryUsedWeightShipsAsAStaticFace()
    {
        Assert.True(Directory.Exists(FontsDirectory), FontsDirectory);
        foreach (var face in StaticFaces)
        {
            var path = Path.Combine(FontsDirectory, face);
            Assert.True(File.Exists(path), "missing static font " + face);
            Assert.True(new FileInfo(path).Length > 100_000, face + " looks empty");
        }
    }

    [Fact]
    public void NoVariableFontIsReferencedAnyMore()
    {
        foreach (var file in Directory.EnumerateFiles(FontsDirectory))
            Assert.DoesNotContain("Variable", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-VF", string.Join("|", Directory.EnumerateFiles(FontsDirectory).Select(Path.GetFileName)));
        Assert.DoesNotContain("Variable", Theme);
        Assert.DoesNotContain("Variable", Program);
        Assert.DoesNotContain("Avalonia.Fonts.Inter", Project);
        Assert.DoesNotContain("WithInterFont", Program);
    }

    [Fact]
    public void FontFamiliesPairLatinAndChineseAtTheSameWeight()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["SansFamily"] = ["Inter-Regular.ttf#Inter", "NotoSansSC-Regular.otf#Noto Sans SC"],
            ["SansMediumFamily"] = ["Inter-Medium.ttf#Inter", "NotoSansSC-Medium.otf#Noto Sans SC"],
            ["SansSemiBoldFamily"] = ["Inter-SemiBold.ttf#Inter", "NotoSansSC-SemiBold.otf#Noto Sans SC"],
            ["SerifFamily"] = ["SourceSerif4-Medium.ttf#Source Serif 4", "NotoSerifSC-Medium.otf#Noto Serif SC"]
        };
        foreach (var (key, members) in expected)
        {
            var match = Regex.Match(Theme, $"<FontFamily x:Key=\"{key}\">([^<]+)</FontFamily>");
            Assert.True(match.Success, "font family resource missing: " + key);
            foreach (var member in members) Assert.Contains(member, match.Groups[1].Value);
            // A single family resource carries both scripts, so one TextBlock never mixes families by line.
            Assert.DoesNotContain("Microsoft YaHei UI", match.Groups[1].Value);
        }
    }

    [Theory]
    [InlineData("hero", "38", "Medium", "SerifFamily")]
    [InlineData("display", "30", "Medium", "SerifFamily")]
    [InlineData("section", "19", "Medium", "SerifFamily")]
    [InlineData("prompt", "22", "Medium", "SansMediumFamily")]
    [InlineData("title", "15", "SemiBold", "SansSemiBoldFamily")]
    [InlineData("body", "15", null, "SansFamily")]
    [InlineData("label", "13", "Medium", "SansMediumFamily")]
    [InlineData("caption", "12", null, "SansFamily")]
    [InlineData("overline", "11", "SemiBold", "SansSemiBoldFamily")]
    [InlineData("code", "13", null, "MonoFamily")]
    public void TypeScaleMatchesTheMacSizeWeightAndFamily(string style, string size, string? weight, string family)
    {
        var block = Regex.Match(Theme,
            "<Style Selector=\"TextBlock\\." + style + "\">(?<body>.*?)</Style>", RegexOptions.Singleline);
        Assert.True(block.Success, "text style missing: " + style);
        var body = block.Groups["body"].Value;
        Assert.Contains($"Property=\"FontSize\" Value=\"{size}\"", body);
        var familyMatch = Regex.Match(body, "Property=\"FontFamily\" Value=\"\\{StaticResource (?<family>[^}]+)\\}\"");
        if (family == "MonoFamily")
            familyMatch = Regex.Match(body, "Property=\"FontFamily\" Value=\"\\{StaticResource (?<family>[^}]+)\\}\"");
        Assert.True(familyMatch.Success, style + " must name its family explicitly");
        Assert.Equal(family, familyMatch.Groups["family"].Value);
        if (weight is null)
            Assert.DoesNotContain("Property=\"FontWeight\"", body);
        else
            Assert.Contains($"Property=\"FontWeight\" Value=\"{weight}\"", body);
    }

    [Fact]
    public void TextColoursComeOnlyFromTheInkTokens()
    {
        // §5.1: text uses Ink ink / secondary / tertiary; no opacity stacked on top to fake a lighter tone.
        foreach (Match block in Regex.Matches(Theme, "<Style Selector=\"TextBlock[^\"]*\">(?<body>.*?)</Style>", RegexOptions.Singleline))
        {
            var body = block.Groups["body"].Value;
            foreach (Match foreground in Regex.Matches(body, "Property=\"Foreground\" Value=\"\\{DynamicResource (?<key>[^}]+)\\}\""))
                Assert.Contains(foreground.Groups["key"].Value, new[] { "InkBrush", "Secondary", "Tertiary" });
            Assert.DoesNotContain("Property=\"Opacity\"", body);
        }
    }

    [Fact]
    public void TheSpecimenPageAndHarnessExist()
    {
        Assert.True(File.Exists(Path.Combine(Desktop, "SelfTest", "TypographySampleWindow.axaml")),
            "typography specimen window missing");
        var harness = File.ReadAllText(Path.Combine(Desktop, "SelfTest", "RunScreenshotsAsync.cs"));
        Assert.Contains("TypographySampleWindow", harness);
        Assert.Contains("typography-light", harness);
        Assert.Contains("typography-dark", harness);
        var compare = Path.Combine(Root(), "Windows", "validation", "screens", "typography", "compare.md");
        Assert.True(File.Exists(compare), "typography compare.md missing");
        var text = File.ReadAllText(compare);
        foreach (var level in new[] { "hero", "display", "section", "prompt", "title", "body", "label", "caption", "overline", "code" })
            Assert.Contains(level, text);
        Assert.Matches("逐条一致|以下几条不一致", text);
    }
}
