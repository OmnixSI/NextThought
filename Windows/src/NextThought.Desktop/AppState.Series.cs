using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// 系列、经验与知识库（port of the series + knowledge parts of Workspace.swift).
/// Accepted lessons and the question bank feed answers; unreviewed candidates stay inert.
/// </summary>
public partial class AppState
{
    private WorkspaceStore? workspaceStore;
    private WorkspaceData workspace = WorkspaceData.Empty;

    public ObservableCollection<SeriesRow> SeriesRows { get; } = [];
    public ObservableCollection<RecentMeetingRow> SeriesMeetings { get; } = [];
    public ObservableCollection<SeriesSkillRow> SeriesSkills { get; } = [];

    [ObservableProperty] private SeriesRow? selectedSeries;
    /// <summary>Which editor tab is open (0 lessons, 1 pending); bound so checks can capture both.</summary>
    [ObservableProperty] private int seriesSelectedTab;
    [ObservableProperty] private string newSeriesName = "";
    [ObservableProperty] private string newLessonText = "";

    public bool HasSeries => SeriesRows.Count > 0;
    public bool HasSelectedSeries => SelectedSeries is not null;
    public bool SeriesIsEmpty => SeriesRows.Count == 0;
    public string SeriesSummary => SeriesRows.Count == 0
        ? UiText.T("把同一门课、同一个项目、同一类面试的会议放在一个系列里，经验会攒下来。")
        : $"{UiText.SeriesCount(SeriesRows.Count)} · {UiText.Meetings(SeriesRows.Sum(row => row.Count))}";
    public string SelectedSeriesTitle => SelectedSeries?.Name ?? UiText.T("还没有选择系列");
    public string SelectedSeriesMeta => SelectedSeries is not { } row
        ? UiText.T("在左边选择一个系列，或者先新建一个。")
        : $"{UiText.Meetings(row.Count)} · {UiText.Lessons(row.LessonCount)}" + (SeriesCandidates.Count > 0 ? $" · {UiText.Pending(SeriesCandidates.Count)}" : "");


    /// <summary>J3: the Mac SeriesStatsLine under the selected series' name.</summary>
    public string SelectedSeriesStatsLine => SelectedSeries is { } row
        ? MeetingStatsText.SeriesLine(MeetingStats.Of(History.Select(item => item.Meta), series: SeriesGuid(row.Id)))
        : "";

    public bool HasSelectedSeriesStats => SelectedSeriesStatsLine.Length > 0;

    private static Guid? SeriesGuid(string id) => Guid.TryParse(id, out var parsed) ? parsed : null;

    /// <summary>A7: meta files spell ids in canonical case, series files may use the folder spelling.</summary>
    private static bool SameSessionId(Guid metaId, string seriesId) =>
        Guid.TryParse(seriesId, out var parsed) ? metaId == parsed : metaId.ToString() == seriesId;

    private void RefreshSeriesStats()
    {
        OnPropertyChanged(nameof(SelectedSeriesStatsLine));
        OnPropertyChanged(nameof(HasSelectedSeriesStats));
    }

    [RelayCommand]
    private void OpenNextMeetingInSeries()
    {
        if (SelectedSeries is { } row) PrepareNextMeetingInSeries(row.Id);
    }

    public NextThought.Core.SeriesBriefing? SelectedSeriesBriefing => SelectedSeries is { } row && workspace.Find(row.Id) is { } series
        ? NextThought.Core.SeriesBriefingBuilder.Build(series, History.Select(item => item.Meta).ToList())
        : null;


    public System.Collections.ObjectModel.ObservableCollection<SeriesChangeRow> SeriesChangeRows { get; } = [];

    private void RefreshSeriesChanges()
    {
        SeriesChangeRows.Clear();
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        foreach (var candidate in series.SeriesChangeCandidates
                     .Where(candidate => candidate.Status == WorkspaceSeriesChangeCandidate.StatusValue.Pending)
                     .OrderByDescending(candidate => candidate.CreatedAt))
            SeriesChangeRows.Add(new SeriesChangeRow(candidate, series.SeriesItems, IsCandidateStale(candidate)));
        OnPropertyChanged(nameof(HasSeriesChanges));
        OnPropertyChanged(nameof(SeriesChangesHeader));
    }

