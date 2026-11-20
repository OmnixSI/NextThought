using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E26b: the quick settings panel has the Mac 提词岛/浮窗 picker and the 双屏演讲讲稿 toggle, and
/// both keys reach the floating cue window (cueSurface switches island ↔ window; presenterSlideNotes
/// gates the island's slide notes while presenting).
/// </summary>
public sealed class CuePresentationPanelTests
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

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    [Fact]
    public void TheQuickPanelHasTheCueSurfacePickerAndTheNotesToggle()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("Settings.CueSurfaceChoices", xaml);
        Assert.Contains("Settings.SelectedCueSurface", xaml);
        Assert.Contains("new(\"island\", \"提词岛\")", Read(Path.Combine("Windows", "src", "NextThought.Desktop", "ViewModels", "SettingsViewModel.cs")));
        Assert.Contains("{loc:Tr 讲的时候显示这一页的讲稿}", xaml);
        Assert.Contains("Settings.PresenterSlideNotes", xaml);
        Assert.Contains("CueSurface_SelectionChanged", xaml);
        Assert.Contains("PresenterSlideNotes_Click", xaml);
    }

    [Fact]
    public void TheCueSurfaceKeySwitchesBetweenIslandAndWindow()
    {
        var shell = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "MainWindow.Shell.cs"));
        Assert.Contains("CueSurface == \"window\"", shell);
        Assert.Contains("ShowCueWindow()", shell);
        Assert.Contains("state.PresenterLayoutActive = true;", shell);
        Assert.Contains("state.PresenterLayoutActive = false;", shell);
    }

    [Fact]
    public void ThePresenterNotesKeyGatesTheIslandSlideNotes()
    {
        var prepare = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Prepare.cs"));
        Assert.Contains("PresenterLayoutActive || Settings.PresenterSlideNotes", prepare);
        var cue = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Cue.cs"));
        Assert.Contains("NotifyPresenterSlideNotesChanged", cue);
    }

    [Fact]
    public void TheScreenshotBatchCoversBothQuickPanelThemes()
    {
        var runner = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs"));
        Assert.Contains("\"quick-settings\" + quickSuffix", runner);
        Assert.DoesNotContain("quick-settings-light.png", runner);
    }
}
