using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class HomeView : UserControl
{
    private AppState? syncedState;

    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncSceneSelection();
        // D1: demo mode sets the scene from code, so the highlighted pill has to follow the state.
        DataContextChanged += (_, _) => AttachSceneSync();
    }

    private void AttachSceneSync()
    {
        if (ReferenceEquals(syncedState, DataContext as AppState)) return;
        if (syncedState is not null) syncedState.PropertyChanged -= OnStatePropertyChanged;
        syncedState = DataContext as AppState;
        if (syncedState is not null) syncedState.PropertyChanged += OnStatePropertyChanged;
        SyncSceneSelection();
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedMode) or nameof(AppState.SelectedScene)) SyncSceneSelection();
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
        try
        {
        if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
        await state.SelectHistoryAsync(row.Meta);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private void Scene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SceneChoice scene } || DataContext is not AppState state) return;
        state.SetSceneCommand.Execute(scene.Mode);
        SyncSceneSelection();
    }
}
