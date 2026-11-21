using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>One highlight under 重点, with the time label the notes list shows.</summary>
public sealed record MeetingHighlightRow(SessionHighlight Highlight)
{
    public string TimeText => NotesTime.Text(Highlight.OffsetSeconds);
    public string Quote => Highlight.Quote;
}

/// <summary>Consecutive highlights merged into one paragraph (E8 分段合并).</summary>
public sealed record MeetingHighlightGroup(string RangeText, int StartSeconds, IReadOnlyList<MeetingHighlightRow> Rows);

/// <summary>
/// E8: the meeting's personal notes and highlights (MeetingUserNotes.swift). One draft owns the
/// text for the page; every save writes PersonalNotes + Highlights into user-content.json, so the
/// generated minutes are never overwritten. Highlights toggle on/off (Mac toggleHighlight) and the
/// notes list merges nearby highlights into paragraphs.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private string meetingNoteDraft = "";
    [ObservableProperty] private bool meetingNotesSaving;
    [ObservableProperty] private bool meetingNotesDirty;

    public ObservableCollection<SessionHighlight> MeetingHighlights { get; } = [];

    /// <summary>Flat highlight rows in the order they were taken.</summary>
    public ObservableCollection<MeetingHighlightRow> MeetingHighlightRows { get; } = [];

    /// <summary>Highlights merged into paragraphs by their gap (newest paragraph first).</summary>
    public ObservableCollection<MeetingHighlightGroup> MeetingHighlightGroups { get; } = [];

    public string MeetingHighlightCountText => UiText.Format("重点 · %lld", MeetingHighlights.Count);

    // ---- save status (Mac: 正在保存… / 尚未保存 / 已保存在本机) ----------------------

    public string MeetingNotesStatusText => MeetingNotesSaving
        ? UiText.T("正在保存…")
        : MeetingNotesDirty ? UiText.T("尚未保存") : UiText.T("已保存在本机");

    public bool MeetingNotesStatusIsDirty => MeetingNotesDirty && !MeetingNotesSaving;

    public bool MeetingNotesStatusSaved => !MeetingNotesDirty && !MeetingNotesSaving;

    private bool loadingMeetingNotes;

    partial void OnMeetingNoteDraftChanged(string value)
    {
        if (loadingMeetingNotes) return;
        MeetingNotesDirty = true;
        RaiseMeetingNotesStatus();
    }

    partial void OnMeetingNotesSavingChanged(bool value) => RaiseMeetingNotesStatus();

    partial void OnMeetingNotesDirtyChanged(bool value) => RaiseMeetingNotesStatus();

    private void RaiseMeetingNotesStatus()
    {
        OnPropertyChanged(nameof(MeetingNotesStatusText));
        OnPropertyChanged(nameof(MeetingNotesStatusIsDirty));
        OnPropertyChanged(nameof(MeetingNotesStatusSaved));
    }

    private void LoadMeetingNotes(SessionUserContent content)
    {
        loadingMeetingNotes = true;
        try
        {
            MeetingNoteDraft = content.PersonalNotes;
            MeetingHighlights.Clear();
            foreach (var highlight in content.Highlights) MeetingHighlights.Add(highlight);
        }
        finally
        {
            loadingMeetingNotes = false;
        }
        MeetingNotesDirty = false;
        RefreshMeetingHighlights();
    }

    /// <summary>Rebuilds the flat rows and the merged paragraphs from the highlight list.</summary>
    public void RefreshMeetingHighlights()
    {
        MeetingHighlightRows.Clear();
        foreach (var highlight in MeetingHighlights)
            MeetingHighlightRows.Add(new MeetingHighlightRow(highlight));
        MeetingHighlightGroups.Clear();
        var segments = MeetingHighlightSegments.Join(MeetingHighlights);
        foreach (var segment in segments.Reverse())
        {
            MeetingHighlightGroups.Add(new MeetingHighlightGroup(segment.RangeText, segment.StartSeconds,
                segment.Highlights.Select(highlight => new MeetingHighlightRow(highlight)).ToList()));
        }
        OnPropertyChanged(nameof(HasMeetingHighlights));
        OnPropertyChanged(nameof(MeetingHighlightCountText));
        OnPropertyChanged(nameof(MeetingHighlightRows));
        OnPropertyChanged(nameof(MeetingHighlightGroups));
    }

    [RelayCommand]
    private void SaveMeetingNotes()
    {
        if (CurrentSession is not { } meta) { SetStatus("先开始或打开一场会议。", true); return; }
        MeetingNotesSaving = true;
        try
        {
            PersistMeetingNotes(meta);
            MeetingNotesDirty = false;
            SetStatus("会中笔记已保存。");
        }
        catch (Exception ex) { SetStatus("保存会中笔记失败：" + ex.Message, true); }
        finally { MeetingNotesSaving = false; }
    }

    private void PersistMeetingNotes(SessionMeta meta)
    {
        var folder = Sessions.Folder(meta.Id);
        var content = SessionUserContentStore.Load(folder);
        SessionUserContentStore.Save(content with
        {
            PersonalNotes = MeetingNoteDraft,
            Highlights = MeetingHighlights.ToList()
        }, folder);
    }

    /// <summary>Mac toggleHighlight: the same command adds the highlight or removes it again.</summary>
    [RelayCommand]
    private void HighlightLine(TranscriptRow? row)
    {
        if (row is null || CurrentSession is not { } meta) return;
        var existing = MeetingHighlights.FirstOrDefault(item => item.LineId == row.Id);
        if (existing is not null)
        {
            MeetingHighlights.Remove(existing);
            row.IsHighlighted = false;
            RefreshMeetingHighlights();
            try { PersistMeetingNotes(meta); SetStatus("已取消高亮。"); }
            catch (Exception ex) { SetStatus("保存高亮失败：" + ex.Message, true); }
            return;
        }
        var offset = Math.Max(0, (int)(row.Date - meta.StartedAt).TotalSeconds);
        MeetingHighlights.Add(new SessionHighlight { LineId = row.Id, OffsetSeconds = offset, Quote = row.Text });
        row.IsHighlighted = true;
        RefreshMeetingHighlights();
        try
        {
            PersistMeetingNotes(meta);
            SetStatus("已高亮并保存。");
        }
        catch (Exception ex) { SetStatus("保存高亮失败：" + ex.Message, true); }
    }

    /// <summary>The 重点 row: jump back to the transcript line the highlight was taken from.</summary>
    [RelayCommand]
    private void OpenMeetingHighlight(MeetingHighlightRow? row)
    {
        if (row is not null) JumpToMeetingHighlight(row.Highlight);
    }
}
