using Xunit;

namespace NextThought.Tests;

/// <summary>
/// Decisions §3.3 speakers 2/3/5: the speaker tag appears only where the speaker changes, the live
/// page keeps the same strip as the Mac, and every mark is a design-system icon glyph instead of a
/// geometry character.
/// </summary>
public sealed class SpeakerMarkAndLiveStripTests
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
    public void TheSpeakerMarkUsesTheBundledIconGlyphs()
    {
        var icons = Read("Styles", "Icons.axaml");
        assert(icons, "IconCircle");
        assert(icons, "IconSquare");
        assert(icons, "IconTriangle");
        assert(icons, "IconDiamond");
        assert(icons, "IconPersonFill");
        var row = Read("SpeakerRow.cs");
        Assert.Contains("MarkGlyph", row, StringComparison.Ordinal);
        Assert.Contains("\"\\uF2BC\"", row, StringComparison.Ordinal);
        Assert.Contains("\"\\uF292\"", row, StringComparison.Ordinal);
        Assert.Contains("\"\\uF03A5\"", row, StringComparison.Ordinal);
        Assert.Contains("\"\\uE4BC\"", row, StringComparison.Ordinal);
        var notes = Read("Views", "NotesView.axaml");
        Assert.Contains("{Binding MarkGlyph}", notes, StringComparison.Ordinal);
        var editor = Read("Views", "SpeakerEditorWindow.axaml");
        Assert.Contains("{Binding MarkGlyph}", editor, StringComparison.Ordinal);

        static void assert(string xaml, string key) => Assert.Contains("x:Key=\"" + key + "\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLivePageKeepsTheMacSpeakerStrip()
    {
        var live = Read("Views", "LiveView.axaml");
        Assert.Contains("LiveSpeakerStrip", live, StringComparison.Ordinal);
        Assert.Contains("LiveSpeakerStripVisible", live, StringComparison.Ordinal);
        Assert.Contains("SpeakerChip_Click", live, StringComparison.Ordinal);
        Assert.Contains("LiveWhoAmIButton", live, StringComparison.Ordinal);
        Assert.Contains("{Binding MarkGlyph}", live, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{StaticResource IconFont}\"", live, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSpeakerTagOnlyShowsAtASpeakerChange()
    {
        var live = Read("Views", "LiveView.axaml");
        Assert.Contains("IsVisible=\"{Binding ShowSpeakerLabel}\"", live, StringComparison.Ordinal);
        var notes = Read("Views", "NotesView.axaml");
        Assert.Contains("IsVisible=\"{Binding ShowSpeakerLabel}\"", notes, StringComparison.Ordinal);
        var row = Read("TranscriptRow.cs");
        Assert.Contains("RefreshSpeakerLabels", row, StringComparison.Ordinal);
        Assert.Contains("rows[index].ShowSpeakerLabel = index == 0", row, StringComparison.Ordinal);
        // A failed speaker save keeps the retry row (item 4) from the same window.
        var editor = Read("Views", "SpeakerEditorWindow.axaml");
        Assert.Contains("RetrySpeakerSaveCommand", editor, StringComparison.Ordinal);
        Assert.Contains("HasSpeakerSaveError", editor, StringComparison.Ordinal);
    }
}
