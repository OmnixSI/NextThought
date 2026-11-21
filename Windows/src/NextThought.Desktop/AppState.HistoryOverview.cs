using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §6.7 E1: the archive-overview half of HistoryView.swift. Mac has no screenshot for this
/// window, so the labels follow the Swift source (HistoryView.archiveOverview): the root subtitle
/// counts the current matches and series, the stats line repeats the session count.
/// </summary>
public partial class AppState
{
    // "本机记录 · 当前匹配 N 场会议、M 个系列 · 系列按最近匹配会议排序" (HistoryView.archiveOverview)
    public string HistoryOverviewSubtitle => UiText.Format(
        "本机记录 · 当前匹配 %lld 场会议、%lld 个系列 · 系列按最近匹配会议排序",
        (long)FilteredHistory.Count,
        (long)HistoryGroups.Count(group => group.SeriesId is not null));

    // MeetingArchiveOverview: "N 场会议" + (isRoot ? "M 个系列" : "N 篇已整理").
    public string HistoryOverviewStats => HasSelectedHistory
        ? SelectedSessionFacts
        : UiText.Meetings(FilteredHistory.Count) + " · " +
          UiText.Format("%lld 个系列", (long)HistoryGroups.Count(group => group.SeriesId is not null));

    /// <summary>MeetingArchiveOverview "onOpenRoot": drop the selection and show the root overview.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void BackToHistoryOverview()
    {
        CurrentSession = null;
        Transcript.Clear();
        Sources.Clear();
    }
}