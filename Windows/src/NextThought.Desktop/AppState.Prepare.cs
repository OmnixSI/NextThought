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

    /// <summary>Sound is ready when the local or cloud recognizer can start.</summary>
    public bool PrepareAudioReady => ModelReady || Settings.SelectedCloudStt is { Value.Length: > 0 };
    public bool PrepareAnswerReady => Settings.HasApiKey;
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


    /// <summary>E9: jump to the first slide whose title appears in the final line.</summary>
    private void TryFollowSlide(string text)
    {
        if (!Settings.FollowSlides || text.Length < 2) return;
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        for (var index = 0; index < slides.Count; index++)
        {
            var title = slides[index].Text.Split('\n').FirstOrDefault(line => line.Trim().Length > 0)?.Trim() ?? "";
            if (title.Length < 2) continue;
            if (!text.Contains(title, StringComparison.OrdinalIgnoreCase)) continue;
            if (index == slideIndex) return;
            slideIndex = index;
            OnPropertyChanged(nameof(SlideStatus));
            OnPropertyChanged(nameof(CurrentSlideText));
            SetStatus(UiText.Format("已跟随讲话切到第 %lld 页。", slides[index].Number));
            return;
        }
    }


    /// <summary>E9: follow the deck from a screenshot's OCR text.</summary>
    public void FollowSlideFromCapture(string screenText) => FollowSlideFromScreen(screenText);

    private void FollowSlideFromScreen(string screenText)
    {
        if (!Settings.FollowSlides || screenText.Trim().Length == 0) return;
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        if (slides.Count == 0) return;
        var pages = slides.Select(info => new NextThought.Core.SlidePage
        {
            Page = info.Number, PageCount = slides.Count, DocumentName = info.DocumentName, Text = info.Text
        }).ToList();
        var follower = new NextThought.Core.SlideFollower(pages);
        if (follower.Observe(screenText) is not { } matched) return;
        var index = slides.ToList().FindIndex(info => info.Number == matched.Page);
        if (index < 0 || index == slideIndex) return;
        slideIndex = index;
        OnPropertyChanged(nameof(SlideStatus));
        OnPropertyChanged(nameof(CurrentSlideText));
        OnPropertyChanged(nameof(CurrentSlidePointsText));
        OnPropertyChanged(nameof(HasCurrentSlidePoints));
        SetStatus(UiText.Format("已按屏幕切到第 %lld 页。", matched.Page));
    }


    private readonly Dictionary<int, string> slideNotesCache = [];

    /// <summary>E9: cached talking points for the current slide (first lines of its text).</summary>
    public string CurrentSlidePointsText
    {
        get
        {
            if (CurrentSlide is not { } slide) return "";
            if (!slideNotesCache.TryGetValue(slide.Number, out var cached))
            {
                var lines = slide.Text.Split('\n')
                    .Select(line => line.Trim().TrimStart('#', '-', '*', ' '))
                    .Where(line => line.Length > 1)
                    .Take(4)
                    .Select((line, index) => $"{index + 1}. {line}");
                cached = string.Join("\n", lines);
                slideNotesCache[slide.Number] = cached;
            }
            return cached;
        }
    }

    /// <summary>E26b: while presenting, the Mac presenterSlideNotes key decides whether the island
    /// shows this slide's notes.</summary>
    public bool HasCurrentSlidePoints => CurrentSlidePointsText.Length > 0
                                         && (!PresenterLayoutActive || Settings.PresenterSlideNotes);

    private void MoveSlide(int delta)
    {
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        if (slides.Count == 0) { SetStatus("还没有导入 PPTX，导入后可翻页。", true); return; }
        slideIndex = Math.Clamp(slideIndex + delta, 0, slides.Count - 1);
        OnPropertyChanged(nameof(SlideStatus));
        SetStatus(UiText.Format("幻灯片：%@ · %@", SlideStatus, slides[slideIndex].Text[..Math.Min(80, slides[slideIndex].Text.Length)]));
    }

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
    }
}
