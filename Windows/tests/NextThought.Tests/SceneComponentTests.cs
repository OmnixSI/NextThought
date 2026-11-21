using Xunit;

namespace NextThought.Tests;

/// <summary>
/// D6: the scene theme components follow Stage.swift — the ten pigments and three moments are
/// locked by Core's SceneThemeTests; here the InkWash animation parameters and the wiring of
/// SceneGlyph/SceneTag into the pages are checked.
/// </summary>
public sealed class SceneComponentTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "Controls", "InkWash.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop() => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop");

    private static string View(string name) => File.ReadAllText(Path.Combine(Desktop(), "Views", name));

    [Fact]
    public void InkWashMatchesTheMacParameters()
    {
        var wash = File.ReadAllText(Path.Combine(Desktop(), "Views", "Controls", "InkWash.cs"));
        // Stage.swift InkWash: relative blooms, 250/230 tall, opacity 0.2/0.09, blur 80.
        Assert.Contains("width * 0.44", wash);
        Assert.Contains("first.Height = 250", wash);
        Assert.Contains("width * 0.32", wash);
        Assert.Contains("second.Height = 230", wash);
        Assert.Contains("new SolidColorBrush(tint, 0.20)", wash);
        Assert.Contains("new SolidColorBrush(tint, 0.09)", wash);
        Assert.Contains("new BlurEffect { Radius = 80 }", wash);
        Assert.Contains("width * 0.58 + offset", wash);
        Assert.Contains("width * 0.08 - offset * 0.6", wash);
        // Breathing: 20 fps, sin(t * 0.18) * 18 px, and the graded fade mask.
        Assert.Contains("FramesPerSecond = 20", wash);
        Assert.Contains("DriftRate = 0.18", wash);
        Assert.Contains("DriftAmplitude = 18", wash);
        Assert.Contains("Math.Sin(", wash);
        Assert.Contains("OpacityMask", wash);
        Assert.Contains("new GradientStop(Colors.Black, 0.16)", wash);
        Assert.Contains("new GradientStop(Colors.Black, 0.55)", wash);
        // G5/D6: reduced motion stops the wash; the page decides whether it animates at all.
        Assert.Contains("Motion.Reduce", wash);
        Assert.Contains("AnimatedProperty", wash);
        Assert.Contains("ActualThemeVariant == ThemeVariant.Dark", wash);
        Assert.Contains("Tint(dark)", wash);
    }

    [Fact]
    public void HomeUsesAnimatedWashAndTheThirtyEightPointGlyph()
    {
        var home = View("HomeView.axaml");
        Assert.Contains("<controls:InkWash Mode=\"{Binding SelectedMode}\"", home);
        Assert.Contains("Animated=\"True\"", home);
        // E1: the glyph moved into the home composer subview.
        var composer = File.ReadAllText(Path.Combine(Desktop(), "Views", "Home", "StartComposer.axaml"));
        Assert.Contains("<controls:SceneGlyph Mode=\"{Binding SelectedMode}\" Size=\"38\"", composer);
        // The scene card shows the promise and the three moments from the theme.
        Assert.Contains("SelectedScene.Promise", composer);
        Assert.Contains("SelectedScene.Moments", composer);
    }

    [Fact]
    public void PrepareUsesTheStillWashAndTheFortyFourPointGlyph()
    {
        var prepare = View("PrepareView.axaml");
        // Mac PrepareView: InkWash(animated: false), SceneGlyph(size: 44).
        Assert.Contains("<controls:InkWash Mode=\"{Binding SelectedMode}\"", prepare);
        Assert.Contains("Animated=\"False\"", prepare);
        Assert.Contains("<controls:SceneGlyph Mode=\"{Binding SelectedMode}\" Size=\"44\"", prepare);
    }

    [Fact]
    public void LiveBarShowsTheSceneTag()
    {
        var live = View("LiveView.axaml");
        Assert.Contains("xmlns:controls=\"using:NextThought.Desktop.Views.Controls\"", live);
        Assert.Contains("x:Name=\"LiveSceneTag\"", live);
        Assert.Contains("Mode=\"{Binding SelectedMode}\"", live);
        Assert.Contains("Name=\"{Binding SceneTitle}\"", live);
    }

    [Fact]
    public void GlyphAndTagFollowTheAppearanceAndTheStringTable()
    {
        var glyph = File.ReadAllText(Path.Combine(Desktop(), "Views", "Controls", "SceneGlyph.cs"));
        Assert.Contains("theme.Tint(ActualThemeVariant == ThemeVariant.Dark)", glyph);
        Assert.Contains("ActualThemeVariantChanged", glyph);
        Assert.Contains("Size * 0.3", glyph);
        Assert.Contains("Size * 0.44", glyph);
        Assert.Contains("0.13", glyph);
        var tag = File.ReadAllText(Path.Combine(Desktop(), "Views", "Controls", "SceneTag.cs"));
        Assert.Contains("UiText.T(", tag);
        Assert.Contains("theme.Tint(dark)", tag);
        Assert.Contains("Width = 6, Height = 6", tag);
        Assert.Contains("ActualThemeVariantChanged", tag);
    }

    [Fact]
    public void SceneComponentsAreWiredIntoTheShell()
    {
        // Home/prepare render the glyph and the wash; the live bar renders the tag; nothing else
        // re-implements the pigment or the promise locally.
        var views = Directory.GetFiles(Path.Combine(Desktop(), "Views"), "*.axaml", SearchOption.AllDirectories);
        var texts = views.ToDictionary(path => path, File.ReadAllText);
        Assert.Contains(texts.Values, text => text.Contains("<controls:InkWash"));
        Assert.Contains(texts.Values, text => text.Contains("<controls:SceneGlyph"));
        Assert.Contains(texts.Values, text => text.Contains("<controls:SceneTag"));
        foreach (var (path, text) in texts)
        {
            if (path.EndsWith("HomeView.axaml", StringComparison.Ordinal) || path.EndsWith("PrepareView.axaml", StringComparison.Ordinal)) continue;
            Assert.DoesNotContain("<controls:InkWash", text);
        }
    }
}
