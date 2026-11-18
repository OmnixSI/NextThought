using Avalonia.Controls;
using Avalonia.Interactivity;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class SeriesView : UserControl
{
    public SeriesView() => InitializeComponent();

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
}
