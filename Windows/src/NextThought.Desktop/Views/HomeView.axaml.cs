using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncSceneSelection();
    }

    private void SyncSceneSelection()
    {
        if (DataContext is not AppState state) return;
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
        {
            if (!button.Classes.Contains("scene") || button.Tag is not SceneChoice scene) continue;
            var selected = scene.Mode == state.SelectedMode;
            button.Classes.Set("selected", selected);
            if (selected)
                button.Background = new SolidColorBrush(Color.Parse(scene.Theme.TintLight), 0.13);
            else
                button.ClearValue(Button.BackgroundProperty);
        }
    }

    private async void Recent_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        await state.SelectHistoryAsync(row.Meta);
    }

    private void Scene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SceneChoice scene } || DataContext is not AppState state) return;
        state.SetSceneCommand.Execute(scene.Mode);
        SyncSceneSelection();
    }
}
