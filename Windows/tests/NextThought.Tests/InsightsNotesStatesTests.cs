using Xunit;

namespace NextThought.Tests;

/// <summary>
/// Decisions §3.3 insights 2: the slide section shows the Mac per-page spinner while the talking
/// points are written and the orange error note when writing fails, instead of only the placeholder.
/// </summary>
public sealed class InsightsNotesStatesTests
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
    public void TheSlideSectionHasTheGenerationSpinner()
    {
        var state = Read("AppState.Insights.cs");
        Assert.Contains("slideNotesGenerating", state, StringComparison.Ordinal);
        Assert.Contains("SlideNotesGenerating", state, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => BuildRegeneratedSlideNotes(pageInfo))", state, StringComparison.Ordinal);
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("{Binding SlideNotesGenerating}", panel, StringComparison.Ordinal);
        Assert.Contains("{Binding !SlideNotesGenerating}", panel, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"True\"", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void AGenerationFailureShowsTheOrangeNoteAndHidesThePlaceholder()
    {
        var state = Read("AppState.Insights.cs");
        Assert.Contains("HasSlideNotesError", state, StringComparison.Ordinal);
        Assert.Contains("SlideNotesWaiting", state, StringComparison.Ordinal);
        Assert.Contains("讲稿提示生成失败：%@", state, StringComparison.Ordinal);
        var panel = Read("Views", "InsightsPanel.axaml");
        Assert.Contains("{Binding SlideNotesError}", panel, StringComparison.Ordinal);
        Assert.Contains("{Binding HasSlideNotesError}", panel, StringComparison.Ordinal);
        Assert.Contains("{Binding SlideNotesWaiting}", panel, StringComparison.Ordinal);
        // The Mac Note(error, color: .orange) is the caution colour, not body ink.
        var start = panel.IndexOf("Text=\"{Binding SlideNotesError}\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = panel.IndexOf("/>", start, StringComparison.Ordinal);
        Assert.Contains("Caution", panel[start..end], StringComparison.Ordinal);
    }
}
