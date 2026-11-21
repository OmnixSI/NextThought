using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E26b: the quick-settings presentation keys (Mac cueSurface / presenterSlideNotes) that change
/// what the floating cue shows without reopening the settings page.
/// </summary>
public partial class AppState
{
    /// <summary>True while PresenterMode keeps the meeting on the camera display's island.</summary>
    [ObservableProperty] private bool presenterLayoutActive;

    partial void OnPresenterLayoutActiveChanged(bool value) => OnPropertyChanged(nameof(HasCurrentSlidePoints));

    /// <summary>The quick panel toggled 讲的时候显示这一页的讲稿; the island follows immediately.</summary>
    public void NotifyPresenterSlideNotesChanged() => OnPropertyChanged(nameof(HasCurrentSlidePoints));

    // =========================================================================================
    // C5–C9: the cue island's data (Mac CueIslandView.swift). The window keeps the interaction
    // state (hover, pin, collapse) and asks these properties for what the island shows.
    // =========================================================================================

    // ---- meeting pulse and clock -----------------------------------------------------------

    public bool CueIslandRunning => Recording;

    public bool CueIslandPaused => ListeningPaused;

    public bool CueIslandAnswering => Answering;

    /// <summary>The clock on the right ear (CueIslandView.clock): m:ss, or h:mm:ss past the hour.</summary>
    public string CueIslandClockText
    {
        get
        {
            if (!Recording) return "";
            var seconds = Math.Max(0, (int)(DateTimeOffset.Now - recordingStartedAt).TotalSeconds);
            return seconds >= 3600
                ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
                : $"{seconds / 60}:{seconds % 60:00}";
        }
    }

    // ---- answer / question -----------------------------------------------------------------

    public string CueIslandDisplayedAnswer => DisplayedAnswerText;

    public string CueIslandDisplayedQuestion => DisplayedQuestionText;

    /// <summary>An answer on show, or one being written (CueIslandView.hasAnswer).</summary>
    public bool CueIslandHasAnswer => DisplayedAnswerText.Length > 0 || Answering;

    public bool CueIslandIsBilingual => BilingualAnswer.IsBilingual(DisplayedAnswerText);

    /// <summary>What was asked, in my own language (the "问：" gloss of a bilingual answer).</summary>
    public string CueIslandAsked => BilingualAnswer.Question(DisplayedAnswerText) ?? "";

    /// <summary>A question of several sentences ends with what is actually asked.</summary>
    public string CueIslandQuestion
    {
        get
        {
            var text = AnswerRefinements.OriginalQuestion(DisplayedQuestionText);
            return text.Length > 0 ? text : "";
        }
    }

    public CueStyle CueIslandCueStyle => SceneCueStyle;

    public double CueIslandFontSize => Settings.TeleprompterFontSize;

    public CueIslandWidth CueIslandWidthChoice => Settings.SelectedCueWidth?.Value switch
    {
        "narrow" => CueIslandWidth.Narrow,
        "wide" => CueIslandWidth.Wide,
        _ => CueIslandWidth.Regular
    };

    /// <summary>The cue lines of the answer on show, long enough for the island to window them.</summary>
    public IReadOnlyList<CueLine> CueIslandCueLines()
    {
        if (string.IsNullOrWhiteSpace(DisplayedAnswerText)) return [];
        return CueScript.Lines(DisplayedAnswerText, CueIslandCueStyle);
    }

    /// <summary>My own finished sentences since the answer started, for the coverage ticks.</summary>
    public IReadOnlyList<TranscriptLine> CueIslandMySpeechSince(DateTimeOffset since) => currentLines
        .Where(line => line.Speaker == "me" && line.SuspectedEcho != true && line.Date >= since)
        .ToList();

    // ---- what the other side is saying -----------------------------------------------------

    public string CueIslandSystemPartial => SystemPartial;

    public string CueIslandMicrophonePartial => MicrophonePartial;

    public string CueIslandPartialTranslation => LiveTranslationEnabled ? SystemPartialTranslation : "";

    /// <summary>
    /// C8: the island keeps one translation lane for their speech (Mac IslandTranslation); a
    /// per-line interpretation wins when the translator has already returned it.
    /// </summary>
    public string CueIslandIslandTranslation => IslandTranslation.Length > 0 ? IslandTranslation : SystemPartialTranslation;

    public bool CueIslandLiveTranslation => LiveTranslationEnabled;

    /// <summary>下一条要问的问题: the detector's candidate, while it is still worth offering.</summary>
    public string CueIslandCandidateText => liveCandidateOverride ?? tracker.Current?.FullText ?? "";

