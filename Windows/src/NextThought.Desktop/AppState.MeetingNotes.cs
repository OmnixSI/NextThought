using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string meetingNoteDraft = "";
    public System.Collections.ObjectModel.ObservableCollection<SessionHighlight> MeetingHighlights { get; } = [];

    private void LoadMeetingNotes(SessionUserContent content)
    {
        MeetingNoteDraft = content.PersonalNotes;
        MeetingHighlights.Clear();
        foreach (var highlight in content.Highlights) MeetingHighlights.Add(highlight);
    }

    [RelayCommand]
    private void SaveMeetingNotes()
    {
        if (CurrentSession is not { } meta) { SetStatus("先开始或打开一场会议。", true); return; }
        try
        {
            var folder = Sessions.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            SessionUserContentStore.Save(content with
            {
                PersonalNotes = MeetingNoteDraft,
                Highlights = MeetingHighlights.ToList()
            }, folder);
            SetStatus("会中笔记已保存。");
        }
        catch (Exception ex) { SetStatus("保存会中笔记失败：" + ex.Message, true); }
    }

    [RelayCommand]
    private void HighlightLine(TranscriptRow? row)
    {
        if (row is null || CurrentSession is not { } meta) return;
        if (MeetingHighlights.Any(item => item.LineId == row.Id)) return;
        var offset = Math.Max(0, (int)(row.Date - meta.StartedAt).TotalSeconds);
        MeetingHighlights.Add(new SessionHighlight { LineId = row.Id, OffsetSeconds = offset, Quote = row.Text });
        try
        {
            var folder = Sessions.Folder(meta.Id);
            var content = SessionUserContentStore.Load(folder);
            SessionUserContentStore.Save(content with
            {
                PersonalNotes = MeetingNoteDraft,
                Highlights = MeetingHighlights.ToList()
            }, folder);
            SetStatus("已高亮并保存。");
        }
        catch (Exception ex) { SetStatus("保存高亮失败：" + ex.Message, true); }
    }
}
