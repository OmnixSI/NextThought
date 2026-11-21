using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Assistant;

/// <summary>
/// E5: every answer of the open session, newest first (AssistantView.swift AnswerHistoryPanel).
/// While recording the list is the live history; after a meeting it is read from the archive.
/// Clicking a row shows that answer in the answer column.
/// </summary>
public partial class AnswerHistoryPanel : UserControl
{
    public AnswerHistoryPanel() => InitializeComponent();

    private void Row_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AnswerHistoryRow row } && DataContext is AppState state)
            state.SelectAnswerHistory(row.Index);
    }

    private void Latest_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.ShowLatestAnswer();
}