    public bool CueIslandHasCandidate => CueIslandCandidateText.Trim().Length > 0;

    /// <summary>
    /// What the other side has said in this turn (CueIslandView.theirTurn): their finished
    /// sentences of the last half minute and the one still being said, with the interpretation.
    /// </summary>
    public (string Text, string Translation) CueIslandTheirTurn
    {
        get
        {
            var texts = new List<string>();
            var translations = new List<string>();
            var recent = currentLines.Where(line => line.SuspectedEcho != true).TakeLast(6).ToList();
            if (recent.LastOrDefault() is { Speaker: not "me" } last)
            {
                for (var index = recent.Count - 1; index >= 0; index--)
                {
                    var entry = recent[index];
                    if (entry.Speaker == "me" || (last.Date - entry.Date).TotalSeconds >= 30) break;
                    texts.Insert(0, entry.Text);
                    var translated = entry.TranslatedText ?? entry.Translation?.Text ?? "";
                    if (translated.Length > 0) translations.Insert(0, translated);
                }
            }
            var partial = SystemPartial.Trim();
            if (partial.Length > 0)
            {
                texts.Add(partial);
                if (SystemPartialTranslation.Length > 0) translations.Add(SystemPartialTranslation);
            }
            return (string.Join(" ", texts),
                LiveTranslationEnabled ? string.Join(" ", translations) : "");
        }
    }

    /// <summary>The last thing said was a sentence of theirs, and nothing has answered it yet.</summary>
    public bool CueIslandTheirFloor
    {
        get
        {
            var last = currentLines.LastOrDefault(line => line.SuspectedEcho != true);
            return last is { Speaker: not "me" } && last.Text.Length >= 12
                && (DateTimeOffset.Now - last.Date).TotalSeconds < 45;
        }
    }

    /// <summary>They have said a sentence or more that no answer covers, and I have not replied.</summary>
    public bool CueIslandUnansweredTurn
    {
        get
        {
            if (questionTurn.Unanswered < 8 || questionTurn.LastHeardAt is not { } heard) return false;
            if ((DateTimeOffset.Now - heard).TotalSeconds >= 20) return false;
            return microphoneLevel < 0.012;
        }
    }

    // ---- deck ------------------------------------------------------------------------------

    public bool CueIslandAutomaticAnswers => Settings.AutomaticAnswers;

    /// <summary>True when an answer service is connected; the scripted captures override it.</summary>
    public bool CueIslandAnswerEngineReady => cueAnswerReadyOverride ?? AnswerEngineReady;

    private bool? cueAnswerReadyOverride;

    /// <summary>Demo/self-check hook: pretend the answer service is (or is not) connected.</summary>
    public void CueIslandDemoAnswerReady(bool? ready)
    {
        cueAnswerReadyOverride = ready;
        OnPropertyChanged(nameof(CueIslandAnswerEngineReady));
        OnPropertyChanged(nameof(CueIslandModeWarning));
    }

    public string CueIslandModeText => ListeningPaused
        ? UiText.T("已暂停")
        : Settings.AutomaticAnswers ? UiText.T("自动回答") : UiText.T("只记录");

    public bool CueIslandModeWarning => Settings.AutomaticAnswers && !ListeningPaused && !CueIslandAnswerEngineReady;

    /// <summary>The newest line of the conversation, while it is still being said if possible.</summary>
    public (bool IsMe, string Text, string Translation)? CueIslandLastHeard
    {
        get
        {
            var partial = SystemPartial.Trim();
            if (partial.Length > 0)
                return (false, partial, LiveTranslationEnabled ? SystemPartialTranslation : "");
            var entry = currentLines.LastOrDefault(line => line.SuspectedEcho != true);
            if (entry is null) return null;
            var isMe = entry.Speaker == "me";
            return (isMe, entry.Text,
                LiveTranslationEnabled && !isMe ? entry.TranslatedText ?? entry.Translation?.Text ?? "" : "");
        }
    }

    /// <summary>The meeting's series name (Mac workspace.name), or the meeting title without one.</summary>
    public string CueIslandSeriesName
    {
        get
        {
            if (cueSeriesNameOverride is { Length: > 0 } overrideName) return overrideName;
            if (LiveSeriesName.Length > 0) return LiveSeriesName;
            if (CurrentSession?.WorkspaceName is { Length: > 0 } name) return name;
            return CurrentSession?.Title ?? UiText.T("会议");
        }
    }

    private string cueSeriesNameOverride = "";

