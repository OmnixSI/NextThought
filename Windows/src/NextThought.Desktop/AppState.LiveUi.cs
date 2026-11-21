using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Styling;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>One numbered web source behind the web version of the answer (Mac WebSources [n]).</summary>
public sealed record WebSourceRow(int Number, WebResult Result)
{
    public string Title => Result.Title;
    public string Url => Result.Url ?? "";
    public string Label => "[" + Number + "] " + Result.Title;
}

/// <summary>One material excerpt behind an answer (Mac EvidenceDetails materials).</summary>
public sealed record EvidenceMaterialRow(string Citation, string Text);

/// <summary>
/// E4: the live page's Mac-only surfaces — the top bar's listening status, phone/screen status,
/// answer-mode switch, the answer settings menu, the web status line with its sources, the
/// evidence line/details, and the bottom composer.
/// </summary>
public partial class AppState
{
    // ---- top bar: recording timer / listening status -------------------------------

    /// <summary>The scene's pigment for the question marker (Mac QuestionMark capsule).</summary>
    public IBrush LiveSceneTintBrush => new SolidColorBrush(Color.Parse(LiveSceneTint()));

    public IBrush LiveSceneTintSoftBrush => new SolidColorBrush(Color.Parse(LiveSceneTint()), 0.13);

    /// <summary>Mac Brand.accent.opacity(0.06): the slide section's wash (InsightsPanel SlideSection).</summary>
    public IBrush LiveSceneTintWashBrush => new SolidColorBrush(Color.Parse(LiveSceneTint()), 0.06);

    private string LiveSceneTint()
    {
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        return SceneTheme.For(SelectedMode).Tint(dark);
    }

    public void RaiseLiveSceneUi()
    {
        OnPropertyChanged(nameof(LiveSceneTintBrush));
        OnPropertyChanged(nameof(LiveSceneTintSoftBrush));
        OnPropertyChanged(nameof(LiveSceneTintWashBrush));
    }


    public bool IsLiveOnAir => Recording && !ListeningPaused;

    public string LiveListeningStatus => !Recording
        ? UiText.T("未在聆听")
        : ListeningPaused ? UiText.T("已暂停") : UiText.T("正在聆听");

    public string LivePauseButtonLabel => ListeningPaused ? UiText.T("继续聆听") : UiText.T("暂停聆听");

    public string LivePauseButtonText => ListeningPaused ? UiText.T("继续") : UiText.T("暂停");

    public string LiveAudioEngineLabel => Settings.SelectedCloudStt is { Value.Length: > 0 }
        ? UiText.T("云端识别")
        : UiText.T("本机识别");

    // ---- top bar: phone and screen status -----------------------------------------

    public bool LiveCompanionOn => CompanionRunning;

    public bool LiveScreenWatchOn => Settings.InsightScreenWatch;

    public bool LiveScreenWatchHasError => ScreenWatchError.Length > 0;

    public string LiveScreenWatchHelp => LiveScreenWatchHasError
        ? ScreenWatchError
        : UiText.T("屏幕感知已开启：跟随讲稿，相关的屏幕文字会用于回答");

    // ---- top bar: automatic answers / record only ---------------------------------

    public bool AnswerModeIsAuto => Settings.AutomaticAnswers;

    /// <summary>Mac setLiveMode(prompting:): the switch only decides automatic answers.</summary>
    public void SetAnswerMode(bool automatic)
    {
        if (Settings.AutomaticAnswers == automatic) return;
        Settings.AutomaticAnswers = automatic;
        if (!automatic) DetectedQuestion = "";
        OnPropertyChanged(nameof(AnswerModeIsAuto));
        SetStatus(automatic ? "切换为自动回答：识别到问题后会生成参考回答。" : "切换为只记录：不再自动回答，仍可手动提问。");
    }

    // ---- top bar: answer settings menu (model / fast / web / length) --------------

    public bool LiveWebSearchIsOn
    {
        get => Settings.WebSearch;
        set
        {
            if (Settings.WebSearch == value) return;
            Settings.WebSearch = value;
            OnPropertyChanged(nameof(LiveWebSearchIsOn));
            _ = Settings.SaveAsync();
        }
    }

    public bool LiveFastModeAvailable => Settings.CodexFastAvailable;

    public bool LiveFastMode
    {
        get => Settings.CodexFastMode;
        set
        {
            if (Settings.CodexFastMode == value) return;
            Settings.CodexFastMode = value;
            OnPropertyChanged(nameof(LiveFastMode));
            _ = Settings.SaveAsync();
        }
    }

    public string LiveFastModeLabel => UiText.Format("快速模式 ⚡ · %@", Settings.CodexFastLabel);

    public string LiveAnswerModelLabel => Settings.Model.Length == 0 ? UiText.T("选择模型") : Settings.Model;

    public IReadOnlyList<ModelChoice> LiveModelChoices => Settings.ModelChoicesList;

