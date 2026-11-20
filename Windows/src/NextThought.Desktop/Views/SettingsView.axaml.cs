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

    public SettingsView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        RefreshScreens();
        BuildShortcutRows();
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
        CueScreenCombo.Items.Add(new ComboBoxItem { Content = "内置屏 / 主屏（自动）", Tag = "" });
        foreach (var screen in top.Screens.All)
        {
            var label = screen.DisplayName ?? "显示器";
            if (ReferenceEquals(screen, top.Screens.Primary)) label += "（主屏）";
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

    private void ShowPage(string page)
    {
        currentPage = page;
        foreach (var card in this.GetVisualDescendants().OfType<Border>()
                     .Where(border => border.Classes.Contains("card") && border.Name != "SettingsSearchCard"))
            card.IsVisible = card.Tag as string == page;
        SettingsScroll.Offset = new Avalonia.Vector(0, 0);
    }

    /// <summary>Filters the visible setting cards by their text (Mac settings search).</summary>
    private void SettingsSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var query = box.Text?.Trim() ?? "";
        foreach (var card in this.GetVisualDescendants().OfType<Border>()
                     .Where(border => border.Classes.Contains("card") && border.Name != "SettingsSearchCard"))
        {
            if (query.Length == 0) { card.IsVisible = card.Tag as string == currentPage; continue; }
            var text = string.Join(" ", card.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? "")
                .Concat(card.GetVisualDescendants().OfType<ContentControl>().Select(control => control.Content as string ?? "")));
            card.IsVisible = card.Tag as string == currentPage && text.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }


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

    private string? recordingShortcutId;

    /// <summary>G4: one row per Mac hotkey action; click the binding to record a key combination.</summary>
    private void BuildShortcutRows()
    {
        if (DataContext is not AppState state) return;
        ShortcutRecorderPanel.Children.Clear();
        var bindings = state.ShortcutText.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var action in NextThought.Core.ShortcutKeys.Actions)
        {
            var grid = new Avalonia.Controls.Grid
            {
                ColumnDefinitions = new Avalonia.Controls.ColumnDefinitions("*,Auto,Auto,Auto")
            };
            grid.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = action.Label, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            var value = new Avalonia.Controls.Button
            {
                Content = bindings.TryGetValue(action.Id, out var text)
                    ? text
                    : NextThought.Core.ShortcutKeys.Format(action.DefaultModifiers, action.DefaultKey),
                Tag = action.Id,
                FontFamily = new Avalonia.Media.FontFamily("Consolas,monospace"),
                FontSize = 11,
                MinWidth = 140
            };
            value.Click += (_, _) => BeginShortcutRecording(action.Id, value);
            value.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, ShortcutRecorder_KeyDown,
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Avalonia.Controls.Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            var clear = new Avalonia.Controls.Button { Content = "清除", Tag = action.Id, FontSize = 11, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
            clear.Click += (_, _) => ApplyShortcut(action.Id, action.DefaultModifiers, action.DefaultKey);
            Avalonia.Controls.Grid.SetColumn(clear, 2);
            grid.Children.Add(clear);
            var restore = new Avalonia.Controls.Button { Content = "默认", Tag = action.Id, FontSize = 11, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
            restore.Click += (_, _) => ApplyShortcut(action.Id, action.DefaultModifiers, action.DefaultKey);
            Avalonia.Controls.Grid.SetColumn(restore, 3);
            grid.Children.Add(restore);
            ShortcutRecorderPanel.Children.Add(grid);
        }
    }

    private void BeginShortcutRecording(string id, Avalonia.Controls.Button button)
    {
        recordingShortcutId = id;
        button.Content = "按下组合键…";
        button.Focus();
        ShortcutRecorderStatus.Text = "按下带 Ctrl/Alt/Shift/Win 的组合键；Esc 取消。";
    }

    private void ShortcutRecorder_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (sender is not Avalonia.Controls.Button { Tag: string id } button || recordingShortcutId != id) return;
        e.Handled = true;
        if (e.Key == Avalonia.Input.Key.Escape && e.KeyModifiers == Avalonia.Input.KeyModifiers.None)
        {
            recordingShortcutId = null;
            ShortcutRecorderStatus.Text = "";
            BuildShortcutRows();
            return;
        }
        var modifiers = 0u;
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control)) modifiers |= NextThought.Core.ShortcutKeys.ControlModifier;
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt)) modifiers |= NextThought.Core.ShortcutKeys.AltModifier;
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)) modifiers |= NextThought.Core.ShortcutKeys.ShiftModifier;
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta)) modifiers |= NextThought.Core.ShortcutKeys.WindowsModifier;
        var key = ToVirtualKey(e.Key);
        if (key == 0) { ShortcutRecorderStatus.Text = "这个键不能用作全局快捷键。"; return; }
        if (modifiers == 0)
        {
            ShortcutRecorderStatus.Text = "快捷键必须包含 Ctrl、Alt 或 Shift，不能只使用普通按键。";
            return;
        }
        ApplyShortcut(id, modifiers, key);
        ShortcutRecorderStatus.Text = "已改为 " + NextThought.Core.ShortcutKeys.Format(modifiers, key) + "，立即生效。";
    }

    private void ApplyShortcut(string id, uint modifiers, uint key)
    {
        if (DataContext is not AppState state) return;
        recordingShortcutId = null;
        var lines = state.ShortcutText.Replace("\r\n", "\n").Split('\n').ToList();
        var replacement = id + "=" + NextThought.Core.ShortcutKeys.Format(modifiers, key);
        var index = lines.FindIndex(line => line.TrimStart().StartsWith(id + "=", StringComparison.OrdinalIgnoreCase));
        if (index >= 0) lines[index] = replacement; else lines.Add(replacement);
        state.ShortcutText = string.Join("\n", lines);
        state.SaveShortcutsCommand.Execute(null);
        BuildShortcutRows();
    }

    private static uint ToVirtualKey(Avalonia.Input.Key key) => key switch
    {
        Avalonia.Input.Key.OemComma => 0xBC,
        Avalonia.Input.Key.OemPeriod => 0xBE,
        Avalonia.Input.Key.OemQuestion => 0xBF,
        Avalonia.Input.Key.OemMinus => 0xBD,
        Avalonia.Input.Key.OemPlus => 0xBB,
        Avalonia.Input.Key.OemOpenBrackets => 0xDB,
        Avalonia.Input.Key.OemCloseBrackets => 0xDD,
        Avalonia.Input.Key.OemBackslash => 0xDC,
        Avalonia.Input.Key.OemSemicolon => 0xBA,
        Avalonia.Input.Key.OemQuotes => 0xDE,
        Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return => 0x0D,
        Avalonia.Input.Key.Escape => 0x1B,
        Avalonia.Input.Key.Space => 0x20,
        Avalonia.Input.Key.PageUp => 0x21,
        Avalonia.Input.Key.PageDown => 0x22,
        Avalonia.Input.Key.Left => 0x25,
        Avalonia.Input.Key.Up => 0x26,
        Avalonia.Input.Key.Right => 0x27,
        Avalonia.Input.Key.Down => 0x28,
        _ => NextThought.Core.ShortcutKeys.Parse(key.ToString(), 0)
    };

    /// <summary>E28: one editor per skill, with create/delete/install-demo.</summary>
    private void BuildSkillRows()
    {
        if (DataContext is not AppState state) return;
        SkillEditorPanel.Children.Clear();
        foreach (var skill in state.EditableSkills)
        {
            var card = new Avalonia.Controls.Border
            {
                BorderBrush = Avalonia.Media.Brush.Parse("#33ECEBE8"),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 0.5),
                Padding = new Avalonia.Thickness(0, 6, 0, 6)
            };
            var stack = new Avalonia.Controls.StackPanel { Spacing = 4 };
            var header = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            var name = new Avalonia.Controls.TextBox { Text = skill.Name, Width = 160, FontSize = 11 };
            var summary = new Avalonia.Controls.TextBox { Text = skill.Summary, Watermark = "摘要", Width = 260, FontSize = 11 };
            var enabled = new Avalonia.Controls.CheckBox { Content = "启用", IsChecked = skill.Enabled, FontSize = 11 };
            var global = new Avalonia.Controls.CheckBox { Content = "全部会议", IsChecked = skill.Global, FontSize = 11 };
            header.Children.Add(name); header.Children.Add(summary); header.Children.Add(enabled); header.Children.Add(global);
            var content = new Avalonia.Controls.TextBox
            {
                Text = skill.Content, AcceptsReturn = true, Height = 70, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 11, Watermark = "这个技能的要求（Markdown）"
            };
            var actions = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            var save = new Avalonia.Controls.Button { Content = "保存", FontSize = 11 };
            save.Click += (_, _) =>
            {
                state.SaveSkillEdit(skill with
                {
                    Name = name.Text ?? "", Summary = summary.Text ?? "", Content = content.Text ?? "",
                    Enabled = enabled.IsChecked == true, Global = global.IsChecked == true
                });
                BuildSkillRows();
            };
            var remove = new Avalonia.Controls.Button { Content = "删除", FontSize = 11 };
            remove.Click += (_, _) => { state.DeleteSkill(skill.Id); BuildSkillRows(); };
            actions.Children.Add(save); actions.Children.Add(remove);
            stack.Children.Add(header); stack.Children.Add(content); stack.Children.Add(actions);
            card.Child = stack;
            SkillEditorPanel.Children.Add(card);
        }
    }

    private void NewSkill_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.AddSkill();
        BuildSkillRows();
    }

    private void InstallDemoSkill_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        state.InstallDemoSkill();
        BuildSkillRows();
    }
}
