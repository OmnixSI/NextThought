using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    private async void Recent_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        await state.SelectHistoryAsync(row.Meta);
    }

    private void Scene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SceneChoice scene } || DataContext is not AppState state) return;
        state.SetSceneCommand.Execute(scene.Mode);
    }
}
