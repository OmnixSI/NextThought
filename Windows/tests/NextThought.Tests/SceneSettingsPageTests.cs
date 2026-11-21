using Xunit;

namespace NextThought.Tests;

/// <summary>E26: the scene page exposes the Mac SceneRow fields and reset, and the quick panel has
/// the Mac 提词字号 / 提词岛一直展开 / 双屏演讲 rows.</summary>
public sealed class SceneSettingsPageTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    [Fact]
    public void TheSceneRowHasTheFourMacColumnsAndReset()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        foreach (var column in new[] { "LiveMode", "AnswerStyle", "NotesTemplate", "CueStyle" })
            Assert.Contains($"Tag=\"{column}\"", xaml);
        Assert.Contains("{loc:Tr 恢复默认}", xaml);
        Assert.Contains("IsVisible=\"{Binding Customised}\"", xaml);
        Assert.Contains("ResetScene_Click", Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs")));
        Assert.Contains("{loc:Tr 开始一场会时按这里的设置来；会中仍可随时切换。}", xaml);
    }

    [Fact]
    public void TheSceneRowUsesPerModeDefaultsAndAllTemplates()
    {
        var row = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "SceneSettingRow.cs"));
        Assert.Contains("SceneDefaults.For(mode)", row);
        Assert.Contains("SceneTemplateOption", row);
        Assert.Contains("public bool Customised", row);
        Assert.Contains("RestoreDefaults()", row);
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.SceneSettings.cs"));
        Assert.Contains("SceneTemplateOptions()", state);
        Assert.Contains("SceneDefaults.For(mode)", state);
        var templates = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Notes.cs"));
        Assert.Contains("DefaultTemplateIdFor(SelectedMode)", templates);
    }

    [Fact]
    public void TheQuickPanelHasTheMissingMacRows()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "QuickSettingsPanel.axaml"));
        Assert.Contains("{loc:Tr 提词字号}", xaml);
        Assert.Contains("Settings.TeleprompterFontSize", xaml);
        Assert.Contains("{loc:Tr 提词岛一直展开}", xaml);
        Assert.Contains("{loc:Tr 双屏演讲}", xaml);
        Assert.Contains("Settings.PresenterSummary", xaml);
    }
}
