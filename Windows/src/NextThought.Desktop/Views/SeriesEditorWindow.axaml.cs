using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.IO;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E20: the series editor window (WorkspaceEditor + WorkspaceBasicTab / RequirementsTab /
/// DocumentsTab / PreparationQueueSummary / ReviewTab). Documents import and remove immediately;
/// the other sections commit through 保存.
/// </summary>
public partial class SeriesEditorWindow : Window
{
    public SeriesEditorWindow()
    {
        InitializeComponent();
        Opened += (_, _) => PopulateConfigTypeMenu();
        Closing += OnClosing;
    }

    private string currentSection = "records";
    private string? pendingSection;
    private bool pendingClose;
    private bool allowClose;

    /// <summary>WorkspaceEditor.shouldCloseWindow: an unsaved draft asks first (决议 §3.5 series-editor 6).</summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose) return;
        if (DataContext is not AppState state || !state.SeriesEditorHasChanges) return;
        e.Cancel = true;
        pendingClose = true;
        state.ShowSeriesEditorUnsaved();
    }

    /// <summary>WorkspaceEditor.request: every transition away from a dirty draft asks first.</summary>
    private void RequestSection(string section)
    {
        if (section == currentSection) return;
        if (DataContext is AppState { SeriesEditorHasChanges: true } state)
        {
            pendingSection = section;
            pendingClose = false;
            state.ShowSeriesEditorUnsaved();
            return;
        }
        ApplySection(section);
    }

    private void SaveUnsavedSeries_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.SaveSeriesEditorCommand.Execute(null);
            state.HideSeriesEditorUnsaved();
        }
        ContinueAfterUnsavedPrompt();
    }

    private void DiscardUnsavedSeries_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.LoadSeriesEditor();
            state.HideSeriesEditorUnsaved();
        }
        ContinueAfterUnsavedPrompt();
    }

    private void KeepEditingSeries_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as AppState)?.HideSeriesEditorUnsaved();
        pendingSection = null;
        pendingClose = false;
    }

    private void ContinueAfterUnsavedPrompt()
    {
        if (pendingClose)
        {
            pendingClose = false;
            allowClose = true;
            Close();
            return;
        }
        var section = pendingSection ?? "records";
        pendingSection = null;
        ApplySection(section);
    }

    /// <summary>WorkspaceEditor editorActions: the type picker lives in the 配置 menu.</summary>
    private void PopulateConfigTypeMenu()
    {
        if (DataContext is not AppState state || ConfigButton?.Flyout is not MenuFlyout flyout) return;
        if (flyout.Items.OfType<MenuItem>().FirstOrDefault(item => item.Name == "ConfigTypeMenu") is not { } typeMenu) return;
        typeMenu.Items.Clear();
        foreach (var choice in state.SeriesEditorModeChoices)
        {
            var item = new MenuItem { Header = choice };
            item.Click += (_, _) =>
            {
                state.SeriesEditorMode = choice;
                RequestSection("basic");
            };
            typeMenu.Items.Add(item);
        }
    }

    /// <summary>WorkspaceEditor section switching: 记录/资料/经验积累 plus the 配置 sections.</summary>
    private void ApplySection(string section)
    {
        if (RecordsSection is null) return;
        currentSection = section;
        var primary = section is "records" or "documents" or "knowledge";
        RecordsSection.IsVisible = section == "records";
        DocumentsSection.IsVisible = section == "documents";
        KnowledgeSection.IsVisible = section == "knowledge";
        BasicSection.IsVisible = section == "basic";
        RequirementsSection.IsVisible = section == "requirements";
        SkillsSection.IsVisible = section == "skills";
        SetTab(RecordsTab, section == "records");
        SetTab(DocumentsTab, section == "documents");
        SetTab(KnowledgeTabButton, section == "knowledge");
        EditActionsBar.IsVisible = section is "basic" or "requirements";
        EditActionsLabel.Text = section == "basic" ? UiText.T("正在编辑系列信息") : UiText.T("正在编辑回答要求");
        _ = primary;
    }

    private static void SetTab(Button tab, bool active) => tab.Classes.Set("active", active);

    private void SectionRecords_Click(object? sender, RoutedEventArgs e) => RequestSection("records");
    private void SectionDocuments_Click(object? sender, RoutedEventArgs e) => RequestSection("documents");
    private void SectionKnowledge_Click(object? sender, RoutedEventArgs e) => RequestSection("knowledge");
    private void SectionBasic_Click(object? sender, RoutedEventArgs e) => RequestSection("basic");
    private void SectionRequirements_Click(object? sender, RoutedEventArgs e) => RequestSection("requirements");
    private void SectionSkills_Click(object? sender, RoutedEventArgs e) => RequestSection("skills");

    /// <summary>WorkspaceEditor.save in the draft bar: commit the basic/requirements edits.</summary>
    private void SaveDraft_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.SaveSeriesEditorCommand.Execute(null);
            state.HideSeriesEditorUnsaved();
        }
        ApplySection("records");
    }

    /// <summary>WorkspaceEditor.cancelDraft.</summary>
    private void CancelDraft_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.LoadSeriesEditor();
            state.HideSeriesEditorUnsaved();
        }
        ApplySection("records");
    }
    private void ToggleKnowledgeEditing_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.ToggleSeriesEditorKnowledgeEditing();

    private void ToggleAllFields_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.ToggleSeriesEditorAllFields();

    internal void ShowSectionForCheck(string section) => ApplySection(section);

    private static AppState? StateOf(object? sender) => (sender as Control)?.DataContext as AppState;

    private void RemoveDocument_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SeriesDocumentRow row } && DataContext is AppState state)
                state.RemoveSeriesDocumentCommand.Execute(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void DocumentEnabled_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is CheckBox { Tag: SeriesDocumentRow row } && DataContext is AppState state)
                state.SetSeriesDocumentEnabledCommand.Execute(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void MoveCategory_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SeriesDocumentRow row } && DataContext is AppState state)
                state.MoveSeriesDocumentToCategoryCommand.Execute(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void RetryPreparation_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SeriesPreparationRow row } && DataContext is AppState state)
                state.RetrySeriesPreparationCommand.Execute(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private void CancelPreparation_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SeriesPreparationRow row } && DataContext is AppState state)
                state.CancelSeriesPreparationCommand.Execute(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>WorkspaceDocumentsTab 导入资料 menu: 自动归类 or 导入到 a material slot (Mac).</summary>
    private void ImportDocuments_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not AppState state) return;
        var menu = new MenuFlyout();
        var auto = new MenuItem { Header = UiText.T("自动归类") };
        auto.Click += async (_, _) => await PickAndImportSeriesDocumentsAsync(null);
        menu.Items.Add(auto);
        menu.Items.Add(new Separator());
        foreach (var category in state.SeriesEditorImportCategories)
        {
            var pickedCategory = category;
            var item = new MenuItem { Header = UiText.Format("导入到 %@", category) };
            item.Click += async (_, _) => await PickAndImportSeriesDocumentsAsync(pickedCategory);
            menu.Items.Add(item);
        }
        menu.ShowAt(button);
    }

    /// <summary>WorkspaceDocumentsTab: pick files and import them into one slot (null = 自动归类).</summary>
    private async Task PickAndImportSeriesDocumentsAsync(string? category)
    {
        try
        {
            if (DataContext is not AppState state) return;
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = category is null ? UiText.T("自动归类") : UiText.Format("导入到 %@", category),
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(UiText.T("资料"))
                    {
                        Patterns = ["*.pdf", "*.docx", "*.pptx", "*.xlsx", "*.md", "*.txt", "*.png", "*.jpg",
                            "*.jpeg", "*.bmp", "*.tif", "*.tiff", "*.rtf", "*.html", "*.csv"]
                    },
                    new FilePickerFileType(UiText.T("全部文件")) { Patterns = ["*"] }
                ]
            });
            var paths = files.Select(file => file.TryGetLocalPath()).Where(path => path is not null)
                .Cast<string>().ToList();
            if (paths.Count > 0) await state.ImportSeriesDocumentsAsync(paths, category);
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void EditLesson_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: WorkspaceLesson lesson } || DataContext is not AppState state) return;
            var text = await new RenameSessionDialog(lesson.Text, UiText.T("编辑经验")).ShowDialog<string?>(this);
            if (!string.IsNullOrWhiteSpace(text)) state.UpdateSeriesLesson(lesson, text);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void DeleteLesson_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: WorkspaceLesson lesson } || DataContext is not AppState state) return;
            var confirmed = await new ConfirmDialog(UiText.T("删除这条经验？")).ShowDialog<bool>(this);
            if (confirmed) state.DeleteSeriesLessonCommand.Execute(lesson);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void EditBankItem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: QuestionBankRow row } || DataContext is not AppState state) return;
            var (question, answer) = await ShowBankEditorAsync(row.Question, row.Answer, this);
            if (question is null) return;
            state.UpdateSeriesQuestionBankItem(row.Item, question, answer ?? "");
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    private async void DeleteBankItem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: QuestionBankRow row } || DataContext is not AppState state) return;
            var confirmed = await new ConfirmDialog(UiText.T("删除这道题？")).ShowDialog<bool>(this);
            if (confirmed) state.DeleteSeriesBankItemCommand.Execute(row.Item);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>BankRow edits both the question and the best answer (answer history is kept).</summary>
    private static async Task<(string? Question, string? Answer)> ShowBankEditorAsync(string question, string answer,
        Window owner)
    {
        string? editedQuestion = null;
        string? editedAnswer = null;
        var dialog = new Window
        {
            Title = UiText.T("编辑题目"),
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.FindResource("Paper") as IBrush
        };
        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(22) };
        var questionBox = new TextBox { Text = question };
        var answerBox = new TextBox { Text = answer, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 120 };
        panel.Children.Add(new TextBlock { Text = UiText.T("问题"), Classes = { "caption" } });
        panel.Children.Add(questionBox);
        panel.Children.Add(new TextBlock { Text = UiText.T("回答"), Classes = { "caption" } });
        panel.Children.Add(answerBox);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var cancel = new Button { Content = UiText.T("取消"), Classes = { "quiet" } };
        cancel.Click += (_, _) => dialog.Close();
        var save = new Button { Content = UiText.T("保存"), Classes = { "primary" } };
        save.Click += (_, _) =>
        {
            editedQuestion = questionBox.Text?.Trim();
            editedAnswer = answerBox.Text?.Trim();
            dialog.Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        await dialog.ShowDialog(owner);
        return (editedQuestion, editedAnswer);
    }

    /// <summary>WorkspaceActions.deleteWorkspace: the series and its meeting records are deleted.</summary>
    private async void DeleteSeries_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state || state.SelectedSeries is not { } selected) return;
            var confirmed = await new ConfirmDialog(
                UiText.Format("删除系列“%@”？系列及其会议记录将被永久删除，无法恢复。", selected.Name))
                .ShowDialog<bool>(this);
            if (!confirmed) return;
            await state.DeleteSeriesFromEditorAsync();
            Close();
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>WorkspaceRecordsTab: 查看 opens the meeting page for that record.</summary>
    private async void OpenRecord_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: RecentMeetingRow row } || DataContext is not AppState state) return;
            Close();
            await state.OpenSeriesRecordAsync(row);
        }
        catch (Exception ex) { StateOf(sender)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>Mac 导入并用于此系列…: import Markdown skills and attach them to this series.</summary>
    private async void ImportSeriesSkills_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state) return;
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = UiText.T("导入技能（Markdown）"),
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("Markdown") { Patterns = ["*.md", "*.markdown", "*.txt"] }]
            });
            foreach (var file in files)
            {
                if (file.TryGetLocalPath() is not { } path) continue;
                try
                {
                    var skill = Skill.Parse(await File.ReadAllTextAsync(path), Path.GetFileName(path));
                    state.AddSkills([skill]);
                    state.AttachSkillToSelectedSeries(skill.Id);
                }
                catch (IOException) { }
            }
            state.RefreshSeriesSkillsForCheck();
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus(UiText.T("操作失败：") + ex.Message, true); }
    }

    /// <summary>Mac 管理技能: open 设置 → 回答与资料 → 技能.</summary>
    private void ManageSkills_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state) state.OpenSettingsPage("Answer", "技能");
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
