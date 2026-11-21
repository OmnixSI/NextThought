using System.Text.Json;

namespace NextThought.Core;

/// <summary>Conversion between the navigable slide info and the Core slide page used by the rules.</summary>
public static class SlidePageConversion
{
    public static SlidePage ToSlidePage(this SlidePageInfo info) => new()
    {
        DocumentId = Guid.TryParse(info.DocumentId, out var id) ? id : DeterministicId(info.DocumentName),
        Page = info.Number,
        PageCount = info.PageCount,
        DocumentName = info.DocumentName,
        Text = info.Text,
        Notes = info.Notes
    };

    private static Guid DeterministicId(string name)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
        return new Guid(hash.AsSpan(0, 16));
    }
}

/// <summary>Mac ScreenKind: what kind of screen the reader is looking at (titles stay in the UI layer).</summary>
public enum ScreenKind
{
    Question,
    Code,
    Slides,
    Document,
    Chat,
    VideoCall,
    Other
}

public static class ScreenClassification
{
    public static string Raw(this ScreenKind kind) => kind switch
    {
        ScreenKind.Question => "question",
        ScreenKind.Code => "code",
        ScreenKind.Slides => "slides",
        ScreenKind.Document => "document",
        ScreenKind.Chat => "chat",
        ScreenKind.VideoCall => "video_call",
        _ => "other"
    };

    /// <summary>
    /// Cheap local classification of one OCR frame (the Mac app got the kind from the screen
    /// context model; the Windows port keeps the same categories with local rules).
    /// </summary>
    public static ScreenKind Classify(string? text)
    {
        var value = (text ?? "").Trim();
        if (value.Length < 8) return ScreenKind.Other;
        var lines = value.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        var lineCount = lines.Count;
        var codeMarks = value.Count(chars => chars is '{' or '}' or ';' or '=' or '(' or ')');
        var chatMarks = lines.Count(line => line.Contains(':') || line.Contains('：'));
        var videoMarks = new[] { "mute", "unmute", "camera", "camera off", "静音", "关闭摄像头", "参会者", "participants", "raise hand" }
            .Count(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));
        var questionMarks = lines.Count(line => line.TrimEnd().EndsWith('?') || line.TrimEnd().EndsWith('？'));
        var slideMarks = lines.Count(line => line.Length <= 60);
        if (codeMarks >= Math.Max(8, value.Length / 25)) return ScreenKind.Code;
        if (videoMarks >= 2 && lineCount <= 20) return ScreenKind.VideoCall;
        if (questionMarks >= 2 && lineCount <= 24) return ScreenKind.Question;
        if (chatMarks >= Math.Max(3, lineCount / 3) && lines.Average(line => line.Length) <= 80) return ScreenKind.Chat;
        if (lineCount <= 30 && (double)slideMarks / lineCount >= 0.8) return ScreenKind.Slides;
        if (lines.Average(line => line.Length) >= 80) return ScreenKind.Document;
        return ScreenKind.Other;
    }

    public static bool IsSlides(this ScreenKind kind) => kind == ScreenKind.Slides;
}

public enum SlideSource
{
    Screen,
    Manual,
    Speech
}

/// <summary>
/// Port of the Mac slide-following rules (AppState+Insights): screen matching keeps the manual
/// page until a different slide shows up, a lost match keeps the last page for a grace period,
/// and the speaker's own script can move the notes forward even when the screen cannot be read.
/// </summary>
public sealed class SlideFollowController
{
    public SlideFollowController(IReadOnlyList<SlidePage> pages) => Pages = pages;

    public IReadOnlyList<SlidePage> Pages { get; private set; }
    public bool FollowSlides { get; set; } = true;
    public SlidePage? Current { get; private set; }
    public SlideSource Source { get; private set; } = SlideSource.Screen;
    public DateTimeOffset Since { get; private set; }
    public DateTimeOffset? SeenAt { get; private set; }
    public string? ManualAnchor { get; private set; }

    public bool SlideIsManual => Source != SlideSource.Screen;

    public void ReplacePages(IReadOnlyList<SlidePage> pages)
    {
        Pages = pages;
        if (Current is { } current
            && !pages.Any(page => page.DocumentId == current.DocumentId && page.Page == current.Page))
            Reset();
    }

    /// <summary>One screen observation; returns the page that should be showing (or null).</summary>
    public SlidePage? ObserveScreen(string screenText, DateTimeOffset now, bool presenting = false)
    {
        if (!FollowSlides || Pages.Count == 0 || screenText.Trim().Length == 0) return Current;
        var ranked = SlideMatcher.Rank(screenText, Pages);
        var decision = SlideMatcher.Decide(ranked, Current);
        SlidePage? page = null;
        if (decision.Kind == SlideDecisionKind.Page) page = decision.Page;
        else if (decision.Kind == SlideDecisionKind.Ambiguous && Current is { } current
                 && decision.Matches.Any(match => Same(match.Page, current) && match.Score >= 0.35))
            page = current;
        if (SlideIsManual)
        {
            // Stepped by hand: the screen decides again once it shows a slide other than the
            // one it saw when the step was made.
            if (page is null || page.Key == ManualAnchor) return Current;
            Source = SlideSource.Screen;
            ManualAnchor = null;
        }
        if (page is null)
        {
            // A demo, a video or the blank between two slides: keep the last page for a while.
            var grace = presenting ? 120 : 20;
            if (Current is not null && SeenAt is { } seen && (now - seen).TotalSeconds < grace) return Current;
            Current = null;
            SeenAt = null;
            return null;
        }
        return Show(page, SlideSource.Screen, now);
    }

