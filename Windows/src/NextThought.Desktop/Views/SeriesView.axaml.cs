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

    private void RejectCandidate_Click(object? sender, RoutedEventArgs e) => ReviewCandidate(sender, WorkspaceKnowledgeDecision.Reject);

    /// <summary>The three review buttons all carry the row they belong to; only the decision differs.</summary>
    private void ReviewCandidate(object? sender, WorkspaceKnowledgeDecision decision)
    {
        if (sender is Button { Tag: KnowledgeCandidateRow row } && DataContext is AppState state)
            state.ReviewCandidate(row, decision);
    }

    private void AcceptSeriesChange_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SeriesChangeRow row } && DataContext is AppState state)
            state.ReviewSeriesChange(row.Id, NextThought.Core.WorkspaceSeriesChangeDecision.Add);
    }

    private void RejectSeriesChange_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SeriesChangeRow row } && DataContext is AppState state)
            state.ReviewSeriesChange(row.Id, NextThought.Core.WorkspaceSeriesChangeDecision.Reject);
    }

    private async void EditSeries_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
        state.LoadSeriesEditor();
        await new SeriesEditorWindow { DataContext = state }.ShowDialog(owner);
    }
}
