using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E1/§6.4 polish: the five residual visual differences from validation/screens/home/compare.md plus
/// the continue-series ordering fallback. Written first, then the home page implements them:
/// InkWash honours reducedEffects, the scene pill slides between chips (matched geometry) and scales
/// on press, the glyph and promise play their Mac transitions, the composer stroke takes the scene
/// tint at 45% while the title is focused, and series order falls back to Workspace.updatedAt.
/// </summary>
public sealed class HomePolishTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheHomeInkWashFollowsReducedEffects()
    {
        var home = Read("Views", "HomeView.axaml");
        Assert.Contains("Animated=\"{Binding HomeInkWashAnimated}\"", home);
        Assert.DoesNotContain("Animated=\"True\"", home);
        var state = Read("AppState.Home.cs");
        Assert.Contains("HomeInkWashAnimated", state);
        Assert.Contains("Motion.Reduce", state);
    }

    [Fact]
    public void TheScenePillSlidesBetweenChipsLikeMatchedGeometry()
    {
        var composer = Read("Views", "Home", "StartComposer.axaml");
        // One capsule behind the chips; the chips themselves stay text-only.
        Assert.Contains("ChipHost", composer);
        Assert.Contains("SelectionLayer", composer);
        Assert.Contains("SelectionCapsule", composer);
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("SlideSelection", code);
        Assert.Contains("Motion.Kind.Snappy", code);
        // Mac SceneChipStyle: pressed scale 0.96.
        Assert.Contains("ScaleTransform", composer);
        Assert.Contains("0.96", composer);
    }

    [Fact]
    public void TheSceneGlyphScalesInWhenTheSceneChanges()
    {
        var composer = Read("Views", "Home", "StartComposer.axaml");
        Assert.Contains("SceneGlyphControl", composer);
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("AnimateGlyph", code);
        Assert.Contains("0.8", code);
    }

    [Fact]
    public void ThePromiseAndMomentsBlurReplaceOnSceneChange()
    {
        var composer = Read("Views", "Home", "StartComposer.axaml");
        Assert.Contains("PromisePanel", composer);
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("BlurReplacePromise", code);
        Assert.Contains("BlurEffect", code);
        Assert.Contains("Motion.Kind.Expand", code);
    }

    [Fact]
    public void TheComposerStrokeTakesTheSceneTintWhileTheTitleIsFocused()
    {
        var composer = Read("Views", "Home", "StartComposer.axaml");
        Assert.Contains("ComposerCard", composer);
        Assert.Contains("BrushTransition", composer);
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("UpdateTitleFocus", code);
        Assert.Contains("GotFocus", code);
        Assert.Contains("0.45", code);
    }

    [Fact]
    public void ContinueSeriesFallsBackToUpdatedAtLikeHomeViewSwift()
    {
        var state = Read("AppState.Home.cs");
        Assert.Contains("EffectiveUpdatedAt", state);
        Assert.DoesNotContain("item.Series.CreatedAt)", state);
        // Still: standing rows excluded, at most three.
        Assert.Contains("Standing != true", state);
        Assert.Contains("Take(3)", state);
    }
}
