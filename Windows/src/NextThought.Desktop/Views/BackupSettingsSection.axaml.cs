using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

/// <summary>
/// E30: the 备份与恢复 settings block (port of SettingsGeneralData.swift BackupSettingsSection).
/// Import and restore first prepare a review plan and show <see cref="BackupRestoreReviewView"/>.
/// </summary>
public partial class BackupSettingsSection : UserControl
{
    public BackupSettingsSection() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        NextThought.Core.UiTextSource.Instance.PropertyChanged += OnLanguageChanged;
        if (DataContext is AppState state)
        {
            state.LoadBackupPreferences();
            _ = state.RefreshBackupsAsync();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        NextThought.Core.UiTextSource.Instance.PropertyChanged -= OnLanguageChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>G8: the backup list rows and status lines follow a live language switch.</summary>
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is AppState state) state.NotifyBackupLanguageChanged();
    }

    private static AppState? StateOf(object? sender) => (sender as Control)?.DataContext as AppState;

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var state = StateOf(sender);
            if (state?.PickOpenFile is null) return;
            if (await state.PickOpenFile(UiText.T("NextThought 备份|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore")) is not { } file) return;
            await OpenReviewAsync(state, file);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("导入失败：") + ex.Message, true); }
    }

    private async void RestoreRow_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var state = StateOf(sender);
            if (state is null || (sender as Control)?.Tag is not BackupFileRow row) return;
            await OpenReviewAsync(state, row.File.Path);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("恢复失败：") + ex.Message, true); }
    }

    private async void DeleteRow_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var state = StateOf(sender);
            if (state is null || (sender as Control)?.Tag is not BackupFileRow row) return;
            await state.DeleteBackupCommand.ExecuteAsync(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("删除失败：") + ex.Message, true); }
    }

    private void RevealRow_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as Control)?.Tag is not BackupFileRow row) return;
            if (!File.Exists(row.File.Path)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + row.File.Path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("打开位置失败：") + ex.Message, true); }
    }

    private async Task OpenReviewAsync(AppState state, string file)
    {
        var prepared = await state.PrepareBackupImportAsync(file);
        if (prepared is null) return;
        var view = new BackupRestoreReviewView(prepared, state);
        var owner = TopLevel.GetTopLevel(this) as Window;
        var window = new Window
        {
            Title = UiText.T("导入与恢复预览"),
            Width = 820,
            Height = 640,
            MinWidth = 700,
            MinHeight = 540,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Background = this.FindResource("Paper") as Avalonia.Media.IBrush,
            Content = view
        };
        if (owner is not null) await window.ShowDialog(owner);
        else window.Show();
        if (!view.Applied) prepared.Discard();
    }
}
