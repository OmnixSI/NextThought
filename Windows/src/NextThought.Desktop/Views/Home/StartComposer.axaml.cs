using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views.Home;

/// <summary>
/// E1: the home composer (HomeView.swift StartComposer) — scene glyph, title field that guesses
/// the scene, scene pills with the selected one washed in its own pigment, the promise and its
/// three moments, and the start / prepare actions.
/// </summary>
public partial class StartComposer : UserControl
{
    private AppState? state;
    private bool syncing;
    /// <summary>Mac scenePinned: after picking a scene by hand the title no longer guesses it.</summary>
    private bool scenePinned;

    public StartComposer()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncSceneSelection();
        DataContextChanged += (_, _) => Attach();
        TitleBox.LostFocus += (_, _) => GuessFromTitle();
    }

    private void Attach()
    {
        if (state is not null) state.PropertyChanged -= OnStatePropertyChanged;
        state = DataContext as AppState;
        if (state is not null) state.PropertyChanged += OnStatePropertyChanged;
        SyncSceneSelection();
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedMode) or nameof(AppState.SelectedScene)) SyncSceneSelection();
    }

    private void Title_Changed(object? sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        if (string.IsNullOrWhiteSpace(TitleBox.Text)) scenePinned = false;
        GuessFromTitle();
    }

    private void GuessFromTitle()
    {
        if (state is null || syncing || scenePinned) return;
        syncing = true;
        try
        {
            // E1: typing a title guesses the scene; the highlight follows the state below.
            state.GuessSceneFromTitle(TitleBox.Text);
        }
        finally
        {
            syncing = false;
        }
    }

    private void Scene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SceneChoice scene } || state is null) return;
        scenePinned = true;
        state.SetSceneCommand.Execute(scene.Mode);
        SyncSceneSelection();
    }

    private void Prepare_Click(object? sender, RoutedEventArgs e)
    {
        if (state is not null) state.StartMeetingFromHome(startNow: false, state.SelectedMode);
    }

    private void Start_Click(object? sender, RoutedEventArgs e)
    {
        if (state is not null) state.StartMeetingFromHome(startNow: true, state.SelectedMode);
    }

    /// <summary>Washes the selected pill and the moment dots with the scene pigment (Mac StartComposer).</summary>
    private void SyncSceneSelection()
    {
        if (state is null) return;
        var dark = TopLevel.GetTopLevel(this)?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var tint = SceneTheme.For(state.SelectedMode).Tint(dark);
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
        {
            if (!button.Classes.Contains("scene") || button.Tag is not SceneChoice scene) continue;
            var selected = scene.Mode == state.SelectedMode;
            button.Classes.Set("selected", selected);
            if (!selected) { button.ClearValue(Button.BackgroundProperty); continue; }
            var pigment = SceneTheme.For(scene.Mode).Tint(dark);
            button.Background = new SolidColorBrush(Color.Parse(pigment), 0.13);
            foreach (var text in button.GetVisualDescendants().OfType<TextBlock>())
                text.Foreground = new SolidColorBrush(Color.Parse(pigment));
        }
        foreach (var dot in MomentsList.GetVisualDescendants().OfType<Ellipse>())
            dot.Fill = new SolidColorBrush(Color.Parse(tint));
    }
}
