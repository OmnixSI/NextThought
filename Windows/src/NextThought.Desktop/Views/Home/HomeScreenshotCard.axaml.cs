using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Home;

/// <summary>
/// E1: the home screenshot card (HomeView.swift HomeScreenshotCard) — a region/window capture
/// waits here until the person analyzes it or cancels it.
/// </summary>
public partial class HomeScreenshotCard : UserControl
{
    public HomeScreenshotCard() => InitializeComponent();

    private async void Analyze_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        try { await state.AnalyzeScreenshotAsync(); }
        catch (Exception error) { state.SetStatus(error.Message, true); }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.ClearScreenshotPreview();
    }
}
