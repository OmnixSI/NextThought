using System.Collections.ObjectModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>One talking point of the current slide, with its covered/next state.</summary>
public sealed record SlidePointRow(int Index, string Text, bool Done, bool Next)
{
    public bool Pending => !Done && !Next;
}

/// <summary>
/// E7: the 屏幕与讲稿 insights module (InsightsPanel.swift) — the current slide and its talking
/// points, the screen section and the answer-progress strip. The slide following itself lives in
/// AppState.SlideFollow; this partial only layers the Mac presentation and the progress tracking
/// on top of it.
/// </summary>
public partial class AppState
{
    // ---- slide section (Mac SlideSection) ------------------------------------------

    public ObservableCollection<SlidePointRow> SlidePointRows { get; } = [];

    public bool HasSlide => CurrentSlide is not null;

    /// <summary>Mac: 「第 N / M 页」 (SlideSection).</summary>
    public string SlidePositionText => CurrentSlide is { } slide
        ? UiText.Format("第 %lld 页，共 %lld 页", slide.Number, slides.Count)
        : "";

    public bool HasSlidePoints => SlidePointRows.Count > 0;

    public string SlideDocumentName => CurrentSlide?.DocumentName ?? "";

    public bool SlideHasPrevious => CurrentSlide is { Number: > 1 };

    public bool SlideHasNext => CurrentSlide is { } slide && slide.Number < slides.Count;

    public bool SlideNotesAreScript => CurrentSlide is { } slide && SlideNotesFor(slide)?.IsScript == true;

    public string SlideTransitionText
    {
        get
        {
            if (CurrentSlide is not { } slide || SlideNotesFor(slide) is not { } notes || notes.Transition.Length == 0) return "";
            return notes.IsScript
                ? UiText.Format("下一页：%@", notes.Transition)
                : UiText.Format("过渡：%@", notes.Transition);
        }
    }

    public IReadOnlyList<string> SlideQuestionRows => CurrentSlide is { } slide && SlideNotesFor(slide) is { } notes
        ? notes.Questions
        : [];

    public bool HasSlideQuestions => SlideQuestionRows.Count > 0;

