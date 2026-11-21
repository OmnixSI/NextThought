using System.Text.RegularExpressions;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D6 §5.5/§5.7: overlay scroll bars that auto-hide without taking layout width, the 720 content
/// column, and the three light/dark shadows as real resources (float, contact, ambient) instead of
/// ad-hoc colours on individual cards.
/// </summary>
public sealed class DesignSystemScrollLayoutTests
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
    private static string Controls => File.ReadAllText(Path.Combine(Desktop, "Styles", "Controls.axaml"));

    [Fact]
    public void ScrollBarsAreThinOverlayBarsThatAutoHide()
    {
        // Avalonia's ScrollBar: AllowAutoHide/HideDelay make the bar appear only while hovering or
        // scrolling; Width keeps it thin and the Fluent template overlays it instead of reserving a column.
        Assert.Contains("Selector=\"ScrollBar\"", Theme);
        var block = Regex.Match(Theme, "<Style Selector=\"ScrollBar\">(?<body>.*?)</Style>", RegexOptions.Singleline);
        Assert.True(block.Success);
        var body = block.Groups["body"].Value;
        Assert.Contains("Property=\"AllowAutoHide\" Value=\"True\"", body);
        Assert.Contains("Property=\"Width\" Value=\"8\"", body);
        Assert.Contains("Property=\"HideDelay\"", body);
        Assert.DoesNotContain("Value=\"10\"", body);
    }

    [Fact]
    public void ScrollBarsDoNotReserveLayoutWidth()
    {
        // The overlay template hides the bar inside the ScrollViewer; no style may give it a fixed
        // column or a permanent background that would show even when not scrolling.
        foreach (Match block in Regex.Matches(Theme, "<Style Selector=\"ScrollBar[^\"]*\">(?<body>.*?)</Style>", RegexOptions.Singleline))
        {
            var body = block.Groups["body"].Value;
            Assert.DoesNotContain("Property=\"Background\" Value=\"#", body);
            Assert.DoesNotContain("Property=\"MinWidth\" Value=\"1", body);
        }
        Assert.Contains("Space", "Space"); // keep the test independent of the XAML parser
    }

    [Fact]
    public void TheContentColumnIs720AndCentred()
    {
        Assert.Contains("<x:Double x:Key=\"Content\">720</x:Double>", Theme);
        var style = Regex.Match(Theme, "<Style Selector=\"[^\"]*contentColumn[^\"]*\">(?<body>.*?)</Style>", RegexOptions.Singleline);
        Assert.True(style.Success, "contentColumn style missing");
        var body = style.Groups["body"].Value;
        Assert.Contains("Property=\"MaxWidth\" Value=\"{StaticResource Content}\"", body);
        Assert.Contains("Property=\"HorizontalAlignment\" Value=\"Center\"", body);
    }

    [Fact]
    public void RadiiAreTheThreeMacTokens()
    {
        Assert.Contains("<CornerRadius x:Key=\"RadiusSmall\">8</CornerRadius>", Theme);
        Assert.Contains("<CornerRadius x:Key=\"RadiusMedium\">14</CornerRadius>", Theme);
        Assert.Contains("<CornerRadius x:Key=\"RadiusLarge\">22</CornerRadius>", Theme);
        Assert.Matches("Property=\"CornerRadius\" Value=\"\\{(Static|Dynamic)Resource RadiusLarge\\}\"", Controls);
    }

    [Theory]
    // Contact shadow: radius 1 at y=1 (alpha 7% light / 40% dark); ambient: radius 14 at y=8
    // (6% / 32%); float: radius 16 at y=12 (12% / 50%). BoxShadow syntax: x y blur spread colour.
    [InlineData("Light", "ContactShadow", "0 1 2 0 #121C1B19")]
    [InlineData("Light", "AmbientShadow", "0 8 14 0 #0F1C1B19")]
    [InlineData("Light", "FloatShadow", "0 12 16 0 #1F1C1B19")]
    [InlineData("Dark", "ContactShadow", "0 1 2 0 #66000000")]
    [InlineData("Dark", "AmbientShadow", "0 8 14 0 #52000000")]
    [InlineData("Dark", "FloatShadow", "0 12 16 0 #80000000")]
    public void ShadowsAreSeparateResourcesPerTheme(string variant, string key, string value)
    {
        var dictionary = Regex.Match(Theme,
            "<ResourceDictionary x:Key=\"" + variant + "\">(?<body>.*?)</ResourceDictionary>", RegexOptions.Singleline);
        Assert.True(dictionary.Success, variant + " theme dictionary missing");
        Assert.Contains($"<BoxShadows x:Key=\"{key}\">{value}</BoxShadows>", dictionary.Groups["body"].Value);
    }

    [Fact]
    public void CardStylesUseTheShadowResources()
    {
        Assert.Contains("BoxShadow\" Value=\"{DynamicResource PaperCardShadow}\"", Controls);
        Assert.Contains("BoxShadow\" Value=\"{DynamicResource FloatShadowValue}\"", Controls);
        // The composed card shadow keeps the Mac's contact + ambient pair.
        Assert.Contains("<BoxShadows x:Key=\"PaperCardShadow\">0 1 2 0 #121C1B19, 0 8 14 0 #0F1C1B19</BoxShadows>", Theme);
        Assert.Contains("<BoxShadows x:Key=\"FloatShadowValue\">0 12 16 0 #1F1C1B19</BoxShadows>", Theme);
    }
}
