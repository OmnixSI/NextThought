using Avalonia.Controls;

namespace NextThought.Desktop.Views;

/// <summary>
/// E1: the home page (HomeView.swift). The composer, continue-series, screenshot card and recent
/// meetings are named subviews in Views/Home so each Mac view has a same-named Windows counterpart.
/// </summary>
public partial class HomeView : UserControl
{
    private AppState? state;

    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (state is not null) state.PropertyChanged -= OnStatePropertyChanged;
        state = DataContext as AppState;
        if (state is not null) state.PropertyChanged += OnStatePropertyChanged;
        Refresh();
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // A new recording, a fresh archive or a scene switch changes the home lists.
        if (e.PropertyName is nameof(AppState.Recording) or nameof(AppState.CurrentSession)
            or nameof(AppState.SelectedMode)) Refresh();
    }

    private void Refresh() => state?.RefreshHome();
}
