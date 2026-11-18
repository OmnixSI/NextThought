using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>系列与经验（port of the series part of Workspace.swift, focused on the library view).</summary>
public partial class AppState
{
    private WorkspaceStore? workspaceStore;
    private WorkspaceData workspace = WorkspaceData.Empty;

    public ObservableCollection<SeriesRow> SeriesRows { get; } = [];
    public ObservableCollection<RecentMeetingRow> SeriesMeetings { get; } = [];

    [ObservableProperty] private SeriesRow? selectedSeries;
    [ObservableProperty] private string newSeriesName = "";
    [ObservableProperty] private string newLessonText = "";

    public bool HasSeries => SeriesRows.Count > 0;
    public bool HasSelectedSeries => SelectedSeries is not null;
    public bool SeriesIsEmpty => SeriesRows.Count == 0;
    public string SeriesSummary => SeriesRows.Count == 0
        ? "把同一门课、同一个项目、同一类面试的会议放在一个系列里，经验会攒下来。"
        : $"{SeriesRows.Count} 个系列 · {SeriesRows.Sum(row => row.Count)} 场会议";
    public string SelectedSeriesTitle => SelectedSeries?.Name ?? "还没有选择系列";
    public string SelectedSeriesMeta => SelectedSeries is not { } row
        ? "在左边选择一个系列，或者先新建一个。"
        : $"{row.Count} 场会议 · {row.LessonCount} 条经验";

    partial void OnSelectedSeriesChanged(SeriesRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedSeries));
        OnPropertyChanged(nameof(SelectedSeriesTitle));
        OnPropertyChanged(nameof(SelectedSeriesMeta));
        RefreshSeriesMeetings();
        RefreshSeriesLessons();
        AssignCurrentToSeriesCommand.NotifyCanExecuteChanged();
        AddLessonCommand.NotifyCanExecuteChanged();
    }

    private void LoadWorkspace()
    {
        workspaceStore = new WorkspaceStore(Paths.Root);
        workspace = workspaceStore.Load();
        RefreshSeries();
    }

    private void SaveWorkspace()
    {
        try { workspaceStore?.Save(workspace); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

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
                last?.Title ?? "还没有会议", series.Lessons.LastOrDefault()?.Text ?? ""));
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

    public bool HasSeriesLessons => SeriesLessons.Count > 0;

    private void RefreshSeriesLessons()
    {
        SeriesLessons.Clear();
        if (SelectedSeries is { } row && workspace.Find(row.Id) is { } series)
            foreach (var lesson in series.Lessons.Reverse()) SeriesLessons.Add(lesson);
        OnPropertyChanged(nameof(HasSeriesLessons));
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
        SetStatus($"已新建系列「{name}」。");
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
