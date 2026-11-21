using System.Text.Json;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E5 smoke check: the answer-area modules. It proves the answer history reads live answers
    /// while recording and the archive after a meeting, that the history cursor shows an older
    /// answer, that the module layout presets/detach/dock round-trip through JSON, and it captures
    /// the detached module windows (answer / history / question) as screenshots next to the report.
    /// </summary>
    private async Task RunAssistantUiCheckAsync(string outputPath)
    {
        var failures = new List<string>();
        var checks = new List<string>();
        void Check(bool ok, string what)
        {
            checks.Add((ok ? "ok: " : "FAILED: ") + what);
            if (!ok) failures.Add(what);
        }

        var shotDirectory = Path.Combine(Path.GetDirectoryName(outputPath)!, "assistant-shots");
        Directory.CreateDirectory(shotDirectory);
        var windows = new List<DetachedModuleWindow>();
        try
        {
            state.LoadDemo(DemoStages.Live);
            state.Section = AppSection.Live;
            await Task.Delay(500);
            await PumpAsync();
            state.RefreshSessionAnswerHistory();

            // ---- answer history reads the live session while recording ---------------------
            Check(state.Recording, "demo live stage is recording");
            Check(state.SessionAnswerHistory.Count >= 2, "answer history lists the live answers");
            Check(state.AnswerHistoryRows.Count == state.SessionAnswerHistory.Count, "history rows mirror the records");
            Check(state.AnswerHistoryRows[0].Index == state.SessionAnswerHistory.Count - 1, "history rows are newest first");
            Check(state.AnswerHistoryHasMultiple, "history navigation is offered with several answers");

            // ---- history cursor shows an older answer and can return to the latest ---------
            var oldest = state.SessionAnswerHistory[0];
            state.SelectAnswerHistory(0);
            Check(state.AnswerHistoryCursor == 0, "clicking the first row sets the history cursor");
            Check(state.DisplayedAnswerText == oldest.Answer, "the answer column shows the archived answer text");
            Check(state.DisplayedQuestionText == oldest.Question, "the answer column shows that answer's question");
            Check(state.HistoryPositionText.StartsWith("1 / "), "the history position reads 1 / N");
            Check(state.DisplayedAnswerRecord?.Id == oldest.Id, "the displayed record follows the cursor");
            state.ShowLatestAnswer();
            Check(state.AnswerHistoryCursor is null && state.DisplayedAnswerText == state.AnswerText, "回到最新 clears the cursor");

            // ---- after a meeting the same panel reads the archive ---------------------------
            state.Recording = false;
            await Task.Delay(100);
            state.RefreshSessionAnswerHistory();
            Check(state.SelectedSessionAnswers.Count > 0 && state.SessionAnswerHistory.Count == state.SelectedSessionAnswers.Count,
                "answer history falls back to the archived answers");
            Check(state.SessionAnswerHistory.Count > 0 && state.SessionAnswerHistory[0].Id == state.SelectedSessionAnswers[0].Id,
                "archived answers keep their order");
            state.Recording = true;

            // ---- layout presets, detach/dock and persistence --------------------------------
            var standard = state.AssistantLayoutState;
            Check(standard.UsesTwoColumns, "standard layout uses two columns");
            state.ApplyAssistantPreset(AssistantLayout.Preset.AnswerOnly);
            Check(state.AssistantLayoutState.Slots.Count == 2, "answer-only preset keeps question + answer");
            state.ApplyAssistantPreset(AssistantLayout.Preset.Standard);
            state.OpenDetachedModule(AssistantModule.Answer);
            Check(state.AssistantLayoutState.IsDetached(AssistantModule.Answer), "detaching the answer module records it in the layout");
            state.DockDetachedModule(AssistantModule.Answer);
            Check(state.AssistantLayoutState.IsDocked(AssistantModule.Answer), "docking the answer module puts it back");
            state.OpenDetachedModule(AssistantModule.Answer);
            state.OpenDetachedModule(AssistantModule.History);
            state.DockDetachedModule(AssistantModule.History);
            state.DockDetachedModule(AssistantModule.Answer);
            Check(File.Exists(state.AssistantLayoutJsonPath), "the layout is saved as JSON next to the data");
            var reloaded = AssistantLayout.FromJson(File.ReadAllText(state.AssistantLayoutJsonPath));
            Check(reloaded.IsDocked(AssistantModule.Conversation) && reloaded.IsDocked(AssistantModule.Answer),
                "the saved layout round-trips");

            // ---- detection strip + screenshot card state -----------------------------------
            Check(state.HasLiveCandidate && state.LiveCandidateText.Length > 0, "detection strip has a candidate");
            var previewPath = Path.Combine(Path.GetTempPath(), "nextthought-assistant-preview-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(previewPath, TinyPng);
            using (var bitmap = new Avalonia.Media.Imaging.Bitmap(previewPath))
                state.SetScreenshotPreview(previewPath, bitmap);
            Check(state.HasLiveScreenshot, "screenshot card shows the waiting screenshot");
            state.ClearScreenshotPreview();
            Check(!state.HasLiveScreenshot, "cancelling clears the screenshot card");

            // ---- capture the detached module windows (assistant evidence) ------------------
            foreach (var module in new[] { AssistantModule.Answer, AssistantModule.History, AssistantModule.Question })
            {
                var window = new DetachedModuleWindow { DataContext = state, Module = module };
                windows.Add(window);
                window.Show();
                await Task.Delay(350);
                await PumpAsync();
                CaptureVisual(window, Path.Combine(shotDirectory,
                    "module-" + AssistantLayout.ModuleKey(module) + ".png"));
            }
        }
        catch (Exception error)
        {
            failures.Add(error.GetType().Name + ": " + error.Message);
        }
        finally
        {
            foreach (var window in windows)
            {
                try { window.Close(); } catch { }
            }
            state.ApplyAssistantPreset(AssistantLayout.Preset.Standard);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            passed = failures.Count == 0,
            failures,
            checks,
            screenshots = Directory.GetFiles(shotDirectory).Select(Path.GetFileName).ToArray()
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
        Console.WriteLine(failures.Count == 0
            ? "assistant-ui-check: passed"
            : "assistant-ui-check: failed " + string.Join("; ", failures));
    }
}
