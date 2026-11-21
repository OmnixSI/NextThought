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

            // ---- §6.5 stage 4: speaker labels, three tabs, live notes, echo restore ----------
            var speakerNames = state.Transcript.Select(row => row.SpeakerName).Distinct().ToList();
            Check(speakerNames.Contains(UiText.T("对方")) && speakerNames.Contains(UiText.T("我")),
                "the transcript speaker column reads 对方 / 我");
            Check(state.Transcript.All(row => !row.SpeakerName.Contains(':')),
                "the speaker column carries no timestamp");
            Check(state.LiveNotesStatusText.Length > 0, "实时纪要 tab has the Mac status line");
            Check(state.LiveNotesHintText.Length > 0, "实时纪要 tab has the Mac empty state");
            var livePage = PageHost.Content as LiveView;
            Check(livePage is not null, "the live page is mounted");
            Check(livePage?.FindControl<RadioButton>("LiveNotesTab") is not null,
                "the right column has the 实时纪要 tab");
            Check(livePage?.SelectRightTabForCheck("live-notes") == true, "实时纪要 tab can be selected");
            await Task.Delay(250);
            await PumpAsync();
            var stage4Shots = Path.Combine(Path.GetDirectoryName(outputPath)!, "live-shots");
            Directory.CreateDirectory(stage4Shots);
            CaptureVisual(this, Path.Combine(stage4Shots, "live-notes-light.png"));
            // The same tab in the dark theme, then back to light.
            if (Application.Current is { } app)
                app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            await Task.Delay(300);
            await PumpAsync();
            CaptureVisual(this, Path.Combine(stage4Shots, "live-notes-dark.png"));
            if (Application.Current is { } lightAgain)
                lightAgain.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            await Task.Delay(200);
            await PumpAsync();
            Check(state.LiveShowsInsightsPanel ==
                  (state.Settings.InsightScreenWatch && state.SelectedMode == MeetingMode.Academic),
                "the insights panel follows Mac's screenWatch && followsSlides rule");
            // E7 evidence: the 屏幕与讲稿 panel embedded in the transcript column (Mac 240 pt) for
            // the academic scene while screen awareness is on.
            var savedMode = state.SelectedMode;
            var savedWatch = state.Settings.InsightScreenWatch;
            state.Settings.InsightScreenWatch = true;
            state.SelectedMode = MeetingMode.Academic;
            state.RaiseLiveInsightsEmbed();
            await Task.Delay(300);
            await PumpAsync();
            Check(livePage?.FindControl<InsightsPanel>("LiveInsightsPanel")?.IsVisible == true,
                "the 屏幕与讲稿 panel is embedded in the live transcript column");
            CaptureVisual(this, Path.Combine(stage4Shots, "insights-embedded-light.png"));
            state.Settings.InsightScreenWatch = savedWatch;
            state.SelectedMode = savedMode;
            state.RaiseLiveInsightsEmbed();
            await Task.Delay(200);
            await PumpAsync();
            livePage?.SelectRightTabForCheck(state.AnswerModeIsAuto ? "answers" : "my-notes");
            // Echo restore last: it mutates the demo transcript the screenshots show.
            var echoRow = state.Transcript.FirstOrDefault(row => row.IsEcho);
            Check(echoRow is not null, "the demo transcript carries a suspected-echo line");
            if (echoRow is not null)
            {
                state.MarkNotEcho(echoRow.Id);
                Check(!echoRow.IsEcho, "是我说的 puts the echo line back into the conversation");
            }

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

            // ---- E6: answer language choice, bilingual pair layout, question turn -------------
            var english = AnswerLanguage.Fields("en", true, DemoScript.LiveQuestion);
            Check(english.Answer == "English" && english.Gloss == "\u7b80\u4f53\u4e2d\u6587",
                "回答语言=English + 对照 asks for an English answer with a Chinese gloss");
            var chinese = AnswerLanguage.Fields("zh", true, "Why use Redis?");
            Check(chinese.Answer == "\u7b80\u4f53\u4e2d\u6587" && chinese.Gloss is null,
                "回答语言=中文 turns the gloss off");
            var auto = AnswerLanguage.Fields("auto", true, DemoScript.LiveQuestion);
            Check(auto.Answer != null, "跟随问题 decides the language from the question");

            var bilingual = "> \u95ee\uff1aWhat does the examiner want to know?\n\n" +
                "The result holds even without ground-truth labels.\n" +
                "> \u8bd1\uff1a\u6ca1\u6709\u771f\u5b9e\u6807\u7b7e\u4e5f\u6210\u7acb\u3002\n\n" +
                "We compare it with a simpler baseline.\n" +
                "> \u8bd1\uff1a\u6211\u4eec\u548c\u66f4\u7b80\u5355\u7684\u57fa\u7ebf\u6bd4\u8f83\u3002";
            state.AnswerText = bilingual;
            Check(state.IsBilingualAnswer, "a bilingual answer is detected");
            Check(!BilingualAnswer.Spoken(bilingual).Contains('>'), "the spoken text drops the gloss lines");
            Check(BilingualAnswer.Question(bilingual)?.StartsWith("What does") == true, "the asked question is read from the gloss");
            Check(state.IslandHeadlineGloss.Length > 0 || state.IslandDetailGloss.Length > 0,
                "the cue island carries the meaning under its line");
            var shots = Path.Combine(Path.GetDirectoryName(outputPath)!, "live-shots");
            Directory.CreateDirectory(shots);
            await Task.Delay(250);
            await PumpAsync();
            CaptureVisual(this, Path.Combine(shots, "bilingual-answer.png"));
            state.AnswerText = state.CurrentAnswerHistory.Count > 0 ? state.CurrentAnswerHistory[^1].Answer : DemoScript.LiveAnswer;
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
