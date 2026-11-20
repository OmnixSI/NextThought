using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class GlossaryWindow : Window
{
    public GlossaryWindow() => InitializeComponent();


    private void AddFromTranscript_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TranscriptRow row } || DataContext is not AppState state) return;
        state.NewGlossaryTerm = row.Text.Trim();
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GlossaryEntryRow row }) row.Remove(row.Entry);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
