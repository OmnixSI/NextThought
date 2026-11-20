using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// D1: the demo mode. The Mac demo script (DemoMode.swift / DemoFilm.swift) is ported verbatim
/// into <see cref="DemoScript"/>; this file adapts it to the Windows view state. Nothing here is
/// ever written to the user's data directory.
/// </summary>
public partial class AppState
{
    /// <summary>The resolved Mac demo data for the current demo session (null outside demo mode).</summary>
    private DemoDataset? demoData;

    /// <summary>
    /// Fills the interface with the Mac demonstration data (DemoMode.swift populateDemo,
    /// demoReviewNotes and demoWeeklyMeeting). Never touches the data directory; the old invented
    /// activity feeder and its structural-sample copy are gone.
    /// </summary>
    public void LoadDemo()
    {
        var demo = DemoContent.Create();
        demoData = demo;
        // Mac: the demo window is the ordinary shell; the running interview starts on the live page.
        MeetingTitle = "";
        SelectedMode = MeetingMode.Interview;
        OnPropertyChanged(nameof(SelectedScene));
        recordingStartedAt = demo.Live.StartedAt;
        Recording = false;
        // Series: three Mac workspaces, newest-first as the home page lists them.
        workspace = new WorkspaceData(demo.Series);
        History.Clear();
        HomeRecent.Clear();
        foreach (var session in new[] { demo.Weekly, demo.Review })
        {
            History.Add(new RecentMeetingRow(session.Meta));
            if (HomeRecent.Count < 5) HomeRecent.Add(new RecentMeetingRow(session.Meta));
        }
        CurrentSession = demo.Review.Meta;
        // Live page: the running interview's question and answer.
        DetectedQuestion = demo.Live.Question;
        AnswerText = demo.Live.Answer;
        RetrievalInfo = demo.Live.RetrievalNote;
        SystemPartial = MicrophonePartial = "";
        SystemPartialTranslation = MicrophonePartialTranslation = "";
        IslandTranslation = DemoContent.IslandTranslation;
        currentLines.Clear();
        Transcript.Clear();
        foreach (var line in demo.Live.Lines)
        {
            currentLines.Add(line);
            Transcript.Add(new TranscriptRow(line));
        }
        CurrentAnswerHistory.Clear();
        foreach (var answer in demo.Live.AnswerHistory) CurrentAnswerHistory.Add(answer);
        // Notes page: the review meeting's summary, facts line and template.
        SummaryText = demo.Review.SummaryMarkdown;
        PageSubtitle = ReviewFacts(demo);
        TemplateKind = NotesTemplateKind.Interview;
        SelectedTemplateId = NotesTemplateKind.Interview.Raw();
        SelectedNotesFormat = NotesFormat.Standard;
        StructuredPayload = null;
        StructuredMarkdown = "";
        SelectedSessionAnswers.Clear();
        foreach (var answer in demo.Review.Answers) SelectedSessionAnswers.Add(answer);
        Sources.Clear();
        foreach (var screenshot in CurrentScreenshots) screenshot.Dispose();
        CurrentScreenshots.Clear();
        SelectedSessionScreenshotCount = 0;
        // Library: the workspaces' materials as the Mac demo shows them (parsed locally).
        Library.Materials.Clear();
        foreach (var material in demo.Materials) Library.Materials.Add(material);
        Library.SelectedMaterial = null;
        // Skills: the three Mac demo skills; the design one is attached to the interview series.
        skills = demo.Skills;
        SkillCount = skills.Count;
        SkillsText = SkillStore.ToText(skills);
        SkillsStatus = $"{skills.Count} 个技能；回答时按需遵守。";
        RefreshSkillRows();
        // Series page and the history's series grouping.
        RefreshSeries();
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == demo.InterviewSeries.Id);
        RefreshSeriesMeetings();
        RefreshSeriesLessons();
        RefreshSeriesSkills();
        ApplyHistoryFilter();
        OnPropertyChanged(nameof(HasTranscript));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(HasStructuredNotes));
        OnPropertyChanged(nameof(StructuredHint));
        OnPropertyChanged(nameof(NotesFormatIsStandard));
        OnPropertyChanged(nameof(NotesFormatIsFishbone));
        OnPropertyChanged(nameof(NotesFormatIsOutline));
        OnPropertyChanged(nameof(NotesFormatIsViewpoints));
        OnPropertyChanged(nameof(HasBilingualQuestion));
        OnPropertyChanged(nameof(RecordingElapsedText));
        ModelStatusText = "演示模式：未加载识别模型";
        SetPageSubtitle("演示数据 · 不会写入你的资料");
    }

    /// <summary>
    /// D1: per-page demo state, mirroring Mac's NEXTTHOUGHT_DEMO_SECTION: the home and prepare
    /// pages are idle, the live page is the running interview, the notes page is the archived
    /// review (and the work-notes capture is the weekly work meeting).
    /// </summary>
    public void ApplyDemoSection(string section)
    {
        if (demoData is not { } demo) return;
        switch (section)
        {
            case "home":
                MeetingTitle = "";
                Recording = false;
                SetPageSubtitle("演示数据 · 不会写入你的资料");
                break;
            case "prepare":
                MeetingTitle = demo.Draft.Title;
                Recording = false;
                SelectedMode = MeetingMode.Interview;
                OnPropertyChanged(nameof(SelectedScene));
                SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == demo.InterviewSeries.Id);
                SetPageSubtitle("准备这场会 · 类型、资料与检查");
                break;
            case "live":
                MeetingTitle = demo.Live.PlannedMeetingTitle;
                Recording = true;
                DetectedQuestion = demo.Live.Question;
                AnswerText = demo.Live.Answer;
                SetPageSubtitle("演示数据 · 不会写入你的资料");
                break;
            case "notes":
                MeetingTitle = demo.Review.Meta.Title;
                SummaryText = demo.Review.SummaryMarkdown;
                PageSubtitle = ReviewFacts(demo);
                break;
            case "work":
                MeetingTitle = demo.Weekly.Meta.Title;
                SummaryText = demo.Weekly.SummaryMarkdown;
                PageSubtitle = NotesMarkdown.Facts(demo.Weekly.Notes, demo.Weekly.Meta.StartedAt,
                    (demo.Weekly.Meta.EndedAt!.Value - demo.Weekly.Meta.StartedAt).TotalSeconds,
                    demo.Weekly.Meta.InPerson, UiText.EnglishUi);
                break;
            case "history":
                MeetingTitle = demo.Review.Meta.Title;
                break;
            case "series":
                SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == demo.InterviewSeries.Id);
                break;
        }
        OnPropertyChanged(nameof(RecordingElapsedText));
        OnPropertyChanged(nameof(StatsText));
    }

    /// <summary>D1: the demo's 工作纪要 tab follows Mac demoNotes=work (the weekly work meeting).</summary>
    public void ApplyDemoNotesTab(int index)
    {
        if (demoData is null) return;
        ApplyDemoSection(index == 7 ? "work" : "notes");
    }

    /// <summary>D1: the notes header's facts line, exactly as Mac NotesMarkdown.facts builds it.</summary>
    private string ReviewFacts(DemoDataset demo) => NotesMarkdown.Facts(demo.Review.Notes, demo.Review.Meta.StartedAt,
        (demo.Review.Meta.EndedAt!.Value - demo.Review.Meta.StartedAt).TotalSeconds,
        demo.Review.Meta.InPerson, UiText.EnglishUi);
}
