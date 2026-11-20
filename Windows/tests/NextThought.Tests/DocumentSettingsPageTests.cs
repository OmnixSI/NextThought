using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E23: the document pipeline settings follow the Mac surfaces — 资料 (reading method) on
/// 回答与资料, 资料组件 on 高级 (runtime, local models, fallback, Codex fields, semantic retrieval),
/// and the importer honours the saved method. No Avalonia page is instantiated here.
/// </summary>
public sealed class DocumentSettingsPageTests
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

    private static string SettingsXaml() => Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));

    /// <summary>The Tag of the settings card that contains <paramref name="anchor"/>.</summary>
    private static string CardTag(string xaml, string anchor)
    {
        var index = xaml.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(index >= 0, "settings card 未找到：" + anchor);
        var start = xaml.LastIndexOf("<Border", index, StringComparison.Ordinal);
        var tagAt = xaml.IndexOf("Tag=\"", start, StringComparison.Ordinal);
        Assert.True(tagAt > 0, "card Tag not found");
        return xaml[(tagAt + 5)..xaml.IndexOf('"', tagAt + 5)];
    }

    [Fact]
    public void TheReadingMethodLivesOnTheAnswerPage()
    {
        var xaml = SettingsXaml();
        Assert.Contains("{loc:Tr 读取资料的方式}", xaml);
        Assert.Contains("Settings.DocumentProviderChoices", xaml);
        Assert.Contains("Settings.SelectedDocumentProvider", xaml);
        Assert.Contains("Settings.DocumentProviderHelp", xaml);
        Assert.Contains("DocumentProviderNeedsSetup", xaml);
        Assert.Contains("PrepareDocumentComponentsCommand", xaml);
        Assert.Contains("UseSystemDocumentReader_Click", xaml);
        Assert.Equal("Answer", CardTag(xaml, "Settings.DocumentProviderChoices"));
    }

    [Fact]
    public void TheDocumentComponentsCardMovedToTheAdvancedPage()
    {
        var xaml = SettingsXaml();
        Assert.Equal("Advanced", CardTag(xaml, "{loc:Tr 资料组件}"));
        foreach (var member in new[]
                 {
                     "Settings.DocumentFallbackToVision", "Settings.ShowCodexDocumentSettings",
                     "Settings.CodexOcrModel", "Settings.CodexOcrEffort", "Settings.CodexOcrTimeout",
                     "DocumentRuntimeStatusText", "DocumentOcrModelsStatusText", "DocumentEmbeddingStatusText",
                     "InstallDocumentRuntimeCommand", "VerifyDocumentRuntimeCommand", "DeleteDocumentRuntimeCommand",
                     "InstallDocumentOcrModelsCommand", "VerifyDocumentOcrModelsCommand", "DeleteDocumentOcrModelsCommand",
                     "PrepareRetrievalCommand", "Settings.UseSemanticRetrieval",
                     "{loc:Tr 本机识别模型}", "{loc:Tr 共用文档组件 · 已安装}", "{loc:Tr 校验／修复}",
                     "{loc:Tr 本机识别失败时用系统识别补读，并标明降级内容}", "{loc:Tr 每个文件超时（秒）}"
                 })
            Assert.Contains(member, xaml);
    }

    [Fact]
    public void TheImporterHonoursTheSavedReadingMethod()
    {
        var library = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "ViewModels", "LibraryViewModel.cs"));
        Assert.Contains("DocumentAssets.UseEnhancedOcr", library);
        Assert.Contains("settings.DocumentOcrProvider", library);
        Assert.Contains("settings.OcrFallbackToVision", library);
        var store = Read(Path.Combine("Windows", "src", "NextThought.Platform", "MaterialStore.cs"));
        Assert.Contains("enhancedOcr", store);
    }

    [Fact]
    public void TheRuntimeInstallerNeedsAnIsolatedPythonAndVerifiesTheWorker()
    {
        var installer = Read(Path.Combine("Windows", "src", "NextThought.Platform", "DocumentRuntimeInstaller.cs"));
        Assert.Contains("-m\", \"venv", installer);
        Assert.Contains("paddle, paddlex", installer);
        Assert.Contains("ocr-requirements-lock.txt", installer);
        var csproj = Read(Path.Combine("Windows", "src", "NextThought.Platform", "NextThought.Platform.csproj"));
        Assert.Contains("Resources/ocr-requirements-lock.txt", csproj);
    }

    [Fact]
    public void TheModelDownloaderUsesThePinnedManifests()
    {
        var assets = Read(Path.Combine("Windows", "src", "NextThought.Platform", "DocumentAssets.cs"));
        Assert.Contains("PaddlePaddle/PaddleOCR-VL-1.6", assets);
        Assert.Contains("132c45f52c0a26ed5a6b2f6bd8e3985794cc27dd", assets);
        Assert.Contains("PaddlePaddle/PP-DocLayoutV3", assets);
        Assert.Contains("241f8bdfc77a7c7bee915a5057aaee58c235a8d3", assets);
        Assert.Contains("ocr-native-assets.json", assets);
        Assert.Contains("SHA256", assets);
        // Local folders of the pinned manifests used by the old preparation scripts.
        Assert.Contains("pp-doclayout-v3", assets);
        Assert.Contains("paddleocr-vl-1.6-native", assets);
    }

    [Fact]
    public void TheMacLabelsComeFromTheStringTable()
    {
        var catalogue = Read(Path.Combine("Windows", "src", "NextThought.Core", "UiTextMac.g.cs"));
        foreach (var key in new[]
                 {
                     "资料", "读取资料的方式", "系统识别", "本机增强", "Codex 代读",
                     "资料组件", "本机识别模型", "共用文档组件 · 已安装", "已安装", "未安装",
                     "安装", "校验／修复", "删除", "准备", "扫描本机已有组件",
                     "导入 Word、Excel、PowerPoint 需要文档组件", "最多下载约 186 MB，只需一次。",
                     "已选的识别方式还没准备好，当前读取会失败。", "先用系统识别",
                     "本机识别失败时用系统识别补读，并标明降级内容",
                     "关闭后，识别失败会让该文件导入失败；不会自动切到在线 Codex。",
                     "Codex 模型", "推理强度", "每个文件超时（秒）", "运行包大小见发布清单",
                     "适合扫描件、表格和公式，在本机计算，占一些内存。",
                     "读取原文件，不用下载识别模型。需要已登录的 Codex，占用账号额度。",
                     "Mac 自带，离线、无需下载。普通文字够用；复杂表格和公式可能丢失结构。"
                 })
            Assert.Contains($"[\"{key}\"]", catalogue);
    }

    [Fact]
    public void TheComponentStateIsLoadedWithTheSettingsPage()
    {
        var viewModel = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "ViewModels", "SettingsViewModel.cs"));
        Assert.Contains("LoadDocumentSettings(settings)", viewModel);
        Assert.Contains("DocumentOcrProvider = SelectedDocumentProvider?.Value", viewModel);
        Assert.Contains("OcrFallbackToVision = DocumentFallbackToVision", viewModel);
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("RefreshDocumentComponents()", code);
    }

    [Fact]
    public void TheDocumentModelPathsStayInsideTheModelDirectory()
    {
        // Pure checks against the shared paths so a buggy manifest cannot escape %LOCALAPPDATA%.
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        Assert.False(DocumentAssets.OcrInstalled(paths));
        Assert.False(DocumentAssets.UseEnhancedOcr(paths, "system", true));
        Assert.False(DocumentAssets.UseEnhancedOcr(paths, "paddle", true));
        Assert.Throws<InvalidOperationException>(() => DocumentAssets.UseEnhancedOcr(paths, "paddle", false));
        Assert.Throws<NotSupportedException>(() => DocumentAssets.UseEnhancedOcr(paths, "codex", true));
        Assert.All(DocumentAssets.Models, model => Assert.True(model.Bytes > 0));
        Assert.True(DocumentAssets.Ocr.Bytes > DocumentAssets.Layout.Bytes);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("nt-doc-settings-").FullName;
        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch (IOException) { }
        }
    }
}
