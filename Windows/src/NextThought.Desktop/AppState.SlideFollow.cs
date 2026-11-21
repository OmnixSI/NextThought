using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E9: slide following and slide-note caching (port of AppState+Insights.followSlide / followSpeech
/// / prepareSlideNotes and SlideNotesCache.swift). Screen reads and the speaker's own script both
/// move the current page; a manual step keeps the page until the screen shows another slide.
/// </summary>
public partial class AppState
{
    private SlideFollowController? slideFollow;
    private readonly Dictionary<string, SlideNotes> slideNotesCacheByPage = [];
    private DateTimeOffset slideFollowSince;
    /// <summary>Where the current slide came from (Mac LiveInsightsState.slideSource): screen, manual step or speech.</summary>
    private SlideSource lastSlideSource = SlideSource.Screen;

    /// <summary>E4/island 7: the 讲稿「手动」tag on the cue island reads this.</summary>
    public bool SlideIsManual => lastSlideSource == SlideSource.Manual;

    /// <summary>E9: the last screen classification (Mac ScreenKind) and its local relevance.</summary>
    [ObservableProperty] private ScreenKind screenWatchKind = ScreenKind.Other;
    [ObservableProperty] private double screenWatchRelevance;
    [ObservableProperty] private string screenWatchObservation = "";


    public ScreenKind ScreenKind => ScreenWatchKind;

    private string SlideNotesDirectory => System.IO.Path.Combine(Paths.Root, "SlideNotes");
    private string SlideNotesMode => CurrentSession?.Mode ?? "meeting";

    private SlideFollowController SlideFollow
    {
        get
        {
            if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
            var pages = SlidePageRecords();
            if (slideFollow is null) slideFollow = new SlideFollowController(pages);
            else slideFollow.ReplacePages(pages);
            slideFollow.FollowSlides = Settings.InsightFollowSlides;
            return slideFollow;
        }
    }

    /// <summary>The navigable slide list the insights/presenter UI binds (Mac SlidePage rows).</summary>
    private IReadOnlyList<SlidePageInfo> SlidePages()
    {
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        return slides;
    }

    /// <summary>The same slides as the Core follow-rule pages.</summary>
    private List<SlidePage> SlidePageRecords() => slides.Select(info => new SlidePage
    {
        DocumentId = DocumentIdFor(info),
        Page = info.Number,
        PageCount = slides.Count,
        DocumentName = info.DocumentName,
        Text = info.Text,
        Notes = info.Notes
    }).ToList();

    private static Guid DocumentIdFor(SlidePageInfo info)
    {
        if (Guid.TryParse(info.DocumentId, out var id)) return id;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(info.DocumentName));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>Mac followSpeech: only the user's own lines move the slide notes forward.</summary>
    private void FollowSpeechForLine(TranscriptLine line)
    {
        if (!Recording || ListeningPaused || !Settings.InsightFollowSlides) return;
        if (line.Speaker != "me") return;
        // Presenting notes on the island may be switched off; following still works, the cue just
        // does not show the notes (Mac followSpeech has no presenter gate; the island hides them).
        var controller = SlideFollow;
        if (controller.Current is { } current && controller.Since > slideFollowSince) slideFollowSince = controller.Since;
        var since = controller.Current is null
            ? DateTimeOffset.UtcNow.AddSeconds(-30)
            : (slideFollowSince == default ? DateTimeOffset.UtcNow.AddSeconds(-30) : slideFollowSince);
        var speech = string.Join(" ", currentLines
            .Where(item => item.Speaker == "me" && (item.StartedAt ?? item.Date) >= since)
            .Select(item => item.Text));
        if (speech.Length < 16) return;
        var page = controller.ObserveSpeech(speech, DateTimeOffset.UtcNow);
        if (page is not null) ApplyFollowedSlide(page, SlideSource.Speech);
    }

    /// <summary>Mac followSlide on one screen OCR text.</summary>
    private void FollowSlideFromScreenCore(string screenText)
    {
        if (!Settings.InsightFollowSlides || screenText.Trim().Length == 0) return;
        var controller = SlideFollow;
        var page = controller.ObserveScreen(screenText, DateTimeOffset.UtcNow, PresenterLayoutActive);
        if (page is not null)
        {
            ApplyFollowedSlide(page, controller.Source);
            return;
        }
        // No slide matched: Mac clears the insights slide but the manual/current page stays.
        slideFollowSince = DateTimeOffset.UtcNow;
    }

    /// <summary>Mac stepSlide: manual page turn pauses automatic following until the screen moves on.</summary>
    private void StepSlide(int delta)
    {
        if (slides.Count == 0) slides = SlideDecks.FromDocuments(Library.DocumentsForRetrieval());
        if (slides.Count == 0) { SetStatus("还没有导入 PPTX，导入后可翻页。", true); return; }
        var page = SlideFollow.Step(delta, DateTimeOffset.UtcNow);
        if (page is null) return;
        ApplyFollowedSlide(page, SlideSource.Manual);
    }

    private void ApplyFollowedSlide(SlidePage page, SlideSource source)
    {
        var index = slides.ToList().FindIndex(info => info.Number == page.Page && info.DocumentName == page.DocumentName);
        if (index < 0) return;
        var changed = index != slideIndex;
        slideIndex = index;
        if (lastSlideSource != source)
        {
            lastSlideSource = source;
            OnPropertyChanged(nameof(SlideIsManual));
        }
        slideFollowSince = DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(SlideStatus));
        OnPropertyChanged(nameof(CurrentSlideText));
        OnPropertyChanged(nameof(CurrentSlidePointsText));
        OnPropertyChanged(nameof(HasCurrentSlidePoints));
        if (!changed) return;
        PrepareSlideNotes(page);
        SetStatus(source switch
        {
            SlideSource.Speech => UiText.Format("已跟随讲话切到第 %lld 页。", page.Page),
            SlideSource.Manual => UiText.Format("幻灯片：第 %lld 页（手动）。", page.Page),
            _ => UiText.Format("已按屏幕切到第 %lld 页。", page.Page)
        });
    }

    /// <summary>
    /// Mac prepareSlideNotes: the presenter's script is used as-is; otherwise the slide's own lines
    /// become the talking points. Loaded from and saved to the note cache by content fingerprint.
    /// </summary>
    private SlideNotes? PrepareSlideNotes(SlidePageInfo info)
    {
        var page = SlidePageRecords().FirstOrDefault(item =>
            item.Page == info.Number && item.DocumentName == info.DocumentName);
        return page is null ? null : PrepareSlideNotes(page);
    }

    private SlideNotes? PrepareSlideNotes(SlidePage page)
    {
        var key = page.Key;
        if (slideNotesCacheByPage.TryGetValue(key, out var cached)) return cached;
        var fingerprint = SlideNotesCacheStore.Fingerprint(page, SlideNotesMode);
        if (SlideNotesCacheStore.Notes(SlideNotesDirectory, page, fingerprint) is { } persisted)
        {
            slideNotesCacheByPage[key] = persisted;
            return persisted;
        }
        var next = SlidePageRecords().FirstOrDefault(item =>
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
        slideNotesCacheByPage[key] = notes;
        SlideNotesCacheStore.Save(SlideNotesDirectory, page, notes, fingerprint);
        return notes;
    }

    /// <summary>Notes for the currently shown slide, rendered as the cue's talking points.</summary>
    private string CurrentSlidePoints()
    {
        if (CurrentSlide is not { } info) return "";
        var page = SlidePageRecords().FirstOrDefault(item => item.Page == info.Number && item.DocumentName == info.DocumentName);
        if (page is null) return "";
        var notes = PrepareSlideNotes(page);
        if (notes is null) return "";
        var lines = notes.Points.Take(4).Select((point, index) => $"{index + 1}. {point}").ToList();
        if (notes.Transition.Length > 0) lines.Add("→ " + notes.Transition);
        return string.Join("\n", lines);
    }

    /// <summary>E9: classify one OCR frame and keep the local relevance for the next question.</summary>
    private void NoteScreenObservation(string text)
    {
        ScreenWatchKind = ScreenClassification.Classify(text);
        var since = DateTimeOffset.UtcNow.AddSeconds(-30);
        var speech = string.Join(" ", currentLines
            .Where(line => line.Speaker != "me" && (line.StartedAt ?? line.Date) >= since)
            .Select(line => line.Text));
        ScreenWatchRelevance = NextThought.Core.ScreenText.HeuristicRelevance(text, speech) ? 0.6 : 0.2;
        ScreenWatchObservation = ScreenWatchKind.Raw() + " · " + ScreenWatchRelevance.ToString("0.00");
    }
}
