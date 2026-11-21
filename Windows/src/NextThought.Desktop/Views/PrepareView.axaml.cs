using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NextThought.Desktop.Views;

/// <summary>
/// E2: the preparation page (Mac PrepareView). The code-behind keeps the one-shot UI actions that need
/// a control: scene/custom-type chips, the drag-and-drop target and the draft lifecycle.
/// </summary>
public partial class PrepareView : UserControl
{
    public PrepareView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as AppState)?.EnsureDraft();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void PrepareScene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SceneChoice scene } && DataContext is AppState state)
            state.SetDraftType(scene.Mode);
    }

    private async void VoiceTest_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        try
        {
            await new OnboardingVoiceWindow { DataContext = state }.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            state.SetStatus(UiText.T("操作失败：") + ex.Message, true);
        }
    }

    private void CustomType_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.SetDraftCustomTypeCommand.Execute(null);

    /// <summary>True while the page itself is republishing the base-type list (ComboBox re-entrancy).</summary>
    private bool applyingDraft;

    private void CustomBase_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (applyingDraft) return;
        if (sender is not ComboBox { SelectedItem: SceneChoice choice } || DataContext is not AppState state) return;
        // Keep the draft on the picked base type while the custom type stays selected.
        applyingDraft = true;
        try
        {
            state.SetDraftCustomBase(choice.Mode);
        }
        finally
        {
            applyingDraft = false;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer is IAsyncDataTransfer transfer && transfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not AppState state || e.DataTransfer is not IAsyncDataTransfer transfer) return;
        try
        {
            var files = await transfer.TryGetFilesAsync();
            if (files is null) return;
            var paths = files.Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => path!)
                .ToList();
            if (paths.Count > 0) await state.ImportDraftDocumentsAsync(paths);
        }
        catch (Exception ex)
        {
            state.SetStatus(UiText.T("导入失败：") + ex.Message, true);
        }
    }
}
