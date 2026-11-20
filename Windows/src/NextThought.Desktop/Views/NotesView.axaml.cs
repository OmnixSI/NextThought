using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
        Fishbone.Jump += seconds => (DataContext as AppState)?.JumpToTranscript(seconds);
        Viewpoints.Jump += seconds => (DataContext as AppState)?.JumpToTranscript(seconds);
    }

    private async void EditNotes_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
        state.LoadNotesEditor();
        var dialog = new NotesEditorWindow { DataContext = state };
        await dialog.ShowDialog(owner);
    }
}
