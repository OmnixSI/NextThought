using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NextThought.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>--settings-scroll lets the screenshot tool show a card below the fold.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (StartupOptions.SettingsScroll is { } offset) SettingsScroll.Offset = new Avalonia.Vector(0, offset);
    }

    private async void RefreshDevices_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        await state.Settings.RefreshDevicesAsync();
        state.SetStatus("声音设备已刷新。");
    }

    private async void ImportTemplate_Click(object? sender, RoutedEventArgs e)
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

    private async void ImportSkill_Click(object? sender, RoutedEventArgs e)
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

    private async void CopyCompanion_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state || state.CompanionLink.Length == 0) return;
        if (state.CopyText is { } copy) await copy(state.CompanionLink);
        state.SetStatus("配对链接已复制。");
    }

    private void Notices_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        var path = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        if (File.Exists(path)) state.OpenExternal(path);
        else state.SetStatus("未找到第三方许可文件。");
    }
}
