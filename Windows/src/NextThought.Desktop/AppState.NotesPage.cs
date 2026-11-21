using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// task-62/E15: the Mac 3.4 NotesPageView header — the breadcrumb, the meeting title with its facts
/// line and the 复制纪要 primary action (the notes themselves stay in NotesView/AppState.Notes).
/// </summary>
public partial class AppState
{
    /// <summary>Mac: 所有会议 › meeting › 复盘.</summary>
    public string NotesBreadcrumb
    {
        get
        {
            var meeting = CurrentSession?.Title ?? "";
            var parts = new List<string> { UiText.T("所有会议") };
            if (meeting.Length > 0) parts.Add(meeting);
            parts.Add(UiText.T("复盘"));
            return string.Join(" › ", parts);
        }
    }

    public string NotesMeetingTitle => CurrentSession?.Title ?? "";

    /// <summary>Mac 事实行: date · duration · venue (the exact wording is compared in notes/compare.md).</summary>
    public string NotesFactsLine
    {
        get
        {
            if (CurrentSession is not { } meta) return PageSubtitle;
            var minutes = (int)Math.Max(0, ((meta.EndedAt ?? meta.StartedAt) - meta.StartedAt).TotalMinutes);
            var venue = meta.InPerson ? UiText.T("线下") : UiText.T("线上");
            return meta.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + " · " + minutes + " min · " + venue;
        }
    }

    /// <summary>Mac 复制纪要: copy the notes markdown to the clipboard.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task CopyNotesAsync()
    {
        if (CopyText is not { } copy) return;
        var markdown = SummaryText.Length > 0 ? SummaryText : StructuredMarkdown;
        if (markdown.Length == 0) return;
        try { await copy(markdown); }
        catch (Exception error) { SetStatus(error.Message, true); }
    }

    public void RefreshNotesHeader()
    {
        foreach (var name in new[] { nameof(NotesBreadcrumb), nameof(NotesMeetingTitle), nameof(NotesFactsLine) })
            OnPropertyChanged(name);
    }
}
