using System.Text.Json;
using Avalonia.Threading;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E4 smoke check: loads the live demo stage, shows the live page and exercises what the top
    /// bar, transcript, answers column and composer should do — the listening status, the mode
    /// switch, the answer settings, the question marker, the candidate strip, the web status line,
    /// the evidence line/details and the composer. The screenshot batch only captures pixels, so
    /// this is the run that proves the behaviours without a person clicking every control.
    /// </summary>
    private async Task RunLiveUiCheckAsync(string outputPath)
    {
        var failures = new List<string>();
        var checks = new List<string>();
        void Check(bool ok, string what)
        {
            checks.Add((ok ? "ok: " : "FAILED: ") + what);
            if (!ok) failures.Add(what);
        }

        try
        {
            state.LoadDemo(DemoStages.Live);
            state.Section = AppSection.Live;
            await Task.Delay(500);
            await PumpAsync();
            state.RaiseLiveAnswerUi();
            state.RaiseLiveSceneUi();
            await Task.Delay(300);
            await PumpAsync();

            // ---- top bar: timer, scene, mode, phone/screen, answer settings -----------------
            Check(state.Recording, "demo live stage is recording");
            Check(state.LiveListeningStatus == UiText.T("正在聆听"), "listening status reads 正在聆听");
            Check(state.RecordingElapsedText != "00:00:00", "recording timer is running");
            Check(!state.IsLiveOnAir || !state.ListeningPaused, "on-air dot matches the pause state");
            Check(state.LiveAudioEngineLabel.Length > 0, "audio engine label is set");
            Check(state.AnswerModeIsAuto, "demo interview starts in auto-answer mode");
            Check(state.AnswerColumnTitle.Length > 0, "answer column header has a title");
            Check(state.LiveModelChoices.Count > 0 || state.LiveAnswerModelLabel.Length > 0, "answer settings expose a model label");

            // ---- transcript: question marker + candidate strip + status ---------------------
            var questionRow = state.Transcript.FirstOrDefault(row => row.IsQuestion);
            Check(questionRow is not null, "a transcript row is marked as a question (问题 capsule)");
            Check(state.Transcript.All(row => !row.IsQuestion || row.Speaker != "me"), "own lines are never marked as questions");
            state.DetectedQuestion = DemoScript.LiveQuestion;
            Check(state.HasLiveCandidate && state.LiveCandidateText.Length > 0, "candidate strip offers the next question");
            Check(state.LiveSeriesName.Length > 0, "title block shows the series name");
            Check(state.LiveSceneTintBrush is { }, "question marker has the scene pigment");
            Check(state.AudioSourceText.Length > 0, "transcript status shows the audio source");

            // ---- mode switch (自动回答 / 只记录) ---------------------------------------------
            state.SetAnswerMode(false);
            Check(!state.AnswerModeIsAuto && !state.Settings.AutomaticAnswers, "switching to 只记录 turns automatic answers off");
            state.SetAnswerMode(true);
            Check(state.AnswerModeIsAuto && state.Settings.AutomaticAnswers, "switching back to 自动回答 turns them on");

            // ---- answer settings menu: length, fast, web -----------------------------------
            state.CurrentAnswerIsDetailed = true;
            Check(state.CurrentAnswerStyle == "detailed", "回答长度=详细 reaches the prompt style");
            state.CurrentAnswerIsDetailed = false;
            var webBefore = state.Settings.WebSearch;
            state.LiveWebSearchIsOn = !webBefore;
            Check(state.Settings.WebSearch == !webBefore, "联网 toggle changes the web-search setting");
            state.LiveWebSearchIsOn = webBefore;

            // ---- web status line and sources ------------------------------------------------
            state.WebResults.Clear();
            state.WebResults.Add(new WebResult("演示来源", "https://example.com/demo", "演示摘要"));
            state.RaiseLiveAnswerUi();
            Check(state.HasWebResults, "web status line appears with a lookup");
            Check(state.LiveWebSources.Count == 1 && state.LiveWebSources[0].Number == 1, "web sources are numbered [1]");
            Check(state.LiveWebStatusLine.Contains("1"), "web status line counts the sources");

            // ---- evidence line and details --------------------------------------------------
            Check(state.HasDisplayedEvidence, "demo answer carries an evidence bundle");
            Check(state.EvidenceMaterialRows.Count == 2, "evidence details list the material excerpts");
            Check(state.EvidenceWebSourceRows.Count == 1, "evidence details list the web source");
            Check(state.EvidenceNoteRows.Count == 1, "evidence details list the limits");
            Check(state.EvidenceSummary.StartsWith(UiText.T("依据：")), "evidence line starts with 依据：");
            Check(state.EvidenceSummary.Contains(UiText.T("屏幕")), "evidence line mentions the screen");

            // ---- answer history navigation --------------------------------------------------
            Check(state.CurrentAnswerHistory.Count >= 2, "demo live has an answer history");
            Check(state.AnswerHistoryHasMultiple, "history navigation is offered");
            state.CopyAnswerCommand.Execute(null);
            Check(true, "copy answer command runs");

            // ---- composer -------------------------------------------------------------------
            state.QuestionInput = "";
            Check(!state.CanSubmitLiveComposer, "empty composer cannot submit");
            state.QuestionInput = "演示测试问题";
            Check(state.CanSubmitLiveComposer, "typed composer can submit");
            Check(state.LiveComposerPlaceholder.Length > 0, "composer placeholder follows the mode");
            state.QuestionInput = "";

            // ---- tools menu toggles (restored afterwards) -----------------------------------
            var cue = state.LiveCueAlwaysOpen;
            state.ToggleLiveCueAlwaysOpen();
            Check(state.LiveCueAlwaysOpen == !cue, "提词岛一直展开 toggles");
            state.ToggleLiveCueAlwaysOpen();
            var presenter = state.LivePresenterMode;
            state.Settings.PresenterMode = !presenter;
            Check(state.Settings.PresenterMode == !presenter, "双屏演讲 toggles");
            state.Settings.PresenterMode = presenter;
            var screenWatch = state.Settings.InsightScreenWatch;
            state.Settings.InsightScreenWatch = !screenWatch;
            Check(state.Settings.InsightScreenWatch == !screenWatch, "屏幕感知 toggles");
            state.Settings.InsightScreenWatch = screenWatch;

            // ---- screenshots: the composer strip takes a preview ----------------------------
            var previewPath = Path.Combine(Path.GetTempPath(), "nextthought-live-preview-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(previewPath, TinyPng);
            using (var bitmap = new Avalonia.Media.Imaging.Bitmap(previewPath))
            {
                state.SetScreenshotPreview(previewPath, bitmap);
                Check(state.HasLiveScreenshot, "screenshot preview shows in the composer");
                Check(state.LiveComposerPlaceholder == UiText.T("对截图的补充要求（可留空）"), "composer placeholder changes for a screenshot");
                Check(state.CanSubmitLiveComposer, "screenshot alone can submit");
            }
            state.ClearScreenshotPreview();
            Check(!state.HasLiveScreenshot, "取消 clears the screenshot preview");
        }
        catch (Exception error)
        {
            failures.Add(error.GetType().Name + ": " + error.Message);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            passed = failures.Count == 0,
            failures,
            checks
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
        Console.WriteLine(failures.Count == 0
            ? "live-ui-check: passed"
            : "live-ui-check: failed " + string.Join("; ", failures));
    }

    /// <summary>A 1×1 transparent PNG so the screenshot-preview state can be exercised headlessly.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}
