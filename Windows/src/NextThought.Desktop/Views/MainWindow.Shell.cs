using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>H1 extraction: hotkeys, tray, floating windows and presenter mode.</summary>
public partial class MainWindow
{
    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Off-screen startup self-check: writes a JSON report and exits, like the WPF baseline.</summary>


    public MainWindow() : this(new AppState()) { }

    /// <summary>Retracts the cue island while a known screen share is visible (macOS 15.4 fallback).</summary>
    private void EvaluateScreenSharing()
    {
        if (closing) return;
        var active = ScreenShareDetector.IsSharingActive();
        if (active == sharingActive) return;
        sharingActive = active;
        if (active)
        {
            if (state.Recording && island is { IsVisible: true })
            {
                sharingRetractedIsland = true;
                _ = FadeOutAndHideAsync(island);
                state.SetStatus("检测到屏幕共享，已自动收回提示岛。");
            }
        }
        else if (sharingRetractedIsland)
        {
            sharingRetractedIsland = false;
            if (state.Recording && !hiddenAll && island is not { IsVisible: true }) ShowCueIsland();
        }
    }

    internal void ApplyScreenSharingForCheck(bool active)
    {
        screenShareTimer.Stop();
        sharingActive = active;
        if (active)
        {
            if (island is { IsVisible: true }) sharingRetractedIsland = true;
            if (island is { IsVisible: true }) _ = FadeOutAndHideAsync(island);
        }
        else if (sharingRetractedIsland)
        {
            sharingRetractedIsland = false;
            ShowCueIsland();
        }
    }

    private CueIslandWindow? island;
    private SubtitleBarWindow? subtitleBar;
    private GlobalHotkeys? hotkeys;
    private TeleprompterWindow? teleprompter;
    private TrayIntegration? tray;
    private bool hiddenAll;

    /// <summary>Ctrl+Alt shortcuts for the actions the Mac app binds to ⌘⇧ (plan §7 table).</summary>
    private void ReloadHotkeys()
    {
        hotkeys?.Dispose();
        hotkeys = null;
        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        GlobalHotkeys.Binding B(string id, string label, uint fallback, Action action)
        {
            var binding = state.ShortcutBinding(id, fallback);
            return new GlobalHotkeys.Binding(
                $"{NextThought.Core.ShortcutKeys.Format(binding.Modifiers, binding.Key)} {label}", binding.Key, action)
            {
                Modifiers = binding.Modifiers
            };
        }
        hotkeys = new GlobalHotkeys(
        [
            B("record", "开始或停止记录", 0x52, ToggleRecording),
            B("island", "显示或隐藏提示岛", 0x49, ToggleCueIsland),
            B("live", "切换会中窗口", 0x4D, () => state.CycleLiveModeCommand.Execute(null)),
            B("region", "区域截图", 0x53, () => _ = CaptureRegionAsync()),
            B("teleprompter", "提词器", 0x55, ToggleTeleprompter),
            B("hide", "隐藏或恢复所有窗口", 0xDC, ToggleHideAll),
            B("main", "唤起主窗口", 0x4E, RestoreMainWindow),
            B("copy", "复制当前回答", 0x48, () => state.CopyAnswerCommand.Execute(null)),
            B("regenerate", "重新生成上一题", 0x47, () => state.RegenerateAnswerCommand.Execute(null)),
            B("shorter", "把回答改短", 0x4B, () => state.ShorterAnswerCommand.Execute(null)),
            B("cancel", "取消当前回答", 0x58, () => state.CancelCommand.Execute(null)),
            B("previous", "上一条回答", 0x25, () => state.PreviousAnswerCommand.Execute(null)),
            B("next", "下一条回答", 0x27, () => state.NextAnswerCommand.Execute(null)),
            B("through", "提词浮窗鼠标穿透", 0x4A, ToggleTeleprompterClickThrough),
            B("slideNext", "下一页幻灯片", 0x22, () => state.NextSlideCommand.Execute(null)),
            B("slidePrevious", "上一页幻灯片", 0x21, () => state.PreviousSlideCommand.Execute(null)),
            B("force", "立即回答最新转写", 0x51, () => state.ForceAnswerCommand.Execute(null)),
            B("window", "窗口截图", 0x57, () => _ = CaptureWindowAsync()),
            B("pause", "暂停或恢复聆听", 0x20, () => state.TogglePauseListeningCommand.Execute(null))
        ]);
        if (hotkeys.Unregistered.Count > 0)
            state.SetStatus("以下全局快捷键被其他程序占用：" + string.Join("、", hotkeys.Unregistered) + "；界面按钮仍可使用。", true);
    }

