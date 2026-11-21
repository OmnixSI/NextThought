using NextThought.Core;

namespace NextThought.Platform;

/// <summary>The four Mac shell destinations (Models.swift AppSection).</summary>
public enum DemoPage { Home, Meetings, Library, Settings }

/// <summary>
/// E31: one staged entry of the Mac demo mode — the effect of
/// <c>NEXTTHOUGHT_DEMO_SECTION</c> plus the defaults table (DemoMode.swift <c>apply()</c>).
/// The Windows screenshot tool maps one capture per stage, exactly like the Mac script that
/// re-launches the app per section.
/// </summary>
public sealed record DemoStagePlan(
    string Name,
    DemoPage Page,
    bool IsRunning,
    bool AutomaticAnswers,
    bool ClearsAnswerState,
    bool PopulatesDraft,
    bool Onboarding,
    IReadOnlyDictionary<string, string> Defaults)
{
    /// <summary>The Mac screen the stage belongs to (captured as the window title row in English).</summary>
    public string PageName => Page switch
    {
        DemoPage.Home => "home",
        DemoPage.Meetings => "meetings",
        DemoPage.Library => "library",
        _ => "settings"
    };
}

/// <summary>
/// The Mac demo section names and their aliases. DemoMode.swift accepts the older script names
/// (assistant / materials / history) and otherwise treats an unknown section as home.
/// </summary>
public static class DemoStages
{
    public const string Home = "home";
    public const string Prepare = "prepare";
    public const string Live = "live";
    public const string LiveNotes = "live-notes";
    public const string Meetings = "meetings";
    public const string Library = "library";
    public const string Settings = "settings";
    public const string Assistant = "assistant";

    public static IReadOnlyList<string> Names { get; } = [Home, Prepare, Live, LiveNotes, Meetings, Library, Settings];

    private static readonly IReadOnlyDictionary<string, string> NoDefaults = new Dictionary<string, string>();

    /// <summary>Resolves a Mac demo section name (or an older alias) to its staged plan.</summary>
    public static DemoStagePlan For(string? name)
    {
        var key = (name ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            // AppSection(demoName:): assistant/live/prepare are the home screen.
            "assistant" or "live" or "prepare" => ForPage(key, DemoPage.Home),
            "live-notes" => new DemoStagePlan(LiveNotes, DemoPage.Home, IsRunning: true,
                AutomaticAnswers: false, ClearsAnswerState: true, PopulatesDraft: false,
                Onboarding: false, NoDefaults),
            "materials" or "library" => new DemoStagePlan(Library, DemoPage.Library, IsRunning: false,
                AutomaticAnswers: true, ClearsAnswerState: false, PopulatesDraft: false,
                Onboarding: false, NoDefaults),
            "history" or "meetings" => new DemoStagePlan(Meetings, DemoPage.Meetings, IsRunning: false,
                AutomaticAnswers: true, ClearsAnswerState: false, PopulatesDraft: false,
                Onboarding: false, NoDefaults),
            "settings" => new DemoStagePlan(Settings, DemoPage.Settings, IsRunning: false,
                AutomaticAnswers: true, ClearsAnswerState: false, PopulatesDraft: false,
                Onboarding: false, NoDefaults),
            _ => new DemoStagePlan(Home, DemoPage.Home, IsRunning: false,
                AutomaticAnswers: true, ClearsAnswerState: false, PopulatesDraft: false,
                Onboarding: false, NoDefaults)
        };
    }

    private static DemoStagePlan ForPage(string name, DemoPage page) => name switch
    {
        "prepare" => new DemoStagePlan(Prepare, page, IsRunning: false, AutomaticAnswers: true,
            ClearsAnswerState: false, PopulatesDraft: true, Onboarding: false, NoDefaults),
        "live" => new DemoStagePlan(Live, page, IsRunning: true, AutomaticAnswers: true,
            ClearsAnswerState: false, PopulatesDraft: false, Onboarding: false, NoDefaults),
        // Mac's default section (no NEXTTHOUGHT_DEMO_SECTION) is "assistant": a running meeting.
        _ => new DemoStagePlan(Assistant, page, IsRunning: true, AutomaticAnswers: true,
            ClearsAnswerState: false, PopulatesDraft: false, Onboarding: false, NoDefaults)
    };

