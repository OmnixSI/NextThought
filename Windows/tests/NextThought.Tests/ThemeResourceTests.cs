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
        // §5.7: contact/ambient/float are BoxShadows resources now, one set per theme.
        Assert.Contains("<BoxShadows x:Key=\"ContactShadow\">0 1 2 0 #66000000</BoxShadows>", theme);
        Assert.Contains("<BoxShadows x:Key=\"AmbientShadow\">0 8 14 0 #52000000</BoxShadows>", theme);
        Assert.Contains("<BoxShadows x:Key=\"FloatShadow\">0 12 16 0 #80000000</BoxShadows>", theme);
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
    public void ViewsHaveNoNumericSpacingOrRadiusLiterals()
    {
        var views = Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Views");
        foreach (var file in Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var matches = System.Text.RegularExpressions.Regex.Matches(text,
                "\\b(Margin|Padding|Spacing|CornerRadius)=\"[0-9.,-]+\"");
            Assert.True(matches.Count == 0, file + " has " + matches.Count + " numeric spacing values");
        }
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        Assert.Contains("x:Key=\"D4Margin_", theme);
        Assert.Contains("x:Key=\"D4Padding_", theme);
    }

    [Fact]
    public void SpacingResourcesMatchMacSpace()
    {
        var root = RepoRoot();
        var theme = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        foreach (var pair in new[]
                 {
                     ("S12", "48"), ("Control", "32"), ("ControlLarge", "40"), ("Sidebar", "220"), ("Content", "720"),
                     // D4/HitTargets.swift: least clickable height (32), icon-only button (36), whole row (36).
                     ("HitMin", "32"), ("HitIcon", "36"), ("HitRow", "36")
                 })
            Assert.Contains($"<x:Double x:Key=\"{pair.Item1}\">{pair.Item2}</x:Double>", theme);
        var main = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ColumnDefinitions=\"Auto,*\"", main);
        // HomeView.swift uses 760 for the home page; the shared Content token (720) covers the
        // other pages (settings below).
        var home = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml"));
        Assert.Contains("MaxWidth=\"760\"", home);
        var settings = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("MaxWidth=\"720\"", settings);
        var sidebar = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("Sidebar", theme);
        Assert.True(sidebar.Length > 0);
    }

    [Fact]
    public void ClickTargetsUseTheMacHitTokens()
    {
        var root = RepoRoot();
        var controls = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        // Text buttons, text fields and row toggles all use the 32 pt Mac control height.
        foreach (var selector in new[] { "Button", "TextBox", "ComboBox", "CheckBox" })
            Assert.Contains("MinHeight\" Value=\"{DynamicResource Control}\"", StyleBlock(controls, selector));
        // Whole-row controls and chips use the row / control hit targets.
        Assert.Contains("MinHeight\" Value=\"{DynamicResource HitRow}\"", StyleBlock(controls, "RadioButton.nav"));
        Assert.Contains("MinHeight\" Value=\"{DynamicResource Control}\"", StyleBlock(controls, "RadioButton.chip"));
        Assert.Contains("MinHeight\" Value=\"{DynamicResource Control}\"", StyleBlock(controls, "Button.scene"));
        // The only remaining numeric heights in the style sheet are documented exceptions:
        // the title-bar buttons (0, laid out by their own template), the compact teleprompter
        // controls (24) and the 3 px progress bar.
        var numeric = System.Text.RegularExpressions.Regex.Matches(controls, "Property=\"MinHeight\" Value=\"([0-9]+)\"")
            .Select(match => match.Groups[1].Value).OrderBy(value => value).ToArray();
        Assert.Equal(new[] { "0", "24", "3" }, numeric);
        // Primary action buttons in the pages use the 36 pt row height; the only remaining page
        // clickable literal is the compact teleprompter handle button.
        foreach (var view in new[] { "LiveView.axaml", "NotesView.axaml", "LibraryView.axaml" })
        {
            var text = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", view));
            Assert.Contains("MinHeight=\"{DynamicResource HitRow}\"", text);
        }
        var series = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "SeriesView.axaml"));
        Assert.Contains("MinHeight=\"{DynamicResource Control}\"", series);
        // Two clickable sizes stay literal on purpose: the compact teleprompter handle button
        // (Mac's floating teleprompter controls are small too) and the live question composer,
        // which is a taller multi-line field (58) rather than a single-line control.
        var allowed = new Dictionary<string, string[]>
        {
            ["TeleprompterHandleWindow.axaml"] = ["24"],
            ["LiveView.axaml"] = ["58"]
        };
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views"), "*.axaml", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var text = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,
                         "<(Button|CheckBox|RadioButton|ListBoxItem|TextBox)[^>]*MinHeight=\"([0-9]+)\""))
            {
                var value = match.Groups[2].Value;
                if (allowed.TryGetValue(name, out var values) && values.Contains(value)) continue;
                offenders.Add(name + " MinHeight=" + value);
            }
        }
        Assert.Empty(offenders);
    }

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
    public void PrimaryAndChipContentTextUsesOnInk()
    {
        var controls = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("Button.primary /template/ ContentPresenter TextBlock", controls);
        Assert.Contains("RadioButton.chip:checked /template/ ContentPresenter TextBlock", controls);
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Views", "ControlSampleWindow.axaml")));
    }

    [Fact]
    public void ControlFocusRingAndDestructiveStyleMatchMac()
    {
        var controls = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("Button:focus-visible /template/ Border#Root", controls);
        Assert.Contains("Value=\"{DynamicResource Ai}\"", controls);
        Assert.Contains("Value=\"2\"", controls);
        Assert.Contains("Button.destructive", controls);
        Assert.Contains("Button.destructive:pointerover", controls);
    }

    [Fact]
    public void ProgramDoesNotForceSoftwareRendering()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop", "Program.cs"));
        Assert.DoesNotContain("NEXTTHOUGHT_GPU_RENDER", program);
        Assert.DoesNotContain("Win32RenderingMode.Software", program);
    }
}
