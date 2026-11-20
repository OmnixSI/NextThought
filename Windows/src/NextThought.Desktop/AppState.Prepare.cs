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
        ActivePlan = new NextThought.Core.MeetingPlan
        {
            Mode = SelectedMode,
            Carry = ActiveCarry,
            BaselineDocumentIds = series?.DocumentIds.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(),
            PreviousSessionId = PreviousTranscriptContext()?.Id
        };
        activePreparedSeries = series is null ? null : ActivePlan.PreparedWorkspace(series, PreviousTranscriptContext());
    }

    public IReadOnlyList<NextThought.Core.ContextDocument>? ActivePreparedDocuments =>
        activePreparedSeries is { Documents.Count: > 0 } ? activePreparedSeries.Documents : null;

    public NextThought.Core.MeetingCarry? ActiveCarry { get; private set; }

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
        ? $"第 {slide.Number} 页 · {slide.DocumentName}\n{slide.Text}"
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
            SetStatus($"已跟随讲话切到第 {slides[index].Number} 页。");
            return;
        }
    }

    private void MoveSlide(int delta)
    {
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        if (slides.Count == 0) { SetStatus("还没有导入 PPTX，导入后可翻页。", true); return; }
        slideIndex = Math.Clamp(slideIndex + delta, 0, slides.Count - 1);
        OnPropertyChanged(nameof(SlideStatus));
        SetStatus($"幻灯片：{SlideStatus} · {slides[slideIndex].Text[..Math.Min(80, slides[slideIndex].Text.Length)]}");
    }

    public void OpenMaterial(SourceCitation citation) => Library.OpenCitation(citation);
}
