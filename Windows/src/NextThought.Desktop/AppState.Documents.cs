using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E23: document reading setup (Mac DocumentReadingSettings / DocumentComponentsAdvanced /
/// DocumentRuntimeInstaller). Windows reads PDF/Office/text with built-in readers; the optional
/// on-device enhancement is the PaddleOCR-VL models plus an isolated Python 3.12 environment.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private bool documentRuntimeInstalled;
    [ObservableProperty] private bool documentOcrModelsInstalled;
    [ObservableProperty] private bool documentEmbeddingInstalled;
    [ObservableProperty] private bool documentSetupBusy;
    [ObservableProperty] private double documentSetupProgress;
    [ObservableProperty] private string documentSetupStatus = "";
    [ObservableProperty] private string documentRuntimeDetail = "";
    [ObservableProperty] private string documentOcrModelsDetail = "";
    [ObservableProperty] private string documentEmbeddingDetail = "";

    /// <summary>E23: one row per document component, rendered by Views/ManagedModelRow (Mac ManagedModelRow).</summary>
    public ObservableCollection<DocumentComponentRow> DocumentComponentRows { get; } = [];

    public bool DocumentProviderReady => (Settings.SelectedDocumentProvider?.Value ?? "system") switch
    {
        "paddle" => DocumentRuntimeInstalled && DocumentOcrModelsInstalled,
        "codex" => CodexDocumentReader.IsAvailable(),
        _ => true
    };

    public bool DocumentProviderNeedsSetup => !DocumentProviderReady && Settings.SelectedDocumentProvider?.Value is "paddle" or "codex";

    public string DocumentProviderStatus => DocumentProviderReady
        ? ""
        : Settings.SelectedDocumentProvider?.Value == "codex"
            ? UiText.T("未检测到 Codex")
            : UiText.T("已选的识别方式还没准备好，当前读取会失败。");

    public string DocumentRuntimeStatusText => UiText.T(DocumentRuntimeInstalled ? "已安装" : "未安装");
    public string DocumentOcrModelsStatusText => UiText.T(DocumentOcrModelsInstalled ? "已安装" : "未安装");
    public string DocumentEmbeddingStatusText => UiText.T(DocumentEmbeddingInstalled ? "已安装" : "未安装");

    partial void OnDocumentRuntimeInstalledChanged(bool value) => NotifyDocumentProvider();
    partial void OnDocumentOcrModelsInstalledChanged(bool value) => NotifyDocumentProvider();

    private void NotifyDocumentProvider()
    {
        OnPropertyChanged(nameof(DocumentProviderReady));
        OnPropertyChanged(nameof(DocumentProviderNeedsSetup));
        OnPropertyChanged(nameof(DocumentProviderStatus));
        OnPropertyChanged(nameof(DocumentRuntimeStatusText));
        OnPropertyChanged(nameof(DocumentOcrModelsStatusText));
        OnPropertyChanged(nameof(DocumentEmbeddingStatusText));
    }

    /// <summary>Re-reads installed state; called when the settings page opens or a component changes.</summary>
    public void RefreshDocumentComponents()
    {
        var runtime = new DocumentRuntimeInstaller(Paths);
        DocumentRuntimeInstalled = runtime.Installed;
        DocumentOcrModelsInstalled = DocumentAssets.OcrInstalled(Paths);
        DocumentEmbeddingInstalled = new EmbeddingModelStore(Paths).IsInstalled;
        DocumentRuntimeDetail = runtime.Installed
            ? runtime.Python
            : UiText.T("运行包大小见发布清单");
        DocumentOcrModelsDetail = FormatBytes(DocumentAssets.Ocr.Bytes + DocumentAssets.Layout.Bytes);
        DocumentEmbeddingDetail = FormatBytes(EmbeddingModelStore.Manifest.Files.Sum(file => file.Size));
        RefreshDocumentComponentRows();
        NotifyDocumentProvider();
    }

    private void RefreshDocumentComponentRows()
    {
        DocumentComponentRows.Clear();
        var installed = UiText.T("已安装");
        var missing = UiText.T("未安装");
        DocumentComponentRows.Add(new DocumentComponentRow("runtime", UiText.T("共用文档组件"),
            DocumentRuntimeDetail, DocumentRuntimeInstalled ? installed : missing, DocumentRuntimeInstalled));
        DocumentComponentRows.Add(new DocumentComponentRow("ocr", "PaddleOCR-VL 1.6 + PP-DocLayoutV3",
            DocumentOcrModelsDetail, DocumentOcrModelsInstalled ? installed : missing, DocumentOcrModelsInstalled));
        DocumentComponentRows.Add(new DocumentComponentRow("embedding", "Qwen3-Embedding-0.6B Q8",
            DocumentEmbeddingDetail, DocumentEmbeddingInstalled ? installed : missing, DocumentEmbeddingInstalled));
    }

    /// <summary>Mac ManagedModelRow buttons (准备/cancel/校验／修复/删除) for one component row.</summary>
    public async Task RunDocumentComponentActionAsync(DocumentComponentRow row, string action)
    {
        switch (row.Id)
        {
            case "runtime":
                if (action == "install") await InstallDocumentRuntimeAsync();
                else if (action == "verify") await VerifyDocumentRuntimeAsync();
                else DeleteDocumentRuntime();
                break;
            case "ocr":
                if (action == "install") await InstallDocumentOcrModelsAsync();
                else if (action == "verify") await VerifyDocumentOcrModelsAsync();
                else DeleteDocumentOcrModels();
                break;
            case "embedding":
                if (action == "install") await PrepareRetrievalAsync();
                else if (action == "verify") await VerifyEmbeddingAsync();
                else DeleteEmbeddingModel();
                break;
        }
    }

    private async Task VerifyEmbeddingAsync()
    {
        if (DocumentSetupBusy) return;
        DocumentSetupBusy = true;
        try
        {
            await new EmbeddingModelStore(Paths).VerifyAsync(CancellationToken.None);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
        finally { DocumentSetupBusy = false; }
    }

    private void DeleteEmbeddingModel()
    {
        try
        {
            var store = new EmbeddingModelStore(Paths);
            var root = Path.GetFullPath(store.Root);
            if (!root.StartsWith(Path.GetFullPath(Paths.Models) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("语义模型路径无效。");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000.0:0.0} GB" : $"{bytes / 1_000_000.0:0} MB";

    /// <summary>Mac prepareDocumentFeature(.office/.paddle): install whatever the chosen reader needs.</summary>
    [RelayCommand]
    private async Task PrepareDocumentComponentsAsync()
    {
        if (DocumentSetupBusy) return;
        DocumentSetupBusy = true;
        DocumentSetupStatus = "";
        DocumentSetupProgress = 0;
        try
        {
            var progress = new Progress<ModelProgress>(value =>
            {
                DocumentSetupProgress = value.Fraction;
                DocumentSetupStatus = value.Message;
            });
            var runtime = new DocumentRuntimeInstaller(Paths);
            if (!runtime.Installed)
            {
                var lines = new Progress<string>(line => DocumentSetupStatus = line);
                await runtime.InstallAsync(lines);
            }
            if (!DocumentAssets.OcrInstalled(Paths))
            {
                await DocumentAssets.InstallAsync(Paths, DocumentAssets.Ocr, progress);
                await DocumentAssets.InstallAsync(Paths, DocumentAssets.Layout, progress);
            }
            DocumentSetupProgress = 1;
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception)
        {
            DocumentSetupStatus = exception.Message;
        }
        finally { DocumentSetupBusy = false; }
    }

    [RelayCommand]
    private async Task InstallDocumentRuntimeAsync()
    {
        if (DocumentSetupBusy) return;
        DocumentSetupBusy = true;
        try
        {
            var lines = new Progress<string>(line => DocumentSetupStatus = line);
            await new DocumentRuntimeInstaller(Paths).InstallAsync(lines);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
        finally { DocumentSetupBusy = false; }
    }

    [RelayCommand]
    private async Task VerifyDocumentRuntimeAsync()
    {
        if (DocumentSetupBusy) return;
        DocumentSetupBusy = true;
        try
        {
            var lines = new Progress<string>(line => DocumentSetupStatus = line);
            await new DocumentRuntimeInstaller(Paths).VerifyAsync(lines);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
        finally { DocumentSetupBusy = false; }
    }

    [RelayCommand]
    private void DeleteDocumentRuntime()
    {
        try
        {
            new DocumentRuntimeInstaller(Paths).Delete();
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
    }

    [RelayCommand]
    private async Task InstallDocumentOcrModelsAsync()
    {
        if (DocumentSetupBusy) return;
        DocumentSetupBusy = true;
        try
        {
            var progress = new Progress<ModelProgress>(value =>
            {
                DocumentSetupProgress = value.Fraction;
                DocumentSetupStatus = value.Message;
            });
            await DocumentAssets.InstallAsync(Paths, DocumentAssets.Ocr, progress);
            await DocumentAssets.InstallAsync(Paths, DocumentAssets.Layout, progress);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
        finally { DocumentSetupBusy = false; }
    }

    [RelayCommand]
    private async Task VerifyDocumentOcrModelsAsync()
    {
        if (DocumentSetupBusy) return;
        DocumentSetupBusy = true;
        try
        {
            await DocumentAssets.VerifyAsync(Paths, DocumentAssets.Ocr);
            await DocumentAssets.VerifyAsync(Paths, DocumentAssets.Layout);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
        finally { DocumentSetupBusy = false; }
    }

    [RelayCommand]
    private void DeleteDocumentOcrModels()
    {
        try
        {
            DocumentAssets.Delete(Paths, DocumentAssets.Ocr);
            DocumentAssets.Delete(Paths, DocumentAssets.Layout);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (Exception exception) { DocumentSetupStatus = exception.Message; }
    }

}

/// <summary>E23: one installed/missing document component (Mac ManagedModelRow input).</summary>
public sealed record DocumentComponentRow(string Id, string Name, string Detail, string Status, bool Installed);
