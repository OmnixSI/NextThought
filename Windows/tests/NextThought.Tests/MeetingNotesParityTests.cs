using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E8: the meeting personal notes (MeetingUserNotes.swift) — one draft owner, save state,
/// highlight toggle, merged highlight paragraphs, citations that jump back, and user-content.
/// </summary>
public sealed class MeetingNotesParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MeetingUserNotes.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void TheMacViewHasASameNamedWindowsControl()
    {
        var mac = File.ReadAllText(Path.Combine(Root(), "Sources", "NextThought", "MeetingUserNotes.swift"));
        Assert.Contains("struct MeetingUserNotes", mac);
        Assert.Contains("func toggleHighlight", mac);
        Assert.True(File.Exists(Desktop("Views", "MeetingUserNotes.axaml")));
        var code = Read("Views", "MeetingUserNotes.axaml.cs");
        Assert.Contains("public partial class MeetingUserNotes", code);
        Assert.Contains("OpenMeetingHighlightCommand", code);
    }

    [Fact]
    public void TheNotesTabHostsTheControl()
    {
        var notes = Read("Views", "NotesView.axaml");
        Assert.Contains("<TabItem Header=\"{loc:Tr 个人笔记}\">", notes);
        Assert.Contains("<views:MeetingUserNotes", notes);
        // The old inline notes editor is replaced by the single draft owner.
        Assert.DoesNotContain("PersonalNotesText", notes);
    }

    [Fact]
    public void TheViewShowsDraftStateAndDescription()
    {
        var view = Read("Views", "MeetingUserNotes.axaml");
        Assert.Contains("{loc:Tr 我的笔记}", view);
        Assert.Contains("{Binding MeetingNotesStatusText}", view);
        Assert.Contains("{Binding MeetingNotesSaving}", view);
        Assert.Contains("{Binding MeetingNoteDraft}", view);
        Assert.Contains("{loc:Tr 记下自己的想法、别人的反馈和待查的问题。AI 重新整理不会覆盖这里。}", view);
        Assert.Contains("{loc:Tr 保存个人笔记}", view);
        Assert.Contains("SaveMeetingNotesCommand", view);
        Assert.Contains("MeetingNotesStatusIsDirty", view);
    }

    [Fact]
    public void HighlightsShowTimesAndJumpBackAsCitations()
    {
        var view = Read("Views", "MeetingUserNotes.axaml");
        Assert.Contains("{Binding MeetingHighlightCountText}", view);
        Assert.Contains("{Binding MeetingHighlightGroups}", view);
        Assert.Contains("{Binding RangeText}", view);
        Assert.Contains("{Binding Quote}", view);
        Assert.Contains("ToolTip.Tip=\"{loc:Tr 跳到原话}\"", view);
        var code = Read("Views", "MeetingUserNotes.axaml.cs");
        Assert.Contains("MeetingHighlightRow", code);
        var state = Read("AppState.MeetingNotes.cs");
        var notesState = Read("AppState.Notes.cs");
        Assert.Contains("JumpToMeetingHighlight", notesState);
        Assert.Contains("NotesTime.Text", state);
    }

    [Fact]
    public void HighlightsMergeIntoParagraphs()
    {
        var state = Read("AppState.MeetingNotes.cs");
        Assert.Contains("MeetingHighlightSegments.Join", state);
        Assert.Contains("public void RefreshMeetingHighlights()", state);
        Assert.Contains("MeetingHighlightGroups", state);
        var core = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "MeetingHighlightSegments.cs"));
        Assert.Contains("public static IReadOnlyList<Segment> Join", core);
        Assert.Contains("gapSeconds", core);
    }

    [Fact]
    public void HighlightToggleAndUserContentWritesAreWired()
    {
        var state = Read("AppState.MeetingNotes.cs");
        // Mac toggleHighlight: the same command removes an existing highlight again.
        Assert.Contains("MeetingHighlights.Remove(existing)", state);
        Assert.Contains("row.IsHighlighted = false", state);
        Assert.Contains("row.IsHighlighted = true", state);
        Assert.Contains("SessionUserContentStore.Save", state);
        Assert.Contains("PersonalNotes = MeetingNoteDraft", state);
        Assert.Contains("Highlights = MeetingHighlights.ToList()", state);
    }

    [Fact]
    public void EvidenceExists()
    {
        Assert.True(File.Exists(Path.Combine(Root(), "Windows", "validation", "screens", "meeting-notes", "compare.md")));
    }
}
