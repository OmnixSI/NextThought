using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E31/D1: the staged demo mode. The Mac demo script (DemoMode.swift / DemoFilm.swift) is ported
/// verbatim into <see cref="DemoScript"/>/<see cref="DemoStages"/>; this file seeds the data once
/// and then applies the same stages the Mac <c>NEXTTHOUGHT_DEMO_SECTION</c> entries apply.
/// Nothing here is ever written to the user's data directory.
/// </summary>
public partial class AppState
{
    /// <summary>The resolved Mac demo data for the current demo session (null outside demo mode).</summary>
    private DemoDataset? demoData;

    /// <summary>
    /// Fills the interface with the Mac demonstration data (DemoMode.swift populateDemo,
    /// demoReviewNotes and demoWeeklyMeeting) and applies a Mac demo stage. The Windows shell opens
    /// on the home page, so the default stage is the idle "home" one; the Mac default "assistant"
    /// stage (a running meeting) is available for callers that ask for it.
    /// Never touches the data directory; the old invented activity feeder and its structural-sample
    /// copy are gone.
    /// </summary>
    public void LoadDemo(string stage = DemoStages.Home)
    {
        var demo = DemoContent.Create();
        demoData = demo;
        // The demo is an interview; every stage starts from that scene (Mac's demo workspaces
        // were installed as an interview set).
        SelectedMode = MeetingMode.Interview;
        OnPropertyChanged(nameof(SelectedScene));
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
        SystemPartial = MicrophonePartial = "";
        SystemPartialTranslation = MicrophonePartialTranslation = "";
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
        // E1: the home page's own lists (continue-series order, recent outcome lines).
        RefreshHome();
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
        ApplyDemoStage(stage);
    }

    /// <summary>
    /// E31: applies one Mac demo stage. The plan (DemoStages.For) carries the differences the Mac
    /// apply() makes: a running meeting only for assistant/live/live-notes, the record-only live-notes
    /// state that clears the visible answer, the prepare draft, and which shell page the stage belongs to.
    /// </summary>
    public void ApplyDemoStage(string stage, IReadOnlyDictionary<string, string>? defaults = null)
    {
        if (demoData is not { } demo) return;
        var plan = DemoStages.For(stage);
        if (defaults is { Count: > 0 }) plan = plan with { Defaults = defaults };
        // Mac: demo-only live states; every other stage is an idle archived view.
        if (plan.ClearsAnswerState)
        {
            DetectedQuestion = "";
            AnswerText = "";
            CurrentAnswerHistory.Clear();
            RetrievalInfo = "";
            IslandTranslation = "";
        }
        if (plan.IsRunning && (!Recording || plan.Name == DemoStages.LiveNotes))
            recordingStartedAt = demo.Live.StartedAt;
        Recording = plan.IsRunning;
        Settings.AutomaticAnswers = plan.AutomaticAnswers;
        if (plan.PopulatesDraft)
        {
            // Mac populateDemoDraft: the next interview's draft, with the series' documents as baseline.
            MeetingTitle = demo.Draft.Title;
            SelectedMode = MeetingMode.Interview;
            OnPropertyChanged(nameof(SelectedScene));
            SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == demo.InterviewSeries.Id);
        }
        else if (plan.Page == DemoPage.Meetings && plan.Defaults.TryGetValue("demoNotes", out var notes) && notes == "work")
        {
            // Mac notes-work capture (demoNotes=work): the weekly work meeting instead of the review.
            MeetingTitle = demo.Weekly.Meta.Title;
            SummaryText = demo.Weekly.SummaryMarkdown;
            PageSubtitle = NotesMarkdown.Facts(demo.Weekly.Notes, demo.Weekly.Meta.StartedAt,
                (demo.Weekly.Meta.EndedAt!.Value - demo.Weekly.Meta.StartedAt).TotalSeconds,
                demo.Weekly.Meta.InPerson, UiText.EnglishUi);
        }
        else if (plan.Name == DemoStages.Home || plan.Name == DemoStages.Assistant)
        {
            if (plan.IsRunning)
            {
                // Mac's default section: the demo meeting runs behind the home page.
                MeetingTitle = demo.Live.PlannedMeetingTitle;
                DetectedQuestion = demo.Live.Question;
                AnswerText = demo.Live.Answer;
                RetrievalInfo = demo.Live.RetrievalNote;
                IslandTranslation = DemoContent.IslandTranslation;
            }
            else
            {
                MeetingTitle = "";
            }
            SetPageSubtitle("演示数据 · 不会写入你的资料");
        }
        OnPropertyChanged(nameof(RecordingElapsedText));
        OnPropertyChanged(nameof(StatsText));
    }

    /// <summary>
    /// D1/E31: per-page demo state, mapping the Windows capture pages to Mac demo stages:
    /// home→home, prepare→prepare, live→live, notes/history→meetings (archived review),
    /// series/library→library, settings→settings, work→meetings with demoNotes=work.
    /// </summary>
    public void ApplyDemoSection(string section)
    {
        if (demoData is not { } demo) return;
        switch (section)
        {
            case "home":
                ApplyDemoStage(DemoStages.Home);
                MeetingTitle = "";
                SetPageSubtitle("演示数据 · 不会写入你的资料");
                break;
            case "prepare":
                ApplyDemoStage(DemoStages.Prepare);
                SetPageSubtitle("准备这场会 · 类型、资料与检查");
                break;
            case "live":
                ApplyDemoStage(DemoStages.Live);
                MeetingTitle = demo.Live.PlannedMeetingTitle;
                DetectedQuestion = demo.Live.Question;
                AnswerText = demo.Live.Answer;
                SetPageSubtitle("演示数据 · 不会写入你的资料");
                break;
            case "notes":
                ApplyDemoStage(DemoStages.Meetings);
                MeetingTitle = demo.Review.Meta.Title;
                SummaryText = demo.Review.SummaryMarkdown;
                PageSubtitle = ReviewFacts(demo);
                break;
            case "work":
                var work = DemoStages.MeetingsWithWorkNotes();
                ApplyDemoStage(work.Name, work.Defaults);
                break;
            case "history":
                ApplyDemoStage(DemoStages.Meetings);
                MeetingTitle = demo.Review.Meta.Title;
                break;
            case "series":
            case "library":
                ApplyDemoStage(DemoStages.Library);
                MeetingTitle = "";
                SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == demo.InterviewSeries.Id);
                break;
            case "settings":
                ApplyDemoStage(DemoStages.Settings);
                MeetingTitle = "";
                break;
        }
        OnPropertyChanged(nameof(RecordingElapsedText));
        OnPropertyChanged(nameof(StatsText));
    }

    /// <summary>D1/E31: the demo's 工作纪要 tab follows Mac demoNotes=work (the weekly work meeting).</summary>
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
