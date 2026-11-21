using Avalonia.Media.Imaging;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>A series row on the home page: "● ACL 2026 口头报告  汇报 / 答辩   0 场记录 · 2 份资料 →".</summary>
public sealed record HomeSeriesRow(string Id, string Name, string TypeName, string Counts, MeetingMode Mode);

/// <summary>A recent-meeting row on the home page: title, outcome line, when, scene pigment.</summary>
public sealed record HomeRecentRow(RecentMeetingRow Row, string Outcome, string When, MeetingMode Mode)
{
    public string Title => Row.Title;
    public SessionMeta Meta => Row.Meta;
}

/// <summary>
/// E1: the home page state (HomeView.swift): greeting, scene guessing, continue-series,
/// recent meetings with the Mac outcome lines, the screenshot card and the first-run resume row.
/// </summary>
public partial class AppState
{
    // ---- greeting (HomeView.swift greeting) ------------------------------------------

    public string HomeGreeting => DateTime.Now.Hour switch
    {
        < 5 => UiText.T("夜深了。"),
        < 12 => UiText.T("上午好。"),
        < 18 => UiText.T("下午好。"),
        _ => UiText.T("晚上好。")
    };

    /// <summary>"接下来是什么场合？" in the hero's second line.</summary>
    public string HomeQuestion => UiText.T("接下来是什么场合？");

    // ---- scene guessing (StartComposer) ---------------------------------------------

