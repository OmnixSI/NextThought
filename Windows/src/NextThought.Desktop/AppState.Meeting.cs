using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    public string SceneTitle => SelectedScene?.Title ?? UiText.T("通用");

    partial void OnLiveModeChanged(LiveWindowMode value) => OnPropertyChanged(nameof(LiveModeLabel));

    public ObservableCollection<RecentMeetingRow> HomeRecent { get; } = [];

    private static bool IsNewMeetingTitle(string title) => title is "新会议" or "New meeting";

    /// <summary>G8: the mode button label follows the interface language.</summary>
    public string LiveModeLabel => UiText.T(LiveMode.Title());

    // MARK: E18 会议列表收尾：内容搜索、命中导航、会议操作菜单、删除会议

    private readonly Dictionary<Guid, IReadOnlyList<SessionSearchHit>> historyHitsBySession = [];
    private CancellationTokenSource? historySearchCancellation;
    private int historySearchGeneration;

    /// <summary>The 重新整理 → 纪要语言 override consumed by the template summarizer.</summary>
    private bool? summaryLanguageOverride;

    [ObservableProperty] private bool historySearching;
    [ObservableProperty] private string historySearchStatus = "";
    [ObservableProperty] private int historyHitIndex = -1;
    [ObservableProperty] private bool selectingHistorySeries;
    // §6.7 E1: HistoryView's content-search progress and unreadable-section counter.
    [ObservableProperty] private int historyChecked;
    [ObservableProperty] private int historyTotal;
    [ObservableProperty] private int historyUnreadable;

    public ObservableCollection<HistoryHitRow> HistoryHits { get; } = [];
    public HashSet<string> SelectedHistorySeries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool HasHistoryHits => HistoryHits.Count > 0;
    public bool HistoryHasSearch => historyHitsBySession.Count > 0;
    public int HistoryMatchedSessionCount => historyHitsBySession.Count;
    public string HistorySelectionText => UiText.Format("已选 %lld 个系列", SelectedHistorySeries.Count);
    public bool IsSearchingHistory => HistorySearching;

    public string HistoryHitPosition => HistoryHits.Count == 0 || HistoryHitIndex < 0
        ? ""
        : UiText.Format("搜索命中 %lld/%lld", HistoryHitIndex + 1, HistoryHits.Count);
    public string HistoryHitLine => HistoryHits.Count == 0 || HistoryHitIndex < 0 ? "" : HistoryHits[HistoryHitIndex].Line;
    public string HistoryHitExcerpt => HistoryHits.Count == 0 || HistoryHitIndex < 0 ? "" : HistoryHits[HistoryHitIndex].Excerpt;
    public bool HasMultipleHistoryHits => HistoryHits.Count > 1;
    public bool HasHistorySearchStatus => HistorySearchStatus.Length > 0;

    partial void OnHistoryHitIndexChanged(int value)
    {
        OnPropertyChanged(nameof(HistoryHitPosition));
        OnPropertyChanged(nameof(HistoryHitLine));
        OnPropertyChanged(nameof(HistoryHitExcerpt));
    }

    partial void OnHistorySearchStatusChanged(string value) => OnPropertyChanged(nameof(HasHistorySearchStatus));

    /// <summary>
    /// E18 (HistoryView.task(id: contentSearchKey)): typing in the history search runs the
    /// projection-backed content search after a short debounce; the previous scan is cancelled.
    /// </summary>
    public void ScheduleHistoryContentSearch()
    {
        historySearchCancellation?.Cancel();
        historySearchCancellation?.Dispose();
        historySearchCancellation = null;
        var query = HistoryQuery.Trim();
        if (query.Length < 2) { ClearHistoryContentSearch(); return; }
        var generation = ++historySearchGeneration;
        var cancellation = new CancellationTokenSource();
        historySearchCancellation = cancellation;
        _ = RunHistoryContentSearchAsync(query, generation, cancellation.Token);
    }

    private async Task RunHistoryContentSearchAsync(string query, int generation, CancellationToken ct)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => HistorySearching = true);
            await Task.Delay(350, ct);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HistoryTotal = History.Count;
                HistoryChecked = 0;
                HistoryUnreadable = 0;
            });
            var results = await Task.Run(() => Sessions.SearchContentHits(query, 200, ct), ct);
            if (generation != historySearchGeneration) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyHistorySearchResults(results);
                HistoryChecked = History.Count;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (generation == historySearchGeneration) SetStatus(UiText.T("全文搜索失败：") + ex.Message, true);
        }
        finally
        {
            if (generation == historySearchGeneration)
                await Dispatcher.UIThread.InvokeAsync(() => HistorySearching = false);
        }
    }

    private void ApplyHistorySearchResults(IReadOnlyList<SessionContentSearchResult> results)
    {
        historyHitsBySession.Clear();
        foreach (var result in results) historyHitsBySession[result.Meta.Id] = result.Hits;
        foreach (var row in History)
            row.ApplySearchHit(historyHitsBySession.TryGetValue(row.Meta.Id, out var hits) ? hits.FirstOrDefault() : null);
        HistoryContentHits.Clear();
        foreach (var result in results)
            if (result.First is { } first)
                HistoryContentHits.Add(new SessionContentHit(result.Meta, first.Excerpt, "", result.Meta.StartedAt, 0));
        ApplyHistoryFilter();
        RefreshHistoryHitsForSelection();
        HistorySearchStatus = UiText.Format("全文搜索：%lld 场会议有匹配内容。", results.Count);
        OnPropertyChanged(nameof(HistoryHasSearch));
        OnPropertyChanged(nameof(HistoryMatchedSessionCount));
    }

    private void ClearHistoryContentSearch()
    {
        historySearchGeneration++;
        historyHitsBySession.Clear();
        HistoryContentHits.Clear();
        foreach (var row in History) row.ApplySearchHit(null);
        HistoryHits.Clear();
        HistoryHitIndex = -1;
        HistorySearchStatus = "";
        OnPropertyChanged(nameof(HistoryHasSearch));
        OnPropertyChanged(nameof(HistoryMatchedSessionCount));
        ApplyHistoryFilter();
    }

    /// <summary>The navigator follows the selected meeting's hits (HistoryView.searchHitNavigator).</summary>
    public void RefreshHistoryHitsForSelection()
    {
        HistoryHits.Clear();
        if (CurrentSession is { } meta && historyHitsBySession.TryGetValue(meta.Id, out var hits))
            foreach (var hit in hits) HistoryHits.Add(new HistoryHitRow(hit));
        HistoryHitIndex = HistoryHits.Count == 0 ? -1 : 0;
        OnPropertyChanged(nameof(HasHistoryHits));
        OnPropertyChanged(nameof(HasMultipleHistoryHits));
        OnPropertyChanged(nameof(HistoryHitPosition));
        OnPropertyChanged(nameof(HistoryHitLine));
        OnPropertyChanged(nameof(HistoryHitExcerpt));
    }

    [RelayCommand]
    private void PreviousHistoryHit() => MoveHistoryHit(-1);

    [RelayCommand]
    private void NextHistoryHit() => MoveHistoryHit(1);

    private void MoveHistoryHit(int delta)
    {
        if (HistoryHits.Count == 0) return;
        HistoryHitIndex = (HistoryHitIndex + delta + HistoryHits.Count) % HistoryHits.Count;
        OpenHistoryHitInMeeting(HistoryHits[HistoryHitIndex].Hit);
    }

    // MARK: 会议操作菜单 (MeetingActionsMenu.swift)

    public bool CanManageSessions => !Recording && !Working;

    /// <summary>MeetingMenuItems: an active recording cannot be deleted; the tooltip says why.</summary>
    public bool CanDeleteSessionRow(RecentMeetingRow row) => CanManageSessions && CurrentSession?.Id != row.Meta.Id;
    public string SessionDeleteDisabledReason(RecentMeetingRow row) => Recording
        ? UiText.T("正在记录的会议不能删除。")
        : Working ? UiText.T("先停止当前任务，再删除会议。") : "";

    public string SessionFolderPath(RecentMeetingRow row) => Sessions.Folder(row.Meta.Id);

    /// <summary>MeetingMenuItems: 在 Finder 中显示 → opens the meeting folder in Explorer.</summary>
    public void RevealSessionInExplorer(RecentMeetingRow row)
    {
        try { OpenExternal(Sessions.Folder(row.Meta.Id)); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    /// <summary>SessionInfoView: the meeting's stored facts for the 详细信息 sheet.</summary>
    public IReadOnlyList<(string Label, string Value)> SessionInfoLines(RecentMeetingRow row)
    {
        var meta = row.Meta;
        var duration = meta.EndedAt is { } ended ? ended - meta.StartedAt : DateTimeOffset.Now - meta.StartedAt;
        var series = workspace.SeriesOfSession(meta.Id.ToString());
        return
        [
            (UiText.T("标题"), row.Title),
            (UiText.T("开始时间"), meta.LocalStartedAt),
            (UiText.T("时长"), DurationText(duration)),
            (UiText.T("模式"), meta.Mode),
            (UiText.T("系列"), series?.Name ?? UiText.T("未归入系列")),
            (UiText.T("录音来源"), meta.AudioSource),
            (UiText.T("识别引擎"), meta.Recognizer),
            (UiText.T("回答模型"), meta.AnswerModel),
            (UiText.T("转写"), UiText.Transcripts(meta.TranscriptCount)),
            (UiText.T("回答"), UiText.Answers(meta.AnswerCount)),
            (UiText.T("问题"), UiText.Format("%lld 个", meta.QuestionCount)),
            (UiText.T("纪要"), meta.SummaryState),
            (UiText.T("文件夹"), Sessions.Folder(meta.Id))
        ];
    }

    /// <summary>MeetingMenuItems: 以此为上一场，准备下一场.</summary>
    public bool CanPrepareNextMeeting(RecentMeetingRow row) =>
        !Recording && !Working && workspace.SeriesOfSession(row.Meta.Id.ToString()) is not null;

    public void PrepareNextMeetingAfter(RecentMeetingRow row)
    {
        if (Recording || Working) { SetStatus(UiText.T("先停止当前任务，再准备下一场。"), true); return; }
        if (workspace.SeriesOfSession(row.Meta.Id.ToString()) is not { } series) return;
        var seriesRow = SeriesRows.FirstOrDefault(item => item.Id == series.Id);
        if (seriesRow is not null) SelectedSeries = seriesRow;
        // Mac's MeetingDraft.previousSessionID: the previous transcript is carried into the next one.
        CarryTranscript = true;
        CurrentSession = row.Meta;
        previousMeetingForNext = row.Meta.Id;
        Section = AppSection.Prepare;
        SetStatus(UiText.Format("已把「%@」当作上一场，准备下一场。", row.Title));
    }

    private Guid? previousMeetingForNext;

    /// <summary>E2: the previous transcript chosen by 以此为上一场，准备下一场.</summary>
    public ContextDocument? PreviousMeetingTranscript()
    {
        if (previousMeetingForNext is not { } id) return null;
        try
        {
            var data = Sessions.Load(id);
            var text = string.Join("\n", data.Lines.Select(line => line.Speaker + ": " + line.Text));
            return text.Length == 0 ? null : new ContextDocument
            {
                Id = id.ToString("D"),
                Name = UiText.T("上一场 · ") + data.Meta.Title,
                Pages = [text],
                Enabled = true
            };
        }
        catch { return null; }
    }


    /// <summary>Formatting for the overview facts: mm:ss, or h:mm:ss for long meetings.</summary>
    private static string DurationText(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
    // MARK: 归档概览与回答列表 (MeetingArchiveOverview / SessionAnswersList)

    public ObservableCollection<HistoryAnswerRow> HistoryAnswers { get; } = [];
    public bool HasSelectedSessionAnswers => HistoryAnswers.Count > 0;
    public bool HasSelectedSessionScreenshots => SelectedSessionScreenshots.Count > 0;
    public string CurrentSessionTitle => CurrentSession?.Title ?? "";
    public bool HasSummaryError => CurrentSession?.SummaryError is { Length: > 0 };

    public string SelectedSessionFacts
    {
        get
        {
            if (CurrentSession is not { } meta) return "";
            var duration = meta.EndedAt is { } ended ? ended - meta.StartedAt : DateTimeOffset.Now - meta.StartedAt;
            var series = workspace.SeriesOfSession(meta.Id.ToString());
            var parts = new List<string> { meta.LocalStartedAt, DurationText(duration), meta.Mode };
            if (series is not null) parts.Add(series.Name);
            parts.Add(UiText.Transcripts(meta.TranscriptCount));
            parts.Add(UiText.Answers(meta.AnswerCount));
            parts.Add(UiText.Format("问题 %lld 个", meta.QuestionCount));
            parts.Add(meta.SummaryState switch
            {
                "ready" => UiText.T("纪要已整理好"),
                "pending" => UiText.T("纪要整理中"),
                "failed" => UiText.T("纪要整理失败"),
                _ => UiText.T("还没有纪要。")
            });
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Fills the 归档概览 card for the selected meeting; called after a history selection.</summary>
    public void RefreshHistoryOverview()
    {
        HistoryAnswers.Clear();
        if (CurrentSession is { } meta)
            foreach (var answer in SelectedSessionAnswers) HistoryAnswers.Add(new HistoryAnswerRow(answer, meta.Id));
        OnPropertyChanged(nameof(CurrentSessionTitle));
        OnPropertyChanged(nameof(SelectedSessionFacts));
        OnPropertyChanged(nameof(HasSummaryError));
        OnPropertyChanged(nameof(HasSelectedSessionAnswers));
        OnPropertyChanged(nameof(HasSelectedSessionScreenshots));
    }

    /// <summary>The archived per-answer screenshot path, resolved inside the session folder.</summary>
    public string? AnswerScreenshotFullPath(HistoryAnswerRow row)
    {
        if (!row.HasScreenshot) return null;
        var path = Path.Combine(Sessions.Folder(row.SessionId), row.ScreenshotPath);
        return File.Exists(path) ? path : null;
    }

    // MARK: 重新整理 / 纪要语言 (MeetingMenuItems)

    /// <summary>
    /// MeetingMenuItems: 重新整理 re-summarizes the archived meeting; 纪要语言 passes a forced
    /// language into the template summarizer (NotesRegeneration.request(sessionID:english:)).
    /// </summary>
    public async Task RegenerateSessionNotesAsync(RecentMeetingRow row, bool? english)
    {
        if (Recording || Working || Summarizing) { SetStatus(UiText.T("先停止当前任务，再重新整理纪要。"), true); return; }
        await SelectHistoryAsync(row.Meta);
        if (english is { } language)
        {
            summaryLanguageOverride = language;
            try { await SummarizeWithTemplateAsync(); }
            finally { summaryLanguageOverride = null; }
        }
        else if (SummarizeCommand.CanExecute(null))
        {
            SummarizeCommand.Execute(null);
        }
    }

    // MARK: 删除会议 / 系列 (HistoryView deleteSession / deleteSelectedSeries)

    /// <summary>Does any accepted lesson or series item depend on these meetings (retain option)?</summary>
    public bool HasRetainableContent(IReadOnlyCollection<RecentMeetingRow> rows)
    {
        var ids = rows.Select(row => row.Meta.Id.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return workspace.Series.Any(series => WorkspaceSourceIntegrity.HasRetainableAcceptedContent(series, ids));
    }

    /// <summary>HistoryView.seriesGroupHeader 删除系列…: a series and its meeting records go together.</summary>
    public async Task DeleteSeriesWithMeetingsAsync(string seriesId)
    {
        if (Recording || Working) { SetStatus("先停止当前任务，再删除系列。", true); return; }
        var series = workspace.Find(seriesId);
        if (series is null) return;
        var sessionIds = series.SessionIds
            .Concat(History.Where(row => row.Meta.WorkspaceId == seriesId).Select(row => row.Meta.Id.ToString("D")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var sessionId in sessionIds)
            if (Guid.TryParse(sessionId, out var id) && History.FirstOrDefault(row => row.Meta.Id == id) is { } row)
                await DeleteSessionAsync(row);
        workspace = workspace.RemoveSeries(seriesId);
        SaveWorkspace();
        RefreshSeries();
        RefreshHistory();
        SetStatus(UiText.Format("已删除系列「%@」及其会议记录。", series.Name));
    }

    /// <summary>HistoryView.deleteSelectedSeries: series and their meeting records go together.</summary>
    [RelayCommand]
    private async Task DeleteSelectedSeriesAsync()
    {
        var ids = SelectedHistorySeries.ToList();
        ExitHistorySeriesSelection();
        if (ids.Count == 0 || Recording || Working) return;
        foreach (var seriesId in ids)
        {
            var series = workspace.Find(seriesId);
            if (series is null) continue;
            var sessionIds = series.SessionIds
                .Concat(History.Where(row => row.Meta.WorkspaceId == seriesId).Select(row => row.Meta.Id.ToString("D")))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var sessionId in sessionIds)
                if (Guid.TryParse(sessionId, out var id) && History.FirstOrDefault(row => row.Meta.Id == id) is { } row)
                    await DeleteSessionAsync(row);
            workspace = workspace.RemoveSeries(seriesId);
        }
        SaveWorkspace();
        RefreshSeries();
        RefreshHistory();
        SetStatus(UiText.Format("已删除 %lld 个系列及其会议记录。", ids.Count));
    }

    public void ToggleHistorySeriesSelection(string? seriesId)
    {
        if (seriesId is null) return;
        if (!SelectedHistorySeries.Remove(seriesId)) SelectedHistorySeries.Add(seriesId);
        foreach (var group in HistoryGroups) group.IsSelected = SelectedHistorySeries.Contains(group.SeriesId ?? "");
        OnPropertyChanged(nameof(HistorySelectionText));
        OnPropertyChanged(nameof(CanDeleteSelectedSeries));
    }

    public bool CanDeleteSelectedSeries => SelectedHistorySeries.Count > 0;

    [RelayCommand]
    private void SelectAllHistorySeries()
    {
        foreach (var group in HistoryGroups.Where(group => group.CanManage))
            if (group.SeriesId is { } id) SelectedHistorySeries.Add(id);
        foreach (var group in HistoryGroups) group.IsSelected = SelectedHistorySeries.Contains(group.SeriesId ?? "");
        OnPropertyChanged(nameof(HistorySelectionText));
        OnPropertyChanged(nameof(CanDeleteSelectedSeries));
    }

    [RelayCommand]
    private void BeginHistorySeriesSelection()
    {
        SelectedHistorySeries.Clear();
        foreach (var group in HistoryGroups) group.IsSelected = false;
        SelectingHistorySeries = true;
        OnPropertyChanged(nameof(HistorySelectionText));
        OnPropertyChanged(nameof(CanDeleteSelectedSeries));
    }

    [RelayCommand]
    private void ExitHistorySeriesSelection()
    {
        SelectingHistorySeries = false;
        SelectedHistorySeries.Clear();
        foreach (var group in HistoryGroups) group.IsSelected = false;
        OnPropertyChanged(nameof(HistorySelectionText));
        OnPropertyChanged(nameof(CanDeleteSelectedSeries));
    }
}
