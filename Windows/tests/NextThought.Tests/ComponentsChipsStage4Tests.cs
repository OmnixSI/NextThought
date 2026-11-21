using Xunit;

namespace NextThought.Tests;

/// <summary>
/// components-6: the meeting page's format selector and the corrected-transcript version selector use
/// the design-system PillTabs (SelectedIndex bridge) instead of hand-styled chip RadioButtons.
/// </summary>
public sealed class ComponentsChipsStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "NotesView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheNotesFormatSelectorUsesPillTabs()
    {
        var view = Read("Views", "NotesView.axaml");
        Assert.Contains("<c:PillTabs", view);
        Assert.Contains("SelectedIndex=\"{Binding NotesFormatIndex}\"", view);
        Assert.DoesNotContain("GroupName=\"notesformat\"", view);
        var state = Read("AppState.DesignSystemIndex.cs");
        Assert.Contains("OnNotesFormatIndexChanged", state);
        Assert.Contains("NotesFormatOrder", state);
        Assert.Contains("OnSelectedNotesFormatChanged", state);
    }

    [Fact]
    public void TheCorrectedTranscriptVersionSelectorUsesPillTabs()
    {
        var view = Read("Views", "CorrectedTranscriptView.axaml");
        Assert.Contains("<c:PillTabs", view);
        Assert.Contains("SelectedIndex=\"{Binding CorrectedTranscriptVersionIndex}\"", view);
        Assert.DoesNotContain("GroupName=\"correctedVersion\"", view);
        var state = Read("AppState.DesignSystemIndex.cs");
        Assert.Contains("OnCorrectedTranscriptVersionIndexChanged", state);
        Assert.Contains("CorrectedTranscriptShowOriginal", state);
    }
}