    /// <summary>Demo/self-check hook: the series name the island's deck shows (Mac workspace.name).</summary>
    public void CueIslandDemoSeriesName(string? name)
    {
        cueSeriesNameOverride = name ?? "";
        OnPropertyChanged(nameof(CueIslandSeriesName));
    }

    /// <summary>Demo/self-check hook: the candidate question the island offers while they speak.</summary>
    public void CueIslandDemoCandidate(string? text)
    {
        liveCandidateOverride = text is { Length: > 0 } ? text : null;
        OnPropertyChanged(nameof(CueIslandCandidateText));
        OnPropertyChanged(nameof(CueIslandHasCandidate));
    }

    public string CueIslandSceneName => SceneTitle;

    /// <summary>"语速 240/分 · 我说了 46%" from what the coach has so far (Mac coachStats).</summary>
    public string CueIslandCoachStats
    {
        get
        {
            if (coach is null || !Recording || CurrentSession is not { } session) return "";
            var snapshot = coach.Snapshot((DateTimeOffset.Now - session.StartedAt).TotalSeconds);
            var parts = new List<string>();
            if (snapshot.Pace is { } pace) parts.Add(UiText.Format("语速 %lld/分", (int)Math.Round(pace)));
            if (snapshot.TalkShare is { } share) parts.Add(UiText.Format("我说了 %lld%%", (int)Math.Round(share * 100)));
            return string.Join(" · ", parts);
        }
    }

    // ---- coach ear -------------------------------------------------------------------------

    public bool CueIslandCoachEnabled => coach is not null && Recording;

    /// <summary>The coach's nudge (Mac state.coach.nudge); empty while the coach is quiet.</summary>
    public string CueIslandCoachHint => CoachHint;

    /// <summary>The symbol of the last nudge, so the ear says what kind of hint it is.</summary>
    public string CueIslandCoachSymbol => lastNudge switch
    {
        CoachNudge.WrapUp => "moon.zzz",
        CoachNudge.SlowDown => "tortoise",
        CoachNudge.LetThemTalk => "bubble.left",
        CoachNudge.SpeakUp => "waveform",
        _ => "eye.fill"
    };

    // ---- slide notes (presenting) ----------------------------------------------------------

    public bool CueIslandPresenting => PresenterLayoutActive;

    public bool CueIslandPresenterShowsSlideNotes => PresenterLayoutActive
        ? Settings.PresenterSlideNotes
        : Settings.InsightFollowSlides;

    public bool CueIslandHasSlide => CurrentSlide is not null;

    /// <summary>第 4 页 · 后端二面 · 项目介绍.pptx / 第 4 页 · 我的讲稿.</summary>
    public string CueIslandSlideHeader
    {
        get
        {
            if (CurrentSlide is not { } slide) return "";
            var notes = SlideNotesFor(slide);
            var name = notes?.IsScript == true ? UiText.T("我的讲稿") : slide.DocumentName;
            return UiText.Format("第 %lld 页", slide.Number) + " · " + name;
        }
    }

    public bool CueIslandSlideIsScript => CurrentSlide is { } slide && SlideNotesFor(slide)?.IsScript == true;

    /// <summary>Covered/next markers of the talking points, from what I have read aloud.</summary>
    public IReadOnlyList<(string Text, bool Done, bool Next)> CueIslandSlidePoints
    {
        get
        {
            if (CurrentSlide is not { } slide || SlideNotesFor(slide) is not { } notes) return [];
            var (covered, next) = cueSlideCoveredOverride is { } scripted
                ? (scripted, AnswerPoints.Next(scripted, notes.Points.Count))
                : SlideCoverage(notes);
            return notes.Points.Select((point, index) => (point, covered.Contains(index), next == index)).ToList();
        }
    }

    private HashSet<int>? cueSlideCoveredOverride;

    public string CueIslandSlideTransition =>
        CurrentSlide is { } slide && SlideNotesFor(slide) is { } notes && notes.Transition.Length > 0
            ? UiText.T("下一页") + "  " + notes.Transition
            : "";

    /// <summary>
    /// Mac insights.generatingNotes shows ThinkingDots while a page's notes are being written.
    /// The Windows port prepares notes synchronously from the local cache, so there is never a
    /// page in flight; the property stays so the island keeps the Mac element and the self-check
    /// can assert its visibility rule.
    /// </summary>
    public bool CueIslandSlideNotesGenerating => false;

    public bool CueIslandSlidePrevEnabled => CurrentSlide is { Number: > 1 };

    public bool CueIslandSlideNextEnabled => CurrentSlide is { } slide && slide.Number < slides.Count;

