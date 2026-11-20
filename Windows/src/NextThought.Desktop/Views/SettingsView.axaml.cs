using System.IO;
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
        ShowPage(currentPage);
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
}