    /// <summary>Follows the speaker's script (Mac followSpeech); the caller filters the spoken lines.</summary>
    public SlidePage? ObserveSpeech(string speech, DateTimeOffset now)
    {
        if (!FollowSlides || speech.Length < 16) return null;
        if (Pages.Count == 0 || !Pages.Any(page => page.Notes.Length > 0)) return null;
        var candidates = Current is { } current
            ? Pages.Where(page => page.DocumentId == current.DocumentId
                                  && page.Page >= current.Page + 1 && page.Page <= current.Page + 2).ToList()
            : Pages.ToList();
        if (candidates.Count == 0) return null;
        var page = SlideMatcher.SpokenPage(candidates, speech);
        return page is null ? null : Hold(page, SlideSource.Speech, now);
    }

    /// <summary>Manual step (Mac stepSlide); also pauses screen/speech following until the screen moves on.</summary>
    public SlidePage? Step(int delta, DateTimeOffset now)
    {
        if (Pages.Count == 0) return null;
        SlidePage? target;
        if (Current is { } current)
            target = Pages.FirstOrDefault(page => page.DocumentId == current.DocumentId && page.Page == current.Page + delta);
        else
            target = Pages[0];
        return target is null ? null : Hold(target, SlideSource.Manual, now);
    }

    public void Reset()
    {
        Current = null;
        Source = SlideSource.Screen;
        Since = default;
        SeenAt = null;
        ManualAnchor = null;
    }

    /// <summary>The page whose script comes next, if any (the caller prepares its notes).</summary>
    public IEnumerable<SlidePage> UpcomingNotes(int count = 3)
    {
        if (Current is not { } current) yield break;
        for (var offset = 0; offset < count; offset++)
        {
            var page = Pages.FirstOrDefault(item => item.DocumentId == current.DocumentId && item.Page == current.Page + offset);
            if (page is not null) yield return page;
        }
    }

    private SlidePage Show(SlidePage page, SlideSource source, DateTimeOffset now)
    {
        var changed = Current is null || !Same(Current, page);
        Current = page;
        SeenAt = now;
        if (changed || Source != source)
        {
            Source = source;
            Since = now;
        }
        return page;
    }

    private SlidePage Hold(SlidePage page, SlideSource source, DateTimeOffset now)
    {
        if (!SlideIsManual) ManualAnchor = Current?.Key;
        Source = source;
        SeenAt = now;
        Since = now;
        Current = page;
        return page;
    }

    private static bool Same(SlidePage left, SlidePage right) =>
        left.DocumentId == right.DocumentId && left.Page == right.Page;
}

/// <summary>Talking points kept between meetings (port of SlideNotesCache.swift).</summary>
public static class SlideNotesCacheStore
{
    public static string Fingerprint(SlidePageInfo page, string mode) => Fingerprint(page.ToSlidePage(), mode);

    public static SlideNotes? Notes(string directory, SlidePageInfo page, string fingerprint) =>
        Notes(directory, page.ToSlidePage(), fingerprint);

    public static void Save(string directory, SlidePageInfo page, SlideNotes notes, string fingerprint) =>
        Save(directory, page.ToSlidePage(), notes, fingerprint);

    public sealed record Entry(string Fingerprint, SlideNotes Notes);

    public static string Fingerprint(SlidePage page, string mode) =>
        ContextFingerprint.Hash("1\n" + mode + "\n" + page.Text);

    public static SlideNotes? Notes(string directory, SlidePage page, string fingerprint)
    {
        var entries = Load(directory, page.DocumentId);
        return entries.TryGetValue(page.Page.ToString(), out var entry)
               && entry.Fingerprint == fingerprint
               && entry.Notes.Points.Count > 0
            ? entry.Notes
            : null;
    }

    public static void Save(string directory, SlidePage page, SlideNotes notes, string fingerprint)
    {
        var entries = Load(directory, page.DocumentId);
        entries[page.Page.ToString()] = new Entry(fingerprint, notes);
        Directory.CreateDirectory(directory);
        var file = FileFor(directory, page.DocumentId);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, JsonDefaults.Options));
            File.Move(temporary, file, true);
        }
        catch (IOException)
        {
        }
        finally
        {
            if (File.Exists(temporary)) try { File.Delete(temporary); } catch (IOException) { }
        }
    }

    private static Dictionary<string, Entry> Load(string directory, Guid documentId)
    {
        try
        {
            var file = FileFor(directory, documentId);
            if (!File.Exists(file)) return [];
            return JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(file), JsonDefaults.Options) ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return [];
        }
    }

    private static string FileFor(string directory, Guid documentId) =>
        Path.Combine(directory, documentId.ToString("D").ToUpperInvariant() + ".json");
}
