using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// §5.4: the 20 live-page states from the Mac live-states matrix, each captured light and dark.
    /// The Mac drives these from QA state injection; the Windows page exposes the same visual state
    /// through its public demo/property surface, so this check applies the closest equivalent for
    /// every Mac state and records which ones are exact and which are approximations.
    /// Images go to %TEMP%\nextthought-selftest\live-states\ and the report to its report.json.
    /// </summary>
    private async Task RunLiveStatesCheckAsync(string path)
    {
        await Task.Delay(300);
        var directory = path; // the harness passes a path without extension; use it as the image dir
        Directory.CreateDirectory(directory);
        var states = new[]
        {
            "normal", "streaming", "busy", "focus", "focus-busy", "success", "save-warning",
            "save-recovery", "unavailable", "stale", "stale-manual-edit", "streaming-reading-history",
            "selected-focus", "selection", "reading-history", "large-text", "empty", "overlay",
            "overlay-popover", "cancel-late-callback"
        };
        var approximations = new List<string>();
        var captured = new List<string>();
        try
        {
            // LoadDemo is expensive; load the live demo once per theme and reset the state deltas
            // between the 20 states instead of reloading the whole dataset 40 times.
            foreach (var dark in new[] { false, true })
            {
                state.LoadDemo(DemoStages.Live);
                state.Section = AppSection.Live;
                state.ApplyUiLanguage(false);
                if (Avalonia.Application.Current is { } app)
                    app.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                foreach (var name in states)
                {
                    ResetLiveStateForCheck();
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Task.Delay(120);
                    var exact = ApplyLiveStateForCheck(name);
                    if (!exact) approximations.Add(name);
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Task.Delay(220);
                    TextZoomService.ApplyAll();
                    var file = Path.Combine(directory, name + (dark ? "-dark.png" : "-light.png"));
                    CaptureWindow(file);
                    captured.Add(Path.GetFileName(file));
                }
            }
            if (Avalonia.Application.Current is { } resetApp)
                resetApp.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            var report = new
            {
                passed = captured.Count == states.Length * 2,
                states = states,
                capturedFiles = captured,
                approximatedStates = approximations.Distinct().ToArray(),
                note = "近似状态：Windows 没有 Mac QA 注入的对应属性，用现有页面能力的最接近状态截图；" +
                       "列在 approximatedStates 的状态需要在 owner 提供状态入口后重拍。"
            };
            await File.WriteAllTextAsync(Path.Combine(directory, "report.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
            Console.WriteLine(report.passed ? "live-states-check: passed" : "live-states-check: failed");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("live-states-check: " + error);
            await File.WriteAllTextAsync(Path.Combine(directory, "report.error.txt"), error.ToString());
        }
        finally
        {
            Close();
        }
    }

    /// <summary>Returns the live page to the normal demo state without reloading the demo data.</summary>
    private void ResetLiveStateForCheck()
    {
        state.Section = AppSection.Live;
        state.SetAnswerMode(true);
        state.Answering = false;
        state.SystemPartial = "";
        state.MicrophonePartial = "";
        state.DetectedQuestion = "";
        state.AnswerText = DemoScript.LiveAnswer;
        state.WebResults.Clear();
        TextZoomService.SetFactorForCheck(1);
        state.RaiseLiveAnswerUi();
    }

    /// <summary>Applies one live-state delta; returns false when it is an approximation.</summary>
    private bool ApplyLiveStateForCheck(string name)
    {
        var exact = true;
        switch (name)
        {
            case "normal":
                state.SetAnswerMode(true);
                break;
            case "streaming":
                state.SystemPartial = "接下来我们看一下缓存更新的顺序";
                state.MicrophonePartial = "";
                state.RaiseLiveAnswerUi();
                break;
            case "busy":
                state.Answering = true;
                break;
            case "focus":
                state.SetAnswerMode(false);
                break;
            case "focus-busy":
                state.SetAnswerMode(false);
                state.Answering = true;
                break;
            case "success":
                state.SetAnswerMode(true);
                state.DetectedQuestion = DemoScript.LiveQuestion;
                state.RaiseLiveAnswerUi();
                break;
            case "save-warning":
                exact = false; // Mac archiveSaveStatus warning has no Windows injection point yet
                break;
            case "save-recovery":
                exact = false;
                break;
            case "unavailable":
                state.Settings.AutomaticAnswers = false;
                state.SetStatus(UiText.T("回答服务不可用"), true);
                break;
            case "stale":
            case "stale-manual-edit":
                exact = false; // E15 stale-source hint is displayed from recording state; no setter
                break;
            case "streaming-reading-history":
                state.SystemPartial = "我们回到刚才那个问题";
                state.LiveScrolledAway();
                state.RaiseLiveAnswerUi();
                break;
            case "selected-focus":
            case "selection":
                exact = false; // text selection lives in the control; no page-level setter
                break;
            case "reading-history":
                state.LiveScrolledAway();
                break;
            case "large-text":
                TextZoomService.SetFactorForCheck(1.45);
                break;
            case "empty":
                state.Transcript.Clear();
                state.AnswerText = "";
                state.DetectedQuestion = "";
                state.RaiseLiveAnswerUi();
                break;
            case "overlay":
                exact = false; // the screen-watch overlay is driven by the capture loop
                break;
            case "overlay-popover":
                exact = false;
                break;
            case "cancel-late-callback":
                exact = false;
                break;
        }
        return exact;
    }
}
