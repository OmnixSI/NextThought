using Xunit;

namespace NextThought.Tests;

/// <summary>D3/D9: design tokens match the Mac values and GPU rendering is not forced off.</summary>
public sealed class ThemeResourceTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    [Fact]
    public void ThemeUsesMacLightSunkenAndDarkShadows()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        Assert.Contains("<SolidColorBrush x:Key=\"Sunken\" Color=\"#0A1C1B19\" />", theme);
        Assert.Contains("<SolidColorBrush x:Key=\"ContactShadow\" Color=\"#66000000\" />", theme);
        Assert.Contains("<SolidColorBrush x:Key=\"AmbientShadow\" Color=\"#52000000\" />", theme);
        Assert.Contains("<BoxShadows x:Key=\"FloatShadowValue\">0 12 16 0 #80000000</BoxShadows>", theme);
        Assert.Contains("<BoxShadows x:Key=\"PaperCardShadow\">0 1 2 0 #66000000", theme);
    }



    [Fact]
    public void EveryMacPaletteValueMatches()
    {
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        var expected = new[]
        {
            ("Paper", "#F7F5F0"), ("Surface", "#FFFFFF"), ("SurfaceRaised", "#FFFFFF"),
            ("Sunken", "#0A1C1B19"), ("Hover", "#0F1C1B19"), ("Pressed", "#1C1C1B19"),
            ("InkBrush", "#1C1B19"), ("OnInk", "#FFFFFF"), ("Secondary", "#6B6963"),
            ("Tertiary", "#706E68"), ("Hairline", "#171C1B19"), ("Ai", "#595751"),
            ("AiSoft", "#0B1C1B19"), ("PromptPanel", "#F0EFEC"), ("Seal", "#C4452C"),
            ("Ready", "#237542"), ("Caution", "#8A5707")
        };
        foreach (var (key, color) in expected)
            Assert.Contains($"<SolidColorBrush x:Key=\"{key}\" Color=\"{color}\" />", theme);
        foreach (var (key, color) in new[]
        {
            ("Paper", "#121110"), ("Surface", "#1B1B19"), ("SurfaceRaised", "#242422"),
            ("Sunken", "#0DECEBE8"), ("Hover", "#14ECEBE8"), ("Pressed", "#24ECEBE8"),
            ("InkBrush", "#ECEBE8"), ("OnInk", "#131312"), ("Secondary", "#A3A19B"),
            ("Tertiary", "#95938C"), ("Hairline", "#1CECEBE8"), ("Ai", "#B8B6AF"),
            ("AiSoft", "#12ECEBE8"), ("PromptPanel", "#242422"), ("Seal", "#F07055"),
            ("Ready", "#4CC38A"), ("Caution", "#F0B24A")
        })
            Assert.Contains($"<SolidColorBrush x:Key=\"{key}\" Color=\"{color}\" />", theme);
    }

    [Fact]
    public void SpacingResourcesMatchMacSpace()
    {
        var root = RepoRoot();
        var theme = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        foreach (var pair in new[] { ("S12", "48"), ("Control", "32"), ("ControlLarge", "40"), ("Sidebar", "220"), ("Content", "720") })
            Assert.Contains($"<x:Double x:Key=\"{pair.Item1}\">{pair.Item2}</x:Double>", theme);
        var main = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ColumnDefinitions=\"220,*\"", main);
        var home = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml"));
        Assert.Contains("MaxWidth=\"720\"", home);
        var settings = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("MaxWidth=\"720\"", settings);
    }

    [Fact]
    public void ProgramDoesNotForceSoftwareRendering()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Program.cs"));
        Assert.DoesNotContain("NEXTTHOUGHT_GPU_RENDER", program);
        Assert.DoesNotContain("Win32RenderingMode.Software", program);
    }
}
