using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E23/E30: document reading setup (Mac DocumentReadingSettings / DocumentComponentsAdvanced /
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
    [ObservableProperty] private string documentSetupCancelLabel = "取消准备";
    [ObservableProperty] private string documentRuntimeDetail = "";
    [ObservableProperty] private string documentOcrModelsDetail = "";
    [ObservableProperty] private string documentEmbeddingDetail = "";

    /// <summary>§3.5: Mac LocalDocumentAssets — scanning existing on-disk model files.</summary>
    [ObservableProperty] private bool documentAssetsScanning;
    [ObservableProperty] private string documentAssetsScanStatus = "";

    private CancellationTokenSource? documentSetupCts;
    private CancellationTokenSource? documentAssetScanCts;

    public bool CanCancelDocumentSetup => DocumentSetupBusy;
    public bool CanCancelDocumentScan => DocumentAssetsScanning;

    partial void OnDocumentSetupBusyChanged(bool value) => OnPropertyChanged(nameof(CanCancelDocumentSetup));
    partial void OnDocumentAssetsScanningChanged(bool value) => OnPropertyChanged(nameof(CanCancelDocumentScan));

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
        // §3.5: Mac ManagedModelRow note = size · license · summary, plus the 模型来源与版本 link.
        DocumentComponentRows.Add(new DocumentComponentRow("runtime", UiText.T("共用文档组件"),
            DocumentRuntimeDetail, DocumentRuntimeInstalled ? installed : missing, DocumentRuntimeInstalled,
            UiText.T("第三方组件"), UiText.T("隔离的 Python 3.12 环境，读取 PDF、Office 与文本文件。"), ""));
        DocumentComponentRows.Add(new DocumentComponentRow("ocr", "PaddleOCR-VL 1.6 + PP-DocLayoutV3",
            DocumentOcrModelsDetail, DocumentOcrModelsInstalled ? installed : missing, DocumentOcrModelsInstalled,
            "Apache-2.0", UiText.T("按版面区域识别中英正文、表格与公式。"),
            $"https://huggingface.co/{DocumentAssets.OcrRepository}/tree/{DocumentAssets.OcrRevision}"));
        DocumentComponentRows.Add(new DocumentComponentRow("embedding", "Qwen3-Embedding-0.6B Q8",
            DocumentEmbeddingDetail, DocumentEmbeddingInstalled ? installed : missing, DocumentEmbeddingInstalled,
            "Apache-2.0", UiText.T("中英语义检索，1024 维向量。"),
            $"https://huggingface.co/{EmbeddingModelStore.Manifest.Repository}/tree/{EmbeddingModelStore.Manifest.Revision}"));
    }

    /// <summary>Mac ManagedModelRow buttons (准备/取消/校验／修复/删除) for one component row.</summary>
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

    // MARK: - cancellation (Mac documentSetupTask / runtimeDownloadTask / localAssetScanTask cancel())

    private CancellationToken BeginDocumentSetup(string status, string? cancelLabel = null)
    {
        documentSetupCts?.Dispose();
        documentSetupCts = new CancellationTokenSource();
        DocumentSetupBusy = true;
        DocumentSetupProgress = 0;
        DocumentSetupStatus = status;
        DocumentSetupCancelLabel = UiText.T(cancelLabel ?? "取消准备");
        return documentSetupCts.Token;
    }

    private void EndDocumentSetup()
    {
        DocumentSetupBusy = false;
        DocumentSetupProgress = 0;
        documentSetupCts?.Dispose();
        documentSetupCts = null;
    }

    private void ReportDocumentSetupError(Exception exception)
    {
        DocumentSetupStatus = exception is OperationCanceledException
            ? UiText.T("下载已取消。")
            : exception.Message;
    }

    /// <summary>Mac 取消准备 / 取消下载组件 / 取消（扫描）share the running task's cancellation.</summary>
    [RelayCommand]
    private void CancelDocumentSetup()
    {
        documentSetupCts?.Cancel();
        documentAssetScanCts?.Cancel();
    }

    private async Task VerifyEmbeddingAsync()
    {
        if (DocumentSetupBusy) return;
        var ct = BeginDocumentSetup(UiText.T("正在校验文档组件…"));
        try
        {
            await new EmbeddingModelStore(Paths).VerifyAsync(ct);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (OperationCanceledException) { ReportDocumentSetupError(new OperationCanceledException()); }
        catch (Exception exception) { ReportDocumentSetupError(exception); }
        finally { EndDocumentSetup(); }
    }

    private void DeleteEmbeddingModel()
    {
        try
        {
            var store = new EmbeddingModelStore(Paths);
            var root = Path.GetFullPath(store.Root);
            if (!root.StartsWith(Path.GetFullPath(Paths.Models) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(UiText.T("语义模型路径无效。"));
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
        var ct = BeginDocumentSetup("");
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
                await runtime.InstallAsync(lines, ct);
            }
            if (!DocumentAssets.OcrInstalled(Paths))
            {
                await DocumentAssets.InstallAsync(Paths, DocumentAssets.Ocr, progress, ct);
                await DocumentAssets.InstallAsync(Paths, DocumentAssets.Layout, progress, ct);
            }
            DocumentSetupProgress = 1;
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (OperationCanceledException) { ReportDocumentSetupError(new OperationCanceledException()); }
        catch (Exception exception) { ReportDocumentSetupError(exception); }
        finally { EndDocumentSetup(); }
    }

    [RelayCommand]
    private async Task InstallDocumentRuntimeAsync()
    {
        if (DocumentSetupBusy) return;
        var ct = BeginDocumentSetup(UiText.T("正在下载文档运行包…"), "取消下载组件");
        try
        {
            var lines = new Progress<string>(line => DocumentSetupStatus = line);
            await new DocumentRuntimeInstaller(Paths).InstallAsync(lines, ct);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (OperationCanceledException) { ReportDocumentSetupError(new OperationCanceledException()); }
        catch (Exception exception) { ReportDocumentSetupError(exception); }
        finally { EndDocumentSetup(); }
    }

    [RelayCommand]
    private async Task VerifyDocumentRuntimeAsync()
    {
        if (DocumentSetupBusy) return;
        var ct = BeginDocumentSetup(UiText.T("正在校验文档组件…"));
        try
        {
            var lines = new Progress<string>(line => DocumentSetupStatus = line);
            await new DocumentRuntimeInstaller(Paths).VerifyAsync(lines, ct);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (OperationCanceledException) { ReportDocumentSetupError(new OperationCanceledException()); }
        catch (Exception exception) { ReportDocumentSetupError(exception); }
        finally { EndDocumentSetup(); }
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
        var ct = BeginDocumentSetup("");
        try
        {
            var progress = new Progress<ModelProgress>(value =>
            {
                DocumentSetupProgress = value.Fraction;
                DocumentSetupStatus = value.Message;
            });
            await DocumentAssets.InstallAsync(Paths, DocumentAssets.Ocr, progress, ct);
            await DocumentAssets.InstallAsync(Paths, DocumentAssets.Layout, progress, ct);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (OperationCanceledException) { ReportDocumentSetupError(new OperationCanceledException()); }
        catch (Exception exception) { ReportDocumentSetupError(exception); }
        finally { EndDocumentSetup(); }
    }

    [RelayCommand]
    private async Task VerifyDocumentOcrModelsAsync()
    {
        if (DocumentSetupBusy) return;
        var ct = BeginDocumentSetup(UiText.T("正在校验文档组件…"));
        try
        {
            await DocumentAssets.VerifyAsync(Paths, DocumentAssets.Ocr, ct);
            await DocumentAssets.VerifyAsync(Paths, DocumentAssets.Layout, ct);
            DocumentSetupStatus = "";
            RefreshDocumentComponents();
        }
        catch (OperationCanceledException) { ReportDocumentSetupError(new OperationCanceledException()); }
        catch (Exception exception) { ReportDocumentSetupError(exception); }
        finally { EndDocumentSetup(); }
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

    // MARK: - local discovery (Mac LocalDocumentAssets.scanLocalDocumentAssets / chooseLocalDocumentAssets)

    /// <summary>Mac 扫描本机已有组件: find the pinned model files on disk, verify them and reuse them.</summary>
    [RelayCommand]
    private Task ScanLocalDocumentAssetsAsync() => ScanLocalDocumentAssetsAsync(null, null);

    [RelayCommand]
    private async Task ChooseLocalDocumentAssetsAsync()
    {
        if (PickFolder is null || DocumentSetupBusy || DocumentAssetsScanning) return;
        var directory = await PickFolder(UiText.T("选择已下载权重或运行包所在目录，应用会检查兼容性并复用，原文件保留。"));
        if (directory is null) return;
        await ScanLocalDocumentAssetsAsync(directory, null);
    }

    /// <summary>The scan itself; <paramref name="directory"/> null means the Mac default search roots.</summary>
    public async Task ScanLocalDocumentAssetsAsync(string? directory, IProgress<string>? progress)
    {
        if (DocumentSetupBusy || DocumentAssetsScanning) return;
        documentAssetScanCts?.Dispose();
        documentAssetScanCts = new CancellationTokenSource();
        var ct = documentAssetScanCts.Token;
        DocumentAssetsScanning = true;
        DocumentAssetsScanStatus = UiText.T("正在查找本机已有组件，匹配版本后校验并复用…");
        try
        {
            var roots = directory is null ? LocalDocumentAssetRoots() : [directory];
            var reused = await Task.Run(() => ReuseLocalDocumentAssets(roots, progress, ct), ct);
            ct.ThrowIfCancellationRequested();
            RefreshDocumentComponents();
            DocumentAssetsScanStatus = reused.Models == 0
                ? UiText.T("未找到匹配部分，可选择其他目录或按需下载。")
                : UiText.Format("本应用现有 %lld 项文档模型，支持组件未安装。本次复用 %@，没有联网下载。",
                    DocumentAssets.Models.Count, FormatBytes(reused.Bytes));
        }
        catch (OperationCanceledException)
        {
            DocumentAssetsScanStatus = UiText.T("已取消扫描；已校验并复用的文件保留，原文件没有改变。");
        }
        catch (Exception exception)
        {
            DocumentAssetsScanStatus = UiText.Format("本机扫描失败：%@；可选择具体目录重试。", exception.Message);
        }
        finally
        {
            DocumentAssetsScanning = false;
            documentAssetScanCts?.Dispose();
            documentAssetScanCts = null;
        }
    }

    private static IReadOnlyList<string> LocalDocumentAssetRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("HF_HUB_CACHE"),
            Environment.GetEnvironmentVariable("HF_HOME") is { Length: > 0 } hf ? Path.Combine(hf, "hub") : null,
            home.Length > 0 ? Path.Combine(home, ".cache", "huggingface", "hub") : null,
            home.Length > 0 ? Path.Combine(home, "Downloads") : null,
            home.Length > 0 ? Path.Combine(home, "Documents") : null
        };
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate)) continue;
            var full = Path.GetFullPath(candidate);
            if (seen.Add(full)) roots.Add(full);
        }
        return roots;
    }

    private readonly record struct ReusedDocumentAssets(int Models, long Bytes);

    /// <summary>
    /// Mac LocalDocumentAssets.reuse: match by exact file name and size, hash only those candidates,
    /// copy verified files into the app's model directory (original files stay in place).
    /// </summary>
    private ReusedDocumentAssets ReuseLocalDocumentAssets(IReadOnlyList<string> roots, IProgress<string>? progress, CancellationToken ct)
    {
        var reusedBytes = 0L;
        var reusedModels = 0;
        foreach (var model in DocumentAssets.Models)
        {
            var targetRoot = Path.Combine(Paths.Models, model.Folder);
            Directory.CreateDirectory(targetRoot);
            var complete = true;
            foreach (var file in model.Files)
            {
                ct.ThrowIfCancellationRequested();
                var target = Path.Combine(targetRoot, file.Name);
                if (File.Exists(target) && new FileInfo(target).Length == file.Size)
                {
                    var existing = File.OpenRead(target);
                    try
                    {
                        if (Convert.ToHexStringLower(SHA256.HashData(existing)) == file.Sha256) continue;
                    }
                    finally { existing.Dispose(); }
                }
                var found = false;
                foreach (var root in roots)
                {
                    foreach (var candidate in EnumerateNamedFiles(root, file.Name, ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        var info = new FileInfo(candidate);
                        if (info.Length != file.Size) continue;
                        string hash;
                        using (var stream = File.OpenRead(candidate))
                            hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                        if (hash != file.Sha256) continue;
                        progress?.Report(UiText.Format("正在复用 %@…", file.Name));
                        File.Copy(candidate, target, true);
                        reusedBytes += file.Size;
                        found = true;
                        break;
                    }
                    if (found) break;
                }
                if (!found) complete = false;
            }
            if (complete && DocumentAssets.IsInstalled(Paths, model)) reusedModels++;
        }
        return new ReusedDocumentAssets(reusedModels, reusedBytes);
    }

    private static IEnumerable<string> EnumerateNamedFiles(string root, string name, CancellationToken ct)
    {
        var found = 0;
        var visited = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            if (found >= 40 || ++visited > 4000) yield break;
            var directory = pending.Pop();
            string[] files;
            string[] directories;
            try { files = Directory.GetFiles(directory, name, SearchOption.TopDirectoryOnly); directories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException) { continue; }
            foreach (var file in files)
            {
                found++;
                yield return file;
                if (found >= 40) yield break;
            }
            // Keep the scan bounded: skip the model directory the app itself owns.
            foreach (var child in directories) pending.Push(child);
        }
    }

    // MARK: - semantic enhancement (Mac featureButton(.semantic) / rebuildSemanticIndex())

    /// <summary>Mac 启用语义增强: prepare the model if needed, then turn semantic retrieval on.</summary>
    [RelayCommand]
    private async Task EnableSemanticEnhancementAsync()
    {
        if (!new EmbeddingModelStore(Paths).IsInstalled)
        {
            await PrepareRetrievalAsync();
            if (!new EmbeddingModelStore(Paths).IsInstalled) return;
        }
        Settings.UseSemanticRetrieval = true;
        await Settings.SaveAsync();
        RefreshDocumentComponents();
        SetStatus(UiText.T("已启用语义增强。"));
    }

    /// <summary>Mac 重建语义索引: drop the cached vectors; the next search rebuilds them.</summary>
    [RelayCommand]
    private void RebuildSemanticIndex()
    {
        try
        {
            Retrieval.RebuildIndex();
            RefreshDocumentComponents();
            SetStatus(UiText.T("语义索引已重建，下次搜索会自动生成。"));
        }
        catch (Exception exception) { SetStatus(exception.Message, true); }
    }
}

/// <summary>E23: one installed/missing document component (Mac ManagedModelRow input).</summary>
public sealed record DocumentComponentRow(
    string Id, string Name, string Detail, string Status, bool Installed,
    string License, string Summary, string SourceUrl)
{
    /// <summary>Mac ManagedModelRow note: size · license · summary (size is already the Detail line here).</summary>
    public string LicenseNote => License.Length == 0 ? Summary : $"{License} · {Summary}";
    public bool HasSource => SourceUrl.Length > 0;
}