    /// <summary>Tray icon: restore, hide everything while recording continues, record toggle, quit.</summary>
    private void SetupTray()
    {
        tray = new TrayIntegration(RestoreMainWindow, HideAllWindows, ToggleRecording, RequestExit, state.Settings.UiLanguage == "English");
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Recording))
            {
                tray?.SetRecording(state.Recording);
                UpdateCueIslandForMeeting();
            }
            else if (e.PropertyName == nameof(AppState.Answering) || e.PropertyName == nameof(AppState.DetectedQuestion))
            {
                if (state.Recording && !hiddenAll) ShowCueIsland();
            }
        };
    }

    /// <summary>一键隐藏：主窗口与全部浮窗一起收起，会议继续记录（plan §9）。</summary>
    private void HideAllWindows()
    {
        hiddenAll = true;
        island?.Hide();
        subtitleBar?.HideForUser();
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
    /// <summary>Capture the window that had focus before NextThought was minimized (Mac captureWindow).</summary>
    public async Task CaptureWindowAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再截图。"); return; }
        var wasVisible = IsVisible;
        try
        {
            WindowState = WindowState.Minimized;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var bounds = ScreenCapture.ForegroundWindowBounds();
            if (bounds.Width <= 1 || bounds.Height <= 1) { state.SetStatus("没有找到可截图的窗口。", true); return; }
            var capture = ScreenCapture.VirtualScreen();
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            var cropped = ScreenCapture.Crop(capture, new Avalonia.PixelRect(
                bounds.X - capture.X, bounds.Y - capture.Y, bounds.Width, bounds.Height));
            var path = Path.Combine(Path.GetTempPath(), $"nextthought-window-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await state.Library.ImportAsync([path]);
            state.Section = AppSection.Library;
            state.SetStatus("窗口截图已存入资料库，并已用系统 OCR 读取其中的文字。");
        }
        catch (Exception ex)
        {
            if (wasVisible) { WindowState = WindowState.Normal; Show(); Activate(); }
            state.SetStatus("窗口截图失败：" + ex.Message, true);
        }
    }

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
            UiTranslation.Apply(picker, state.Settings.UiLanguage == "English");
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
        if (teleprompter is { IsVisible: true })
        {
            // Hiding must also leave click-through, otherwise Ctrl+Alt+U cannot bring it back.
            teleprompter.DisableClickThrough();
            _ = FadeOutAndHideAsync(teleprompter);
            return;
        }
        if (teleprompter is null)
        {
            var window = new TeleprompterWindow(state);
            window.Closed += (_, _) => { if (ReferenceEquals(teleprompter, window)) teleprompter = null; };
            teleprompter = window;
            UiTranslation.Apply(window, state.Settings.UiLanguage == "English");
        }
        teleprompter.Show();
        _ = FadeInAsync(teleprompter);
        teleprompter.ApplyClickThrough(teleprompter.ClickThrough);
    }

    /// <summary>Show or hide the floating cue island (the Mac app's notch panel).</summary>
    private void ToggleTeleprompterClickThrough()
    {
        if (teleprompter is null || !teleprompter.IsVisible)
        {
            ToggleTeleprompter();
            if (teleprompter is null || !teleprompter.IsVisible) return;
        }
        teleprompter.ToggleClickThrough();
        state.SetStatus(teleprompter.ClickThrough ? "提词浮窗已开启鼠标穿透，Ctrl+Alt+J 可恢复。" : "提词浮窗已恢复鼠标操作。");
    }

    public void ToggleCueIsland()
    {
        if (island is { IsVisible: true }) { _ = FadeOutAndHideAsync(island); return; }
        ShowCueIsland();
    }

    /// <summary>Meeting start shows the island automatically; stop hides it (W2).</summary>
    private void UpdateCueIslandForMeeting()
    {
        if (state.Recording)
        {
            if (!hiddenAll) ShowCueIsland();
        }
        else if (island is { IsVisible: true })
        {
            _ = FadeOutAndHideAsync(island);
        }
    }

    public void ShowCueIsland()
    {
        if (hiddenAll) return;
        if (sharingActive)
        {
            state.SetStatus("检测到屏幕共享，提示岛保持收回；共享结束后自动恢复。");
            return;
        }
        if (island is null)
        {
            var window = new CueIslandWindow(state);
            window.Closed += (_, _) => { if (ReferenceEquals(island, window)) island = null; };
            island = window;
        }
        UiTranslation.Apply(island, state.Settings.UiLanguage == "English");
        if (!island.IsVisible)
        {
            island.Show();
            _ = FadeInAsync(island);
        }
    }

    /// <summary>A short fade so floating windows do not pop in (plan §8 animation).</summary>
    private static async Task FadeInAsync(Avalonia.Visual visual)
    {
        visual.Opacity = 0;
        var animation = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(160),
            Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            FillMode = Avalonia.Animation.FillMode.Forward,
            Children =
            {
                new Avalonia.Animation.KeyFrame
                {
                    Cue = new Avalonia.Animation.Cue(0),
                    Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 0d) }
                },
                new Avalonia.Animation.KeyFrame
                {
                    Cue = new Avalonia.Animation.Cue(1),
                    Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 1d) }
                }
            }
        };
        try { await animation.RunAsync(visual); } catch { }
        visual.Opacity = 1;
    }

    /// <summary>Mirror of the fade-in; hiding waits for the 120 ms fade.</summary>
    private static async Task FadeOutAndHideAsync(Avalonia.Visual visual)
    {
        try
        {
            var animation = new Avalonia.Animation.Animation
            {
                Duration = TimeSpan.FromMilliseconds(120),
                Easing = new Avalonia.Animation.Easings.CubicEaseIn(),
                FillMode = Avalonia.Animation.FillMode.Forward,
                Children =
                {
                    new Avalonia.Animation.KeyFrame
                    {
                        Cue = new Avalonia.Animation.Cue(0),
                        Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 1d) }
                    },
                    new Avalonia.Animation.KeyFrame
                    {
                        Cue = new Avalonia.Animation.Cue(1),
                        Setters = { new Avalonia.Styling.Setter(Avalonia.Visual.OpacityProperty, 0d) }
                    }
                }
            };
            await animation.RunAsync(visual);
        }
        catch { }
        if (visual is Avalonia.Controls.Window window && window.IsVisible)
        {
            window.Hide();
            visual.Opacity = 1;
        }
    }

    // ---- 双屏演讲（PresenterMode.swift） ----

    private bool presenterActive;

    /// <summary>The displays as PresenterLayout sees them (primary = the one with the camera).</summary>
    public IReadOnlyList<DisplayInfo> BuildDisplays()
    {
        try
        {
            return Screens.All.Select((screen, index) => new DisplayInfo(
                // Avalonia 12's Screen has no DisplayIndex; the origin is stable for a given topology.
                $"display:{screen.Bounds.X}x{screen.Bounds.Y}",
                screen.IsPrimary ? "主屏（摄像头）" : $"显示器 {index + 1}",
                screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height,
                screen.IsPrimary,
                // Avalonia does not expose the built-in panel; in the common laptop case it is the primary display.
                screen.IsPrimary)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void RefreshDisplays()
    {
        try
        {
            DisplayCatalog.Refresh(BuildDisplays());
            state.Settings.RefreshScreens();
        }
        catch
        {
            // The window may not have a screen manager yet during construction.
        }
        if (!presenterActive) return;
        var plan = CurrentPresenterPlan();
        if (!plan.CanActivate) ExitPresenterMode("外接屏已断开，双屏演讲已退出，主窗口已恢复。");
        else state.SetStatus("双屏演讲：" + plan.Summary + "；Ctrl+Alt+N 唤回主窗口。");
    }

    private PresenterPlan CurrentPresenterPlan() =>
        PresenterLayout.Resolve(DisplayCatalog.Displays, state.SettingsSnapshot.PresenterSlidesScreen);

    /// <summary>
    /// Mac PresenterMode.begin: with a second display the island carries the meeting on the
    /// camera display and the page steps aside. One display only: nothing is hidden, because
    /// hiding the page would hide the meeting itself.
    /// </summary>
    private void ApplyPresenterModeForRecording()
    {
        if (presenterActive || !state.SettingsSnapshot.PresenterMode) return;
        var plan = CurrentPresenterPlan();
        if (!plan.CanActivate)
        {
            state.SetStatus("双屏演讲：" + plan.Summary + "（会议窗口保持显示）");
            return;
        }
        presenterActive = true;
        if (island is not { IsVisible: true })
        {
            if (island is null)
            {
                var window = new CueIslandWindow(state);
                window.Closed += (_, _) => { if (ReferenceEquals(island, window)) island = null; };
                island = window;
            }
            UiTranslation.Apply(island, state.Settings.UiLanguage == "English");
            island.Show();
        }
        Hide();
        state.SetStatus($"双屏演讲：{plan.Summary}；Ctrl+Alt+N 唤回主窗口。");
    }

    private void ExitPresenterMode(string? message = null)
    {
        if (!presenterActive) return;
        presenterActive = false;
        Show();
        Activate();
        state.SetStatus(message ?? "双屏演讲已结束，主窗口已恢复。");
    }

    /// <summary>The subtitle bar follows the three-state live window mode from Core.</summary>
    private void ApplyLiveMode(LiveWindowMode mode)
    {
        if (mode == LiveWindowMode.Notes)
        {
            if (subtitleBar is { } notesBar) { notesBar.Persistent = false; notesBar.Hide(); }
            if (!hiddenAll && !IsVisible) { Show(); Activate(); }
            return;
        }
        if (mode == LiveWindowMode.Hidden)
        {
            if (subtitleBar is { } hiddenBar) { hiddenBar.Persistent = false; hiddenBar.Hide(); }
            if (IsVisible) Hide();
            return;
        }
        // Subtitles: the floating bar replaces the main window, so the three modes differ.
        if (IsVisible) Hide();
        if (subtitleBar is null)
        {
            var window = new SubtitleBarWindow(state);
            window.Closed += (_, _) => { if (ReferenceEquals(subtitleBar, window)) subtitleBar = null; };
            subtitleBar = window;
        }
        subtitleBar.Persistent = true;
        UiTranslation.Apply(subtitleBar, state.Settings.UiLanguage == "English");
        if (!subtitleBar.IsVisible) subtitleBar.Show();
        subtitleBar.ResumeAfterUserShow();
    }

    private void HandleInitialized()
    {
        if (state.StartupErrorText is { } error) state.SetStatus(error, true);
    }

}
