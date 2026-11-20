using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class SpeakerEditorWindow : Window
{
    public SpeakerEditorWindow() => InitializeComponent();


    private void MergePrevious_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SpeakerRow row } || DataContext is not AppState state) return;
        var index = state.SpeakerRows.IndexOf(row);
        if (index <= 0 || state.SpeakerRows[index - 1].Voice is not { } into || row.Voice is not { } from) return;
        state.MergeSpeakerVoices(from, into);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
