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
        SetStatus($"已删除选中的 {rows.Count} 场会议。");
    }

    /// <summary>E18: batch delete every meeting currently matching the history filters.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task DeleteFilteredSessionsAsync()
    {
        var rows = FilteredHistory.ToList();
        if (rows.Count == 0 || Recording || Working) return;
        foreach (var row in rows) await DeleteSessionAsync(row);
        SetStatus($"已删除 {rows.Count} 场会议。");
    }

    /// <summary>E18: delete one archived meeting, its series membership and visible lists.</summary>
    public async Task DeleteSessionAsync(RecentMeetingRow row)
    {
        if (Recording || Working) { SetStatus("先停止当前任务，再删除会议。", true); return; }
        try
        {
            await Sessions.DeleteAsync(row.Meta.Id);
            workspace = workspace.Remove(row.Meta.Id.ToString());
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
