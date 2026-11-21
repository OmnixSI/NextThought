using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §6.2/§6.5: the live top bar carries the Mac LiveWindowMenu (LiveWindowController.swift): a small
/// captions-bubble icon button that opens the three 会中窗口 modes and prints the cycle shortcut.
/// The old copy inside the 更多 menu is gone.
/// </summary>
public sealed class LiveWindowMenuParityTests
{
    private static string Windows()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Windows(), "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void LiveTopBarHasTheCaptionsBubbleMenu()
    {
        var view = Read("Views", "LiveView.axaml");
        Assert.Contains("LiveWindowMenuButton", view);
        // LiveWindowMenu label: Image(systemName: "captions.bubble") → IconCaptionsBubble.
        Assert.Contains("IconCaptionsBubble", view);
        Assert.Contains("会中窗口", view);
        // The three LiveWindowMode cases with their Mac titles, wired to the set(mode) command.
        Assert.Contains("纪要窗口", view);
        Assert.Contains("字幕条", view);
        Assert.Contains("隐藏", view);
        Assert.Contains("SetLiveWindowModeCommand", view);
        Assert.Contains("CommandParameter=\"notes\"", view);
        Assert.Contains("CommandParameter=\"subtitles\"", view);
        Assert.Contains("CommandParameter=\"hidden\"", view);
        // Divider + 「按 … 在三者之间切换」 hint (state.shortcutLabel(.toggleLiveWindow)).
        Assert.Contains("ToggleLiveWindowHint", view);
        Assert.Contains("Height=\"0.5\"", view);
    }

    [Fact]
    public void TheMoreMenuNoLongerCarriesTheWindowModeCopy()
    {
        var view = Read("Views", "LiveView.axaml");
        // LiveWindowMenu is its own top-bar button in the Mac layout, not a section of moreMenu.
        Assert.DoesNotContain("<TextBlock Text=\"{loc:Tr 会中窗口}\" Classes=\"caption\" FontSize=\"11\" />", view);
        Assert.Contains("CycleLiveMode", File.ReadAllText(Path.Combine(Windows(), "src", "NextThought.Desktop", "AppState.StructuredNotes.cs"))); // the shortcut path still cycles
    }

    [Fact]
    public void StateExposesTheThreeModeFlagsAndTheShortcutHint()
    {
        var state = Read("AppState.LiveWindowMenu.cs");
        foreach (var member in new[] { "LiveModeIsNotes", "LiveModeIsSubtitles", "LiveModeIsHidden",
                     "SetLiveWindowMode", "LiveWindowShortcut", "ToggleLiveWindowHint" })
            Assert.Contains(member, state);
        Assert.Contains("按 %@ 在三者之间切换", state);
        Assert.Contains("Ctrl+Alt+Shift+S", state);
    }
}
