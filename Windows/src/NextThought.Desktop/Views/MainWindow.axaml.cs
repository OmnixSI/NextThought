using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NextThought.Core;
using Avalonia;

namespace NextThought.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly AppState state;
    private bool closing;

    public MainWindow(AppState state)
    {
        this.state = state;
        InitializeComponent();
        DataContext = state;
        state.PickSaveFile = PickSaveFileAsync;
        state.PickOpenFile = PickOpenFileAsync;
        state.CopyText = async text =>
        {
            if (Clipboard is null) return;
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(text));
            await Clipboard.SetDataAsync(transfer);
        };
        state.Notify = ShowMessageAsync;
        state.Initialized += HandleInitialized;
        state.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.LiveMode)) ApplyLiveMode(state.LiveMode); };
        Opened += async (_, _) =>
        {
            await state.InitializeAsync();
            SetupTray();
            if (StartupOptions.Scene is { } scene) state.SetSceneCommand.Execute(scene);
            if (StartupOptions.HistoryFilter is { } filter) state.HistoryFilter = filter;
            if (StartupOptions.HistoryQuery is { } query) state.HistoryQuery = query;
            if (StartupOptions.ImportWave is { } wave) await RunImportWaveTestAsync(wave);
            if (StartupOptions.Island) ToggleCueIsland();
            if (StartupOptions.Teleprompter) { ToggleTeleprompter(); if (StartupOptions.TeleprompterThrough) teleprompter?.ApplyClickThrough(true); }
            if (StartupOptions.CaptureTest is { } captureTest) await RunCaptureTestAsync(captureTest);
            if (StartupOptions.UpdateCheck is { } feed)
            {
                var update = await state.CheckForUpdatesAsync(feed);
                var target = StartupOptions.UpdateReport ?? feed + ".report.json";
                await File.WriteAllTextAsync(target, System.Text.Json.JsonSerializer.Serialize(update, UpdateReport.Options));
            }
            if (StartupOptions.CheckReport is { } report) await WriteStartupReportAsync(report);
            if (StartupOptions.KnowledgeCheck is { } knowledge) await RunKnowledgeCheckAsync(knowledge);
        };
        Closing += OnClosing;
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MinimizeButton, WindowDecorationsElementRole.MinimizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(MaximizeButton, WindowDecorationsElementRole.MaximizeButton);
        Avalonia.Controls.Chrome.WindowDecorationProperties.SetElementRole(CloseButton, WindowDecorationsElementRole.CloseButton);
        RegisterHotkeys();
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Off-screen startup self-check: writes a JSON report and exits, like the WPF baseline.</summary>
    private async Task WriteStartupReportAsync(string path)
    {
        await Task.Delay(400);
        var passed = state.Ready && state.StartupErrorText is null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var report = new
            {
                testedAt = DateTimeOffset.Now,
                passed,
                scope = "Avalonia 12 startup self-check; layout and interactive acceptance are measured separately",
                seconds = Math.Round(Program.Clock.Elapsed.TotalSeconds, 3),
                section = state.Section.ToString(),
                ready = state.Ready,
                error = state.StartupErrorText,
                historyCount = state.History.Count,
                transcriptCount = state.Transcript.Count,
                initTimings = state.InitTimings,
                // What this build actually carries, so a self-check report is also a capability report.
                features = new[]
                {
                    "capture-dual", "asr-local", "sessions", "summary-markdown", "scenes-9",
                    "cue-island", "subtitles", "teleprompter", "region-capture", "hotkeys-7",
                    "tray", "hide-all", "auto-end", "notes-templates-9", "notes-fishbone",
                    "notes-outline", "notes-viewpoints", "proofread", "due-dates", "series",
                    "glossary", "update-check", "companion-lan",
                    "workspace-knowledge", "knowledge-merge", "question-bank"
                },
                version = typeof(AppState).Assembly.GetName().Version?.ToString(3),
                framework = Environment.Version.ToString(),
                workingSetMiB = Math.Round(Environment.WorkingSet / 1048576.0, 1),
                process = Environment.ProcessPath,
                windowWidth = Width,
                windowHeight = Height,
                clientWidth = ClientSize.Width,
                clientHeight = ClientSize.Height,
                renderScaling = RenderScaling,
                screenScaling = Screens.Primary?.Scaling,
                screenBounds = Screens.Primary?.Bounds.ToString()
            };
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            passed = false;
        }
        Console.WriteLine(passed ? "startup-check: passed" : "startup-check: failed");
        Close();
    }

    /// <summary>
    /// Offline proof of the knowledge loop, with no answer service involved:
    /// template notes → candidates → review → workspace.json → prompt text.
    /// </summary>
    private async Task RunKnowledgeCheckAsync(string path)
    {
        await Task.Delay(300);
        var report = KnowledgeCheck.Run(state);
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch
        {
            report = report with { Passed = false };
        }
        Console.WriteLine(report.Passed ? "knowledge-check: passed" : "knowledge-check: failed");
        Close();
    }

    // The design-time constructor required by the XAML compiler for previews.
    public MainWindow() : this(new AppState()) { }

    private CueIslandWindow? island;
    private SubtitleBarWindow? subtitleBar;
    private GlobalHotkeys? hotkeys;
    private TeleprompterWindow? teleprompter;
    private TrayIntegration? tray;
    private bool hiddenAll;

    /// <summary>Ctrl+Alt shortcuts for the actions the Mac app binds to ⌘⇧ (plan §7 table).</summary>
    private void RegisterHotkeys()
    {
        hotkeys = new GlobalHotkeys(
        [
            new GlobalHotkeys.Binding("Ctrl+Alt+R 开始或停止记录", 0x52, ToggleRecording),
            new GlobalHotkeys.Binding("Ctrl+Alt+O 显示或隐藏提词岛", 0x4F, ToggleCueIsland),
            new GlobalHotkeys.Binding("Ctrl+Alt+M 纪要窗口/字幕条/隐藏", 0x4D, () => state.CycleLiveModeCommand.Execute(null)),
            new GlobalHotkeys.Binding("Ctrl+Alt+S 区域截图", 0x53, () => _ = CaptureRegionAsync()),
            new GlobalHotkeys.Binding("Ctrl+Alt+T 提词器", 0x54, ToggleTeleprompter),
            new GlobalHotkeys.Binding("Ctrl+Alt+\\ 隐藏或恢复所有窗口", 0xDC, ToggleHideAll),
            new GlobalHotkeys.Binding("Ctrl+Alt+N 唤起主窗口", 0x4E, RestoreMainWindow)
        ]);
        if (hotkeys.Unregistered.Count > 0)
            state.SetStatus("以下全局快捷键被其他程序占用：" + string.Join("、", hotkeys.Unregistered) + "；界面按钮仍可使用。", true);
    }

    /// <summary>Tray icon: restore, hide everything while recording continues, record toggle, quit.</summary>
    private void SetupTray()
    {
        tray = new TrayIntegration(RestoreMainWindow, HideAllWindows, ToggleRecording, RequestExit);
        state.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppState.Recording)) tray?.SetRecording(state.Recording); };
    }

    /// <summary>一键隐藏：主窗口与全部浮窗一起收起，会议继续记录（plan §9）。</summary>
    private void HideAllWindows()
    {
        hiddenAll = true;
        island?.Hide();
        subtitleBar?.Hide();
        teleprompter?.Hide();
        Hide();
        state.SetStatus("所有窗口已收起，会议继续记录。Ctrl+Alt+\\ 或托盘图标可恢复。");
    }

    private void ToggleHideAll()
    {
        if (hiddenAll)
        {
            hiddenAll = false;
            RestoreMainWindow();
            ApplyLiveMode(state.LiveMode);
            return;
        }
        HideAllWindows();
    }

    private void RequestExit()
    {
        ExitRequested = true;
        Close();
    }

    public bool ExitRequested { get; private set; }

    private void ToggleRecording()
    {
        if (state.Recording) { if (state.StopRecordingCommand.CanExecute(null)) state.StopRecordingCommand.Execute(null); }
        else if (state.StartRecordingCommand.CanExecute(null)) state.StartRecordingCommand.Execute(null);
        else state.SetStatus("还不能开始记录：请先在设置里准备语音组件。", true);
    }

    private void RestoreMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// 区域截图（方案第 9 节）：先隐藏自己的窗口再抓屏，框选后裁切，然后存入资料库并由
    /// Windows.Media.Ocr 读取文字。
    /// </summary>
    public async Task CaptureRegionAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再截图。"); return; }
        var wasVisible = IsVisible;
        try
        {
            WindowState = WindowState.Minimized; // definitely off the screen, unlike a deferred Hide()
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var capture = ScreenCapture.VirtualScreen();
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            var picker = new RegionCaptureWindow(capture);
            var cropped = await picker.ShowDialog<Avalonia.Media.Imaging.Bitmap?>(this);
            if (cropped is null) { state.SetStatus("已取消截图。"); return; }
            var path = Path.Combine(Path.GetTempPath(), $"nextthought-shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await state.Library.ImportAsync([path]);
            state.Section = AppSection.Library;
            state.SetStatus("截图已存入资料库，并已用系统 OCR 读取其中的文字。");
        }
        catch (Exception ex)
        {
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            state.SetStatus("截图失败：" + ex.Message, true);
        }
    }

    /// <summary>
    /// Unattended end-to-end check of the desktop transcription path: import a WAV, wait for the
    /// session to be archived, then record the outcome next to the input.
    /// </summary>
    private async Task RunImportWaveTestAsync(string wave)
    {
        var report = wave + ".json";
        try
        {
            await state.ImportWaveFileAsync(wave);
            var session = state.CurrentSession;
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                importedAt = DateTimeOffset.Now,
                wave,
                sessionId = session?.Id.ToString("D"),
                title = session?.Title,
                transcriptRows = state.Transcript.Count,
                historyCount = state.History.Count,
                summaryState = session?.SummaryState,
                status = state.Status,
                dataRoot = state.Paths.Root
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(report, body);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(report, "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}");
        }
        Close();
    }

    /// <summary>Unattended capture check: grab the screen, crop the centre, save it and OCR it.</summary>
    private async Task RunCaptureTestAsync(string path)
    {
        try
        {
            Hide();
            await Task.Delay(250);
            var capture = ScreenCapture.VirtualScreen();
            Show();
            var rect = new PixelRect(capture.Width / 4, capture.Height / 4, capture.Width / 2, capture.Height / 2);
            var cropped = ScreenCapture.Crop(capture, rect);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await state.Library.ImportAsync([path]);
            var report = System.Text.Json.JsonSerializer.Serialize(new
            {
                capturedAt = DateTimeOffset.Now,
                screenWidth = capture.Width,
                screenHeight = capture.Height,
                cropWidth = rect.Width,
                cropHeight = rect.Height,
                saved = File.Exists(path),
                bytes = File.Exists(path) ? new FileInfo(path).Length : 0,
                materials = state.Library.Materials.Count
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path + ".json", report);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(path + ".json", "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}");
        }
        Close();
    }

    /// <summary>Floating teleprompter window (plan §9 提词器).</summary>
    public void ToggleTeleprompter()
    {
        if (teleprompter is { IsVisible: true }) { teleprompter.Hide(); return; }
        teleprompter ??= new TeleprompterWindow(state);
        teleprompter.Show();
        teleprompter.ApplyClickThrough(teleprompter.ClickThrough);
    }

    /// <summary>Show or hide the floating cue island (the Mac app's notch panel).</summary>
    public void ToggleCueIsland()
    {
        if (island is { IsVisible: true }) { island.Hide(); return; }
        island ??= new CueIslandWindow(state);
        island.Show();
        island.Activate();
    }

    /// <summary>The subtitle bar follows the three-state live window mode from Core.</summary>
    private void ApplyLiveMode(LiveWindowMode mode)
    {
        if (mode == LiveWindowMode.Notes)
        {
            if (subtitleBar is { IsVisible: true }) subtitleBar.Hide();
            return;
        }
        if (mode == LiveWindowMode.Hidden)
        {
            if (subtitleBar is { IsVisible: true }) subtitleBar.Hide();
            return;
        }
        subtitleBar ??= new SubtitleBarWindow(state);
        if (!subtitleBar.IsVisible) subtitleBar.Show();
    }

    private void HandleInitialized()
    {
        if (state.StartupErrorText is { } error) state.SetStatus(error, true);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = (Avalonia.Media.IBrush?)this.FindResource("Surface") ?? Avalonia.Media.Brushes.White,
            Content = new TextBlock
            {
                Text = message,
                Margin = new Avalonia.Thickness(24),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        };
        await dialog.ShowDialog(this);
    }

    private async Task<string?> PickSaveFileAsync(string filter, string suggestedName)
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存",
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName).TrimStart('.'),
            FileTypeChoices = [BuildType(filter)]
        });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOpenFileAsync(string filter)
    {
        var storage = StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开",
            AllowMultiple = false,
            FileTypeFilter = [BuildType(filter), new FilePickerFileType("全部文件") { Patterns = ["*"] }]
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static FilePickerFileType BuildType(string filter)
    {
        var parts = filter.Split('|');
        var name = parts.Length > 1 ? parts[0] : "文件";
        var patterns = (parts.Length > 1 ? parts[1] : "*").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new FilePickerFileType(name) { Patterns = patterns };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private async void History_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is RecentMeetingRow row) await state.SelectHistoryAsync(row.Meta);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closing) return;
        // AppState+Lifecycle.swift: a meeting in progress never exits on the window's close button;
        // the window goes to the tray and the recording continues.
        if (state.Recording && !ExitRequested)
        {
            e.Cancel = true;
            HideAllWindows();
            return;
        }
        e.Cancel = true;
        closing = true;
        tray?.Dispose();
        tray = null;
        try
        {
            await state.ShutdownAsync();
        }
        catch
        {
            // Never block exit on shutdown errors; everything already written stays on disk.
        }
        Close();
    }
}
