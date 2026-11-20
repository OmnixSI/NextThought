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

    private SlidePageInfo? CurrentSlide
    {
        get
        {
            if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
            return slideIndex >= 0 && slideIndex < slides.Count ? slides[slideIndex] : null;
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
