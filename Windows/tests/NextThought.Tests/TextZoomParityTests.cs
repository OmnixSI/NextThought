using NextThought.Core;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// J7: the text zoom (Mac TextZoom, DesignSystem.swift) is wired into the Windows shell — ⌘+ / ⌘−
/// / ⌘0 become Ctrl + / − / 0, every page font is scaled through <see cref="TextZoom.Scaled"/>, the
/// cue island and the answer window keep their own size buttons, the factor is persisted under the
/// Mac key <c>textZoom</c>, and a display change is only acted on once the layout has been stable
/// for 1.5 s (PresenterMode.swift).
/// </summary>
public sealed class TextZoomParityTests
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

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheServiceScalesThroughTextZoomAndPersistsTheMacKey()
    {
        var service = Read("TextZoomService.cs");
        Assert.Contains("TextZoom.Scaled", service);
        Assert.Contains("TextZoom.PreferenceKey", service);
        Assert.Contains("textZoom", service);
        Assert.Contains("Grow", service);
        Assert.Contains("Shrink", service);
        Assert.Contains("Reset", service);
    }

    [Fact]
    public void TheCueIslandAndTheAnswerWindowKeepTheirOwnSize()
    {
        var service = Read("TextZoomService.cs");
        Assert.Contains("CueIslandWindow", service);
        Assert.Contains("TeleprompterWindow", service);
        Assert.Contains("Unscaled", service);
    }

    [Fact]
    public void NewContentIsScaledOnAttachWithoutTheOldPollingTimer()
    {
        var service = Read("TextZoomService.cs");
        // 决议 §3.1 zoom 2: new content must render at the zoom immediately. The old 600 ms
        // dispatcher timer that patched late rows after they had flashed at 100 % is gone; the
        // scaling is driven by the window list plus each window's layout pass.
        Assert.DoesNotContain("DispatcherTimer", service);
        Assert.DoesNotContain("FromMilliseconds(600)", service);
        Assert.DoesNotContain("Watcher", service);
        Assert.Contains("EnsureVisualHooks", service);
        Assert.Contains("INotifyCollectionChanged", service);
        Assert.Contains("LayoutUpdated", service);
    }

    [Fact]
    public void TheMainWindowBindsControlPlusMinusAndZero()
    {
        var code = Read("Views", "MainWindow.axaml.cs");
        Assert.Contains("TextZoomService", code);
        Assert.Contains("OemPlus", code);
        Assert.Contains("OemMinus", code);
        Assert.Contains("D0", code);
        Assert.Contains("KeyModifiers.Control", code);
    }

    [Fact]
    public void TheDisplaysAreOnlyActedOnAfterTheySettle()
    {
        var shell = Read("Views", "MainWindow.Shell.cs");
        Assert.Contains("1_500", shell);
        Assert.Contains("presenterSettle", shell);
        Assert.Contains("presenterPageWanted", shell);
        // The user bringing the page back during a meeting keeps it visible for the rest of it.
        Assert.Contains("RestoreMainWindow", shell);
        Assert.Contains("presenterPageWanted = true", shell);
    }

    [Fact]
    public void TheScreenshotBatchCoversTheThreeZoomSteps()
    {
        var runner = Read("SelfTest", "RunScreenshotsAsync.cs");
        Assert.Contains("zoom", runner);
        Assert.Contains("0.9", runner);
        Assert.Contains("1.45", runner);
    }

    [Fact]
    public void TheZoomSelfTestHasAssertionsForEveryStep()
    {
        var check = Read("SelfTest", "Checks", "RunTextZoomCheckAsync.cs");
        Assert.Contains("TextZoom.Steps", check);
        Assert.Contains("Scaled", check);
        Assert.Contains("island", check);
        Assert.Contains("zoom-check", check);
    }

    [Fact]
    public void TheCoreStepsAreTheMacStepsAndTheKeyIsTheMacKey()
    {
        Assert.Equal(new[] { 0.9, 1, 1.1, 1.2, 1.3, 1.45 }, TextZoom.Steps);
        Assert.Equal("textZoom", TextZoom.PreferenceKey);
        Assert.Equal(18, new TextZoom(1.2).Scaled(15));
        Assert.Equal(14, new TextZoom(0.9).Scaled(15));
        Assert.Equal(15, new TextZoom().Scaled(15));
    }
}