    public string LiveAnswerStyleIsDetailed => CurrentAnswerStyle == "detailed" ? "True" : "False";

    public bool CurrentAnswerIsDetailed
    {
        get => CurrentAnswerStyle == "detailed";
        set
        {
            var style = value ? "detailed" : "concise";
            if (CurrentAnswerStyle == style) return;
            liveAnswerStyleOverride = style;
            OnPropertyChanged(nameof(CurrentAnswerStyle));
            OnPropertyChanged(nameof(CurrentAnswerIsDetailed));
        }
    }

    /// <summary>The meeting's frozen answer style, overridable from the live menu (Mac liveAnswerStyle).</summary>
    public string CurrentAnswerStyle =>
        liveAnswerStyleOverride
        ?? (CurrentSession is { } meta ? workspace.SeriesOfSession(meta.Id.ToString())?.Style : null)
        ?? "concise";

    private string? liveAnswerStyleOverride;

    // ---- answer column: web status line and sources --------------------------------

    public bool HasWebResults => WebResults.Count > 0;

    public bool HasWebLookupLine => HasWebResults;

    public string LiveWebStatusLine => UiText.Format("联网版 · %lld 个来源", WebResults.Count);

    /// <summary>The numbered sources behind the web version (Mac WebSources).</summary>
    public IReadOnlyList<WebSourceRow> LiveWebSources =>
        WebResults.Select((result, index) => new WebSourceRow(index + 1, result)).ToArray();

    public void OpenWebSource(WebSourceRow row)
    {
        if (row.Url.Length == 0) return;
        try { Process.Start(new ProcessStartInfo(row.Url) { UseShellExecute = true }); }
        catch (Exception error) { SetStatus("打开来源失败：" + error.Message, true); }
    }

    // ---- answer column: evidence line and details ----------------------------------

    public EvidenceBundle? DisplayedEvidence => DisplayedAnswerRecord?.Evidence;

    public bool HasDisplayedEvidence => DisplayedEvidence is not null;

    public bool EvidenceHasLimits => DisplayedEvidence is { Notes.Count: > 0 };

    /// <summary>"依据：资料 2 · 屏幕 · 联网 3 · 1 项限制" (Mac EvidenceLine.summary).</summary>
    public string EvidenceSummary
    {
        get
        {
            if (DisplayedEvidence is not { } evidence) return "";
            var parts = new List<string> { UiText.Format("资料 %lld", evidence.Materials.Count) };
            if (evidence.Screen is not null) parts.Add(UiText.T("屏幕"));
            if (evidence.WebSources.Count > 0) parts.Add(UiText.Format("联网 %lld", evidence.WebSources.Count));
            var line = UiText.Format("依据：%@", string.Join(" · ", parts));
            if (evidence.Notes.Count > 0) line += UiText.Format(" · %lld 项限制", evidence.Notes.Count);
            return line;
        }
    }

    public IReadOnlyList<EvidenceMaterialRow> EvidenceMaterialRows => DisplayedEvidence is { } evidence
        ? evidence.Materials.Select(item => new EvidenceMaterialRow(item.Citation, item.Text)).ToArray()
        : [];

    public string EvidenceRetrievalText => DisplayedEvidence?.Retrieval ?? "";

    public string EvidenceScreenSource => DisplayedEvidence?.Screen?.Source ?? "";

    public IReadOnlyList<WebSourceRow> EvidenceWebSourceRows => DisplayedEvidence is { } evidence
        ? evidence.WebSources.Select((result, index) => new WebSourceRow(index + 1, result)).ToArray()
        : [];

    public IReadOnlyList<string> EvidenceNoteRows => DisplayedEvidence?.Notes ?? [];

    // ---- answer column: header and empty state ------------------------------------

    public string AnswerColumnTitle => Answering ? UiText.T("正在回答") : UiText.T("建议回答");

    public bool HasAnswerText => AnswerText.Length > 0;

    public string AnswerEmptyHint => Answering
        ? UiText.T("正在生成…")
        : UiText.Format("对方提问时，这里会自动给出建议回答。也可以在下方输入问题，或按 %@ 立即回答。", ForceAnswerShortcut);

    public string ForceAnswerShortcut => "Ctrl+Alt+Enter";

