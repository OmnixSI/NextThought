using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class QuickSettingsPanel : UserControl
{
    public QuickSettingsPanel() => InitializeComponent();

    /// <summary>E26b: Mac quick settings save the cue surface immediately.</summary>
    private void CueSurface_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is AppState state) _ = state.Settings.SaveAsync();
    }

    /// <summary>E26b: Mac 双屏演讲的讲稿开关 — persist and refresh the island's slide notes.</summary>
    private void PresenterSlideNotes_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.NotifyPresenterSlideNotesChanged();
        _ = state.Settings.SaveAsync();
    }
}
