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
        if (SelfTestOptions.SeriesTab is { } index && index >= 0 && index < Tabs.ItemCount) Tabs.SelectedIndex = index;
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
}