    /// <summary>Raised when a view needs the computed answer/evidence names refreshed.</summary>
    public void RaiseLiveAnswerUi()
    {
        foreach (var name in new[]
                 {
                     nameof(DisplayedAnswerRecord), nameof(DisplayedEvidence), nameof(HasDisplayedEvidence),
                     nameof(EvidenceHasLimits), nameof(EvidenceSummary), nameof(EvidenceMaterialRows),
                     nameof(EvidenceRetrievalText), nameof(EvidenceScreenSource), nameof(EvidenceWebSourceRows),
                     nameof(EvidenceNoteRows), nameof(AnswerColumnTitle), nameof(HasAnswerText),
                     nameof(AnswerEmptyHint), nameof(AnswerHistoryHasMultiple), nameof(HasWebResults),
                     nameof(HasWebLookupLine), nameof(LiveWebStatusLine), nameof(LiveWebSources),
                     nameof(CurrentAnswerStyle), nameof(CurrentAnswerIsDetailed), nameof(AnswerModeIsAuto),
                     nameof(LiveAnswerModelLabel), nameof(LiveFastMode), nameof(LiveFastModeAvailable),
                     nameof(LiveFastModeLabel), nameof(LiveWebSearchIsOn), nameof(LiveCueAlwaysOpen),
                     nameof(LivePresenterMode), nameof(LivePauseButtonText), nameof(LiveListeningStatus),
                     nameof(LiveCandidateText), nameof(HasLiveCandidate), nameof(LiveSeriesName),
                     nameof(DisplayedAnswerText), nameof(DisplayedQuestionText), nameof(HasDisplayedAnswer),
                     nameof(DisplayedAnswerRecord), nameof(HistoryPositionText), nameof(HistoryCursorActive),
                     nameof(AnswerHistoryCountText), nameof(SessionAnswerHistory), nameof(HasSessionAnswerHistory),
                     nameof(AnswerHistoryHasMultiple), nameof(IsBilingualAnswer),
                     nameof(IslandHeadlineGloss), nameof(IslandDetailGloss),
                     nameof(HasIslandHeadlineGloss), nameof(HasIslandDetailGloss)
                 })
            OnPropertyChanged(name);
    }

    // ---- transcript: candidate strip (Mac CandidateStrip) --------------------------

    /// <summary>The question the candidate strip offers (demo stages pin it; live uses the tracker).</summary>
    public string LiveCandidateText => tracker.Current?.FullText ?? liveCandidateOverride ?? "";

    public bool HasLiveCandidate => LiveCandidateText.Length > 0;

    private string? liveCandidateOverride;

    /// <summary>Answers the candidate row instead of the last detected question (Mac answerCandidate).</summary>
    public void AnswerLiveCandidate()
    {
        var candidate = LiveCandidateText;
        if (candidate.Length == 0) return;
        DetectedQuestion = candidate;
        if (ForceAnswerCommand.CanExecute(null)) ForceAnswerCommand.Execute(null);
        OnPropertyChanged(nameof(HasLiveCandidate));
    }

    // ---- top bar: series name under the title (Mac titleBlock) -----------------------

    public string LiveSeriesName => CurrentSession is { } meta
        ? (meta.WorkspaceName is { Length: > 0 } name ? name : workspace.SeriesOfSession(meta.Id.ToString())?.Name ?? "")
        : "";

    // ---- E6: bilingual answers and the cue island's meaning lines --------------------

    /// <summary>True when the answer carries "> 问：" / "> 译：" gloss lines (Mac BilingualAnswer).</summary>
    public bool IsBilingualAnswer => BilingualAnswer.IsBilingual(DisplayedAnswerText);

    /// <summary>The meaning under the island's first line (Mac CueRow.withGloss).</summary>
    public string IslandHeadlineGloss => IslandLines.Count > 0 ? IslandLines[0].Gloss : "";

    public string IslandDetailGloss => IslandLines.Count > 1
        ? string.Join(" ", IslandLines.Skip(1).Select(line => line.Gloss).Where(gloss => gloss.Length > 0))
        : "";

    public bool HasIslandHeadlineGloss => IslandHeadlineGloss.Length > 0;

    public bool HasIslandDetailGloss => IslandDetailGloss.Length > 0;

    // ---- bottom composer (Mac LiveComposer) ----------------------------------------

    /// <summary>The screenshot waiting in the composer, if any (Mac screenshotPreview).</summary>
    public bool HasLiveScreenshot => HasScreenshotPreview;

    public string LiveComposerPlaceholder => HasScreenshotPreview
        ? UiText.T("对截图的补充要求（可留空）")
        : Settings.AutomaticAnswers
            ? UiText.T("输入问题，回车发送")
            : UiText.Format("需要帮忙？输入问题，或按 %@ 回答对方刚问的", ForceAnswerShortcut);

    public bool CanSubmitLiveComposer => QuestionInput.Trim().Length > 0 || HasScreenshotPreview;

    public void SubmitLiveComposer()
    {
        if (HasScreenshotPreview) { _ = AnalyzeScreenshotAsync(); return; }
        if (AskCommand.CanExecute(null)) AskCommand.Execute(null);
    }

    public void ToggleLiveCueAlwaysOpen()
    {
        Settings.CueAlwaysOpen = !Settings.CueAlwaysOpen;
        _ = Settings.SaveAsync();
        SetStatus(Settings.CueAlwaysOpen ? "提词岛会中一直展开。" : "提词岛恢复为鼠标移上去才展开。");
    }

    public bool LiveCueAlwaysOpen => Settings.CueAlwaysOpen;

    public bool LivePresenterMode => Settings.PresenterMode;
}
