using System.IO;
using NextThought.Core;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace NextThought.Desktop.Views;

public partial class SettingsView : UserControl
{
    private string currentPage = "Scene";

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachLanguageRefresh();
    }

    private AppState? languageState;

    private void AttachLanguageRefresh()
    {
        if (languageState is not null) languageState.PropertyChanged -= OnLanguagePropertyChanged;
        languageState = DataContext as AppState;
        if (languageState is not null) languageState.PropertyChanged += OnLanguagePropertyChanged;
    }

    private void OnLanguagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.SettingsRequestedPage) && sender is AppState requested
            && requested.SettingsRequestedPage is { Length: > 0 } page)
        {
            var anchor = requested.SettingsRequestedAnchor;
            requested.SettingsRequestedPage = "";
            requested.SettingsRequestedAnchor = "";
            ShowPage(page);
            if (anchor.Length > 0) ScrollToSection(anchor);
            return;
        }
        if (e.PropertyName != nameof(AppState.LanguageGeneration)) return;
        RefreshScreens();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        RefreshScreens();
        BuildSkillRows();
        ShowPage(currentPage);
    }




    private bool refreshingScreens;

    private void RefreshScreens()
    {
        if (DataContext is not AppState state) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        refreshingScreens = true;
        CueScreenCombo.Items.Clear();
        CueScreenCombo.Items.Add(new ComboBoxItem { Content = UiText.T("内置屏 / 主屏（自动）"), Tag = "" });
        foreach (var screen in top.Screens.All)
        {
            var label = screen.DisplayName ?? UiText.T("显示器");
            if (ReferenceEquals(screen, top.Screens.Primary)) label += UiText.T("（主屏）");
            CueScreenCombo.Items.Add(new ComboBoxItem { Content = label, Tag = screen.DisplayName ?? "" });
        }
        var selected = CueScreenCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => (string?)item.Tag == state.Settings.CueScreen) ?? CueScreenCombo.Items[0];
        CueScreenCombo.SelectedItem = selected;
        refreshingScreens = false;
    }

    private void CueScreen_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (refreshingScreens || DataContext is not AppState state) return;
        if (CueScreenCombo.SelectedItem is ComboBoxItem { Tag: string name })
        {
            state.Settings.CueScreen = name;
            _ = state.Settings.SaveAsync();
        }
    }

    /// <summary>E10: Mac PermissionSettingsSection 去允许 — open the Windows camera privacy page.</summary>
    private void OpenCameraSettings_Click(object? sender, RoutedEventArgs e) => NextThought.Platform.WindowsPermissions.OpenCameraSettings();

    /// <summary>E26: Mac SceneRow 恢复默认 — every field of the row back to the built-in table.</summary>
    private void ResetScene_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SceneSettingRow row }) row.RestoreDefaults();
    }

    private void SceneOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string field, DataContext: SceneSettingRow row }) return;
        switch (field)
        {
            case "LiveMode": row.LiveMode = Next(row.LiveOptions, row.LiveMode); break;
            case "AnswerStyle": row.AnswerStyle = Next(row.AnswerOptions, row.AnswerStyle); break;
            case "NotesTemplate": row.NotesTemplate = Next(row.TemplateOptions, row.NotesTemplate); break;
            case "CueStyle": row.CueStyle = Next(row.CueOptions, row.CueStyle); break;
        }
    }

    private static string Next(IReadOnlyList<string> options, string current) =>
        options.Count == 0 ? current : options[(options.ToList().IndexOf(current) + 1) % options.Count];

    public void SelectPage(string page) => ShowPage(page);

    /// <summary>
    /// E13 / §6.7: Mac ContentView ShareTestSheet — capture the screen as other participants see it
    /// (this app's own floating windows included) and show it in the share-test window.
    /// </summary>
    private async void ShareVisibilityTest_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        if (!await state.RunShareVisibilityTestAsync()) return;
        var window = new ShareTestWindow { DataContext = state };
        if (TopLevel.GetTopLevel(this) is Window owner) await window.ShowDialog(owner);
        else window.Show();
    }

    private void ShowPage(string page)
    {
        currentPage = page;
        SettingsHeaderTitle.Text = PageTitle(page);
        // §3.6 G2: the sidebar row is a Mac AppNavigationSidebar selection (soft block).
        foreach (var button in this.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("settings-nav")))
        {
            if (string.Equals(button.Tag as string, page, StringComparison.Ordinal)) button.Classes.Add("active");
            else button.Classes.Remove("active");
        }
        foreach (var card in this.GetVisualDescendants().OfType<Border>()
                     .Where(border => border.Classes.Contains("card") && border.Name != "SettingsSearchCard"))
            card.IsVisible = card.Tag as string == page;
        SettingsScroll.Offset = new Avalonia.Vector(0, 0);
        // E29: permissions can change in Windows Settings while this page is open.
        if (page == "General" && DataContext is AppState state) state.RefreshPermissions();
        // E23: installed document components can change from another page while the app runs.
        if (page is "Answer" or "Advanced" && DataContext is AppState documentState) documentState.RefreshDocumentComponents();
    }

    /// <summary>Evidence batch: scroll the settings column to the bottom (shortcut row / UpdateAdvanced).</summary>
    public void ScrollToBottom()
    {
        var maximum = Math.Max(0, SettingsScroll.Extent.Height - SettingsScroll.Viewport.Height);
        SettingsScroll.Offset = new Avalonia.Vector(0, maximum);
    }

    public void ScrollToTop() => SettingsScroll.Offset = new Avalonia.Vector(0, 0);

    /// <summary>Evidence batch: put the UpdateAdvanced card (更新渠道 + GitHub 访问) at the top.</summary>
    public void ScrollToUpdateAdvanced()
    {
        if (this.FindControl<Avalonia.Controls.TextBlock>("AnchorAdvancedUpdate") is not { } anchor) return;
        var y = anchor.TranslatePoint(new Avalonia.Point(0, 0), SettingsScroll)?.Y ?? 0;
        SettingsScroll.Offset = new Avalonia.Vector(0, Math.Max(0, SettingsScroll.Offset.Y + y - 16));
    }

    /// <summary>Evidence batch: expand the 快捷键… row (Mac ShortcutsSettingsRow) before the shot.</summary>
    public bool ExpandShortcutsRow()
    {
        var row = this.GetVisualDescendants().OfType<ShortcutsSettingsRow>().FirstOrDefault();
        row?.SetExpanded(true);
        return row is not null;
    }

    public void CollapseShortcutsRow()
    {
        this.GetVisualDescendants().OfType<ShortcutsSettingsRow>().FirstOrDefault()?.SetExpanded(false);
    }

    /// <summary>
    /// E25: Mac settings search matches items on every page and lists them with the page name;
    /// choosing a result navigates to that page (the old filter stayed inside the current page).
    /// </summary>
    private void SettingsSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var query = box.Text?.Trim() ?? "";
        var cards = this.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains("card") && border.Name != "SettingsSearchCard").ToList();
        SettingsSearchResults.Children.Clear();
        if (query.Length == 0)
        {
            SettingsSearchResults.IsVisible = false;
            foreach (var card in cards) card.IsVisible = card.Tag as string == currentPage;
            return;
        }
        foreach (var card in cards) card.IsVisible = false;
        SettingsSearchResults.IsVisible = true;
        // §3.6 / Mac SettingsSearchItem: the hidden keyword table finds items whose title does not
        // contain the query (刘海 / 钉住 / notch …), and each result carries the Mac page title.
        foreach (var item in NextThought.Core.SettingsSearchItem.Matching(query))
        {
            var title = item.Title + " · " + PageTitle(item.Page);
            var result = new Button { Content = title, Classes = { "quiet" }, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            result.Click += (_, _) =>
            {
                box.Text = "";
                ShowPage(item.Page);
                ScrollToSection(item.Anchor);
            };
            SettingsSearchResults.Children.Add(result);
        }
        if (SettingsSearchResults.Children.Count == 0)
            SettingsSearchResults.Children.Add(new TextBlock
            {
                Text = UiText.T("没有匹配的设置项"),
                Classes = { "caption" }
            });
    }

    /// <summary>Best-effort jump to the card whose section heading is the Mac anchor.</summary>
    private void ScrollToSection(string anchor)
    {
        if (string.IsNullOrEmpty(anchor)) return;
        var heading = this.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(block => block.Classes.Contains("section")
                && (string.Equals(block.Text, UiText.T(anchor), StringComparison.Ordinal)
                    || string.Equals(block.Text, anchor, StringComparison.Ordinal)
                    || (block.Text ?? "").Contains(anchor, StringComparison.Ordinal)));
        if (heading is null) return;
        var y = heading.TranslatePoint(new Avalonia.Point(0, 0), SettingsScroll)?.Y ?? 0;
        SettingsScroll.Offset = new Avalonia.Vector(0, Math.Max(0, SettingsScroll.Offset.Y + y - 16));
    }

    private static string PageTitle(string page) => UiText.T(page switch
    {
        "Answer" => "回答与资料",
        "Coach" => "提词与教练",
        "Notes" => "纪要",
        "General" => "通用",
        "Advanced" => "高级",
        _ => "场景"
    });


    /// <summary>E25: the left rail switches the six Mac settings pages.</summary>
    private void SettingsPage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page }) ShowPage(page);
    }

    private async void RefreshDevices_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state) return;
        try
        {
            await state.Settings.RefreshDevicesAsync();
            state.SetStatus("声音设备已刷新。");
        }
        catch (Exception ex) { state.SetStatus("刷新声音设备失败：" + ex.Message, true); }
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void ImportTemplate_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入纪要模板",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        try
        {
            var templates = System.Text.Json.JsonSerializer.Deserialize<List<NextThought.Core.NotesTemplate>>(
                await File.ReadAllTextAsync(path), NextThought.Core.NotesTemplateStore.Options) ?? [];
            if (templates.Count == 0) { state.SetStatus("模板文件里没有可用模板。", true); return; }
            state.AddTemplates(templates);
        }
        catch (System.Text.Json.JsonException ex) { state.SetStatus("模板文件格式无效：" + ex.Message, true); }
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private void SectionUp_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state && sender is Control { Tag: TemplateSectionRow row }) state.MoveTemplateSection(row, -1);
    }

    private void SectionDown_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state && sender is Control { Tag: TemplateSectionRow row }) state.MoveTemplateSection(row, 1);
    }

    private void SectionDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state && sender is Control { Tag: TemplateSectionRow row }) state.RemoveTemplateSection(row);
    }

    private async void ImportSkill_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入技能（Markdown）",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown") { Patterns = ["*.md", "*.markdown", "*.txt"] }
            ]
        });
        var imported = new List<NextThought.Core.Skill>();
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is null) continue;
            try { imported.Add(NextThought.Core.Skill.Parse(await File.ReadAllTextAsync(path), Path.GetFileName(path))); }
            catch (IOException) { }
        }
        if (imported.Count > 0) state.AddSkills(imported);
        else state.SetStatus("没有可导入的技能文件。");
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private async void CopyCompanion_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || state.CompanionLink.Length == 0) return;
        try
        {
            if (state.CopyText is { } copy) await copy(state.CompanionLink);
            state.SetStatus("配对链接已复制。");
        }
        catch (Exception ex) { state.SetStatus("复制配对链接失败：" + ex.Message, true); }
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    /// <summary>E27: Mac APIPresetManager delete confirmation; removes the preset and its key.</summary>
    private async void DeleteApiPreset_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not AppState state || state.Settings.SelectedApiPreset is not { } preset) return;
            if (TopLevel.GetTopLevel(this) is not Window owner) return;
            var confirmed = await new ConfirmDialog(UiText.Format("删除预设“%@”？", preset.Name)).ShowDialog<bool>(owner);
            if (!confirmed) return;
            state.Settings.DeleteActiveApiPresetConfirmed();
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
    }

    /// <summary>E27: Mac GitHubAccessSection 保存并检查 — stores the token, then re-checks the feed.</summary>
    private void SaveGithubToken_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.Settings.SaveGithubTokenCommand.Execute(null);
        if (state.CheckUpdateCommand.CanExecute(null)) state.CheckUpdateCommand.Execute(null);
    }

    /// <summary>E27: Mac Updater.createTokenURL — opens the fine-grained token page.</summary>
    private void CreateGithubToken_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/settings/personal-access-tokens/new")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("打开令牌页面失败：" + ex.Message, true); }
    }

    private void Notices_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        var path = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        if (File.Exists(path)) state.OpenExternal(path);
        else state.SetStatus("未找到第三方许可文件。");
    }

    private async void OpenGlossary_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
        if (DataContext is not AppState state || Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner) return;
        try
        {
            state.RefreshSeriesGlossaryRows();
            await new GlossaryWindow { DataContext = state }.ShowDialog(owner);
        }
        catch (Exception ex) { state.SetStatus("打开术语表失败：" + ex.Message, true); }
    
        }
        catch (Exception ex) { (DataContext as AppState)?.SetStatus("操作失败：" + ex.Message, true); }
        }

    private void SaveTavily_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.SaveTavilyKeyCommand.Execute(state.Settings.TavilyKey);
        state.Settings.TavilyKeyStatus = "已保存（当前 Windows 用户加密）。";
    }

    private async void OpenMyVoice_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not Window owner) return;
        try { await new MyVoiceWindow { DataContext = state }.ShowDialog(owner); }
        catch (Exception ex) { state.SetStatus("打开我的声音失败：" + ex.Message, true); }
    }


    private async void PickScreenRegion_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || TopLevel.GetTopLevel(this) is not MainWindow owner) return;
        try { await owner.PickScreenRegionForWatchAsync(); }
        catch (Exception ex) { state.SetStatus("框选读屏区域失败：" + ex.Message, true); }
    }

    private async void ClearScreenRegion_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        try
        {
            state.Settings.InsightScreenSource = "display";
            state.Settings.InsightScreenRegion = "";
            state.Settings.SelectedInsightSourceLabel = "显示器";
            await state.Settings.SaveAsync();
        }
        catch (Exception ex) { state.SetStatus("清除读屏区域失败：" + ex.Message, true); }
    }

    private void MoveTeleprompter_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button { Tag: string placement }
            && Avalonia.Controls.TopLevel.GetTopLevel(this) is MainWindow main)
            main.MoveTeleprompterTo(placement);
    }

    /// <summary>
    /// E28 / Mac SkillSettings rows: icon (globe for 所有系列, doc otherwise), name, summary or
    /// character count, "N 个系列在用", the 所有系列 / 启用 toggles (saved immediately like
    /// library.update), 编辑 (opens the SkillEditorWindow sheet) and 删除.
    /// </summary>
    private void BuildSkillRows()
    {
        if (DataContext is not AppState state) return;
        SkillListPanel.Children.Clear();
        foreach (var skill in state.EditableSkills)
        {
            var row = new Avalonia.Controls.Grid
            {
                ColumnDefinitions = new Avalonia.Controls.ColumnDefinitions("Auto,*,Auto"),
                Margin = new Avalonia.Thickness(0, 4, 0, 4)
            };
            var icon = new Avalonia.Controls.TextBlock
            {
                Text = (Avalonia.Application.Current?.FindResource(skill.Global ? "IconGlobe" : "IconDocPlaintext") as string) ?? "",
                FontFamily = (Avalonia.Application.Current?.FindResource("IconFont") as Avalonia.Media.FontFamily) ?? Avalonia.Media.FontFamily.Default,
                FontSize = 14,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Opacity = skill.Enabled ? 1 : 0.4
            };
            Avalonia.Controls.Grid.SetColumn(icon, 0);
            var text = new Avalonia.Controls.StackPanel { Spacing = 1, Margin = new Avalonia.Thickness(8, 0, 8, 0) };
            text.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = skill.Name, FontSize = 12, FontWeight = Avalonia.Media.FontWeight.Medium,
                Opacity = skill.Enabled ? 1 : 0.5
            });
            var used = state.SeriesUsingSkill(skill.Id);
            var sub = skill.Summary.Length > 0 ? skill.Summary : UiText.Format("%lld 字 · 单个技能最多使用约 6000 字", skill.Content.Length);
            text.Children.Add(new Avalonia.Controls.TextBlock { Text = sub, FontSize = 10.5, Opacity = 0.6 });
            text.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = used == 0 ? UiText.T("还没有系列使用") : UiText.Format("%lld 个系列在用", used),
                FontSize = 10.5, Opacity = 0.45
            });
            Avalonia.Controls.Grid.SetColumn(text, 1);
            var actions = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var global = new Avalonia.Controls.CheckBox { Content = UiText.T("所有系列"), IsChecked = skill.Global, FontSize = 11 };
            global.IsCheckedChanged += (_, _) =>
            {
                state.SaveSkillEdit(skill with { Global = global.IsChecked == true });
                BuildSkillRows();
            };
            var enabled = new Avalonia.Controls.CheckBox { Content = UiText.T("启用"), IsChecked = skill.Enabled, FontSize = 11 };
            enabled.IsCheckedChanged += (_, _) =>
            {
                state.SaveSkillEdit(skill with { Enabled = enabled.IsChecked == true });
                BuildSkillRows();
            };
            var edit = new Avalonia.Controls.Button { Content = UiText.T("编辑"), FontSize = 11 };
            edit.Click += async (_, _) =>
            {
                if (TopLevel.GetTopLevel(this) is not Window owner) return;
                var edited = await SkillEditorWindow.EditAsync(owner, skill);
                if (edited is null) return;
                state.SaveSkillEdit(edited);
                BuildSkillRows();
            };
            var remove = new Avalonia.Controls.Button { Content = UiText.T("删除"), FontSize = 11 };
            // Mac SkillSettings confirmationDialog: 删除技能“%@”？
            remove.Click += async (_, _) =>
            {
                if (TopLevel.GetTopLevel(this) is not Window owner) return;
                var confirmed = await new ConfirmDialog(UiText.Format("删除技能“%@”？", skill.Name)).ShowDialog<bool>(owner);
                if (!confirmed) return;
                state.DeleteSkill(skill.Id);
                BuildSkillRows();
            };
            actions.Children.Add(global); actions.Children.Add(enabled); actions.Children.Add(edit); actions.Children.Add(remove);
            Avalonia.Controls.Grid.SetColumn(actions, 2);
            row.Children.Add(icon); row.Children.Add(text); row.Children.Add(actions);
            var line = new Avalonia.Controls.Border
            {
                BorderBrush = Avalonia.Media.Brush.Parse("#33ECEBE8"),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 0.5),
                Child = row
            };
            SkillListPanel.Children.Add(line);
        }
    }

    private async void NewSkill_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.AddSkill();
        BuildSkillRows();
        var skill = state.EditableSkills.LastOrDefault();
        if (skill is null || TopLevel.GetTopLevel(this) is not Window owner) return;
        var edited = await SkillEditorWindow.EditAsync(owner, skill);
        if (edited is not null) state.SaveSkillEdit(edited);
        BuildSkillRows();
    }
}
