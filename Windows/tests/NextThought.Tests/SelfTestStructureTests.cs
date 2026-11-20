using Xunit;

namespace NextThought.Tests;

/// <summary>H1: main window keeps only window code; self-checks live under Desktop/SelfTest.</summary>
public sealed class SelfTestStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }



    [Fact]
    public void LegacyPerCheckFlagsAreGone()
    {
        var root = Root();
        var startup = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "StartupOptions.cs"));
        Assert.DoesNotContain("SelfTestOptions", startup);
        Assert.False(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTestOptions.cs")));
    }











    [Fact]
    public void RecordingHidesNavigationForFocusMode()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        var marker = "IsVisible=\"{Binding !Recording}\"";
        Assert.True(main.Split(marker).Length - 1 >= 3);
    }


    [Fact]
    public void ContextSidebarIsCollapsibleAndPersisted()
    {
        var root = Root();
        var main = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ContextSidebarVisible", main);
        Assert.Contains("ToggleContextSidebarCommand", main);
        var nav = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.Navigation.cs"));
        Assert.Contains("ContextSidebarHidden", nav);
        var settings = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Core", "Models.cs"));
        Assert.Contains("ContextSidebarHidden", settings);
        var model = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "ViewModels", "SettingsViewModel.cs"));
        Assert.Contains("ContextSidebarHidden", model);
    }

    [Fact]
    public void SettingsIsAnOverlay()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("IsVisible=\"{Binding OnSettings}\"", main);
        Assert.Contains("<views:SettingsView", main);
        Assert.Contains("CommandParameter=\"Home\"", main);
    }

    [Fact]
    public void SidebarIsHiddenAndSettingsIsInTopBar()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("ColumnDefinitions=\"Auto,*\"", main);
        Assert.Contains("CommandParameter=\"Settings\"", main);
        Assert.Contains("IsVisible=\"False\"", main);
    }


    [Fact]
    public void TopNavigationHasSnappyBrushTransition()
    {
        var controls = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("BrushTransition Property=\"Background\" Duration=\"0:0:0.28\"", controls);
    }


    [Fact]
    public void CueIslandHasFiveStateEvidenceHook()
    {
        var code = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml.cs"));
        Assert.Contains("ApplyStateForCheck", code);
        Assert.Contains("\"collapsed\"", code);
        Assert.Contains("\"launcher\"", code);
        var runner = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs"));
        Assert.Contains("island-\" + islandState", runner);
    }

    [Fact]
    public void TopNavigationCapsuleExists()
    {
        var main = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml"));
        Assert.Contains("GroupName=\"topnav\"", main);
        Assert.Contains("CommandParameter=\"Home\"", main);
        Assert.Contains("CommandParameter=\"Live\"", main);
        Assert.Contains("CommandParameter=\"Library\"", main);
        var controls = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Controls.axaml"));
        Assert.Contains("RadioButton.nav:checked", controls);
    }

    [Fact]
    public void ViewsDoNotUseTextCharactersForIcons()
    {
        var views = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views");
        foreach (var file in Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var symbol in new[] { "✕", "✓", "⚠" })
                Assert.DoesNotContain(symbol, text);
        }
        var tp = File.ReadAllText(Path.Combine(views, "TeleprompterWindow.axaml"));
        Assert.Contains("&#xE711;", tp);
    }

    [Fact]
    public void FontsAndLicensesAreBundled()
    {
        var dir = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Assets", "Fonts");
        foreach (var file in new[] { "InterVariable.ttf", "NotoSansSC-VF.ttf", "SourceSerif4-VF.ttf", "NotoSerifSC-VF.ttf",
                     "LICENSE-Inter.txt", "OFL-NotoSansSC.txt", "OFL-SourceSerif4.txt", "OFL-NotoSerifSC.txt" })
            Assert.True(File.Exists(Path.Combine(dir, file)), file);
        var theme = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Styles", "Theme.axaml"));
        Assert.Contains("InterVariable.ttf#Inter Variable", theme);
        Assert.Contains("SourceSerif4-VF.ttf#Source Serif 4", theme);
    }

    [Fact]
    public void IconMapCoversEverySwiftSymbol()
    {
        var root = Root();
        var sw = Directory.GetFiles(Path.Combine(root, "Sources", "NextThought"), "*.swift", SearchOption.AllDirectories)
            .SelectMany(path => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "systemName: \"([^\"]+)\"")
                .Select(match => match.Groups[1].Value))
            .Distinct().ToList();
        var icons = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Styles", "Icons.axaml"));
        var map = File.ReadAllText(Path.Combine(root, "Windows", "ICON_MAP.md"));
        Assert.True(sw.Count >= 50);
        foreach (var symbol in sw)
            Assert.Contains(symbol, map);
        Assert.Equal(sw.Count, System.Text.RegularExpressions.Regex.Matches(icons, "<x:String x:Key=").Count);
    }

    [Fact]
    public void SceneGlyphTagAndInkWashAreWiredIntoHome()
    {
        var root = Root();
        foreach (var name in new[] { "SceneGlyph.cs", "SceneTag.cs", "InkWash.cs" })
            Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "Controls", name)));
        var home = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml"));
        Assert.Contains("<controls:SceneGlyph", home);
        Assert.Contains("<controls:InkWash", home);
    }

    [Fact]
    public void MotionTokensAndReduceMotionArePresent()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Motion.cs"));
        foreach (var token in new[] { "Hover", "Expand", "Phase", "Snappy", "Morph", "SystemParametersInfo", "FadeIn", "FadeOut" })
            Assert.Contains(token, text);
        var shell = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.Shell.cs"));
        Assert.Contains("Motion.FadeIn()", shell);
        Assert.Contains("Motion.FadeOut()", shell);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(160)", shell);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(120)", shell);
    }

    [Fact]
    public void AppStateMainFileIsBelow600Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "AppState.cs");
        Assert.True(File.ReadAllLines(path).Length < 600);
        Assert.True(Directory.GetFiles(Path.Combine(Root(), "Windows", "src", "NextThought.Desktop"), "AppState.*.cs").Length >= 15);
    }

    [Fact]
    public void ProductionStartupOptionsOnlyExposeThreeSwitches()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "StartupOptions.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("Demo", text);
        Assert.Contains("Screenshots", text);
        Assert.Contains("SelfTest", text);
        Assert.DoesNotContain("--knowledge-check", text);
        Assert.True(File.ReadAllLines(path).Length < 40);
    }

    [Fact]
    public void MainWindowFileIsBelow400Lines()
    {
        var path = Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs");
        Assert.True(File.ReadAllLines(path).Length < 400);
    }

    [Fact]
    public void SelfChecksLiveOutsideTheWindowFile()
    {
        var root = Root();
        Assert.True(File.Exists(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "GlobalUsings.cs")));
        var window = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs"));
        Assert.DoesNotContain("private async Task RunAcceptanceCheckAsync", window);
        Assert.True(Directory.GetFiles(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "SelfTest", "Checks"), "*.cs").Length > 40);
    }
}
