using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views.Assistant;

/// <summary>E5: the screenshot waiting for analysis in the answer module (Mac ScreenshotCard).</summary>
public partial class ScreenshotCard : UserControl
{
    public ScreenshotCard() => InitializeComponent();

    private void Cancel_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.ClearScreenshotPreview();

    private async void Analyze_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        try { await state.AnalyzeScreenshotAsync(); }
        catch (Exception error) { state.SetStatus(error.Message, true); }
    }
}
