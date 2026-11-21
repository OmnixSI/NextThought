using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

/// <summary>
/// E22 / §6.7: 术语和人名 — the glossary page, ported from GlossaryView.swift. Two scopes
/// (全局/当前系列), a 270 pt list, the entry editor on the right (canonical spelling, 术语/人名,
/// misheard spellings as removable tags, note, delete) or the bulk-paste editor. The open entry is
/// committed as it is typed (debounced), 完成 closes with the default action, and closing tidies
/// duplicates like Mac's onDisappear.
/// </summary>
public partial class GlossaryWindow : Window
{
    private readonly ObservableCollection<string> variants = [];
    private readonly DispatcherTimer commitTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool syncing;

    public GlossaryWindow()
    {
        InitializeComponent();
        VariantTags.ItemsSource = variants;
        commitTimer.Tick += (_, _) =>
        {
            commitTimer.Stop();
            CommitDraft();
        };
        DataContextChanged += (_, _) => Attach();
        Opened += (_, _) => Attach();
        Closing += (_, _) =>
        {
            commitTimer.Stop();
            CommitDraft();
            if (DataContext is AppState state) state.TidyGlossariesCommand.Execute(null);
        };
    }

    private AppState? state;

    private void Attach()
    {
        if (DataContext is not AppState next || ReferenceEquals(next, state)) return;
        state = next;
        state.PropertyChanged += OnStateChanged;
        UpdateViewState();
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.GlossarySelectedId) or nameof(AppState.GlossaryPasteMode)
            or nameof(AppState.GlossaryEditorCanonical) or nameof(AppState.GlossaryRows))
            UpdateViewState();
    }

    /// <summary>Mac body: entry editor, paste editor, or the "nothing selected" card.</summary>
    private void UpdateViewState()
    {
        if (state is null) return;
        var selected = state.GlossarySelectedId.Length > 0;
        var paste = state.GlossaryPasteMode;
        EntryEditor.IsVisible = !paste && selected;
        EmptyEditor.IsVisible = !paste && !selected;
        PasteEditor.IsVisible = paste;
        RefreshRowSelection();
        SyncVariants();
        SyncSegmented();
    }

    /// <summary>The three segmented controls mirror the model.</summary>
    private void SyncSegmented()
    {
        if (state is null) return;
        ScopeGlobalButton.Classes.Set("selected", !state.GlossarySeriesScope);
        ScopeSeriesButton.Classes.Set("selected", state.GlossarySeriesScope);
        KindTermButton.Classes.Set("selected", !state.GlossaryEditorIsPerson);
        KindPersonButton.Classes.Set("selected", state.GlossaryEditorIsPerson);
        PasteTermButton.Classes.Set("selected", !state.GlossaryPastePerson);
        PastePersonButton.Classes.Set("selected", state.GlossaryPastePerson);
    }

    private void ScopeGlobal_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        state?.UseGlobalGlossaryScopeCommand.Execute(null);
        UpdateViewState();
    }

    private void ScopeSeries_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        state?.UseSeriesGlossaryScopeCommand.Execute(null);
        UpdateViewState();
    }

    private void KindTerm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        state?.UseGlossaryTermKindCommand.Execute(null);
        QueueCommit();
        SyncSegmented();
    }

    private void KindPerson_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        state?.UseGlossaryPersonKindCommand.Execute(null);
        QueueCommit();
        SyncSegmented();
    }

    private void PasteTerm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        state?.UsePasteTermKindCommand.Execute(null);
        SyncSegmented();
    }

    private void PastePerson_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        state?.UsePastePersonKindCommand.Execute(null);
        SyncSegmented();
    }

    private void QueueCommit()
    {
        commitTimer.Stop();
        commitTimer.Start();
    }

    /// <summary>Mac draft.variants: the editor's tag list mirrors the comma-joined model field.</summary>
    private void SyncVariants()
    {
        if (state is null || syncing) return;
        syncing = true;
        try
        {
            var parts = state.GlossaryEditorVariants
                .Split([',', '，', '、', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!variants.SequenceEqual(parts))
            {
                variants.Clear();
                foreach (var part in parts) variants.Add(part);
            }
        }
        finally { syncing = false; }
    }

    private void RefreshRowSelection()
    {
        if (state is null) return;
        foreach (var button in GlossaryList.GetVisualDescendants().OfType<Button>())
        {
            if (button.Tag is not GlossaryEntryRow row) continue;
            button.Classes.Set("selected", row.Entry.Id == state.GlossarySelectedId);
        }
    }

    private void Field_Changed(object? sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        commitTimer.Stop();
        commitTimer.Start();
    }

    private void CommitDraft()
    {
        if (state is null) return;
        if (state.GlossarySelectedId.Length == 0 && state.GlossaryEditorCanonical.Trim().Length == 0) return;
        if (state.GlossaryEditorCanonical.Trim().Length == 0) return;
        state.SaveGlossaryEntryCommand.Execute(null);
    }

    // ---- list / entry commands ---------------------------------------------------------

    private void SelectEntry_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: GlossaryEntryRow row } && DataContext is AppState state)
                state.BeginGlossaryEntry(row);
            UpdateViewState();
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.StartNewGlossaryEntryCommand.Execute(null);
        UpdateViewState();
        CanonicalBox.Focus();
    }

    private void DeleteSelected_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            var row = state.GlossaryRows.FirstOrDefault(item => item.Entry.Id == state.GlossarySelectedId);
            if (row is null) return;
            row.Remove(row.Entry);
            state.GlossaryStatusText = UiText.T("已删除");
            UpdateViewState();
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    // ---- misheard spellings as tags (Mac variantTag) -----------------------------------

    private void VariantInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.OemComma)) return;
        e.Handled = true;
        AddVariantsFromInput();
    }

    private void VariantInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (VariantInput.Text is { } text && text.IndexOfAny([',', '，', '、', ';', '；']) >= 0) AddVariantsFromInput();
    }

    private void AddVariantsFromInput()
    {
        if (state is null) return;
        var canonical = state.GlossaryEditorCanonical.Trim();
        var parts = (VariantInput.Text ?? "")
            .Split([',', '，', '、', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length >= 2 && part != canonical);
        foreach (var part in parts)
            if (!variants.Contains(part)) variants.Add(part);
        VariantInput.Text = "";
        PublishVariants();
    }

    private void RemoveVariant_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) variants.Remove(tag);
        PublishVariants();
    }

    private void PublishVariants()
    {
        if (state is null) return;
        syncing = true;
        try { state.GlossaryEditorVariants = string.Join(", ", variants); }
        finally { syncing = false; }
        commitTimer.Stop();
        commitTimer.Start();
    }

    // ---- paste / import / export -------------------------------------------------------

    private void Paste_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.BeginGlossaryPaste();
        UpdateViewState();
    }

    private void CancelPaste_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.GlossaryPasteText = "";
            state.GlossaryPasteMode = false;
            state.GlossaryEditorCanonical = "";
        }
        UpdateViewState();
    }

    private void ImportPasted_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.ImportGlossaryPastedCommand.Execute(null);
        UpdateViewState();
    }

    private async void ImportFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = UiText.T("从文件导入术语"),
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(UiText.T("文本")) { Patterns = ["*.txt", "*.md", "*.csv"] }]
            });
            if (files.Select(file => file.TryGetLocalPath()).FirstOrDefault(path => path is not null) is not { } path) return;
            var added = state.ImportGlossaryText(await File.ReadAllTextAsync(path), NextThought.Core.GlossaryKind.Term);
            state.GlossaryStatusText = added > 0
                ? UiText.Format("已加入 %lld 项", added)
                : UiText.T("没有新的内容");
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void ExportFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = UiText.T("导出术语"),
                SuggestedFileName = "术语和人名.txt",
                DefaultExtension = "txt",
                ShowOverwritePrompt = true
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            await File.WriteAllTextAsync(path, state.ExportGlossaryText());
            state.GlossaryStatusText = UiText.T("已导出");
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void CopyText_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state || Clipboard is null) return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(state.ExportGlossaryText()));
            await Clipboard.SetDataAsync(transfer);
            state.GlossaryStatusText = UiText.T("已复制");
        }
        catch (Exception ex) { state?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>Mac 完成: commits the open entry, imports pending paste text, closes.</summary>
    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state && state.GlossaryPasteMode
            && state.GlossaryPasteText.Trim().Length > 0)
            state.ImportGlossaryPastedCommand.Execute(null);
        commitTimer.Stop();
        CommitDraft();
        Close();
    }
}
