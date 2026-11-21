using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E23 (remaining): the Mac DocumentModelSettings views exist under their Mac names, the settings
/// page mounts them, the document component rows drive Views/ManagedModelRow, and the reading-method
/// card has the enhanced-recognition guide entry (DocumentChoiceSummary).
/// </summary>
public sealed class DocumentSettingsViewsTests
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

    private static string ViewsDir => Path.Combine(Root(), "Windows", "src", "NextThought.Desktop", "Views");

    [Fact]
    public void TheFourMacDocumentViewsExistWithTheirNames()
    {
        foreach (var (name, kind) in new[]
                 {
                     ("DocumentReadingSettings", "UserControl"),
                     ("DocumentComponentsAdvanced", "UserControl"),
                     ("DocumentChoiceSummary", "Window"),
                     ("ManagedModelRow", "UserControl")
                 })
        {
            var axaml = File.ReadAllText(Path.Combine(ViewsDir, name + ".axaml"));
            var code = File.ReadAllText(Path.Combine(ViewsDir, name + ".axaml.cs"));
            Assert.Contains($"x:Class=\"NextThought.Desktop.Views.{name}\"", axaml);
            Assert.Contains($"partial class {name} : {kind}", code);
        }
    }

    [Fact]
    public void TheSettingsPageMountsTheDocumentViews()
    {
        var xaml = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));
        Assert.Contains("<views:DocumentReadingSettings />", xaml);
        Assert.Contains("<views:DocumentComponentsAdvanced />", xaml);
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.DoesNotContain("UseSystemDocumentReader_Click", code);
    }

    [Fact]
    public void TheReadingSettingsHaveTheEnhancedRecognitionGuide()
    {
        var reading = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DocumentReadingSettings.axaml"));
        Assert.Contains("{loc:Tr 读取资料的方式}", reading);
        Assert.Contains("Settings.DocumentProviderChoices", reading);
        Assert.Contains("{loc:Tr 安装支持}", reading);
        Assert.Contains("{loc:Tr 先用系统识别}", reading);
        Assert.Contains("{loc:Tr 选一种即可。PDF、图片和纯文本可先用系统识别，无需下载文档模型。}", reading);
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DocumentReadingSettings.axaml.cs"));
        Assert.Contains("ShowChoiceSummary_Click", code);
        Assert.Contains("new DocumentChoiceSummary", code);
        var summary = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DocumentChoiceSummary.axaml"));
        Assert.Contains("{loc:Tr 读取资料}", summary);
        Assert.Contains("{loc:Tr 查找资料（向量检索可选）}", summary);
    }

    [Fact]
    public void TheComponentRowsUseTheMacManagedModelRow()
    {
        var state = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "AppState.Documents.cs"));
        Assert.Contains("DocumentComponentRows", state);
        Assert.Contains("new DocumentComponentRow(\"runtime\"", state);
        Assert.Contains("new DocumentComponentRow(\"ocr\"", state);
        Assert.Contains("new DocumentComponentRow(\"embedding\"", state);
        Assert.Contains("RunDocumentComponentActionAsync", state);
        var advanced = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DocumentComponentsAdvanced.axaml.cs"));
        Assert.Contains("new ManagedModelRow", advanced);
        var row = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "ManagedModelRow.axaml"));
        Assert.Contains("{loc:Tr 安装}", row);
        Assert.Contains("{loc:Tr 校验／修复}", row);
        Assert.Contains("{loc:Tr 删除}", row);
    }

    [Fact]
    public void TheSelfTestDrivesTheDocumentViews()
    {
        var test = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "SelfTest", "Checks", "RunSettingsPresetsCheckAsync.cs"));
        Assert.Contains("document-components", test);
        Assert.Contains("document-guide", test);
        Assert.Contains("document-runtime-download", test);
        Assert.Contains("document-reading-settings", test);
        Assert.Contains("document-components-advanced", test);
        Assert.Contains("document-choice-summary", test);
        Assert.Contains("managed-model-row", test);
    }
}