    public bool HasSeriesChanges => SeriesChangeRows.Count > 0;
    public string SeriesChangesHeader => UiText.Format("系列变更待确认 · %lld", SeriesChangeRows.Count);
    public string SeriesChangesHint => UiText.T("决定与行动先留在这里复核；负责人和期限可在这里确认，确认后才进入系列记录。");
    public string CollectSeriesChangesLabel => UiText.T("收集已核实的决定与行动");
    public bool CanCollectSeriesChanges => CurrentSession is not null && SelectedSeries is not null;

    public void ReviewSeriesChange(string candidateId, WorkspaceSeriesChangeDecision decision) =>
        ReviewSeriesChange(new SeriesChangeRow(new WorkspaceSeriesChangeCandidate { Id = candidateId },
            []), decision, null);

    /// <summary>
    /// KnowledgeReviewSection's SeriesChangeCandidateCard: owner/due are reviewed here, replace keeps
    /// the target item's old revision in its history.
    /// </summary>
    public void ReviewSeriesChange(SeriesChangeRow row, WorkspaceSeriesChangeDecision decision, SeriesTargetRow? target)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        WorkspaceSeriesChangeReviewDetails? details = row.IsAction
            ? new WorkspaceSeriesChangeReviewDetails(
                string.IsNullOrWhiteSpace(row.Owner) ? null : row.Owner.Trim(),
                string.IsNullOrWhiteSpace(row.Due) ? null : row.Due.Trim())
            : null;
        var (updated, receipt) = WorkspaceSeriesChangeMerger.Apply(series, row.Id, decision, details,
            target?.Item.Id, target?.Item.Revision);
        if (receipt is null)
        {
            SetStatus(UiText.T("这条建议对不上了：原话或已有内容在复核之后改过，刷新后再确认。"), true);
            return;
        }
        workspace = workspace.WithSeries(updated);
        SaveWorkspace();
        RefreshSeries();
        RefreshSeriesChanges();
        RefreshSeriesPanel();
        RefreshSeriesLessons();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == selected.Id);
        SetStatus(decision == WorkspaceSeriesChangeDecision.Reject
            ? UiText.T("这条建议已忽略。")
            : UiText.T("已写入系列记录。"));
    }

    /// <summary>Collects reviewed decisions/actions from the current meeting's evidence into the series.</summary>
    public void CollectSeriesChangeCandidates()
    {
        if (CurrentSession is not { } meta || SelectedSeries is not { } selected
            || workspace.Find(selected.Id) is not { } series) return;
        var evidence = NotesEvidence.Select(row => row.Reference).ToList();
        if (evidence.Count == 0) { SetStatus(UiText.T("这场会议还没有可复核的条目。"), true); return; }
        var (updated, added) = WorkspaceSeriesChangeMerger.CollectCandidates(series, evidence, meta.Id.ToString("D"));
        workspace = workspace.WithSeries(updated);
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == selected.Id);
        SetStatus(added.Count == 0
            ? UiText.T("没有新的已核实决定或行动。")
            : UiText.Format("已把 %lld 条决定/行动加入待复核。", added.Count));
    }

    partial void OnSelectedSeriesChanged(SeriesRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedSeries));
        OnPropertyChanged(nameof(SelectedSeriesTitle));
        OnPropertyChanged(nameof(SelectedSeriesMeta));
        OnPropertyChanged(nameof(SelectedSeriesBriefing));
        RefreshSeriesStats();
        RefreshSeriesMeetings();
        RefreshSeriesLessons();
        RefreshSeriesSkills();
        RefreshSeriesChanges();
        RefreshSeriesPanel();
        AssignCurrentToSeriesCommand.NotifyCanExecuteChanged();
        AddLessonCommand.NotifyCanExecuteChanged();
    }

    private void LoadWorkspace()
    {
        workspaceStore = new WorkspaceStore(Paths.Root);
        workspace = workspaceStore.Load();
        RefreshSeries();
    }

    private void RefreshSeriesSkills()
    {
        SeriesSkills.Clear();
        if (SelectedSeries is null) return;
        var series = workspace.Find(SelectedSeries.Id);
        var attached = series?.SkillIds ?? [];
        foreach (var skill in skills)
        {
            var attachedHere = attached.Any(id => Guid.TryParse(id, out var parsed) && parsed == Guid.Parse(skill.Id));
            // Mac WorkspaceSkillsSection: attached = skill.global || workspace.skillIDs.contains(id).
            SeriesSkills.Add(new SeriesSkillRow(skill, skill.Global || attachedHere, ToggleSeriesSkill));
        }
        OnPropertyChanged(nameof(HasSeriesSkills));
        OnPropertyChanged(nameof(SeriesSkillsHeading));
    }

    /// <summary>Mac 技能 N heading: the number of skills that answer with this series.</summary>
    public string SeriesSkillsHeading => UiText.T("技能") + " " + SeriesSkills.Count(row => row.IsAttached).ToString();

    /// <summary>Mac SkillLibrary.importFiles(global: false) then setSkill(id, attached: true).</summary>
    public void AttachSkillToSelectedSeries(string skillId)
    {
        if (SelectedSeries is null || workspace.Find(SelectedSeries.Id) is not { } series) return;
        var parsedSkill = Guid.TryParse(skillId, out var skillGuid) ? skillGuid : Guid.Empty;
        var ids = (series.SkillIds ?? []).Where(id => !(Guid.TryParse(id, out var existing) && existing == parsedSkill)).ToList();
        if (parsedSkill != Guid.Empty) ids.Add(skillId);
        workspace = workspace.WithSeries(series with { SkillIds = ids });
        SaveWorkspace();
        RefreshSeriesSkills();
    }

    public void RefreshSeriesSkillsForCheck() => RefreshSeriesSkills();

    private void ToggleSeriesSkill(SeriesSkillRow row)
    {
        if (row.IsGlobal) return;
        if (SelectedSeries is null || workspace.Find(SelectedSeries.Id) is not { } series) return;
        var ids = (series.SkillIds ?? []).Where(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Parse(row.SkillId)).ToList();
        if (row.IsAttached) ids.Add(row.SkillId);
        workspace = workspace.WithSeries(series with { SkillIds = ids });
        SaveWorkspace();
        SetStatus(row.IsAttached
    ? UiText.Format("已把「%@」附着到「%@」。", row.Name, series.Name)
    : UiText.Format("已从「%@」移除「%@」。", series.Name, row.Name));
    }

    private void SaveWorkspace()
    {
        try { workspaceStore?.Save(workspace); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    /// <summary>Persists the workspace now (the self-check needs a real file to reload).</summary>
    public void SaveWorkspaceNow() => SaveWorkspace();

    /// <summary>The data directory, for the offline knowledge check.</summary>
    public string WorkspaceRoot => Paths.Root;

    private void RefreshSeries()
    {
        var selectedId = SelectedSeries?.Id;
        SeriesRows.Clear();
        foreach (var series in workspace.Series.OrderByDescending(series => series.CreatedAt))
        {
            var last = series.SessionIds
                .Select(id => History.FirstOrDefault(row => SameSessionId(row.Meta.Id, id))?.Meta)
                .Where(meta => meta is not null)
                .OrderByDescending(meta => meta!.StartedAt)
                .FirstOrDefault();
            SeriesRows.Add(new SeriesRow(series.Id, series.Name, series.SessionIds.Count, series.Lessons.Count,
                last?.Title ?? UiText.T("还没有会议"), series.Lessons.LastOrDefault()?.Text ?? "", series.PendingCandidates.Count, series.Documents.Count)
            {
                Mode = series.EffectiveMode,
                QuestionCount = series.QuestionBank.Count
            });
        }
        OnPropertyChanged(nameof(HasSeries));
        OnPropertyChanged(nameof(SeriesIsEmpty));
        OnPropertyChanged(nameof(SeriesSummary));
        AssignCurrentToSeriesCommand.NotifyCanExecuteChanged();
        AddLessonCommand.NotifyCanExecuteChanged();
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == selectedId) ?? SeriesRows.FirstOrDefault();
        RefreshSeriesShown();
    }

    private void RefreshSeriesMeetings()
    {
        SeriesMeetings.Clear();
        SeriesRecords.Clear();
        if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series)
        {
            foreach (var meeting in series.SessionIds
                         .Select(id => History.FirstOrDefault(item => SameSessionId(item.Meta.Id, id)))
                         .Where(meeting => meeting is not null)
                         .OrderByDescending(meeting => meeting!.Meta.StartedAt))
            {
                SeriesMeetings.Add(meeting!);
                SeriesRecords.Add(meeting!);
            }
        }
        OnPropertyChanged(nameof(HasSeriesRecords));
        OnPropertyChanged(nameof(SeriesRecordsEmptyText));
    }

    /// <summary>
    /// Mac accepts a proposal only while the reviewed evidence still matches the transcript. We do
    /// not keep Mac's evidenceReview chain, so this compares the stored transcriptSourceRevision
    /// with the session's current one (and treats a missing source as stale).
    /// </summary>
    private bool IsCandidateStale(WorkspaceSeriesChangeCandidate candidate)
    {
        if (candidate.SourceMissing || string.IsNullOrEmpty(candidate.TranscriptSourceRevision)) return true;
        if (!Guid.TryParse(candidate.SourceSessionId, out var sessionId)) return true;
        try
        {
            var data = Sessions.Load(sessionId);
            var content = SessionUserContentStore.Load(Sessions.Folder(sessionId));
            var corrections = content.Corrections([], data.Lines);
            var current = SessionProjectionBuilder.TranscriptSourceRevision(data.Meta, data.Lines, corrections);
            return !string.Equals(current, candidate.TranscriptSourceRevision, StringComparison.Ordinal);
        }
        catch { return true; }
    }

    /// <summary>WorkspaceRecordsTab: this series' meetings, newest first.</summary>
    public System.Collections.ObjectModel.ObservableCollection<RecentMeetingRow> SeriesRecords { get; } = [];
    public bool HasSeriesRecords => SeriesRecords.Count > 0;
    public string SeriesRecordsEmptyText => UiText.T("还没有记录。从这个系列开始一场会议，记录和资料会保存在一起。");

    /// <summary>Opens one record in the meeting page (Mac WorkspaceRecordsTab row action).</summary>
    public async Task OpenSeriesRecordAsync(RecentMeetingRow row)
    {
        await SelectHistoryAsync(row.Meta);
        Section = AppSection.Notes;
    }

    /// <summary>Collects the current meeting's candidate knowledge into this series (Mac 收集本场候选).</summary>
    public void CollectKnowledgeCandidates()
    {
        if (CurrentSession is not { } meta || SelectedSeries is not { } selected
            || workspace.Find(selected.Id) is not { } series) return;
        var summary = LoadCurrentSummaryForCollect();
        if (summary is null) { SetStatus(UiText.T("这场会议还没有可收集的候选。"), true); return; }
        var bank = (summary.Bank ?? []).Select(entry => new QuestionBankEntry(entry.Question, entry.Answer)).ToList();
        var (updated, added) = KnowledgeMerger.CollectCandidates(series, summary.Lessons ?? [], bank,
            meta.Id.ToString("D"), transcriptSourceRevision: summary.TranscriptSourceRevision);
        workspace = workspace.WithSeries(updated);
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == selected.Id);
        SetStatus(added.Count == 0
            ? UiText.T("没有新的知识候选。")
            : UiText.Format("已收集 %lld 条知识候选，等待复核。", added.Count));
    }

    public bool CanCollectKnowledgeCandidates => CurrentSession is not null && SelectedSeries is not null;
    public string CollectKnowledgeLabel => UiText.T("收集本场候选");

    private SessionSummary? LoadCurrentSummaryForCollect()
    {
        if (CurrentSession is not { } meta) return null;
        try
        {
            var path = Path.Combine(Sessions.Folder(meta.Id), "summary.json");
            if (!File.Exists(path)) return null;
            return System.Text.Json.JsonSerializer.Deserialize<SessionSummary>(
                File.ReadAllText(path), JsonDefaults.ArchiveOptions);
        }
        catch { return null; }
    }

    /// <summary>The current series' lessons, for the list in the series page.</summary>
    public ObservableCollection<WorkspaceLesson> SeriesLessons { get; } = [];

    /// <summary>Suggestions awaiting a decision; nothing here affects answers yet.</summary>
    public ObservableCollection<KnowledgeCandidateRow> SeriesCandidates { get; } = [];

    /// <summary>Reviewed questions with their best answer so far.</summary>
    public ObservableCollection<QuestionBankRow> SeriesBank { get; } = [];

    public bool HasSeriesLessons => SeriesLessons.Count > 0;
    public bool HasSeriesCandidates => SeriesCandidates.Count > 0;
    public bool HasSeriesBank => SeriesBank.Count > 0;

    public string SeriesLessonsHeader => UiText.Format("经验 %lld", SeriesLessons.Count);
    public string SeriesCandidatesHeader => SeriesCandidates.Count > 0
        ? UiText.Format("待确认 %lld", SeriesCandidates.Count)
        : UiText.T("待确认");
    public string SeriesBankHeader => UiText.Format("题库 %lld", SeriesBank.Count);

    private void RefreshSeriesLessons()
    {
        SeriesLessons.Clear();
        SeriesCandidates.Clear();
        SeriesBank.Clear();
        if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series)
        {
            foreach (var lesson in series.Lessons.Reverse()) SeriesLessons.Add(lesson);
            foreach (var candidate in series.PendingCandidates) SeriesCandidates.Add(new KnowledgeCandidateRow(candidate, series));
            foreach (var item in (series.QuestionBank ?? [])
                         .OrderByDescending(item => item.TimesAsked)
                         .ThenByDescending(item => item.UpdatedAt))
                SeriesBank.Add(new QuestionBankRow(item));
        }
        OnPropertyChanged(nameof(HasSeriesLessons));
        OnPropertyChanged(nameof(HasSeriesCandidates));
        OnPropertyChanged(nameof(HasSeriesBank));
        OnPropertyChanged(nameof(SeriesLessonsHeader));
        OnPropertyChanged(nameof(SeriesCandidatesHeader));
        OnPropertyChanged(nameof(SeriesBankHeader));
        OnPropertyChanged(nameof(SelectedSeriesMeta));
        OnPropertyChanged(nameof(SelectedSeriesBriefing));
    }

    /// <summary>
    /// Queues what a template summary carried (NotesCarry) as review candidates for the series
    /// this meeting belongs to. Nothing is accepted here: the series page asks.
    /// </summary>
    public void CollectFromNotes(SessionMeta meta, StructuredNotesDoc doc, NotesTemplate template)
    {
        var (lessons, bank) = NotesKnowledge.Extract(doc, template);
        if (lessons.Count + bank.Count == 0) return;
        var series = workspace.SeriesOfSession(meta.Id.ToString());
        if (series is null)
        {
            SetStatus("这场会议还不属于任何系列。到「系列」页把它加进去，之后整理纪要就会攒经验。");
            return;
        }
        var (updated, added) = KnowledgeMerger.CollectCandidates(series, lessons, bank, meta.Id.ToString());
        if (added.Count == 0)
        {
            SetStatus("这次没有新的经验和问题：之前整理过同一份纪要。");
            return;
        }
        workspace = workspace.WithSeries(updated);
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == series.Id);
        SetStatus(UiText.Format("「%@」有 %lld 条经验和问题待确认，确认后才会进入知识库。", series.Name, added.Count));
    }

    /// <summary>Applies the user's decision to one queued candidate.</summary>
    public void ReviewCandidate(KnowledgeCandidateRow row, WorkspaceKnowledgeDecision decision)
    {
        if (SelectedSeries is not { } selected || workspace.Find(selected.Id) is not { } series) return;
        var (updated, receipt) = KnowledgeMerger.ApplyCandidate(series, row.Candidate.Id, decision, DateTimeOffset.UtcNow);
        if (receipt is null)
        {
            SetStatus("这条建议对不上了：题库里的回答在整理之后改过，重新整理一次再确认。", true);
            return;
        }
        workspace = workspace.WithSeries(updated);
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == series.Id);
        SetStatus(decision switch
        {
            WorkspaceKnowledgeDecision.Reject => "已忽略这条建议。",
            WorkspaceKnowledgeDecision.ReplaceAnswer => "已替换这道题的回答，旧回答留在历史里。",
            WorkspaceKnowledgeDecision.KeepExisting => "保留了原来的经验，只记下这次会议的来源。",
            _ => "已采纳，下次回答会带上它。"
        });
    }

    /// <summary>Accepted lessons for the answer prompt, or "" when this meeting has no series.</summary>
    public string ExperiencePromptFor(SessionMeta? meta)
    {
        if (meta is null || ActiveCarry?.Lessons == false) return "";
        return workspace.SeriesOfSession(meta.Id.ToString()) is { } series
            ? WorkspaceKnowledgePrompt.ExperienceNotes(series)
            : "";
    }

    /// <summary>The series question bank as prompt context, or "" when empty.</summary>
    public string QuestionBankPromptFor(SessionMeta? meta)
    {
        if (meta is null || ActiveCarry?.Review == false) return "";
        return workspace.SeriesOfSession(meta.Id.ToString()) is { } series
            ? WorkspaceKnowledgePrompt.QuestionBankContext(series)
            : "";
    }

    /// <summary>The workspace as saved, for the offline knowledge check.</summary>
    public WorkspaceData WorkspaceSnapshot => workspace;

    /// <summary>Used by the self-check to put a review decision through the real store.</summary>
    public void ReplaceSeriesForCheck(WorkspaceSeries series)
    {
        workspace = workspace.WithSeries(series);
        SaveWorkspace();
    }

    /// <summary>Accepted lessons for a series id (the self-check has no SessionMeta).</summary>
    public string ExperiencePromptForSession(string sessionId) =>
        workspace.SeriesOfSession(sessionId) is { } series ? WorkspaceKnowledgePrompt.ExperienceNotes(series) : "";

    /// <summary>Creates a throw-away series for the self-check and returns its id ("" on failure).</summary>
    public string CreateSeriesForCheck(string name, string sessionId)
    {
        var before = workspace.Series.Count;
        workspace = workspace.AddSeries(name);
        if (workspace.Series.Count == before) return "";
        var series = workspace.Series[^1];
        workspace = workspace.Assign(sessionId, series.Id);
        SaveWorkspace();
        RefreshSeries();
        return series.Id;
    }

    public WorkspaceSeries? FindSeriesForCheck(string seriesId) => workspace.Find(seriesId);

    /// <summary>Selects the series in the page and rebuilds its lists (self-check path).</summary>
    public void FocusSeriesForCheck(string seriesId)
    {
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == seriesId);
        // Assigning the row that RefreshSeries already selected does not fire OnSelectedSeriesChanged,
        // so rebuild the per-series lists explicitly (self-check path).
        RefreshSeriesLessons();
        RefreshSeriesMeetings();
        RefreshSeriesSkills();
        RefreshSeriesChanges();
        RefreshSeriesPanel();
        RefreshSeriesStats();
    }

    /// <summary>Reloads workspace.json the way a new launch would.</summary>
    public void ReloadWorkspaceForCheck()
    {
        workspace = workspaceStore?.Load() ?? WorkspaceData.Empty;
        RefreshSeries();
    }

    public void RemoveSeriesForCheck(string seriesId)
    {
        workspace = workspace.RemoveSeries(seriesId);
        SaveWorkspace();
        RefreshSeries();
    }



    public void RenameSeries(string seriesId, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || workspace.Find(seriesId) is not { } series) return;
        workspace = workspace.WithSeries(series with { Name = trimmed });
        SaveWorkspace();
        RefreshSeries();
        SetStatus("系列已重命名：" + trimmed);
    }

    [RelayCommand]
    private void DeleteSeries(string? seriesId)
    {
        if (seriesId is null) return;
        workspace = workspace.RemoveSeries(seriesId);
        SaveWorkspace();
        RefreshSeries();
        SetStatus("系列已删除；会议记录保留。");
    }

    [RelayCommand]
    private void OpenSeries(string? seriesId)
    {
        if (seriesId is null) return;
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == seriesId);
        Section = AppSection.Series;
    }

    [RelayCommand]
    private void CreateSeries()
    {
        var name = NewSeriesName.Trim();
        if (name.Length == 0) { SetStatus("先给系列起一个名字。", true); return; }
        var before = workspace.Series.Count;
        workspace = workspace.AddSeries(name);
        if (workspace.Series.Count == before) { SetStatus("已经有同名的系列了。", true); return; }
        NewSeriesName = "";
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Name == name);
        SetStatus(UiText.Format("已新建系列「%@」。", name));
    }

    [RelayCommand(CanExecute = nameof(CanAssignCurrent))]
    private void AssignCurrentToSeries()
    {
        if (CurrentSession is not { } meta || SelectedSeries is not { } row) return;
        // A meeting belongs to one series at most, so assigning may move it: say where it came from.
        var previous = workspace.SeriesOfSession(meta.Id.ToString());
        workspace = workspace.Assign(meta.Id.ToString(), row.Id);
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == row.Id);
        SetStatus(previous is { } from && from.Id != row.Id
            ? UiText.Format("已把「%@」从「%@」移到系列「%@」。", meta.Title, from.Name, row.Name)
            : UiText.Format("已把「%@」放进系列「%@」。", meta.Title, row.Name));
    }

    private bool CanAssignCurrent() => CurrentSession is not null && SelectedSeries is not null;

    [RelayCommand]
    private void RemoveCurrentFromSeries()
    {
        if (CurrentSession is not { } meta) return;
        workspace = workspace.Remove(meta.Id.ToString());
        SaveWorkspace();
        RefreshSeries();
        SetStatus("已从系列中移出。");
    }

    [RelayCommand(CanExecute = nameof(CanAddLesson))]
    private void AddLesson()
    {
        if (SelectedSeries is not { } row) return;
        var text = NewLessonText.Trim();
        if (text.Length == 0) return;
        workspace = workspace.AddLesson(row.Id, text, CurrentSession?.Id.ToString());
        NewLessonText = "";
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == row.Id);
        SetStatus("经验已记下。");
    }

    private bool CanAddLesson() => SelectedSeries is not null;

    [RelayCommand]
    private void RemoveLesson(WorkspaceLesson lesson)
    {
        if (SelectedSeries is not { } row) return;
        workspace = workspace.RemoveLesson(row.Id, lesson.Id);
        SaveWorkspace();
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(item => item.Id == row.Id);
    }

    /// <summary>Selects a series from the list (the view calls this instead of binding selection).</summary>
    public void SelectSeries(SeriesRow row)
    {
        SelectedSeries = row;
        RefreshSeriesLessons();
    }

    public IReadOnlyList<RecentMeetingRow> SeriesMeetingRows => SeriesMeetings;

    /// <summary>Reloads the knowledge lists now (the review self-check drives them directly).</summary>
    public void RefreshSeriesLessonsForCheck() => RefreshSeriesLessons();
}


/// <summary>One skill attachment checkbox for the selected series.</summary>
public sealed partial class SeriesSkillRow : ObservableObject
{
    private readonly Action<SeriesSkillRow> changed;

    public SeriesSkillRow(Skill skill, bool isAttached, Action<SeriesSkillRow> changed)
    {
        SkillId = skill.Id;
        Name = skill.Name;
        IsGlobal = skill.Global;
        Enabled = skill.Enabled;
        Summary = skill.Summary;
        this.changed = changed;
        this.isAttached = isAttached;
    }

    public string SkillId { get; }
    public string Name { get; }

    /// <summary>Mac row badges: 所有系列 / 已停用 and the one-line summary.</summary>
    public bool IsGlobal { get; }
    public bool Enabled { get; }
    public string Summary { get; }
    public bool IsDisabled => !Enabled;
    public bool HasSummary => Summary.Length > 0;
    public bool CanToggle => !IsGlobal;

    [ObservableProperty] private bool isAttached;

    partial void OnIsAttachedChanged(bool value) => changed(this);
}
