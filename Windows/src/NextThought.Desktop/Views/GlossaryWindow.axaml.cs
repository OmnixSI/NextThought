using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NextThought.Desktop.Views;

/// <summary>
/// E22: 术语和人名（GlossaryView.swift）。全局 / 当前系列两个作用域；左列条目，右侧编辑器；
/// 批量粘贴与导入导出；转写查找会把听错的写法一起找出来并显示 MisheardMark。
/// </summary>
public partial class GlossaryWindow : Window
{
    public GlossaryWindow() => InitializeComponent();

    private static AppState? StateOf(object? sender) => (sender as Control)?.DataContext as AppState;

    private void SelectEntry_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: GlossaryEntryRow row } && DataContext is AppState state)
                state.BeginGlossaryEntry(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
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
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void RemoveSeries_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GlossaryEntryRow row }) row.Remove(row.Entry);
    }

    private void ClearFind_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.GlossaryQuery = "";
    }

    private void Paste_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.BeginGlossaryPaste();
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
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
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
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
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
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>AddToGlossarySheet: opens for the matched transcript line (misheard spelling prefilled).</summary>
    private async void AddFromTranscript_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: GlossaryTranscriptHitRow row } || DataContext is not AppState state) return;
            state.BeginAddToGlossary(row.Text);
            if (!row.IsMisheard) state.GlossaryDraftHeard = row.Form;
            await new AddToGlossarySheet { DataContext = state }.ShowDialog(this);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
