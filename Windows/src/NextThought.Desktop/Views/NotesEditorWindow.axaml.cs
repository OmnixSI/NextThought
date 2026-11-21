using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E16: the notes editor (MeetingNotesEditor.swift). Edits live in the user's own version; the
/// evidence sidebar opens the referenced transcript line without leaving the meeting page.
/// </summary>
public partial class NotesEditorWindow : Window
{
    public NotesEditorWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    /// <summary>MeetingNotesEditor: closing with unsaved changes asks first.</summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose || DataContext is not AppState state || !state.NotesEditorDirty) return;
        e.Cancel = true;
        if (await ConfirmDiscardAsync(state)) { allowClose = true; Close(); }
    }

    private bool allowClose;

    private async Task<bool> ConfirmDiscardAsync(AppState state) =>
        await new ConfirmDialog(UiText.T("放弃尚未保存的更改？")).ShowDialog<bool>(this);

    private async void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is AppState state && state.NotesEditorDirty && !await ConfirmDiscardAsync(state)) return;
            allowClose = true;
            Close();
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void ConfirmNoteItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NotesEditorItemRow row } && DataContext is AppState state)
            state.ConfirmNotesEditorItem(row);
    }

    private void FindEvidence_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: SessionEvidenceRow row } || DataContext is not AppState state) return;
            var target = state.NotesEvidence.FirstOrDefault(item => item.Reference.Id == row.Reference.Id);
            if (target is not null) state.JumpToEvidenceLine(target.Reference.LineId, target.Reference.Location);
            allowClose = true;
            Close();
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }
}