    private SlideNotes? SlideNotesFor(SlidePageInfo info)
    {
        var page = SlidePages().FirstOrDefault(item => item.Page == info.Number && item.DocumentName == info.DocumentName);
        return page is null ? null : PrepareSlideNotes(page);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void RegenerateNotes() => RegenerateSlideNotes();

    /// <summary>Mac prepareSlideNotes(force: true): drop the cached notes and write them again.</summary>
    public void RegenerateSlideNotes()
    {
        if (CurrentSlide is not { } info) return;
        var page = SlidePages().FirstOrDefault(item => item.Page == info.Number && item.DocumentName == info.DocumentName);
        if (page is null) return;
        slideNotesCacheByPage.Remove(page.Key);
        var directory = SlideNotesDirectory;
        var fingerprint = SlideNotesCacheStore.Fingerprint(page, SlideNotesMode);
        var next = SlidePages().FirstOrDefault(item =>
            item.DocumentId == page.DocumentId && item.Page == page.Page + 1);
        var notes = SlideNotes.Script(page, next) ?? new SlideNotes
        {
            Points = page.Text.Split('\n')
                .Select(line => line.Trim().TrimStart('#', '-', '*', ' '))
                .Where(line => line.Length > 1)
                .Take(4)
                .ToList(),
            Transition = next?.Title ?? ""
        };
        slideNotesCacheByPage[page.Key] = notes;
        SlideNotesCacheStore.Save(directory, page, notes, fingerprint);
        RefreshInsights();
    }

    /// <summary>Covered/next marker of every talking point, from what I have read aloud so far.</summary>
    private (HashSet<int> Covered, int? Next) SlideCoverage(SlideNotes notes)
    {
        var since = DateTimeOffset.UtcNow.AddSeconds(-120);
        var speech = string.Join(" ", currentLines
            .Where(line => line.Speaker == "me" && (line.StartedAt ?? line.Date) >= since)
            .Select(line => line.Text));
        var covered = new HashSet<int>();
        for (var index = 0; index < notes.Points.Count; index++)
            if (speech.Length > 0 && AnswerPoints.HeuristicCovered(notes.Points[index], speech)) covered.Add(index);
        return (covered, AnswerPoints.Next(covered, notes.Points.Count));
    }

    /// <summary>Fills the slide rows and raises the names the insights panel binds.</summary>
    public void RefreshSlideInsights()
    {
        SlidePointRows.Clear();
        if (CurrentSlide is { } slide && SlideNotesFor(slide) is { } notes)
        {
            var (covered, next) = SlideCoverage(notes);
            for (var index = 0; index < notes.Points.Count; index++)
                SlidePointRows.Add(new SlidePointRow(index, notes.Points[index], covered.Contains(index), next == index));
        }
        foreach (var name in new[]
                 {
                     nameof(HasSlide), nameof(SlidePositionText), nameof(HasSlidePoints), nameof(SlideDocumentName),
                     nameof(SlideHasPrevious), nameof(SlideHasNext), nameof(SlideNotesAreScript), nameof(SlideTransitionText),
                     nameof(SlideQuestionRows), nameof(HasSlideQuestions), nameof(SlidePointRows)
                 })
            OnPropertyChanged(name);
    }

    /// <summary>Mac: 听众可能会问 — clicking a question asks it right away.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task AskSlideQuestionAsync(string? question)
    {
        var text = (question ?? "").Trim();
        if (text.Length == 0) return;
        await AnswerAsync(text, automatic: false);
    }

    // ---- screen section (Mac ScreenSection) ----------------------------------------

    /// <summary>Mac LiveSessionView TranscriptColumn: the panel sits above the transcript while screen
    /// awareness is on and the scene follows slides (MeetingType.followsSlides == academic).
    /// </summary>
    public bool LiveShowsInsightsPanel => Settings.InsightScreenWatch && SelectedMode == MeetingMode.Academic;

    /// <summary>Raised by the live page when the mode or the screen-watch toggle changes.</summary>
    public void RaiseLiveInsightsEmbed() => OnPropertyChanged(nameof(LiveShowsInsightsPanel));

    public bool ScreenWatchOff => !Settings.InsightScreenWatch;

    public bool ScreenWatchRunning => Settings.InsightScreenWatch;

    public bool ScreenIsReading => ScreenWatchCapturing;

    /// <summary>Mac: 「屏幕：题目 / 代码 / 幻灯片 …」 from the frame's classified kind.</summary>
    public string ScreenTitleText => UiText.Format("屏幕：%@", UiText.T(ScreenKindTitle(ScreenWatchKind)));

    private static string ScreenKindTitle(ScreenKind kind) => kind switch
    {
        ScreenKind.Question => "题目",
        ScreenKind.Code => "代码",
        ScreenKind.Slides => "幻灯片",
        ScreenKind.Document => "文档",
        ScreenKind.Chat => "聊天",
        ScreenKind.VideoCall => "视频通话",
        _ => "其他"
    };

    /// <summary>Mac ScreenSection's TimelineView: 「N 秒前」 since the frame was read.</summary>
    public string ScreenAgeText
    {
        get
        {
            if (screenTextSeenAt is not { } at) return "";
            var seconds = Math.Max(0, (int)(DateTimeOffset.UtcNow - at).TotalSeconds);
            return UiText.Format("%lld 秒前", seconds);
        }
    }

    private DateTimeOffset? screenTextSeenAt;
    private string screenTextSeen = "";

    /// <summary>Called by the panel's 5 s clock so the relative time keeps moving (Mac TimelineView).</summary>
    public void RefreshScreenAge() => OnPropertyChanged(nameof(ScreenAgeText));

    public string ScreenStatusLine
    {
        get
        {
            var source = Settings.SelectedInsightSourceLabel;
            var status = ScreenWatchStatus.Length > 0 ? ScreenWatchStatus : ScreenWatchObservation;
            return source.Length == 0 ? status : (status.Length == 0 ? source : status + " · " + source);
        }
    }

    public bool HasScreenError => ScreenWatchError.Length > 0;

    public string ScreenErrorText => ScreenWatchError;

    public bool HasScreenText => ScreenContextText.Length > 0;

    public string ScreenText => ScreenContextText;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool screenTextExpanded;

    partial void OnScreenTextExpandedChanged(bool value) => OnPropertyChanged(nameof(ScreenTextMaxLines));

    public int ScreenTextMaxLines => ScreenTextExpanded ? 30 : 3;

    public void ToggleScreenText() => ScreenTextExpanded = !ScreenTextExpanded;

    /// <summary>Mac ScreenWatchOffHint's 打开屏幕感知.</summary>
    public void EnableScreenWatch()
    {
        Settings.InsightScreenWatch = true;
        _ = Settings.SaveAsync();
        if (ScreenWatchStatus.Length == 0) ScreenWatchStatus = "等待首次读屏…";
        RefreshInsights();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task RequestScreenReadAsync()
    {
        if (!Settings.InsightScreenWatch) EnableScreenWatch();
        await ReadScreenNowAsync();
        RefreshInsights();
    }

    // ---- answer progress strip (Mac AnswerProgressStrip) ----------------------------

    /// <summary>Mac insightAnswerProgress defaults to true; Windows has no separate setting yet.</summary>
    public bool AnswerProgressEnabled => true;

    public bool HasAnswerProgress
    {
        get
        {
            if (!AnswerProgressEnabled || !Recording || Answering) return false;
            if (AnswerHistoryCursor is not null) return false;
            return AnswerProgressPointTexts.Count >= 2;
        }
    }

    public IReadOnlyList<string> AnswerProgressPointTexts
    {
        get
        {
            var answer = BilingualAnswer.Spoken(DisplayedAnswerText);
            return answer.Length == 0 ? [] : AnswerPoints.Split(answer, 10);
        }
    }

    private (HashSet<int> Covered, int? Next) AnswerProgressCoverage()
    {
        var points = AnswerProgressPointTexts;
        if (points.Count == 0) return ([], null);
        var since = DateTimeOffset.UtcNow.AddSeconds(-120);
        var speech = string.Join(" ", currentLines
            .Where(line => line.Speaker == "me" && (line.StartedAt ?? line.Date) >= since)
            .Select(line => line.Text));
        var covered = new HashSet<int>();
        for (var index = 0; index < points.Count; index++)
            if (speech.Length > 0 && AnswerPoints.HeuristicCovered(points[index], speech)) covered.Add(index);
        return (covered, AnswerPoints.Next(covered, points.Count));
    }

    public int AnswerProgressCoveredCount => AnswerProgressCoverage().Covered.Count;

    public int AnswerProgressTotal => AnswerProgressPointTexts.Count;

    /// <summary>Mac AnswerProgressStrip: 「1/6」 (the counter inside 「已讲 1/6」).</summary>
    public string AnswerProgressCounterText => AnswerProgressCoveredCount + "/" + AnswerProgressTotal;

    /// <summary>Mac MetricPill: 「已讲 x/y」.</summary>
    public string AnswerProgressMetricText => UiText.Format("已讲 %lld/%lld", AnswerProgressCoveredCount, AnswerProgressTotal);

    public string NextAnswerPointText
    {
        get
        {
            var points = AnswerProgressPointTexts;
            var next = AnswerProgressCoverage().Next;
            return next is { } index && index >= 0 && index < points.Count ? points[index] : "";
        }
    }

    /// <summary>Recomputes everything the insights panel shows (slide, screen, answer progress).</summary>
    public void RefreshInsights()
    {
        // Mac ScreenSection's relative clock counts from the frame the panel last saw.
        if (ScreenText != screenTextSeen)
        {
            screenTextSeen = ScreenText;
            screenTextSeenAt = ScreenText.Length > 0 ? DateTimeOffset.UtcNow : null;
        }
        RefreshSlideInsights();
        foreach (var name in new[]
                 {
                     nameof(ScreenWatchOff), nameof(ScreenWatchRunning), nameof(ScreenIsReading),
                     nameof(ScreenStatusLine), nameof(HasScreenError), nameof(ScreenErrorText),
                     nameof(HasScreenText), nameof(ScreenText), nameof(ScreenTextMaxLines), nameof(ScreenTextExpanded),
                     nameof(ScreenTitleText), nameof(ScreenAgeText), nameof(LiveShowsInsightsPanel),
                     nameof(HasAnswerProgress), nameof(AnswerProgressCounterText), nameof(AnswerProgressMetricText),
                     nameof(NextAnswerPointText), nameof(AnswerProgressCoveredCount), nameof(AnswerProgressTotal),
                     nameof(AnswerProgressPointTexts)
                 })
            OnPropertyChanged(name);
    }

    /// <summary>Cheap refresh for the user's own speech (answer progress moves as I talk).</summary>
    public void RaiseAnswerProgress()
    {
        OnPropertyChanged(nameof(HasAnswerProgress));
        OnPropertyChanged(nameof(AnswerProgressCounterText));
        OnPropertyChanged(nameof(AnswerProgressMetricText));
        OnPropertyChanged(nameof(NextAnswerPointText));
        OnPropertyChanged(nameof(AnswerProgressCoveredCount));
        OnPropertyChanged(nameof(AnswerProgressTotal));
        OnPropertyChanged(nameof(AnswerProgressPointTexts));
    }
}
