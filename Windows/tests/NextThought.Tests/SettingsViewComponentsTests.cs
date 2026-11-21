using Xunit;

namespace NextThought.Tests;

/// <summary>
/// R6 / §6.7: the three remaining settings views that VIEW_PARITY lists as Mac 3.4.0 UI -
/// UpdateAdvanced (高级 → 更新渠道 + GitHub 访问), ShortcutSettingsView (the hotkey editor) and
/// ShortcutsSettingsRow (the collapsible 快捷键… row on the 提词与教练 page).
/// </summary>
public sealed class SettingsViewComponentsTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "NextThought.Windows.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    [Fact]
    public void UpdateAdvancedLivesOnTheAdvancedPageWithTheChannelAndGithubAccess()
    {
        var view = Read("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml");
        Assert.Contains("{loc:Tr 更新（高级）}", view);
        Assert.Contains("{loc:Tr 更新渠道}", view);
        Assert.Contains("{loc:Tr GitHub 访问}", view);
        Assert.Contains("ItemsSource=\"{Binding UpdateChannels}\"", view);
        // One channel picker in the whole page, and it sits in the Advanced card (Mac UpdateAdvanced),
        // not in the General update card (Mac general UpdateSettings has no channel row).
        Assert.Equal(1, view.Split("{Binding UpdateChannels}").Length - 1);
        var advancedCard = view.IndexOf("{loc:Tr 更新（高级）}", StringComparison.Ordinal);
        var picker = view.IndexOf("{Binding UpdateChannels}", StringComparison.Ordinal);
        Assert.True(advancedCard > 0 && picker > advancedCard);
        // The GitHub access controls are part of that card now, not of the Codex card.
        var codex = view.IndexOf("回答服务（Codex）", StringComparison.Ordinal);
        Assert.True(codex > 0 && codex < advancedCard);
        Assert.DoesNotContain("回答服务的 GitHub 访问", view);
        // The general update card still has the check/download actions.
        Assert.Contains("{loc:Tr 检查更新}", view);
    }

    [Fact]
    public void TheShortcutEditorIsTheMacShortcutSettingsView()
    {
        var editor = Read("Windows", "src", "NextThought.Desktop", "Views", "ShortcutSettingsView.axaml");
        Assert.Contains("x:Class=\"NextThought.Desktop.Views.ShortcutSettingsView\"", editor);
        Assert.Contains("{loc:Tr 全局快捷键（Ctrl+Alt + 按键）}", editor);
        Assert.Contains("{loc:Tr 恢复默认快捷键}", editor);
        Assert.Contains("x:Name=\"ShortcutRecorderPanel\"", editor);
        var code = Read("Windows", "src", "NextThought.Desktop", "Views", "ShortcutSettingsView.axaml.cs");
        Assert.Contains("ResetShortcuts_Click", code);
        Assert.Contains("ShortcutRecorder_KeyDown", code);
        Assert.Contains("SaveShortcutsCommand", code);
        Assert.Contains("ShortcutKeys.Actions", code);
    }

    [Fact]
    public void TheShortcutsRowExpandsInPlaceOnTheCoachPage()
    {
        var row = Read("Windows", "src", "NextThought.Desktop", "Views", "ShortcutsSettingsRow.axaml");
        Assert.Contains("{loc:Tr 快捷键…}", row);
        Assert.Contains("<views:ShortcutSettingsView", row);
        Assert.Contains("x:Name=\"Editor\"", row);
        Assert.Contains("Tag=\"Coach\"", row);
        var code = Read("Windows", "src", "NextThought.Desktop", "Views", "ShortcutsSettingsRow.axaml.cs");
        Assert.Contains("SetExpanded", code);
        Assert.Contains("Editor.IsVisible", code);
        // The 提词与教练 page shows the row instead of the old always-open shortcut card.
        var settings = Read("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml");
        Assert.Contains("<views:ShortcutsSettingsRow />", settings);
        Assert.DoesNotContain("x:Name=\"ShortcutRecorderPanel\"", settings);
        Assert.DoesNotContain("{loc:Tr 全局快捷键（Ctrl+Alt + 按键）}", settings);
    }
}
