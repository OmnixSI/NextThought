using Avalonia.Controls;
using Avalonia.Interactivity;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class SeriesView : UserControl
{
    public SeriesView() => InitializeComponent();

    /// <summary>The screenshots and acceptance runs open a specific tab with --series-tab=N.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
}

    private void Series_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: SeriesRow row } && DataContext is AppState state)
            state.SelectSeries(row);
    }

    private void RemoveLesson_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorkspaceLesson lesson } && DataContext is AppState state)
            state.RemoveLessonCommand.Execute(lesson);
    }

    private void AcceptCandidate_Click(object? sender, RoutedEventArgs e) => ReviewCandidate(sender, WorkspaceKnowledgeDecision.Add);

    private void ReplaceCandidate_Click(object? sender, RoutedEventArgs e) => ReviewCandidate(sender, WorkspaceKnowledgeDecision.ReplaceAnswer);

    private void KeepCandidate_Click(object? sender, RoutedEventArgs e) => ReviewCandidate(sender, WorkspaceKnowledgeDecision.KeepExisting);

    private void RejectCandidate_Click(object? sender, RoutedEventArgs e) => ReviewCandidate(sender, WorkspaceKnowledgeDecision.Reject);

    /// <summary>The three review buttons all carry the row they belong to; only the decision differs.</summary>
    private void ReviewCandidate(object? sender, WorkspaceKnowledgeDecision decision)
    {
        if (sender is Button { Tag: KnowledgeCandidateRow row } && DataContext is AppState state)
            state.ReviewCandidate(row, decision);
    }

    private void CollectSeriesChanges_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.CollectSeriesChangeCandidates();
    }

    private void CollectKnowledge_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.CollectKnowledgeCandidates();
    }

    private void AcceptSeriesChange_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SeriesChangeRow row } && DataContext is AppState state)
            state.ReviewSeriesChange(row, NextThought.Core.WorkspaceSeriesChangeDecision.Add, null);
    }

    private void RejectSeriesChange_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SeriesChangeRow row } && DataContext is AppState state)
            state.ReviewSeriesChange(row, NextThought.Core.WorkspaceSeriesChangeDecision.Reject, null);
    }

    /// <summary>A 更新 button carries its target item in DataContext and the proposal in Tag.</summary>
    private void ReplaceSeriesChange_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SeriesTargetRow target, Tag: SeriesChangeRow candidate }
            && DataContext is AppState state)
            state.ReviewSeriesChange(candidate, NextThought.Core.WorkspaceSeriesChangeDecision.Replace, target);
    }

    /// <summary>WorkspaceRecordsTab: open one record of the selected series.</summary>
    private async void OpenSeriesRecord_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
            await state.OpenSeriesRecordAsync(row);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void EditSeries_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
        state.LoadSeriesEditor();
        await new SeriesEditorWindow { DataContext = state }.ShowDialog(owner);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }
}
