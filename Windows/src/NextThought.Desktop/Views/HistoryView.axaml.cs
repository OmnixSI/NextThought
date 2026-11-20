using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E18: the 所有会议 page. Every row's context menu mirrors MeetingActionsMenu.swift, and the
/// delete flows mirror HistoryView.swift (delete dependent content vs keep accepted series
/// content, delete all meetings, delete a series with its records).
/// </summary>
public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();

    private static AppState? StateOf(object? sender) => (sender as Control)?.DataContext as AppState;
    private static Window? OwnerOf(Control control) => TopLevel.GetTopLevel(control) as Window;

    private async void Row_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            await state.SelectHistoryAsync(row.Meta);
            state.RefreshHistoryHitsForSelection();
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void OpenSession_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            await state.OpenSessionCommand.ExecuteAsync(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void ExportSession_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            await state.ExportSessionCommand.ExecuteAsync(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void Regenerate_Tapped(object? sender, RoutedEventArgs e) => Regenerate(sender, null);
    private void RegenerateChinese_Tapped(object? sender, RoutedEventArgs e) => Regenerate(sender, false);
    private void RegenerateEnglish_Tapped(object? sender, RoutedEventArgs e) => Regenerate(sender, true);

    private static async void Regenerate(object? sender, bool? english)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            await state.RegenerateSessionNotesAsync(row, english);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void RenameSession_Tapped(object? sender, RoutedEventArgs e) => await RenameSessionAsync(sender);

    private async Task RenameSessionAsync(object? sender)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            if (OwnerOf(this) is not { } owner) return;
            var title = await new RenameSessionDialog(row.Title, UiText.T("重命名会议")).ShowDialog<string?>(owner);
            if (!string.IsNullOrWhiteSpace(title)) await state.RenameSessionAsync(row, title);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void RevealSession_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            state.RevealSessionInExplorer(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void SessionInfo_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            if (OwnerOf(this) is not { } owner) return;
            await ShowSessionInfoAsync(state, row, owner);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void PrepareNext_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            state.PrepareNextMeetingAfter(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void DeleteSession_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: RecentMeetingRow row } || StateOf(sender) is not { } state) return;
            if (OwnerOf(this) is not { } owner) return;
            if (!state.CanDeleteSessionRow(row))
            {
                state.SetStatus(state.SessionDeleteDisabledReason(row), true);
                return;
            }
            var keepAccepted = state.HasRetainableContent([row]);
            var action = await ConfirmDeleteAsync(owner,
                UiText.T("删除这场会议？"),
                UiText.T("将永久删除这场会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖它的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。"),
                keepAccepted);
            if (action == DeleteAction.Cancel) return;
            if (action == DeleteAction.DeleteWithDependents)
            {
                await state.DeleteSessionAsync(row);
            }
            else
            {
                await state.DeleteSessionAsync(row, retainAcceptedContent: true);
                state.SetStatus(UiText.Format("已删除会议「%@」，已确认的系列内容已保留。", row.Title));
            }
            state.RefreshHistoryHitsForSelection();
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void DeleteAll_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (StateOf(sender) is not { } state) return;
            if (OwnerOf(this) is not { } owner) return;
            if (state.History.Count == 0) return;
            var rows = state.History.Where(row => state.CurrentSession?.Id != row.Meta.Id).ToList();
            var action = await ConfirmDeleteAsync(owner,
                UiText.T("删除全部会议记录？"),
                UiText.T("将永久删除所有归档会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖这些会议的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。"),
                state.HasRetainableContent(rows));
            if (action == DeleteAction.Cancel) return;
            await state.DeleteAllMeetingsAsync(retainAcceptedContent: action == DeleteAction.KeepAccepted);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void SeriesCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not CheckBox { Tag: string seriesId } || StateOf(sender) is not { } state) return;
            state.ToggleHistorySeriesSelection(seriesId);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void OpenSeries_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Control { Tag: string seriesId } || StateOf(sender) is not { } state) return;
            state.OpenSeriesCommand.Execute(seriesId);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void EditSeriesInfo_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Control { Tag: string seriesId } || StateOf(sender) is not { } state) return;
            if (OwnerOf(this) is not { } owner) return;
            state.FocusSeriesForCheck(seriesId);
            state.LoadSeriesEditor();
            await new SeriesEditorWindow { DataContext = state }.ShowDialog(owner);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void DeleteSeries_Tapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Control { Tag: string seriesId } || StateOf(sender) is not { } state) return;
            if (OwnerOf(this) is not { } owner) return;
            var series = state.SeriesRows.FirstOrDefault(row => row.Id == seriesId);
            var name = series?.Name ?? "";
            var confirmed = await new ConfirmDialog(UiText.Format("删除系列“%@”？系列及其会议记录将被永久删除，无法恢复。", name))
                .ShowDialog<bool>(owner);
            if (!confirmed) return;
            await state.DeleteSeriesWithMeetingsAsync(seriesId);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void Screenshot_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Bitmap bitmap }) return;
        var preview = new Window
        {
            Title = UiText.T("归档截图"),
            Width = 900,
            Height = 620,
            Content = new Image { Source = bitmap, Stretch = Stretch.Uniform },
            Background = Brushes.Black
        };
        preview.Show();
    }

    private void AnswerScreenshot_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: HistoryAnswerRow row } || StateOf(sender) is not { } state) return;
            if (state.AnswerScreenshotFullPath(row) is not { } path) { state.SetStatus(UiText.T("截图文件已不存在"), true); return; }
            var preview = new Window
            {
                Title = UiText.T("归档截图"),
                Width = 900,
                Height = 620,
                Content = new Image { Source = new Bitmap(path), Stretch = Stretch.Uniform },
                Background = Brushes.Black
            };
            preview.Show();
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void SearchHit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: NextThought.Platform.SessionContentHit hit } || StateOf(sender) is not { } state) return;
            await state.SelectHistoryAsync(hit.Meta);
            state.RefreshHistoryHitsForSelection();
            state.Section = AppSection.Notes;
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    // MARK: dialogs

    private enum DeleteAction
    {
        Cancel,
        DeleteWithDependents,
        KeepAccepted
    }

    /// <summary>
    /// HistoryView's two destructive confirmation dialogs. The "keep accepted series content"
    /// action only appears when a lesson or series item depends on the deleted meetings.
    /// </summary>
    private static async Task<DeleteAction> ConfirmDeleteAsync(Window owner, string heading, string message, bool canKeepAccepted)
    {
        var action = DeleteAction.Cancel;
        var dialog = new Window
        {
            Title = heading,
            Width = 540,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.FindResource("Paper") as IBrush
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        void AddButton(string text, DeleteAction value, string classes)
        {
            var button = new Button { Content = text, Classes = { classes } };
            button.Click += (_, _) => { action = value; dialog.Close(); };
            buttons.Children.Add(button);
        }
        if (canKeepAccepted) AddButton(UiText.T("删除会议，保留已确认的系列内容"), DeleteAction.KeepAccepted, "quiet");
        AddButton(UiText.T("取消"), DeleteAction.Cancel, "quiet");
        AddButton(UiText.T("删除会议和依赖内容"), DeleteAction.DeleteWithDependents, "destructive");
        var body = new StackPanel { Spacing = 12, Margin = new Avalonia.Thickness(22) };
        body.Children.Add(new TextBlock { Text = heading, Classes = { "section" }, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = message, Classes = { "caption" }, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(buttons);
        dialog.Content = body;
        await dialog.ShowDialog(owner);
        return action;
    }

    /// <summary>SessionInfoView: the stored facts of one archived meeting.</summary>
    private static async Task ShowSessionInfoAsync(AppState state, RecentMeetingRow row, Window owner)
    {
        var dialog = new Window
        {
            Title = UiText.T("详细信息"),
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.FindResource("Paper") as IBrush
        };
        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(22) };
        panel.Children.Add(new TextBlock { Text = row.Title, Classes = { "section" }, TextWrapping = TextWrapping.Wrap });
        foreach (var (label, value) in state.SessionInfoLines(row))
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*") };
            line.Children.Add(new TextBlock { Text = label, Classes = { "caption" } });
            var valueBlock = new TextBlock { Text = value, Classes = { "caption" }, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(valueBlock, 1);
            line.Children.Add(valueBlock);
            panel.Children.Add(line);
        }
        var close = new Button { Content = UiText.T("关闭"), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        panel.Children.Add(close);
        dialog.Content = panel;
        await dialog.ShowDialog(owner);
    }
}
