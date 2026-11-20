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
        // C19: WDA exclusion is the primary protection; the window-title detector is only a fallback.
        var protectedByCapture = state.Settings.CapturePrivacy
            && (island is null || WindowCaptureHiding.IsHidden(island));
        var active = !protectedByCapture && ScreenShareDetector.IsSharingActive();
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
        GlobalHotkeys.Binding B(string id, Action action)
        {
            var definition = NextThought.Core.ShortcutKeys.Find(id)
                             ?? throw new InvalidOperationException("Unknown shortcut: " + id);
            var binding = state.ShortcutBinding(id, definition.DefaultModifiers, definition.DefaultKey);
            return new GlobalHotkeys.Binding(
                $"{NextThought.Core.ShortcutKeys.Format(binding.Modifiers, binding.Key)} {UiText.T(definition.Label)}", binding.Key, action)
            {
                Modifiers = binding.Modifiers
            };
        }
        hotkeys = new GlobalHotkeys(
        [
            B("window", () => _ = CaptureWindowAsync()),
            B("region", () => _ = CaptureRegionAsync()),
            B("record", ToggleRecording),
            B("regenerate", () => state.RegenerateAnswerCommand.Execute(null)),
            B("force", () => state.ForceAnswerCommand.Execute(null)),
            B("pause", () => state.TogglePauseListeningCommand.Execute(null)),
            B("teleprompter", ToggleTeleprompter),
            B("cancel", () => state.CancelCommand.Execute(null)),
            B("copy", () => state.CopyAnswerCommand.Execute(null)),
            B("through", ToggleTeleprompterClickThrough),
            B("hide", ToggleHideAll),
            B("previous", () => state.PreviousAnswerCommand.Execute(null)),
            B("next", () => state.NextAnswerCommand.Execute(null)),
            B("shorter", () => state.ShorterAnswerCommand.Execute(null)),
            B("live", () => state.CycleLiveModeCommand.Execute(null)),
            B("island", ToggleCueAlwaysOpen),
            B("slidePrevious", () => state.PreviousSlideCommand.Execute(null)),
            B("slideNext", () => state.NextSlideCommand.Execute(null))
        ]);
        state.ShortcutConflicts = hotkeys.Unregistered.Count == 0
            ? ""
            : UiText.T("以下全局快捷键被其他程序占用：") + string.Join(UiText.T("、"), hotkeys.Unregistered) + UiText.T("；界面按钮仍可使用。");
        if (hotkeys.Unregistered.Count > 0)
            state.SetStatus(state.ShortcutConflicts, true);
    }

    /// <summary>Tray icon: restore, hide everything while recording continues, record toggle, quit.</summary>
    private void SetupTray()
    {
        tray = new TrayIntegration(RestoreMainWindow, HideAllWindows, ToggleRecording, RequestExit, NextThought.Core.UiTextSource.Instance.IsEnglish);
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
        teleprompter?.HideForAllWindows();
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

    /// <summary>E13: the library copies the file; the temporary capture should not pile up.</summary>
    private static void TryDeleteCapture(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    /// <summary>Capture the window the user picks (Mac WindowPickerSheet + captureWindow).</summary>
    public async Task CaptureWindowAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再截图。"); return; }
        try
        {
            var picker = new WindowPickerWindow();
            var picked = await picker.ShowDialog<bool>(this);
            if (picked is false || picker.Selected is not { } target)
            {
                state.SetStatus("已取消截图。");
                return;
            }
            await CaptureTargetAsync(target);
        }
        catch (Exception ex)
        {
            RestoreOwnWindowCapture();
            state.SetStatus("窗口截图失败：" + ex.Message, true);
        }
    }

    /// <summary>
    /// E13: hides NextThought's own windows from the capture, grabs the chosen window's frame,
    /// then hands the picture to the question flow (analyzed right away during a meeting).
    /// </summary>
    private async Task CaptureTargetAsync(ScreenCapture.CaptureTarget target)
    {
        var wasVisible = IsVisible;
        string? path = null;
        var committed = false;
        try
        {
            HideOwnWindowsFromCapture();
            if (wasVisible) { WindowState = WindowState.Minimized; }
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var capture = ScreenCapture.CaptureWindow(target);
            RestoreWindow(wasVisible);
            if (capture is null)
            {
                state.SetStatus(UiText.Format("窗口“%@”已关闭或无法捕获，请刷新窗口列表后重试。", target.DisplayTitle), true);
                return;
            }
            path = Path.Combine(Path.GetTempPath(), $"nextthought-window-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            capture.Image.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await CommitScreenshotAsync(path, capture.Image);
            committed = true;
            path = state.Recording ? null : path; // the home card owns the file until analyzed
        }
        catch (Exception ex)
        {
            state.SetStatus("窗口截图失败：" + ex.Message, true);
        }
        finally
        {
            RestoreWindow(wasVisible);
            RestoreOwnWindowCapture();
            if (!committed && path is not null) TryDeleteCapture(path);
        }
    }

    /// <summary>Mac captureWindow/commitRegionScreenshot: analyze at once, or wait on the home card.</summary>
    private async Task CommitScreenshotAsync(string path, Avalonia.Media.Imaging.Bitmap cropped)
    {
        if (state.Recording) await state.AnalyzeScreenshotFileAsync(path);
        else state.SetScreenshotPreview(path, cropped);
    }

    /// <summary>E13: own windows never end up in our own screenshots (Mac excludes the app).</summary>
    private static void HideOwnWindowsFromCapture()
    {
        WindowCaptureHiding.PrivacyEnabled = true;
        WindowCaptureHiding.ApplyAll(true);
    }

    private static void RestoreOwnWindowCapture()
    {
        try { WindowCaptureHiding.ApplyAll(CurrentSettingsCapturePrivacy()); } catch { }
    }

    private static bool CurrentSettingsCapturePrivacy()
    {
        try { return AppState.Current?.Settings.CapturePrivacy ?? true; } catch { return true; }
    }

    private void RestoreWindow(bool wasVisible)
    {
        if (!wasVisible) return;
        WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    /// <summary>E13: screenshots are temporary; cancelled or failed captures must not pile up.</summary>
    public static int CleanupStaleCaptureFiles()
    {
        try { return CaptureFiles.DeleteStale(Path.GetTempPath(), DateTimeOffset.UtcNow.AddHours(-6)); }
        catch { return 0; }
    }


    /// <summary>E9: pick a screen region for the screen watch; the frame itself is discarded.</summary>
    public async Task PickScreenRegionForWatchAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再框选。"); return; }
        var wasVisible = IsVisible;
        try
        {
            HideOwnWindowsFromCapture();
            WindowState = WindowState.Minimized;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var capture = ScreenCapture.VirtualScreen();
            RestoreWindow(wasVisible);
            var picker = new RegionCaptureWindow(capture);
            await picker.ShowDialog<Avalonia.Media.Imaging.Bitmap?>(this);
            RestoreOwnWindowCapture();
            if (picker.SelectedScreenRect is { } rect)
            {
                state.Settings.InsightScreenSource = "region";
                state.Settings.InsightScreenRegion = $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";
                state.Settings.SelectedInsightSourceLabel = "区域";
                await state.Settings.SaveAsync();
                state.SetStatus(UiText.Format("已框选读屏区域：%lld×%lld。", rect.Width, rect.Height));
            }
        }
        catch (Exception ex)
        {
            RestoreWindow(wasVisible);
            RestoreOwnWindowCapture();
            state.SetStatus("框选读屏区域失败：" + ex.Message, true);
        }
    }

    public async Task CaptureRegionAsync()
    {
        if (state.Working) { state.SetStatus("当前有任务在进行，稍后再截图。"); return; }
        var wasVisible = IsVisible;
        string? path = null;
        var committed = false;
        try
        {
            // E13: the frame may not contain our own floating windows (Mac excludes the app).
            HideOwnWindowsFromCapture();
            if (wasVisible) WindowState = WindowState.Minimized;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            await Task.Delay(360);
            var capture = ScreenCapture.VirtualScreen();
            RestoreWindow(wasVisible); // the picker needs a visible, active owner window
            var picker = new RegionCaptureWindow(capture);
            var cropped = await picker.ShowDialog<Avalonia.Media.Imaging.Bitmap?>(this);
            if (cropped is null) { state.SetStatus("已取消截图。"); return; }
            path = Path.Combine(Path.GetTempPath(), $"nextthought-shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            cropped.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            await CommitScreenshotAsync(path, cropped);
            committed = true;
            path = state.Recording ? null : path;
        }
        catch (Exception ex)
        {
            state.SetStatus("截图失败：" + ex.Message, true);
        }
        finally
        {
            RestoreWindow(wasVisible);
            RestoreOwnWindowCapture();
            if (!committed && path is not null) TryDeleteCapture(path);
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

    /// <summary>C14: settings buttons move the teleprompter to one of the three Mac placements.</summary>
    public void MoveTeleprompterTo(string placement)
    {
        if (teleprompter is not { IsVisible: true }) ToggleTeleprompter();
        teleprompter?.MoveTo(placement);
    }

    /// <summary>Floating teleprompter window (plan §9 提词器).</summary>
    /// <summary>G2: the Mac toggleCueOpen action flips the always-open preference.</summary>
    private void ToggleCueAlwaysOpen()
    {
        state.Settings.CueAlwaysOpen = !state.Settings.CueAlwaysOpen;
        _ = state.Settings.SaveAsync();
        state.SetStatus(state.Settings.CueAlwaysOpen ? "提词岛会中一直展开。" : "提词岛恢复为鼠标移上去才展开。");
    }

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
        if (state.SettingsSnapshot.CueSurface == "window" && !presenterActive)
        {
            if (teleprompter is { IsVisible: true }) { teleprompter.DisableClickThrough(); _ = FadeOutAndHideAsync(teleprompter); }
            else ShowCueWindow();
            return;
        }
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
        else
        {
            if (island is { IsVisible: true }) _ = FadeOutAndHideAsync(island);
            if (state.SettingsSnapshot.CueSurface == "window" && teleprompter is { IsVisible: true })
            {
                teleprompter.DisableClickThrough();
                _ = FadeOutAndHideAsync(teleprompter);
            }
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
        // E26b: Mac cueSurface=window shows the floating teleprompter instead of the island.
        if (state.SettingsSnapshot.CueSurface == "window" && !presenterActive)
        {
            ShowCueWindow();
            return;
        }
        if (island is null)
        {
            var window = new CueIslandWindow(state);
            window.Closed += (_, _) => { if (ReferenceEquals(island, window)) island = null; };
            island = window;
        }
        if (!island.IsVisible)
        {
            island.Show();
            _ = FadeInAsync(island);
        }
    }

    /// <summary>E26b: cueSurface=window — bring up the floating cue window.</summary>
    private void ShowCueWindow()
    {
        if (teleprompter is not { IsVisible: true }) ToggleTeleprompter();
    }

    /// <summary>A short fade so floating windows do not pop in (plan §8 animation).</summary>
    private static async Task FadeInAsync(Avalonia.Visual visual)
    {
        // C15: the teleprompter's opacity is a user setting; fades must not overwrite it.
        if (visual is TeleprompterWindow) return;
        var animation = Motion.FadeIn();
        if (animation is null) { visual.Opacity = 1; return; }
        visual.Opacity = 0;
        try { await animation.RunAsync(visual); } catch { }
        visual.Opacity = 1;
    }

    /// <summary>Mirror of the fade-in; hiding waits for the 120 ms fade.</summary>
    private static async Task FadeOutAndHideAsync(Avalonia.Visual visual)
    {
        if (visual is TeleprompterWindow teleprompter)
        {
            teleprompter.Hide();
            return;
        }
        try
        {
            var animation = Motion.FadeOut();
            if (animation is not null) await animation.RunAsync(visual);
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
                screen.IsPrimary ? "主屏（摄像头）" : UiText.Format("显示器 %lld", index + 1),
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
        state.PresenterLayoutActive = true;
        if (island is not { IsVisible: true })
        {
            if (island is null)
            {
                var window = new CueIslandWindow(state);
                window.Closed += (_, _) => { if (ReferenceEquals(island, window)) island = null; };
                island = window;
            }
            island.Show();
        }
        Hide();
        state.SetStatus(UiText.Format("双屏演讲：%@；Ctrl+Alt+N 唤回主窗口。", plan.Summary));
    }

    private void ExitPresenterMode(string? message = null)
    {
        if (!presenterActive) return;
        presenterActive = false;
        state.PresenterLayoutActive = false;
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
        if (!subtitleBar.IsVisible) subtitleBar.Show();
        subtitleBar.ResumeAfterUserShow();
    }

    private void HandleInitialized()
    {
        if (state.StartupErrorText is { } error) state.SetStatus(error, true);
    }

}
