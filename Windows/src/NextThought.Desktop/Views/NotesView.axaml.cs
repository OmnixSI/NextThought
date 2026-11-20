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

    private async void EditEvidence_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SessionEvidenceRow row } || DataContext is not AppState state) return;
        if (row.Target is null || TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new EditTextDialog(row.Claim);
        var text = await dialog.ShowDialog<string?>(owner);
        if (text is not null) state.ApplyNotesEdit(row.Target, text);
    }
}