    /// <summary>
    /// The Mac demo script also opens a specific tab or page through the defaults table
    /// (<c>NEXTTHOUGHT_DEMO_DEFAULTS</c>); the work-notes capture uses <c>demoNotes=work</c>.
    /// </summary>
    public static DemoStagePlan WithDefaults(this DemoStagePlan plan, IReadOnlyDictionary<string, string> defaults) =>
        plan with { Defaults = defaults };

    /// <summary>The capture variant <c>NEXTTHOUGHT_DEMO_DEFAULTS=demoNotes=work</c> (Mac notes-work).</summary>
    public static DemoStagePlan MeetingsWithWorkNotes(IReadOnlyDictionary<string, string>? defaults = null)
    {
        var table = new Dictionary<string, string>(defaults ?? NoDefaults, StringComparer.Ordinal) { ["demoNotes"] = "work" };
        return For(Meetings) with { Defaults = table };
    }
}

/// <summary>Island phases of DemoMode.snapshotIsland (NEXTTHOUGHT_DEMO_ISLAND_PHASE).</summary>
public enum DemoIslandPhase
{
    Cue,
    Open,
    Compact,
    Hearing,
    Deck,
    Launcher,
    Bilingual,
    Pinned,
    Slide
}

/// <summary>One scripted state of the cue island, with the Mac copy it shows.</summary>
public sealed record DemoIslandState(
    DemoIslandPhase Phase,
    string Name,
    bool AlwaysOpen,
    bool Pinned,
    bool Presenting,
    bool Hovering,
    string Question,
    string Answer,
    string Translation,
    string? SlideDocument,
    int SlidePage,
    int SlidePageCount,
    string SlideText,
    IReadOnlyList<string> SlidePoints,
    string SlideTransition,
    string? ExtraTranscriptLine,
    string? ExtraTranscriptTranslation);

/// <summary>The island states of DemoMode.snapshotIsland, verbatim.</summary>
public static class DemoIslands
{
    public static IReadOnlyList<DemoIslandState> All { get; } =
    [
        new(DemoIslandPhase.Cue, "cue", false, false, false, false,
            DemoScript.LiveQuestion, DemoScript.LiveAnswer, "", null, 0, 0, "", [], "", null, null),
        new(DemoIslandPhase.Open, "open", true, false, false, false,
            DemoScript.LiveQuestion, DemoScript.LiveAnswer, "", null, 0, 0, "", [], "", null, null),
        new(DemoIslandPhase.Compact, "compact", false, false, false, false,
            "", "", "", null, 0, 0, "", [], "", null, null),
        new(DemoIslandPhase.Hearing, "hearing", false, false, false, false,
            "", "", "", null, 0, 0, "", [], "", DemoScript.LivePartialTranscript, null),
        new(DemoIslandPhase.Deck, "deck", false, true, false, false,
            DemoScript.LiveQuestion, DemoScript.LiveAnswer, "", null, 0, 0, "", [], "", null, null),
        new(DemoIslandPhase.Launcher, "launcher", false, false, false, true,
            "", "", "", null, 0, 0, "", [], "", null, null),
        new(DemoIslandPhase.Bilingual, "bilingual", true, false, false, false,
            DemoScript.IslandBilingualQuestion, DemoScript.IslandBilingualAnswer,
            DemoScript.IslandBilingualTranslation, null, 0, 0, "", [], "", null, null),
        new(DemoIslandPhase.Pinned, "pinned", true, false, false, false,
            DemoScript.IslandBilingualQuestion, DemoScript.IslandBilingualAnswer,
            DemoScript.IslandBilingualTranslation, null, 0, 0, "", [], "",
            "I am not fully convinced by the term physics informed. Many model-based methods already use the array response.",
            "我不太认同“物理信息”这个说法。很多基于模型的方法已经用了阵列响应。"),
        new(DemoIslandPhase.Slide, "slide", false, true, true, false,
            DemoScript.LiveQuestion, DemoScript.LiveAnswer, "",
            "后端二面 · 项目介绍.pptx", 4, 12, "缓存一致性：先更新数据库，再删除缓存",
            [
                "先说规模：日均 2 亿次读取，P99 从 120 ms 降到 18 ms",
                "一致性：先更新数据库再删缓存，binlog 订阅兜底",
                "删除失败进重试队列，热点 key 延迟双删",
                "我负责方案设计、灰度上线和回滚预案"
            ],
            "讲完数字，接着讲上线时踩过的坑", null, null)
    ];

