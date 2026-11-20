using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();



    private async void RenameSession_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is not MenuItem { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        if (Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new RenameSessionDialog(row.Title);
        var title = await dialog.ShowDialog<string?>(owner);
        if (!string.IsNullOrWhiteSpace(title)) await state.RenameSessionAsync(row, title);
    }

    private async void DeleteSession_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: RecentMeetingRow row } && DataContext is AppState state)
            await state.DeleteSessionAsync(row);
    }

    private async void Row_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        await state.SelectHistoryAsync(row.Meta);
    }
}
