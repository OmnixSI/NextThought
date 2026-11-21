using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using NextThought.Desktop.Controls;

namespace NextThought.Desktop;

public partial class AppState
{


    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool carryMaterials = true;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool carryReview = true;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool carryLessons = true;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool carryTranscript;


    /// <summary>E2: attach the previous meeting of the selected series when 带入上一场转写 is on.</summary>
    public NextThought.Core.ContextDocument? PreviousTranscriptContext()
    {
        if (ActiveCarry?.Transcript != true) return null;
        var series = SelectedSeries is { } selected ? workspace.Find(selected.Id)
            : CurrentSession is { } current ? workspace.SeriesOfSession(current.Id.ToString()) : null;
        if (series is null) return null;
        var previous = History
            .Where(row => series.SessionIds.Contains(row.Meta.Id.ToString()))
            .Where(row => row.Meta.Id != CurrentSession?.Id)
            .OrderByDescending(row => row.Meta.StartedAt)
            .FirstOrDefault();
        if (previous is null) return null;
        try
        {
            var data = Sessions.Load(previous.Meta.Id);
            var text = string.Join("\n", data.Lines.Select(line => $"{line.Speaker}: {line.Text}"));
            return text.Length == 0 ? null : new NextThought.Core.ContextDocument
            {
                Id = previous.Meta.Id.ToString(),
                Name = "上一场 · " + previous.Meta.Title,
                Pages = [text],
                Enabled = true
            };
        }
        catch { return null; }
    }


    public NextThought.Core.MeetingPlan? ActivePlan { get; private set; }
    private NextThought.Core.WorkspaceSeries? activePreparedSeries;

    /// <summary>Builds the Mac MeetingPlan and applies the carry switches to a copy of the series.</summary>
    public void BeginPreparedMeeting(NextThought.Core.WorkspaceSeries? series)
    {
        ActiveCarry = CarryPlan;
        var draft = meetingDraft;
        ActivePlan = new NextThought.Core.MeetingPlan
        {
            Title = draft is { Title.Length: > 0 } ? draft.Title.Trim() : null,
            Mode = draft?.Mode ?? SelectedMode,
            CustomTypeName = draft?.Mode == NextThought.Core.MeetingMode.Custom ? draft.CustomTypeName : null,
            CustomBaseMode = draft?.CustomBaseMode ?? NextThought.Core.MeetingMode.General,
            Carry = ActiveCarry,
            BaselineDocumentIds = draft?.BaselineDocumentIds ?? series?.DocumentIds.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(),
            PreviousSessionId = PreviousTranscriptContext()?.Id
        };
        activePreparedSeries = series is null ? null : ActivePlan.PreparedWorkspace(series, PreviousTranscriptContext());
    }

    public IReadOnlyList<NextThought.Core.ContextDocument>? ActivePreparedDocuments =>
        activePreparedSeries is { Documents.Count: > 0 } ? activePreparedSeries.Documents : null;

    public NextThought.Core.MeetingCarry? ActiveCarry { get; private set; }

    // ---- E2: prepare page status line (Mac PreparationStartBar) ----------------------------------

    /// <summary>
    /// Sound is ready when the local or cloud recognizer can start. The demo shows the Mac
    /// screenshots' ready state (Mac DemoMode.isEnabled fakes the same readiness).
    /// </summary>
    public bool PrepareAudioReady => ModelReady || Settings.SelectedCloudStt is { Value.Length: > 0 } || StartupOptions.Demo;

    /// <summary>J2: the answer service is ready; demo mode shows the Mac screenshot's configured state.</summary>
    public bool PrepareAnswerReady => Settings.HasApiKey || StartupOptions.Demo;
    public bool PreparePhoneReady => CompanionRunning;

    /// <summary>Mac start-bar detail: readiness plus how many series materials this meeting uses.</summary>
    public string PrepareMaterialsLine => PreparedMaterialCount > 0
        ? UiText.Format("本场使用 %lld 份已就绪资料；文字可用即可开始。", PreparedMaterialCount)
        : UiText.T("资料与 AI 都可稍后补充。");

