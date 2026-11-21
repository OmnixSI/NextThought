namespace NextThought.Desktop;

public partial class AppState
{

    /// <summary>E18: rename one archived meeting and update the visible rows.</summary>
    public async Task RenameSessionAsync(RecentMeetingRow row, string title)
    {
        var trimmed = title.Trim();
        if (trimmed.Length == 0 || trimmed == row.Title) return;
        try
        {
            await Sessions.RenameAsync(row.Meta.Id, trimmed);
            row.Meta.Title = trimmed;
            if (CurrentSession?.Id == row.Meta.Id) { CurrentSession.Title = trimmed; MeetingTitle = trimmed; }
            ApplyHistoryFilter();
            SetStatus("已重命名会议：" + trimmed);
        }
        catch (Exception ex) { SetStatus("重命名失败：" + ex.Message, true); }
    }


    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private Task DeleteSession(RecentMeetingRow row) => DeleteSessionAsync(row);



    /// <summary>E18: delete the checkbox-selected meetings.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task DeleteSelectedSessionsAsync()
    {
        var rows = FilteredHistory.Where(row => row.IsSelected).ToList();
        if (rows.Count == 0 || Recording || Working) return;
        foreach (var row in rows) await DeleteSessionAsync(row);
        SetStatus(UiText.Format("已删除选中的 %lld 场会议。", rows.Count));
    }

    /// <summary>E18: batch delete every meeting currently matching the history filters.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task DeleteFilteredSessionsAsync()
    {
        var rows = FilteredHistory.ToList();
        if (rows.Count == 0 || Recording || Working) return;
        foreach (var row in rows) await DeleteSessionAsync(row);
        SetStatus(UiText.Format("已删除 %lld 场会议。", rows.Count));
    }

    /// <summary>
    /// E18 (HistoryView.deleteAllArchivedMeetings): delete every archived meeting except the one
    /// being recorded, optionally retaining the accepted series content they supported.
    /// </summary>
    public async Task DeleteAllMeetingsAsync(bool retainAcceptedContent)
    {
        if (Recording || Working) { SetStatus("先停止当前任务，再删除会议。", true); return; }
        var rows = History.Where(row => CurrentSession?.Id != row.Meta.Id).ToList();
        if (rows.Count == 0) return;
        foreach (var row in rows) await DeleteSessionAsync(row, retainAcceptedContent);
        SetStatus(retainAcceptedContent
            ? UiText.Format("已删除 %lld 场会议；已确认的系列内容已保留。", rows.Count)
            : UiText.Format("已删除 %lld 场会议及其依赖内容。", rows.Count));
    }

    /// <summary>E18: delete one archived meeting, its series membership and visible lists.</summary>
    public async Task DeleteSessionAsync(RecentMeetingRow row, bool retainAcceptedContent = false)
    {
        if (Recording || Working) { SetStatus("先停止当前任务，再删除会议。", true); return; }
        try
        {
            await Sessions.DeleteAsync(row.Meta.Id);
            // B3: remove the meeting from the series and scrub knowledge only it supported;
            // HistoryView offers the Mac "keep accepted series content" variant as well.
            var sessionId = row.Meta.Id.ToString("D");
            workspace = new WorkspaceData(workspace.Series.Select(series =>
                series.SessionIds.Contains(sessionId)
                    ? WorkspaceSourceIntegrity.RemoveMeetings(series, [sessionId], retainAcceptedContent).Series
                    : series).ToList());
            SaveWorkspace();
            History.Remove(row);
            HomeRecent.Remove(row);
            if (CurrentSession?.Id == row.Meta.Id) { CurrentSession = null; Transcript.Clear(); Sources.Clear(); }
            RefreshSeries();
            ApplyHistoryFilter();
            SetStatus("已删除会议：" + row.Title);
        }
        catch (Exception ex) { SetStatus("删除失败：" + ex.Message, true); }
    }
}
