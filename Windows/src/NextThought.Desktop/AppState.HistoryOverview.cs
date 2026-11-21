using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// §6.7 E1: the archive-overview half of HistoryView.swift. History is scoped either to the whole
/// archive or to one series (Mac <c>historyWorkspaceFilter</c>/<c>overviewFolderKey</c>); the empty
/// states, the scope title and the breadcrumb all follow that scope.
/// </summary>
public partial class AppState
{
    /// <summary>Mac <c>historyWorkspaceFilter</c>: the series id the list is scoped to, null = root.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string? historySeriesFilter;

    public bool IsHistoryScoped => HistorySeriesFilter is { Length: > 0 };

    private WorkspaceSeries? ScopedSeries => HistorySeriesFilter is { } id
        ? workspace.Find(id)
        : null;

    // HistoryView.sessionListTitle: the series name, or 所有记录 at the root.
    public string HistoryScopeTitle => ScopedSeries?.Name ?? UiText.T("所有记录");

    // HistoryView.sessionList subtitle: root says 自动保存…; a series says 这个系列中的会议记录.
    public string HistoryScopeSubtitle => IsHistoryScoped
        ? UiText.T("这个系列中的会议记录")
        : UiText.T("自动保存 · 不保存录音");

    public bool HistoryScopeHasNoSessions => IsHistoryScoped && FilteredHistory.Count == 0 && !IsHistoryViewEmpty;

    // HistoryView.archiveOverview: isRoot ? 所有记录 : series name.
    public string HistoryOverviewTitle => ScopedSeries?.Name ?? UiText.T("所有记录");

    // "本机记录 · 当前匹配 N 场会议、M 个系列 · 系列按最近匹配会议排序" (root) / the series line.
    public string HistoryOverviewSubtitle => IsHistoryScoped
        ? UiText.Format("%@ · 系列共 %lld 场会议 · 当前条件匹配 %lld 场",
            ScopedSeries is { } scoped ? UiText.T(SessionModes.Parse(scoped.Mode).Title()) : "",
            (long)(ScopedSeries?.SessionIds?.Count ?? 0), (long)FilteredHistory.Count)
        : UiText.Format("本机记录 · 当前匹配 %lld 场会议、%lld 个系列 · 系列按最近匹配会议排序",
            (long)FilteredHistory.Count,
            (long)HistoryGroups.Count(group => group.SeriesId is not null));

    // MeetingArchiveOverview: "N 场会议" + (isRoot ? "M 个系列" : "N 篇已整理").
    public string HistoryOverviewStats => IsHistoryScoped
        ? UiText.Format("%lld 篇已整理", (long)FilteredHistory.Count(row => row.HasSummary))
        : UiText.Meetings(FilteredHistory.Count) + " · " +
          UiText.Format("%lld 个系列", (long)HistoryGroups.Count(group => group.SeriesId is not null));

    /// <summary>MeetingArchiveOverview "onOpenFolder": scope the list to one series.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenHistorySeries(string? seriesId)
    {
        HistorySeriesFilter = string.IsNullOrWhiteSpace(seriesId) ? null : seriesId;
        ApplyHistoryFilter();
    }

    /// <summary>MeetingArchiveOverview "onOpenRoot": drop the selected meeting.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void BackToHistoryOverview()
    {
        CurrentSession = null;
        Transcript.Clear();
        Sources.Clear();
    }

    /// <summary>MeetingArchiveOverview "onOpenRoot".</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ShowAllHistory() => OpenHistorySeries(null);

    /// <summary>HistoryView.clearFilters: clear search/filter/sort but keep the series scope (Mac).</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ClearHistoryFilters()
    {
        HistoryQuery = "";
        HistoryFilter = UiText.T("全部");
        HistoryDateFilter = HistoryDateChoices.Count > 0 ? HistoryDateChoices[0] : "全部时间";
        HistoryLengthFilter = HistoryLengthChoices.Count > 0 ? HistoryLengthChoices[0] : "全部时长";
        HistorySort = HistorySortChoices.Count > 0 ? HistorySortChoices[0] : "最新优先";
        ApplyHistoryFilter();
    }

    /// <summary>True when a search/filter narrows the list (Mac hasNarrowingFilter).</summary>
    public bool HistoryHasNarrowingFilter =>
        HistoryQuery.Trim().Length > 0
        || HistoryDateFilter != (HistoryDateChoices.Count > 0 ? HistoryDateChoices[0] : "")
        || HistoryLengthFilter != (HistoryLengthChoices.Count > 0 ? HistoryLengthChoices[0] : "")
        || !MatchesFilter(HistoryFilter, "全部");

    public bool IsContentSearchingHistory => HistorySearching;

    /// <summary>HistoryView content search progress: "已检查 N / M 场…".</summary>
    public string HistoryContentSearchProgress => HistorySearching
        ? UiText.Format("已检查 %lld / %lld 场；完成后才会确定没有匹配项。", (long)HistoryChecked, (long)HistoryTotal)
        : "";

    /// <summary>HistoryView archive-read warning: sessions whose content could not be read.</summary>
    // The Windows search API does not expose per-session read failures yet; the Mac warning
    // stays hidden rather than inventing a count (see compare.md unresolved).
    public string HistoryReadIssueText => "";

    public bool HasHistoryReadIssues => false;

    partial void OnHistorySeriesFilterChanged(string? value) => NotifyHistoryScopeState();

    /// <summary>Called from ApplyHistoryFilter after the list is rebuilt.</summary>
    partial void OnHistoryCheckedChanged(int value) => NotifyHistoryScopeState();
    partial void OnHistoryTotalChanged(int value) => NotifyHistoryScopeState();
    partial void OnHistoryUnreadableChanged(int value) => NotifyHistoryScopeState();

    private void NotifyHistoryScopeState()
    {
        OnPropertyChanged(nameof(IsHistoryScoped));
        OnPropertyChanged(nameof(HistoryScopeTitle));
        OnPropertyChanged(nameof(HistoryScopeSubtitle));
        OnPropertyChanged(nameof(HistoryOverviewTitle));
        OnPropertyChanged(nameof(HistoryOverviewSubtitle));
        OnPropertyChanged(nameof(HistoryOverviewStats));
        OnPropertyChanged(nameof(HistoryScopeHasNoSessions));
        OnPropertyChanged(nameof(HistoryHasNarrowingFilter));
        OnPropertyChanged(nameof(HistoryIsEmptyAndNotSearching));
        OnPropertyChanged(nameof(HistoryShowsNoMatch));
        OnPropertyChanged(nameof(HistoryNoMatchDetail));
        OnPropertyChanged(nameof(IsContentSearchingHistory));
        OnPropertyChanged(nameof(HistoryContentSearchProgress));
        OnPropertyChanged(nameof(HistoryReadIssueText));
        OnPropertyChanged(nameof(HasHistoryReadIssues));
    }

    /// <summary>Root and series empty states (HistoryView.sessionList).</summary>
    public bool HistoryIsEmptyAndNotSearching => History.Count == 0 && !HistorySearching;

    /// <summary>HistoryView.sessionList no-match detail (series-scoped vs root).</summary>
    public string HistoryNoMatchDetail => IsHistoryScoped
        ? UiText.Format("这个系列共有 %lld 场会议。清除条件可恢复结果。", (long)History.Count)
        : UiText.T("当前搜索或筛选条件下没有记录，可以清除条件后查看全部。");

    /// <summary>「没有符合条件的记录」 when filters/search hide everything but rows exist.</summary>
    public bool HistoryShowsNoMatch => History.Count > 0 && FilteredHistory.Count == 0 && !HistorySearching;
}
