using Avalonia.Controls;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>E4: shell error banner, level meters, focus mode and the debug-only component
    /// sample sheet (ContentView.swift ErrorBanner/LevelMeters/FocusModeView and
    /// LiveSessionAcceptanceComponentSamplesView), captured light/dark for the shell compare.</summary>
    private async Task RunShellCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail });
        async Task Pump()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(280);
        }
        try
        {
            // A fresh data directory starts behind the first-run guide; the shell evidence needs the
            // real shell, so dismiss it first (as the other shell checks do).
            state.OnboardingVisible = false;
            state.Recording = false;
            state.NavigateCommand.Execute("Home");
            await Pump();
            CaptureVisual(this, Path.Combine(directory, "shell-normal-light.png"));
            Step("shell 布局：标题胶囊 + 内容 + 状态栏", true, "主窗口已渲染");

            // LevelMeters: two capsules fed by the live audio levels (macOS echo flag N/A).
            state.UpdateAudioLevels(0.82, 0.35);
            await Pump();
            var meter = this.GetVisualDescendants().OfType<LevelMeters>().FirstOrDefault();
            Step("LevelMeters：对方（系统声音）与我（麦克风）两条电平",
                meter is not null && state.ShellSystemLevel > 0.5 && state.ShellMicrophoneLevel > 0.1,
                UiText.Format("系统 %.0f%% · 麦克风 %.0f%%", state.ShellSystemLevel * 100, state.ShellMicrophoneLevel * 100));
            CaptureVisual(this, Path.Combine(directory, "shell-meters-light.png"));

            // ErrorBanner: caution triangle + message + 关闭提示, floating card on top.
            state.SetStatus("操作失败：E4 自检错误横幅", true);
            await Pump();
            Step("ErrorBanner：错误横幅（caution 三角 + 可选中文字 + 关闭）", state.StatusIsError && state.Status.Length > 0, state.Status);
            CaptureVisual(this, Path.Combine(directory, "shell-error-light.png"));
            if (Avalonia.Application.Current is { } app)
            {
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Pump();
                CaptureVisual(this, Path.Combine(directory, "shell-error-dark.png"));
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Pump();
            }
            state.CloseStatusCommand.Execute(null);
            await Pump();
            Step("ErrorBanner 关闭后恢复", !state.StatusIsError, "StatusIsError=false");
            if (Avalonia.Application.Current is { } darkApp)
            {
                darkApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Pump();
                CaptureVisual(this, Path.Combine(directory, "shell-normal-dark.png"));
                darkApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Pump();
            }

            // FocusModeView: while recording the window belongs to the meeting (no nav/status bar).
            state.Recording = true;
            await Pump();
            await Task.Delay(200);
            Step("FocusMode：录制中隐藏窗口外壳（标题胶囊/状态栏）", state.Recording && !state.ContextSidebarVisible,
                "Recording=true；ContextSidebarVisible=false");
            CaptureVisual(this, Path.Combine(directory, "shell-focus-light.png"));
            state.Recording = false;
            await Pump();

            // LiveWindowMenu: the top-bar captions-bubble button opens the three-mode picker with
            // the shortcut hint; screenshot the real flyout content, not a mock.
            state.NavigateCommand.Execute("Live");
            await Pump();
            var menuButton = this.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(button => button.Name == "LiveWindowMenuButton");
            var flyout = menuButton?.Flyout as Flyout;
            flyout?.ShowAt(menuButton!);
            await Pump();
            await Task.Delay(200);
            var popup = flyout?.Content as Control;
            Step("LiveWindowMenu：三项会中窗口 + 快捷键提示的弹出层",
                popup is not null && state.ToggleLiveWindowHint.Length > 0,
                state.ToggleLiveWindowHint);
            if (popup is not null) CaptureVisual(popup, Path.Combine(directory, "live-window-menu-light.png"));
            flyout?.Hide();
            await Pump();
            state.NavigateCommand.Execute("Home");
            await Pump();

            // Debug-only component sample sheet.
#if DEBUG
            var samples = new LiveComponentSamplesWindow();
            samples.Show();
            await Pump();
            Step("LiveComponentSamples：调试样张页（合成内容，不触达真实管线）", samples.IsVisible, "1200×1080 样张");
            CaptureVisual(samples, Path.Combine(directory, "live-component-samples-light.png"));
            if (Avalonia.Application.Current is { } samplesApp)
            {
                samplesApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                await Pump();
                CaptureVisual(samples, Path.Combine(directory, "live-component-samples-dark.png"));
                samplesApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                await Pump();
            }
            samples.Close();
#else
            Step("LiveComponentSamples：调试样张页", true, "debug-only sample page");
#endif
            await Pump();
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex) { error = ex.ToString(); }
        var report = new { passed, error, steps };
        try
        {
            File.WriteAllText(Path.Combine(directory, "shell-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "shell-check: passed" : "shell-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