    /// <summary>Demo/self-check hook: install one slide with its talking points (Mac snapshotIsland "slide").</summary>
    public void ApplyIslandSlide(string documentName, int pageNumber, int pageCount, string text,
        IReadOnlyList<string> points, string transition, bool isScript = false, int covered = 0)
    {
        slides =
        [
            new SlidePageInfo(pageNumber, text, documentName)
            {
                PageCount = pageCount,
                Notes = isScript ? string.Join("\n", points) : ""
            }
        ];
        slideIndex = 0;
        cueSlideCoveredOverride = covered > 0 ? new HashSet<int>(Enumerable.Range(0, covered)) : null;
        var info = slides[0];
        slideNotesCacheByPage[info.ToSlidePage().Key] = new SlideNotes
        {
            Points = points,
            Transition = transition,
            IsScript = isScript
        };
        foreach (var name in new[]
                 {
                     nameof(CurrentSlide), nameof(CueIslandHasSlide), nameof(CueIslandSlideHeader),
                     nameof(CueIslandSlideIsScript), nameof(CueIslandSlidePoints), nameof(CueIslandSlideTransition),
                     nameof(CueIslandSlideNotesGenerating), nameof(CueIslandSlidePrevEnabled),
                     nameof(CueIslandSlideNextEnabled), nameof(CueIslandSlidePage), nameof(CueIslandSlidePageCount), nameof(SlidePointRows)
                 })
            OnPropertyChanged(name);
    }

    /// <summary>Demo/self-check hook: put the island's slide state back after a capture.</summary>
    public void ClearIslandSlide()
    {
        slides = [];
        slideIndex = -1;
        cueSlideCoveredOverride = null;
        OnPropertyChanged(nameof(CueIslandHasSlide));
    }

    // ---- auto-end reminder -----------------------------------------------------------------

    /// <summary>The meeting ends by itself soon unless the user says otherwise.</summary>
    public bool CueIslandAutoEndDue => Recording && autoEnd?.Remaining(DateTimeOffset.UtcNow) is not null;

    /// <summary>Seconds left on the auto-end countdown (rounded up, like the Mac reminder).</summary>
    public int CueIslandAutoEndSeconds => (int)Math.Ceiling(autoEnd?.Remaining(DateTimeOffset.UtcNow) ?? 0);

    public void CueIslandKeepRecording()
    {
        if (KeepRecordingCommand.CanExecute(null)) KeepRecordingCommand.Execute(null);
    }

    public string CueIslandAutoEndNotice => UiText.Format("好像没人说话了，%lld 秒后结束记录", CueIslandAutoEndSeconds);

    // ---- voice levels for the bar's sound dots ---------------------------------------------

    public double CueIslandSystemLevel => systemLevel;

    public double CueIslandMicrophoneLevel => microphoneLevel;

    // ---- the island's actions ---------------------------------------------------------------

    /// <summary>Pages in the deck being presented, for the ear's "4/12" (CueIslandView.coachEar).</summary>
    public int CueIslandSlidePageCount
    {
        get
        {
            if (CurrentSlide is not { } slide) return 0;
            if (slide.PageCount > 0) return slide.PageCount;
            return slides.Count > 0 ? slides.Count : slide.Number;
        }
    }

    /// <summary>The page being presented, for the ear's "4/12".</summary>
    public int CueIslandSlidePage => CurrentSlide?.Number ?? 0;

    /// <summary>The slide strip's next/previous buttons (CueIslandView.slideStep).</summary>
    public void StepSlideFromIsland(int delta) => StepSlide(delta);

    /// <summary>The listening panel's "开启同声传译" (Mac liveTranslation.setEnabled(true)).</summary>
    public void CueIslandEnableTranslation()
    {
        if (LiveTranslationEnabled) return;
        LiveTranslationEnabled = true;
        Settings.TranslateSpeech = true;
        Settings.LiveTranslation = true;
        liveTranslationAutoOpen.Remember(true);
        LiveTranslationAutoNotice = "";
        EnsureLiveTranslator();
        try { _ = Settings.SaveAsync(); } catch { }
    }

    /// <summary>The launcher's scene chips (Mac state.startFromIsland(scene:)).</summary>
    public void StartFromCueIsland(MeetingMode mode)
    {
        if (Recording || Working) return;
        SelectedMode = mode;
        OnPropertyChanged(nameof(SelectedScene));
        OnSceneChanged();
        if (StartRecordingCommand.CanExecute(null)) StartRecordingCommand.Execute(null);
    }
}
