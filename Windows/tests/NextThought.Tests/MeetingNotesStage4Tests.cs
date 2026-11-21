using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-78/§3.4 meeting-notes: the personal notes page auto-saves after a short debounce and on
/// leaving, offers the Mac retry row after a read failure, and lists highlights flat (the Windows
/// 30-second paragraph merging is gone).
/// </summary>
public sealed class MeetingNotesStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "AppState.MeetingNotes.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheDraftAutoSavesAndFlushesWhenLeaving()
    {
        var state = Read("AppState.MeetingNotes.cs");
        Assert.Contains("MeetingNotesAutoSaveMilliseconds", state);
        Assert.Contains("DispatcherTimer", state);
        Assert.Contains("ScheduleMeetingNotesAutoSave", state);
        Assert.Contains("public void FlushMeetingNotes()", state);
        var view = Read("Views", "MeetingUserNotes.axaml.cs");
        Assert.Contains("DetachedFromVisualTree", view);
        Assert.Contains("FlushMeetingNotes", view);
    }

    [Fact]
    public void TheReadFailureHasARetryRow()
    {
        var state = Read("AppState.MeetingNotes.cs");
        Assert.Contains("MeetingNotesLoadError", state);
        Assert.Contains("RetryMeetingNotes", state);
        var view = Read("Views", "MeetingUserNotes.axaml");
        Assert.Contains("HasMeetingNotesLoadError", view);
        Assert.Contains("{Binding RetryMeetingNotesCommand}", view);
    }

    [Fact]
    public void TheHighlightsAreListedFlat()
    {
        var view = Read("Views", "MeetingUserNotes.axaml");
        Assert.Contains("{Binding MeetingHighlightRows}", view);
        Assert.DoesNotContain("MeetingHighlightGroups", view);
        Assert.DoesNotContain("RangeText", view);
        Assert.DoesNotContain("SaveMeetingNotesCommand", view);
    }
}
