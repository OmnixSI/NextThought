using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();




    private async void RenameSeries_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        try
        {
        if (sender is not MenuItem { Tag: string seriesId } || DataContext is not AppState state) return;
        if (Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        var current = state.SeriesRows.FirstOrDefault(row => row.Id == seriesId)?.Name ?? "";
        var dialog = new RenameSessionDialog(current, "重命名系列");
        var name = await dialog.ShowDialog<string?>(owner);
        if (!string.IsNullOrWhiteSpace(name)) state.RenameSeries(seriesId, name);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }




    private async void DeleteSelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        var count = state.FilteredHistory.Count(row => row.IsSelected);
        if (count == 0) return;
        var confirmed = await new ConfirmDialog($"删除选中的 {count} 场会议？此操作会移除归档目录。").ShowDialog<bool>(owner);
        if (confirmed) state.DeleteSelectedSessionsCommand.Execute(null);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void DeleteFiltered_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        var count = state.FilteredHistory.Count;
        if (count == 0) return;
        var confirmed = await new ConfirmDialog($"删除当前筛选的 {count} 场会议？此操作会移除归档目录。").ShowDialog<bool>(owner);
        if (confirmed) state.DeleteFilteredSessionsCommand.Execute(null);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void DeleteSession_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        try
        {
        if (sender is not MenuItem { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        if (Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        var confirmed = await new ConfirmDialog($"删除会议「{row.Title}」？会议归档会从磁盘移除。").ShowDialog<bool>(owner);
        if (confirmed) await state.DeleteSessionAsync(row);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void RenameSession_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        try
        {
        if (sender is not MenuItem { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        if (Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new RenameSessionDialog(row.Title);
        var title = await dialog.ShowDialog<string?>(owner);
        if (!string.IsNullOrWhiteSpace(title)) await state.RenameSessionAsync(row, title);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }



    private void Screenshot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Avalonia.Media.Imaging.Bitmap bitmap }) return;
        var preview = new Window
        {
            Title = "归档截图",
            Width = 900,
            Height = 620,
            Content = new Image { Source = bitmap, Stretch = Avalonia.Media.Stretch.Uniform },
            Background = Avalonia.Media.Brushes.Black
        };
        preview.Show();
    }

    private async void SearchHit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
        if (sender is not Button { Tag: NextThought.Platform.SessionContentHit hit } || DataContext is not AppState state) return;
        await state.SelectHistoryAsync(hit.Meta);
        state.Section = AppSection.Notes;
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void DeleteSession_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (sender is MenuItem { Tag: RecentMeetingRow row } && DataContext is AppState state)
            await state.DeleteSessionAsync(row);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void Row_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        await state.SelectHistoryAsync(row.Meta);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }
}