    /// <summary>Mac series carry card: what the selected series brings into this meeting.</summary>
    public string PrepareCarrySummary
    {
        get
        {
            var parts = new List<string>();
            if (CarryMaterials && PreparedMaterialCount > 0) parts.Add(UiText.Format("资料 %lld 份", PreparedMaterialCount));
            if (CarryReview && SelectedSeriesBriefing is { QuestionBank.Count: > 0 } briefing)
                parts.Add(UiText.Format("题库 %lld 题", briefing.QuestionBank.Count));
            if (CarryLessons && SelectedSeriesBriefing is { Lessons.Count: > 0 } lessons)
                parts.Add(UiText.Format("经验 %lld 条", lessons.Lessons.Count));
            if (CarryTranscript) parts.Add(UiText.T("上一场转写"));
            return parts.Count == 0 ? UiText.T("这次不带入系列内容。") : string.Join(UiText.T(" · "), parts);
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task ImportPreparedDocumentsAsync()
    {
        if (PickOpenFile is null) return;
        if (await PickOpenFile(UiText.T("资料文件 (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg")) is not { } file) return;
        await Library.ImportAsync([file]);
        OnPropertyChanged(nameof(PrepareCarrySummary));
        OnPropertyChanged(nameof(PrepareMaterialsLine));
        OnPropertyChanged(nameof(PreparedMaterialCount));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CancelDraft()
    {
        // Mac cancelDraft: drop the in-memory plan; imported materials stay in their series.
        meetingDraft = null;
        SelectedMode = NextThought.Core.MeetingMode.General;
        MeetingTitle = "";
        CarryMaterials = true;
        CarryReview = true;
        CarryLessons = true;
        CarryTranscript = false;
        RaisePrepareDraft();
        Section = AppSection.Home;
    }

    /// <summary>
    /// E2: the materials the prepare page carries into the meeting (Mac series documents). With the
    /// staged demo that is the interview series' four documents; the library also holds two more.
    /// </summary>
    public int PreparedMaterialCount
    {
        get
        {
            if (!CarryMaterials) return 0;
            var series = SelectedSeries is { } selected
                ? workspace.Find(selected.Id)
                : workspace.Series.FirstOrDefault(item => item.DocumentIds.Count > 0);
            if (series is null) return 0;
            var ids = series.DocumentIds.ToHashSet(StringComparer.Ordinal);
            var found = Library.Materials.Count(document => ids.Contains(document.Id));
            if (found > 0) return found;
            // Mac demos record the series documents as metadata (Documents) rather than library ids.
            return series.Documents.Count(document => document.Enabled);
        }
    }

    public void BeginPreparedMeeting() => ActiveCarry = CarryPlan;

    public NextThought.Core.MeetingCarry CarryPlan => new()
    {
        Materials = CarryMaterials,
        Review = CarryReview,
        Lessons = CarryLessons,
        Transcript = CarryTranscript
    };

    public bool OnLibrary => Section == AppSection.Library;

    public bool OnSeries => Section == AppSection.Series;


    public string CurrentSlideText => CurrentSlide is { } slide
        ? UiText.Format("第 %lld 页 · %@\n%@", slide.Number, slide.DocumentName, slide.Text)
        : "还没有幻灯片；导入 PPTX 后可翻页。";

    public int CurrentScreenshotCount => CurrentScreenshots.Count;


    private SlidePageInfo? CurrentSlide
    {
        get
        {
            if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
            return slideIndex >= 0 && slideIndex < slides.Count ? slides[slideIndex] : null;
        }
    }



    /// <summary>E9: follow the deck from a screenshot's OCR text.</summary>
    public void FollowSlideFromCapture(string screenText) => FollowSlideFromScreen(screenText);

    /// <summary>E9: Mac followSlide/slideIsManual live in AppState.SlideFollow.</summary>
    private void FollowSlideFromScreen(string screenText) => FollowSlideFromScreenCore(screenText);


    /// <summary>E9: cached talking points for the current slide (AppState.SlideFollow + SlideNotesCache).</summary>
    public string CurrentSlidePointsText => CurrentSlidePoints();

    /// <summary>E26b: while presenting, the Mac presenterSlideNotes key decides whether the island
    /// shows this slide's notes.</summary>
    public bool HasCurrentSlidePoints => CurrentSlidePointsText.Length > 0
                                         && (!PresenterLayoutActive || Settings.PresenterSlideNotes);

    private void MoveSlide(int delta) => StepSlide(delta);

    public void OpenMaterial(SourceCitation citation) => Library.OpenCitation(citation);
}


public partial class AppState
{
    // E2: the start bar and carry summary follow the switches without a page reload.
    partial void OnCarryMaterialsChanged(bool value) => RefreshPrepareSummary();
    partial void OnCarryReviewChanged(bool value) => RefreshPrepareSummary();
    partial void OnCarryLessonsChanged(bool value) => RefreshPrepareSummary();
    partial void OnCarryTranscriptChanged(bool value) => RefreshPrepareSummary();

    private void RefreshPrepareSummary()
    {
        if (meetingDraft is { } draft) draft.Carry = CarryPlan;
        OnPropertyChanged(nameof(PrepareCarrySummary));
        OnPropertyChanged(nameof(PrepareMaterialsLine));
        OnPropertyChanged(nameof(PrepareAudioReady));
        OnPropertyChanged(nameof(PrepareAnswerReady));
        OnPropertyChanged(nameof(PreparePhoneReady));
        RaisePrepareLayout();
    }
}

// =================================================================================================
// J2 (§6.7): 这场用什么 — the four switches, remembered on the series and applied when the meeting
// starts. Mac LiveNotesEngine.swift:144-178 (meetingFeatures) and PrepareView.swift:284-347.
// =================================================================================================

public partial class AppState
{
    private MeetingFeatureChoices? meetingFeatureChoices;

    private MeetingFeatureChoices FeatureChoices =>
        meetingFeatureChoices ??= MeetingFeatureChoices.Load(FeatureChoicesPath);

    private string FeatureChoicesPath => Path.Combine(Paths.Root, MeetingFeatureChoices.FileName);

    /// <summary>Mac upcomingMode: the scene of the meeting about to start.</summary>
    public MeetingMode UpcomingMode => meetingDraft?.EffectiveMode ?? SelectedMode;

    /// <summary>
    /// Mac speakers.modelAvailable: the Windows build has no voiceprint model bundled, it is
    /// downloaded once. The demo shows the Mac screenshots' on state (Mac DemoMode does the same).
    /// </summary>
    public bool SpeakerModelAvailable => MyVoiceModelAvailable || new SpeakerModelStore(Paths).IsInstalled || StartupOptions.Demo;

    /// <summary>
    /// Mac <c>standardMeetingFeatures</c> (LiveNotesEngine.swift:155-158): what the scene starts
    /// with. In a room people talk face to face, so answers default to what the scene prompts
    /// online only; interpretation is on by default and only shows when another language is heard.
    /// </summary>
    public NextThought.Core.MeetingFeatures StandardMeetingFeatures => NextThought.Core.MeetingFeatures.Standard(
        prompting: StandardPrompting,
        speakers: SpeakerSeparationEnabled);

    private bool StandardPrompting => SceneDefaultsFor(UpcomingMode).LiveMode == "自动回答" && Settings.CaptureSystem;

    /// <summary>Mac speakers.enabled &amp;&amp; speakers.modelAvailable (the row is switchable then).</summary>
    public bool SpeakerSeparationEnabled => (Settings.SeparateSpeakers || StartupOptions.Demo) && SpeakerModelAvailable;

    /// <summary>Mac featuresKey: the series the choices hang on.</summary>
    private string MeetingFeatureSeriesKey => meetingDraft?.SeriesId ?? SelectedSeries?.Id ?? "default";

    /// <summary>Mac <c>meetingFeatures</c>: what the series used last time, else the scene's defaults.</summary>
    public NextThought.Core.MeetingFeatures DraftMeetingFeatures
    {
        get => FeatureChoices.ForSeries(MeetingFeatureSeriesKey, StandardPrompting, SpeakerSeparationEnabled);
        set
        {
            FeatureChoices.Set(MeetingFeatureSeriesKey, value);
            try { FeatureChoices.Save(FeatureChoicesPath); } catch { }
            RaiseMeetingFeatures();
        }
    }

    public bool MeetingFeaturesAreDefault => DraftMeetingFeatures == StandardMeetingFeatures;

    public bool MeetingFeatureAnswers
    {
        get => DraftMeetingFeatures.Answers;
        set => DraftMeetingFeatures = DraftMeetingFeatures with { Answers = value };
    }

    public bool MeetingFeatureLiveNotes
    {
        get => DraftMeetingFeatures.LiveNotes;
        set => DraftMeetingFeatures = DraftMeetingFeatures with { LiveNotes = value };
    }

    public bool MeetingFeatureTranslation
    {
        get => DraftMeetingFeatures.Translation;
        set => DraftMeetingFeatures = DraftMeetingFeatures with { Translation = value };
    }

    public bool MeetingFeatureSpeakers
    {
        get => DraftMeetingFeatures.Speakers;
        set => DraftMeetingFeatures = DraftMeetingFeatures with { Speakers = value };
    }

    // The demo runs without a configured service, exactly like Mac DemoMode, so the row keeps the
    // Mac screenshot's enabled state and copy.
    private bool FeaturePickerAnswerEngineReady => PrepareAnswerReady || StartupOptions.Demo;

    public bool MeetingFeatureAnswersEnabled => FeaturePickerAnswerEngineReady;
    public bool MeetingFeatureLiveNotesEnabled => FeaturePickerAnswerEngineReady;
    public bool MeetingFeatureTranslationEnabled => true;
    public bool MeetingFeatureSpeakersEnabled => SpeakerSeparationEnabled;

    public string MeetingFeatureAnswersDetail => FeaturePickerAnswerEngineReady
        ? UiText.T("听到对方提问，参考回答出现在摄像头下方的提词岛和右侧。")
        : UiText.T("需要先连接回答服务（开始前检查里可以连接）。");

    public string MeetingFeatureLiveNotesDetail => FeaturePickerAnswerEngineReady
        ? UiText.T("边开边整理议题、决定和待办，大约每 45 秒更新一次；结束后仍会整理完整纪要。")
        : UiText.T("需要先连接回答服务。");

    public string MeetingFeatureTranslationDetail =>
        UiText.T("听到对方说英文时自动出现：每句话下面和提词岛上给出中文。在本机翻译，不联网。");

    public string MeetingFeatureSpeakersDetail => SpeakerModelAvailable
        ? UiText.T("按声音分出每个人，可以起名字；纪要里按人来写。认错了随时能改。")
        : UiText.T("这个版本没有附带声纹模型。");

    /// <summary>Mac <c>resetMeetingFeatures</c>: back to what the scene starts with.</summary>
    [RelayCommand]
    private void ResetMeetingFeatures()
    {
        if (FeatureChoices.Remove(MeetingFeatureSeriesKey)) { try { FeatureChoices.Save(FeatureChoicesPath); } catch { } }
        RaiseMeetingFeatures();
    }

    private void RaiseMeetingFeatures()
    {
        OnPropertyChanged(nameof(DraftMeetingFeatures));
        OnPropertyChanged(nameof(MeetingFeaturesAreDefault));
        OnPropertyChanged(nameof(MeetingFeatureAnswers));
        OnPropertyChanged(nameof(MeetingFeatureLiveNotes));
        OnPropertyChanged(nameof(MeetingFeatureTranslation));
        OnPropertyChanged(nameof(MeetingFeatureSpeakers));
        OnPropertyChanged(nameof(MeetingFeatureAnswersEnabled));
        OnPropertyChanged(nameof(MeetingFeatureLiveNotesEnabled));
        OnPropertyChanged(nameof(MeetingFeatureSpeakersEnabled));
        OnPropertyChanged(nameof(MeetingFeatureAnswersDetail));
        OnPropertyChanged(nameof(MeetingFeatureLiveNotesDetail));
        OnPropertyChanged(nameof(MeetingFeatureSpeakersDetail));
        OnPropertyChanged(nameof(PrepareStartDetail));
    }

    /// <summary>The four switches the running meeting actually uses (Mac startPreparedMeeting features).</summary>
    public NextThought.Core.MeetingFeatures ActiveMeetingFeatures { get; private set; } = new(false, false, true, false);

    /// <summary>
    /// Mac startPreparedMeeting (AppState.swift:720-728): the four choices decide what runs in this
    /// meeting — answers only when the engine is ready and the switch is on, live notes when asked
    /// for, interpretation off when the meeting excluded it. Speaker separation is gated where the
    /// voice tagger is created.
    /// </summary>
    public void ApplyActiveMeetingFeatures()
    {
        ActiveMeetingFeatures = DraftMeetingFeatures;
        Settings.AutomaticAnswers = AnswerEngineReady && ActiveMeetingFeatures.Answers;
        LiveNotesEnabled = ActiveMeetingFeatures.LiveNotes;
        RaiseLiveNotesTabUi();
        if (!ActiveMeetingFeatures.Translation) LiveTranslationEnabled = false;
        OnPropertyChanged(nameof(ActiveMeetingFeatures));
    }

    // ---- 接着上一场 (Mac SeriesCarryCard) ---------------------------------------------------

    /// <summary>The series the draft continues (Mac draftSeries).</summary>
    public SeriesRow? DraftSeriesRow => meetingDraft?.SeriesId is { } id
        ? SeriesRows.FirstOrDefault(row => row.Id == id)
        : null;

    private NextThought.Core.WorkspaceSeries? DraftSeriesWorkspace => DraftSeriesRow is { } row ? workspace.Find(row.Id) : null;

    /// <summary>Mac draftContinuesSeries: a real series is bound, so "接着上一场" is offered.</summary>
    public bool DraftContinuesSeries => DraftSeriesRow is { } row && (row.Count > 0 || row.DocumentCount > 0);

    private IReadOnlyList<RecentMeetingRow> DraftSeriesSessions
    {
        get
        {
            if (DraftSeriesWorkspace is not { } series) return [];
            var ids = series.SessionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0) return [];
            return History.Where(row => ids.Contains(row.Meta.Id.ToString())).ToList();
        }
    }

    public int DraftSeriesSessionCount => DraftSeriesSessions.Count;

    public string DraftSeriesHeaderTitle => DraftSeriesRow is { } row
        ? UiText.Format("这是「%@」的第 %lld 场", row.Name, DraftSeriesSessionCount + 1)
        : "";

    public string DraftSeriesPreviousLine => DraftSeriesSessions.OrderByDescending(row => row.Meta.StartedAt).FirstOrDefault() is { } last
        ? UiText.Format("上一场：%@ · %@", last.Title, NextThought.Core.MeetingOutcome.When(last.Meta.StartedAt))
        : UiText.T("这个系列还没有开过会");

    [RelayCommand]
    private void DeclineDraftSeries()
    {
        if (meetingDraft is not { SeriesId: { } id } draft) return;
        draft.DeclinedSeriesIds.Add(id);
        draft.SeriesId = null;
        draft.PreviousSessionId = null;
        draft.Carry = NextThought.Core.MeetingCarry.Standard;
        CarryMaterials = true;
        CarryReview = true;
        CarryLessons = true;
        CarryTranscript = false;
        RaisePrepareDraft();
    }

    /// <summary>Mac continueDraftSeries: bind the draft to one earlier series.</summary>
    public void ContinueDraftSeries(string seriesId)
    {
        EnsureDraft();
        var series = workspace.Find(seriesId);
        if (series is null) return;
        var draft = meetingDraft!;
        draft.SeriesId = series.Id;
        draft.DeclinedSeriesIds.Remove(series.Id);
        draft.BaselineDocumentIds = series.DocumentIds.ToHashSet(StringComparer.Ordinal);
        draft.PreviousSessionId = DraftSeriesSessions.OrderByDescending(row => row.Meta.StartedAt).FirstOrDefault()?.Meta.Id.ToString();
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == series.Id);
        RaisePrepareDraft();
    }

    /// <summary>Series picker for "从以前的会议带入…" (Mac MaterialsActions menu).</summary>
    public IReadOnlyList<SeriesRow> OtherSeriesRows => SeriesRows
        .Where(row => row.Id != (meetingDraft?.SeriesId ?? SelectedSeries?.Id))
        .ToList();

    public bool HasOtherSeries => OtherSeriesRows.Count > 0;

    public bool DraftCarryHasMaterials => (DraftSeriesWorkspace?.DocumentsOf(true).Count ?? 0) > 0;
    public string DraftCarryMaterialsDetail => UiText.Format("%lld 份", DraftSeriesWorkspace?.DocumentsOf(true).Count ?? 0);
    public string DraftCarryMaterialsTitle
    {
        get
        {
            var names = (DraftSeriesWorkspace?.DocumentsOf(true) ?? []).Select(document => document.Name).ToList();
            var shown = string.Join(UiText.T("、"), names.Take(2));
            return UiText.Format("资料 · %@", names.Count > 2 ? shown + "…" : shown);
        }
    }

    public bool DraftCarryHasReview => DraftSeriesWorkspace is { UseKnowledge: true } series && series.QuestionBank.Count > 0;
    public string DraftCarryReviewDetail => UiText.Format("%lld 题", DraftSeriesWorkspace?.QuestionBank.Count ?? 0);

    public bool DraftCarryHasLessons => DraftSeriesWorkspace is { UseKnowledge: true } series && series.Lessons.Count > 0;
    public string DraftCarryLessonsDetail => UiText.Format("%lld 条", DraftSeriesWorkspace?.Lessons.Count ?? 0);
    public string DraftCarryLessonsTitle
    {
        get
        {
            var lessons = DraftSeriesWorkspace?.Lessons ?? [];
            var quoted = string.Concat(lessons.Take(2).Select(lesson =>
                lesson.Text.Length > 14 ? "“" + lesson.Text[..14] + "”" : "“" + lesson.Text + "”"));
            return UiText.Format("经验 · %@", quoted);
        }
    }

    public bool DraftCarryHasTranscript => DraftSeriesSessions.Any(row => row.Meta.TranscriptCount > 0);

    public string DraftCarryTranscriptDetail
    {
        get
        {
            var last = DraftSeriesSessions
                .Where(row => row.Meta.TranscriptCount > 0)
                .OrderByDescending(row => row.Meta.StartedAt)
                .FirstOrDefault();
            if (last is null) return "";
            var minutes = Math.Max(1, (int)((last.Meta.EndedAt ?? last.Meta.StartedAt).Subtract(last.Meta.StartedAt).TotalMinutes));
            return UiText.Format("%lld 分钟", minutes);
        }
    }

    public bool DraftCarryHasAnything => DraftCarryHasMaterials || DraftCarryHasReview || DraftCarryHasLessons || DraftCarryHasTranscript;
    public bool DraftCarryHasNothing => !DraftCarryHasAnything;

    // ---- 系列简报 (Mac SeriesBriefingPanel) -------------------------------------------------

    public bool DraftHasBriefing => DraftSeriesBriefing is not null;
    private NextThought.Core.SeriesBriefing? DraftSeriesBriefing => DraftSeriesRow is { } row && workspace.Find(row.Id) is { } series
        ? NextThought.Core.SeriesBriefingBuilder.Build(series, History.Select(item => item.Meta).ToList())
        : null;

    public string DraftBriefingRecentLine => DraftSeriesBriefing is { } briefing
        ? UiText.Format("最近会议 · 共 %lld 场", briefing.SessionCount)
        : "";

    public bool DraftBriefingHasRecent => DraftSeriesBriefing is { LatestSessions.Count: > 0 };
    public IReadOnlyList<NextThought.Core.SessionMeta> DraftBriefingRecentSessions =>
        DraftSeriesBriefing?.LatestSessions ?? [];
    public bool DraftBriefingHasLessons => DraftSeriesBriefing is { Lessons.Count: > 0 };
    public IReadOnlyList<NextThought.Core.WorkspaceLesson> DraftBriefingLessons => DraftSeriesBriefing?.Lessons ?? [];
    public bool DraftBriefingHasQuestionBank => DraftSeriesBriefing is { QuestionBank.Count: > 0 };
    public IReadOnlyList<NextThought.Core.QuestionBankItem> DraftBriefingQuestionBank =>
        DraftSeriesBriefing?.QuestionBank ?? [];
    public bool DraftBriefingKnowledgeOff => DraftSeriesBriefing is { KnowledgeEnabled: false };
    public bool DraftBriefingIsEmpty => DraftSeriesBriefing is { LatestSessions.Count: 0, Lessons.Count: 0, QuestionBank.Count: 0 };
    public string DraftBriefingPendingLine => DraftSeriesBriefing is { PendingCandidates.Count: > 0 } briefing
        ? UiText.Format("%lld 条建议待确认，不会作为已确认内容带入。", briefing.PendingCandidates.Count)
        : "";
    public bool DraftBriefingHasPending => DraftBriefingPendingLine.Length > 0;

    // ---- 这次新加的资料 (Mac MaterialsBlock / MaterialsActions) -----------------------------

    public bool PrepareHasMaterialRows => PrepareMaterialsRows.Count > 0;

    public string PrepareDropHint
    {
        get
        {
            var hint = NextThought.Desktop.SceneSummary.Hint(UpcomingMode);
            return hint.Length == 0 ? "" : UiText.Format("建议放：%@", hint);
        }
    }

    public bool PrepareDropHintVisible => PrepareDropHint.Length > 0;

    public string PrepareMaterialsSummary => PreparedMaterialCount > 0
        ? UiText.Format("这场会用的资料 · %lld 份", PreparedMaterialCount)
        : UiText.T("把 PDF、Word、PPT、Excel、Markdown 或图片拖到这里，或点「添加」。");

    // ---- 开始前检查 (Mac CheckCard) --------------------------------------------------------

    public StatusTone PrepareAudioTone => PrepareAudioReady ? StatusTone.Ready : StatusTone.Caution;

    public StatusTone PrepareAiTone => EngineTestPassed && PrepareAnswerReady
        ? StatusTone.Ready
        : EngineTestFailed && PrepareAnswerReady ? StatusTone.Caution : StatusTone.Idle;

    public StatusTone PreparePhoneTone => PreparePhoneReady ? StatusTone.Ready : StatusTone.Caution;

    public bool PrepareShowPhoneCheck => PreparePhoneReady;

    public string PrepareAnswerEngineLine => PrepareAnswerReady
        ? PrepareAnswerLine
        : UiText.T("未配置 · 不影响记录");

    public bool PrepareAnswerEngineConfigured => PrepareAnswerReady;

    // ---- 底部开始条 (Mac PreparationStartBar) -----------------------------------------------

    public string PrepareReadinessMessage => PrepareAudioReady
        ? UiText.T("可以开始记录")
        : PrepareAudioProblem;

    public string PrepareStartDetail => PrepareReadinessMessage + "  ·  " + PrepareMaterialsLine;

    public string PrepareStartLabel => PrepareAudioReady ? UiText.T("开始") : UiText.T("准备声音");

    public bool PrepareStartEnabled => IsIdle;

    // ---- 标题与场景 (Mac PrepareComposer / TypePicker) --------------------------------------

    /// <summary>Mac VenuePicker selection: 0 = 线上, 1 = 线下 (InkSegmented).</summary>
    public int DraftVenueIndex
    {
        get => DraftOffline ? 1 : 0;
        set
        {
            if (value is < 0 or > 1 || value == DraftVenueIndex) return;
            _ = SetVenueAsync(offline: value == 1);
        }
    }

    /// <summary>The two venue choices (Mac MeetingVenue.allCases).</summary>
    public IReadOnlyList<string> VenueOptions => [UiText.T("线上"), UiText.T("线下")];

    /// <summary>Mac 自定义… chip: selected like the scene chips, using the general scene pigment.</summary>
    public IBrush DraftCustomChipBackground => meetingDraft?.Mode == MeetingMode.Custom
        ? TintAt13(MeetingMode.General, Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
        : Brushes.Transparent;

    public IBrush DraftCustomChipForeground => meetingDraft?.Mode == MeetingMode.Custom
        ? TintBrush(MeetingMode.General, Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
        : SecondaryBrush;

    /// <summary>Mac scene chips: selected chip takes the scene pigment at 13% and its tint text.</summary>
    public IReadOnlyList<PrepareSceneChip> DraftSceneChips
    {
        get
        {
            var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var current = meetingDraft?.Mode ?? SelectedMode;
            return Scenes.Select(choice => new PrepareSceneChip(
                choice,
                choice.Mode == current,
                TintAt13(choice.Mode, dark),
                TintBrush(choice.Mode, dark),
                SecondaryBrush)).ToArray();
        }
    }

    /// <summary>Mac moments: a scene-tint 4 pt dot and the caption, one line.</summary>
    public IReadOnlyList<PrepareMomentRow> DraftMomentRows
    {
        get
        {
            var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var tint = TintBrush(UpcomingMode, dark);
            return DraftMoments.Select(moment => new PrepareMomentRow(moment, tint)).ToArray();
        }
    }

    public IBrush DraftSceneTintBrush
    {
        get
        {
            var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            return TintBrush(UpcomingMode, dark);
        }
    }

    private static IBrush TintBrush(MeetingMode mode, bool dark) =>
        new SolidColorBrush(Color.Parse(NextThought.Core.SceneTheme.For(mode).Tint(dark)));

    private static IBrush TintAt13(MeetingMode mode, bool dark)
    {
        var color = Color.Parse(NextThought.Core.SceneTheme.For(mode).Tint(dark));
        return new SolidColorBrush(Color.FromArgb(0x21, color.R, color.G, color.B));
    }

    /// <summary>
    /// §5 dark-contrast: Application.FindResource returns the first (light) Secondary definition, so a
    /// dark theme page would paint unselected chips with #6B6963 on #1B1B19 (ratio 3.14). Resolve the
    /// resource in the active theme variant so the dark Secondary (#A3A19B) is used.
    /// </summary>
    private static IBrush SecondaryBrush
    {
        get
        {
            var app = Application.Current;
            if (app is not null)
            {
                if (app.TryGetResource("Secondary", app.ActualThemeVariant, out var themed) && themed is IBrush themedBrush)
                    return themedBrush;
                if (app.FindResource("Secondary") is IBrush fallback) return fallback;
            }
            return new SolidColorBrush(ParseSecondaryFallback());
        }
    }

    private static Color ParseSecondaryFallback() => Color.Parse("#6B6963");

    /// <summary>Raised by RaisePrepareDraft so every new prepare-page block re-reads its state.</summary>
    private void RaisePrepareLayout()
    {
        OnPropertyChanged(nameof(DraftSceneChips));
        OnPropertyChanged(nameof(DraftVenueIndex));
        OnPropertyChanged(nameof(VenueOptions));
        OnPropertyChanged(nameof(DraftCustomChipBackground));
        OnPropertyChanged(nameof(DraftCustomChipForeground));
        OnPropertyChanged(nameof(DraftMomentRows));
        OnPropertyChanged(nameof(DraftSceneTintBrush));
        OnPropertyChanged(nameof(DraftMeetingFeatures));
        OnPropertyChanged(nameof(DraftContinuesSeries));
        OnPropertyChanged(nameof(DraftSeriesRow));
        OnPropertyChanged(nameof(DraftSeriesHeaderTitle));
        OnPropertyChanged(nameof(DraftSeriesPreviousLine));
        OnPropertyChanged(nameof(DraftSeriesSessionCount));
        OnPropertyChanged(nameof(OtherSeriesRows));
        OnPropertyChanged(nameof(HasOtherSeries));
        OnPropertyChanged(nameof(DraftCarryHasMaterials));
        OnPropertyChanged(nameof(DraftCarryMaterialsDetail));
        OnPropertyChanged(nameof(DraftCarryMaterialsTitle));
        OnPropertyChanged(nameof(DraftCarryHasReview));
        OnPropertyChanged(nameof(DraftCarryReviewDetail));
        OnPropertyChanged(nameof(DraftCarryHasLessons));
        OnPropertyChanged(nameof(DraftCarryLessonsDetail));
        OnPropertyChanged(nameof(DraftCarryLessonsTitle));
        OnPropertyChanged(nameof(DraftCarryHasTranscript));
        OnPropertyChanged(nameof(DraftCarryTranscriptDetail));
        OnPropertyChanged(nameof(DraftCarryHasAnything));
        OnPropertyChanged(nameof(DraftCarryHasNothing));
        OnPropertyChanged(nameof(DraftHasBriefing));
        OnPropertyChanged(nameof(DraftBriefingRecentLine));
        OnPropertyChanged(nameof(DraftBriefingHasRecent));
        OnPropertyChanged(nameof(DraftBriefingRecentSessions));
        OnPropertyChanged(nameof(DraftBriefingHasLessons));
        OnPropertyChanged(nameof(DraftBriefingLessons));
        OnPropertyChanged(nameof(DraftBriefingHasQuestionBank));
        OnPropertyChanged(nameof(DraftBriefingQuestionBank));
        OnPropertyChanged(nameof(DraftBriefingKnowledgeOff));
        OnPropertyChanged(nameof(DraftBriefingIsEmpty));
        OnPropertyChanged(nameof(DraftBriefingPendingLine));
        OnPropertyChanged(nameof(DraftBriefingHasPending));
        OnPropertyChanged(nameof(PrepareHasMaterialRows));
        OnPropertyChanged(nameof(PrepareDropHint));
        OnPropertyChanged(nameof(PrepareDropHintVisible));
        OnPropertyChanged(nameof(PrepareMaterialsSummary));
        OnPropertyChanged(nameof(PrepareAudioTone));
        OnPropertyChanged(nameof(PrepareAiTone));
        OnPropertyChanged(nameof(PreparePhoneTone));
        OnPropertyChanged(nameof(PrepareShowPhoneCheck));
        OnPropertyChanged(nameof(PrepareAnswerEngineLine));
        OnPropertyChanged(nameof(PrepareAnswerEngineConfigured));
        OnPropertyChanged(nameof(PrepareReadinessMessage));
        OnPropertyChanged(nameof(PrepareStartDetail));
        OnPropertyChanged(nameof(PrepareStartLabel));
        OnPropertyChanged(nameof(PrepareStartEnabled));
        RaiseMeetingFeatures();
    }
}

/// <summary>Edge J2: one scene chip on the prepare page (Mac TypePicker sceneChip).</summary>
public sealed record PrepareSceneChip(
    SceneChoice Scene,
    bool Selected,
    IBrush SelectedBackground,
    IBrush SelectedForeground,
    IBrush PlainForeground)
{
    public string Title => Scene.Title;
    public string DefaultsSummary => Scene.DefaultsSummary;
    public IBrush Background => Selected ? SelectedBackground : Brushes.Transparent;
    public IBrush Foreground => Selected ? SelectedForeground : PlainForeground;
}

/// <summary>J2: one scene moment (Mac TypePicker moments), with the scene's pigment dot.</summary>
public sealed record PrepareMomentRow(string Text, IBrush Dot);
