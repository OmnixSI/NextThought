using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
        Fishbone.Jump += seconds => (DataContext as AppState)?.JumpToTranscript(seconds);
        Viewpoints.Jump += seconds => (DataContext as AppState)?.JumpToTranscript(seconds);
        // D1: in demo mode the last tab (工作纪要) shows the Mac demo's weekly work meeting, the
        // way Mac's demoNotes=work capture does; the other tabs stay on the archived interview.
        Loaded += (_, _) =>
        {
            if (this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault() is { } tabs)
                tabs.SelectionChanged += (_, _) =>
                    (DataContext as AppState)?.ApplyDemoNotesTab(tabs.SelectedIndex);
        };
    }

    private async void EditNotes_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
        state.LoadNotesEditor();
        var dialog = new NotesEditorWindow { DataContext = state };
        await dialog.ShowDialog(owner);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void EditEvidence_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (sender is not Button { Tag: SessionEvidenceRow row } || DataContext is not AppState state) return;
        if (row.Target is null || TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new EditTextDialog(row.Claim);
        var text = await dialog.ShowDialog<string?>(owner);
        if (text is not null) state.ApplyNotesEdit(row.Target, text);
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }
}