    /// <summary>Resolves a phase name; unknown names fall back to cue (Mac default).</summary>
    public static DemoIslandState For(string? name) => All.FirstOrDefault(state =>
        string.Equals(state.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) ?? All[0];

    /// <summary>Phases a screenshot run records: Mac lists cue/open/compact/hearing/deck/launcher/bilingual/pinned/slide.</summary>
    public static IReadOnlyList<string> Names { get; } = All.Select(state => state.Name).ToList();
}

/// <summary>One frame-timeline step of DemoFilm.swift.</summary>
public enum DemoFilmAction
{
    Candidate,
    Question,
    AnswerDraft,
    Answer,
    HoverOn,
    HoverOff,
    Collapse,
    Pin,
    Unpin,
    Scene
}

public sealed record DemoFilmStep(double At, DemoFilmAction Action, string Text = "", int Number = 0);

/// <summary>One scripted demo film: frame rate, length and every state change on the timeline.</summary>
public sealed record DemoFilm(string Name, double Fps, double Seconds, IReadOnlyList<DemoFilmStep> Steps);

/// <summary>The two scripted demo scenes of DemoFilm.swift (README animation source).</summary>
public static class DemoFilms
{
    public static DemoFilm Island()
    {
        var steps = new List<DemoFilmStep>
        {
            new(1.0, DemoFilmAction.Candidate, DemoScript.LivePartialTranscript),
            new(2.6, DemoFilmAction.Question, DemoScript.LiveQuestion),
            new(5.2, DemoFilmAction.Answer, DemoScript.LiveAnswer),
            new(6.6, DemoFilmAction.HoverOn),
            new(8.4, DemoFilmAction.HoverOff),
            new(9.2, DemoFilmAction.Collapse),
            new(10.0, DemoFilmAction.Pin),
            new(11.8, DemoFilmAction.Unpin)
        };
        // The answer arrives a few characters at a time, as it does from a model: 30 steps of
        // characters.prefix(count * step / steps) between 2.7 s and 4.95 s (DemoFilm.swift).
        const int draftSteps = 30;
        var characters = DemoScript.LiveAnswer;
        for (var step = 1; step <= draftSteps; step++)
        {
            var count = characters.Length * step / draftSteps;
            steps.Add(new DemoFilmStep(2.7 + step * 0.075, DemoFilmAction.AnswerDraft,
                characters[..count], step));
        }
        steps.Sort((left, right) => left.At.CompareTo(right.At));
        return new DemoFilm("island", 20, 12.6, steps);
    }

    public static DemoFilm Home()
    {
        // DemoFilm.swift home: the home page taking on one scene after another.
        var scenes = new[]
        {
            MeetingMode.LabMeeting, MeetingMode.Academic, MeetingMode.GroupProject,
            MeetingMode.Client, MeetingMode.Lesson, MeetingMode.Interview
        };
        var steps = scenes.Select((scene, index) =>
            new DemoFilmStep(1.0 + index * 1.05, DemoFilmAction.Scene, SceneKey(scene), index));
        return new DemoFilm("home", 15, 7.6, steps.ToList());
    }

    /// <summary>The Mac scene raw value (LabMeeting → "labMeeting").</summary>
    private static string SceneKey(MeetingMode mode)
    {
        var name = mode.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static DemoFilm For(string? name) => string.Equals(name?.Trim(), "home", StringComparison.OrdinalIgnoreCase)
        ? Home()
        : Island();
}
