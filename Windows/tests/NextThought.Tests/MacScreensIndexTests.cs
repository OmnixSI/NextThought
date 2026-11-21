using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-48: the Mac 3.4.0 reference screenshots are indexed, every reference image is reachable
/// from the Windows comparison documents, and the small fixes that came out of the per-image check
/// (scene-chip order, title-bar columns, onboarding capture) stay in place.
/// </summary>
public sealed class MacScreensIndexTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Windows(params string[] parts) =>
        Path.Combine([Root(), "Windows", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Windows(parts));

    [Fact]
    public void EveryMacReferenceImageIsIndexedInTheReadme()
    {
        var macDirectory = Windows("validation", "mac-screens");
        var images = Directory.GetFiles(macDirectory, "*.png", SearchOption.AllDirectories);
        Assert.Equal(93, images.Length);
        var readme = File.ReadAllText(Path.Combine(macDirectory, "README.md"));
        Assert.Contains("<!-- mac-win-index:start -->", readme);
        Assert.Contains("<!-- mac-win-index:end -->", readme);
        Assert.Contains("## Windows 对照索引", readme);
        foreach (var image in images)
        {
            var name = Path.GetFileName(image);
            Assert.Contains(name, readme);
        }
        // The Mac's own notes at the top of the file must survive regeneration.
        Assert.Contains("逐元素对照用的 Mac 标准图", readme);
    }

    [Fact]
    public void EveryCoveredViewHasAComparisonDocument()
    {
        foreach (var view in new[]
                 {
                     "home", "prepare", "live", "notes", "history", "series", "settings-structure",
                     "settings-pages", "onboarding", "island", "library"
                 })
        {
            var path = Windows("validation", "screens", view, "compare.md");
            Assert.True(File.Exists(path), view + "/compare.md is missing");
            var text = File.ReadAllText(path);
            Assert.Contains("Mac 3.4.0", text);
        }
    }

    [Fact]
    public void TheComparisonDocumentsLinkTheRealMacImages()
    {
        foreach (var view in new[] { "home", "prepare", "live", "notes", "history", "series", "onboarding", "settings-structure" })
        {
            var text = Read("validation", "screens", view, "compare.md");
            Assert.Contains("../../mac-screens/", text);
        }
        Assert.Contains("../../mac-screens/settings-pages/scenes-light.png", Read("validation", "screens", "settings-pages", "compare.md"));
        Assert.Contains("../../mac-screens/island/cue-light.png", Read("validation", "screens", "island", "compare.md"));
        Assert.Contains("../../mac-screens/series/series-light.png", Read("validation", "screens", "library", "compare.md"));
    }

    [Fact]
    public void TheIndexIsGeneratedByAScript()
    {
        var script = Windows("scripts", "build_mac_screens_index.py");
        Assert.True(File.Exists(script));
        var text = File.ReadAllText(script);
        Assert.Contains("mac-win-index:start", text);
        Assert.Contains("MAPPING", text);
        Assert.Contains("validation/mac-screens", text.Replace("\\", "/"));
    }

    [Fact]
    public void TheSceneChipsFollowTheMacBuiltInOrder()
    {
        var source = Read("src", "NextThought.Desktop", "AppState.Audio.cs");
        var start = source.IndexOf("SceneChoices =", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = source[start..source.IndexOf("];", start, StringComparison.Ordinal)];
        var interview = block.IndexOf("MeetingMode.Interview", StringComparison.Ordinal);
        var lab = block.IndexOf("MeetingMode.LabMeeting", StringComparison.Ordinal);
        var academic = block.IndexOf("MeetingMode.Academic", StringComparison.Ordinal);
        Assert.True(interview >= 0 && lab > interview && academic > lab,
            "Mac SessionMode.builtIn order is interview, labMeeting, academic, ...");
    }

    [Fact]
    public void TheTitleBarKeepsTheTabsCenterAndTheSubtitleRight()
    {
        var xaml = Read("src", "NextThought.Desktop", "Views", "MainWindow.axaml");
        Assert.Contains("ColumnDefinitions=\"Auto,*,Auto\"", xaml);
        Assert.Contains("Grid.Column=\"1\" Orientation=\"Horizontal\" Spacing=\"{DynamicResource D4Spacing_2}\" HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("Grid.Column=\"2\" Orientation=\"Horizontal\" Spacing=\"{DynamicResource D4Spacing_8}\" HorizontalAlignment=\"Right\"", xaml);
    }

    [Fact]
    public void TheScreenshotBatchCapturesAllFourOnboardingStepsInBothThemes()
    {
        var source = Read("src", "NextThought.Desktop", "SelfTest", "RunScreenshotsAsync.cs");
        Assert.Contains("for (var step = 0; step < 4; step++)", source);
        Assert.Contains("onboarding-step", source);
        Assert.Contains("onboarding-first.png", source);
        Assert.Contains("onboarding-last.png", source);
    }
}
