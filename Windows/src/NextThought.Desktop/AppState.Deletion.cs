namespace NextThought.Desktop;

public partial class AppState
{
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
