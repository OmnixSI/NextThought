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

    partial void OnSelectedSeriesChanged(SeriesRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedSeries));
        OnPropertyChanged(nameof(SelectedSeriesTitle));
        OnPropertyChanged(nameof(SelectedSeriesMeta));
        RefreshSeriesMeetings();
        RefreshSeriesLessons();
        RefreshSeriesSkills();
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
            var isAttached = attached.Any(id => Guid.TryParse(id, out var parsed) && parsed == Guid.Parse(skill.Id));
            SeriesSkills.Add(new SeriesSkillRow(skill, isAttached, ToggleSeriesSkill));
        }
    }

    private void ToggleSeriesSkill(SeriesSkillRow row)
    {
        if (SelectedSeries is null || workspace.Find(SelectedSeries.Id) is not { } series) return;
        var ids = (series.SkillIds ?? []).Where(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Parse(row.SkillId)).ToList();
        if (row.IsAttached) ids.Add(row.SkillId);
        workspace = workspace.WithSeries(series with { SkillIds = ids });
        SaveWorkspace();
        SetStatus(row.IsAttached ? $"已把「{row.Name}」附着到「{series.Name}」。" : $"已从「{series.Name}」移除「{row.Name}」。");
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
                .Select(id => History.FirstOrDefault(row => row.Meta.Id.ToString() == id)?.Meta)
                .Where(meta => meta is not null)
                .OrderByDescending(meta => meta!.StartedAt)
                .FirstOrDefault();
            SeriesRows.Add(new SeriesRow(series.Id, series.Name, series.SessionIds.Count, series.Lessons.Count,
                last?.Title ?? "还没有会议", series.Lessons.LastOrDefault()?.Text ?? "", series.PendingCandidates.Count, series.Documents.Count));
        }
        OnPropertyChanged(nameof(HasSeries));
        OnPropertyChanged(nameof(SeriesIsEmpty));
        OnPropertyChanged(nameof(SeriesSummary));
        AssignCurrentToSeriesCommand.NotifyCanExecuteChanged();
        AddLessonCommand.NotifyCanExecuteChanged();
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == selectedId) ?? SeriesRows.FirstOrDefault();
    }

    private void RefreshSeriesMeetings()
    {
        SeriesMeetings.Clear();
        if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series)
        {
            foreach (var id in series.SessionIds)
            {
                var meeting = History.FirstOrDefault(item => item.Meta.Id.ToString() == id);
                if (meeting is not null) SeriesMeetings.Add(meeting);
            }
        }
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

    public string SeriesLessonsHeader => UiText.EnglishUi ? $"Lessons {SeriesLessons.Count}" : $"经验 {SeriesLessons.Count}";
    public string SeriesCandidatesHeader => SeriesCandidates.Count > 0
        ? (UiText.EnglishUi ? $"To confirm {SeriesCandidates.Count}" : $"待确认 {SeriesCandidates.Count}")
        : UiText.T("待确认");
    public string SeriesBankHeader => UiText.EnglishUi ? $"Question bank {SeriesBank.Count}" : $"题库 {SeriesBank.Count}";

    private void RefreshSeriesLessons()
    {
        SeriesLessons.Clear();
        SeriesCandidates.Clear();
        SeriesBank.Clear();
        if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series)
        {
            foreach (var lesson in series.Lessons.Reverse()) SeriesLessons.Add(lesson);
            foreach (var candidate in series.PendingCandidates) SeriesCandidates.Add(new KnowledgeCandidateRow(candidate));
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
        SetStatus($"「{series.Name}」有 {added.Count} 条经验和问题待确认，确认后才会进入知识库。");
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
        RefreshSeriesLessons();
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
        SetStatus(NextThought.Core.UiText.EnglishUi ? $"Series \"{name}\" created." : $"已新建系列「{name}」。");
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
            ? $"已把「{meta.Title}」从「{from.Name}」移到系列「{row.Name}」。"
            : $"已把「{meta.Title}」放进系列「{row.Name}」。");
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
}


/// <summary>One skill attachment checkbox for the selected series.</summary>
public sealed partial class SeriesSkillRow : ObservableObject
{
    private readonly Action<SeriesSkillRow> changed;

    public SeriesSkillRow(Skill skill, bool isAttached, Action<SeriesSkillRow> changed)
    {
        SkillId = skill.Id;
        Name = skill.Name;
        this.changed = changed;
        this.isAttached = isAttached;
    }

    public string SkillId { get; }
    public string Name { get; }

    [ObservableProperty] private bool isAttached;

    partial void OnIsAttachedChanged(bool value) => changed(this);
}
