using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class SeriesEditorWindow : Window
{
    public SeriesEditorWindow() => InitializeComponent();


    private void RemoveDocument_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NextThought.Core.ContextDocument document } && DataContext is AppState state)
            state.RemoveSeriesDocumentCommand.Execute(document);
    }


    private async void DeleteSeries_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || state.SelectedSeries is not { } selected) return;
        var confirmed = await new ConfirmDialog($"删除系列「{selected.Name}」？会议记录会保留。").ShowDialog<bool>(this);
        if (!confirmed) return;
        state.DeleteSeriesCommand.Execute(selected.Id);
        Close();
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