    /// <summary>
    /// E1: guess the scene from the meeting title as it is typed (MeetingTypeGuesser, previously only
    /// used by the self-check). Returns true when the scene changed; a manually picked scene pins
    /// itself until the title is cleared.
    /// </summary>
    public bool GuessSceneFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var guessed = MeetingTypeGuesser.Guess(title);
        if (guessed == SessionMode.General) return false;
        var mode = guessed.ToMeetingMode();
        if (mode == SelectedMode) return false;
        SelectedMode = mode;
        OnPropertyChanged(nameof(SelectedScene));
        OnPropertyChanged(nameof(SceneTitle));
        return true;
    }

    /// <summary>E1: a scene picked on the home page applies its scene defaults right away.</summary>
    public void ApplySceneDefaults(MeetingMode mode)
    {
        var defaults = SceneDefaultsFor(mode);
        Settings.AutomaticAnswers = defaults.LiveMode == "自动回答";
        Settings.SelectedCueWidth = Settings.CueWidthChoices.FirstOrDefault(choice =>
            choice.Value == (defaults.CueStyle == "逐句" ? "wide" : "regular")) ?? Settings.CueWidthChoices[1];
        TemplateKind = defaults.NotesTemplate switch
        {
            "学术汇报" => NotesTemplateKind.Academic,
            "工作会议" => NotesTemplateKind.Work,
            _ => NotesTemplateKind.General
        };
        SelectedTemplateId = TemplateKind.Raw();
    }

    /// <summary>E1: composer's "开始"/"先准备资料" (Mac StartComposer.open).</summary>
    public void StartMeetingFromHome(bool startNow, MeetingMode scene)
    {
        SelectedMode = scene;
        ApplySceneDefaults(scene);
        if (startNow)
        {
            if (StartRecordingCommand.CanExecute(null)) StartRecordingCommand.Execute(null);
            else Section = AppSection.Prepare;
            return;
        }
        Section = AppSection.Prepare;
    }

    // ---- continue series (HomeView.swift ContinueSeries) -----------------------------

    public ObservableCollection<HomeSeriesRow> HomeSeriesRows { get; } = [];
    public bool HasHomeSeries => HomeSeriesRows.Count > 0;
    public bool AreHomeRecentEmpty => HomeRecentRows.Count == 0;

    /// <summary>
    /// E1: workspaces ordered by their latest meeting (Mac ContinueSeries: lastSessionDate ??
    /// updatedAt, standing rows excluded, at most three).
    /// </summary>
    public void RefreshHomeSeries()
    {
        HomeSeriesRows.Clear();
        var sessions = History.Select(row => row.Meta).ToList();
        var rows = workspace.Series
            .Where(series => series.Standing != true)
            .Select(series => new
            {
                Series = series,
                Last = sessions.Where(meta => series.SessionIds.Contains(meta.Id.ToString("D")))
                    .OrderByDescending(meta => meta.StartedAt).FirstOrDefault()
            })
            .OrderByDescending(item => item.Last?.StartedAt ?? item.Series.CreatedAt)
            // Demo meetings share one timestamp; keep the order stable across renders.
            .ThenByDescending(item => item.Series.CreatedAt)
            .ThenBy(item => item.Series.Name, StringComparer.Ordinal)
            .Take(3);
        foreach (var item in rows)
        {
            var mode = item.Series.EffectiveMode;
            var stats = MeetingStats.Of(sessions, series: Guid.TryParse(item.Series.Id, out var seriesGuid) ? seriesGuid : null);
            var counts = stats.Meetings > 0
                ? UiText.Format("%lld 场 · %@ · %lld 份资料", stats.Meetings, MeetingStatsText.Duration(stats), item.Series.Documents.Count)
                : UiText.Format("还没开过 · %lld 份资料", item.Series.Documents.Count);
            HomeSeriesRows.Add(new HomeSeriesRow(item.Series.Id, item.Series.Name, ModeTitle(mode), counts, mode));
        }
        OnPropertyChanged(nameof(HasHomeSeries));
    }

    /// <summary>
    /// E1/J4: clicking a series starts the next meeting in it (Mac ContinueSeries.prepareNext):
    /// with a previous meeting the transcript is carried like 「以此上一场」; a fresh series opens
    /// the preparation draft with its materials, lessons and preferences attached.
    /// </summary>
    public void PrepareNextMeetingInSeries(string seriesId)
    {
        if (Recording || Working) { SetStatus(UiText.T("先停止当前任务，再准备下一场。"), true); return; }
        var series = workspace.Find(seriesId);
        if (series is null) return;
        SelectedSeries = SeriesRows.FirstOrDefault(row => row.Id == seriesId);
        SelectedMode = series.EffectiveMode;
        ApplySceneDefaults(series.EffectiveMode);
        var previous = History
            .Where(row => series.SessionIds.Contains(row.Meta.Id.ToString("D"), StringComparer.OrdinalIgnoreCase)
                          || series.SessionIds.Contains(row.Meta.Id.ToString("D").ToUpperInvariant(), StringComparer.Ordinal))
            .OrderByDescending(row => row.Meta.StartedAt)
            .FirstOrDefault();
        if (previous is not null)
        {
            PrepareNextMeetingAfter(previous);
            return;
        }
        EnsureDraft(series.EffectiveMode);
        Section = AppSection.Prepare;
    }

    // ---- J3: 用了多少 (MeetingStatsViews.swift HomeStats) ---------------------------

    [ObservableProperty] private bool homeStatsAllTime;

    partial void OnHomeStatsAllTimeChanged(bool value) => RefreshHomeStats();

    public bool HomeHasStats => History.Count > 0;

    public MeetingStats CurrentHomeStats => MeetingStats.Of(History.Select(row => row.Meta),
        HomeStatsAllTime ? null : MeetingStats.Month(DateTimeOffset.Now));

    public string HomeStatsMeetings => CurrentHomeStats.Meetings.ToString();
    public string HomeStatsDuration => MeetingStatsText.Duration(CurrentHomeStats);
    public string HomeStatsQuestions => CurrentHomeStats.Questions.ToString();
    public string HomeStatsAnswers => CurrentHomeStats.Answers.ToString();
    public bool HomeStatsIsMonth => !HomeStatsAllTime;
    public bool HomeStatsIsAll => HomeStatsAllTime;

    [RelayCommand]
    private void ShowHomeStatsMonth()
    {
        HomeStatsAllTime = false;
        RefreshHomeStats();
    }

    [RelayCommand]
    private void ShowHomeStatsAllTime()
    {
        HomeStatsAllTime = true;
        RefreshHomeStats();
    }

    public void RefreshHomeStats()
    {
        foreach (var name in new[]
                 {
                     nameof(HomeHasStats), nameof(HomeStatsMeetings), nameof(HomeStatsDuration),
                     nameof(HomeStatsQuestions), nameof(HomeStatsAnswers), nameof(HomeStatsIsMonth),
                     nameof(HomeStatsIsAll)
                 })
            OnPropertyChanged(name);
    }

    // ---- recent meetings (HomeView.swift RecentMeetings) ----------------------------

    public ObservableCollection<HomeRecentRow> HomeRecentRows { get; } = [];

    /// <summary>E1: the four latest meetings, without the meeting that is being recorded.</summary>
    public void RefreshHomeRecent()
    {
        HomeRecentRows.Clear();
        var active = Recording ? CurrentSession?.Id : null;
        foreach (var row in History.Where(row => row.Meta.Id != active)
                     .OrderByDescending(row => row.Meta.StartedAt)
                     .Take(4))
        {
            var meta = row.Meta;
            var mode = ParseMode(meta.Mode);
            HomeRecentRows.Add(new HomeRecentRow(row,
                MeetingOutcome.Line(meta, summarizing: currentSummarizingId == meta.Id),
                MeetingOutcome.When(meta.StartedAt), mode));
        }
        OnPropertyChanged(nameof(HomeRecentRows));
        OnPropertyChanged(nameof(AreHomeRecentEmpty));
    }

    public void OpenRecentMeeting(HomeRecentRow row) => _ = OpenRecentMeetingAsync(row);

    private async Task OpenRecentMeetingAsync(HomeRecentRow row)
    {
        try { await SelectHistoryAsync(row.Meta); }
        catch (Exception error) { SetStatus(error.Message, true); }
    }

    /// <summary>True while this session's notes are being generated (Mac RecentMeetings summarizing).</summary>
    private Guid? currentSummarizingId => Summarizing && CurrentSession is { } session ? session.Id : null;

    private static MeetingMode ParseMode(string? mode) => SessionModes.Parse(mode).ToMeetingMode();

    /// <summary>Localized scene title for a home row (Mac SessionMode.displayName).</summary>
    private static string ModeTitle(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => UiText.T("面试"),
        MeetingMode.Academic => UiText.T("汇报 / 答辩"),
        MeetingMode.LabMeeting => UiText.T("组会"),
        MeetingMode.Meeting => UiText.T("工作会议"),
        MeetingMode.Client => UiText.T("客户沟通"),
        MeetingMode.Lesson => UiText.T("讲座 / 课程"),
        MeetingMode.Discussion => UiText.T("小组讨论"),
        MeetingMode.GroupProject => UiText.T("小组作业"),
        MeetingMode.Custom => UiText.T("自定义"),
        _ => UiText.T("通用")
    };

    // ---- screenshot card (HomeView.swift HomeScreenshotCard / AppState screenshot flow) ----

    [ObservableProperty] private Bitmap? screenshotPreview;
    [ObservableProperty] private string screenshotPreviewPath = "";

    public bool HasScreenshotPreview => ScreenshotPreview is not null;

    partial void OnScreenshotPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasScreenshotPreview));

    /// <summary>E1/Mac commitRegionScreenshot: the crop waits on the home page until analyzed or cancelled.</summary>
    public void SetScreenshotPreview(string path, Bitmap bitmap)
    {
        ClearScreenshotPreview();
        ScreenshotPreviewPath = path;
        ScreenshotPreview = bitmap;
        if (!Recording) Section = AppSection.Home;
    }

    /// <summary>Mac clearScreenshot: drop the temporary file and the preview.</summary>
    public void ClearScreenshotPreview()
    {
        var path = ScreenshotPreviewPath;
        ScreenshotPreviewPath = "";
        ScreenshotPreview = null;
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    /// <summary>Mac answerScreenshot: analyze the preview through the Windows OCR → answer path.</summary>
    public async Task AnalyzeScreenshotAsync()
    {
        if (ScreenshotPreviewPath.Length == 0) return;
        var path = ScreenshotPreviewPath;
        ScreenshotPreview = null;
        ScreenshotPreviewPath = "";
        try { await AnalyzeScreenshotFileAsync(path); }
        finally
        {
            // E13: the temporary picture is deleted after the question, even when analysis fails.
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    /// <summary>The old capture tail (archive → library OCR → prompt → slide follow), shared with live capture.</summary>
    public async Task AnalyzeScreenshotFileAsync(string path)
    {
        try
        {
            await ArchiveScreenshotAsync(path);
            await Library.ImportAsync([path]);
            PromptAboutScreenshot(Library.SelectedPreview);
            FollowSlideFromCapture(Library.SelectedPreview);
        }
        finally
        {
            // E13: Mac keeps the temp file until the question is asked, then deletes it.
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    // ---- page refresh ----------------------------------------------------------------

    /// <summary>Re-reads the home lists from the current workspace/history (HomeView calls this).</summary>
    public void RefreshHome()
    {
        RefreshHomeSeries();
        RefreshHomeRecent();
        OnPropertyChanged(nameof(HomeGreeting));
        OnPropertyChanged(nameof(HomeQuestion));
    }
}
