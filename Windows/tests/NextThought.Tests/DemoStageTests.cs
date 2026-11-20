using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E31: the demo mode's staged entries. The expectations come from DemoMode.swift (apply(),
/// populateDemoDraft(), snapshotIsland()) and DemoFilm.swift; they are checked here so the
/// Windows demo stages can be re-generated without launching the shell.
/// </summary>
public sealed class DemoStageTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "AppState.Demo.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    // ---- sections / stages (NEXTTHOUGHT_DEMO_SECTION) ---------------------------------

    [Fact]
    public void DemoStageNamesMatchTheMacSections()
    {
        Assert.Equal(new[] { "home", "prepare", "live", "live-notes", "meetings", "library", "settings" },
            DemoStages.Names.ToArray());
    }

    [Fact]
    public void DemoStagesCarryTheMacApplyDifferences()
    {
        // Mac default section: "assistant" runs a meeting and lives on the home screen.
        var assistant = DemoStages.For("assistant");
        Assert.Equal(DemoStages.Assistant, assistant.Name);
        Assert.Equal(DemoPage.Home, assistant.Page);
        Assert.True(assistant.IsRunning);

        // Live is the running meeting; prepare is the idle preparation page.
        var live = DemoStages.For("live");
        Assert.Equal(DemoStages.Live, live.Name);
        Assert.True(live.IsRunning);
        Assert.False(live.PopulatesDraft);
        var prepare = DemoStages.For("prepare");
        Assert.Equal(DemoStages.Prepare, prepare.Name);
        Assert.False(prepare.IsRunning);
        Assert.True(prepare.PopulatesDraft);

        // live-notes: Mac keeps the meeting running but clears the visible answer and auto answers.
        var notes = DemoStages.For("live-notes");
        Assert.Equal(DemoStages.LiveNotes, notes.Name);
        Assert.True(notes.IsRunning);
        Assert.False(notes.AutomaticAnswers);
        Assert.True(notes.ClearsAnswerState);

        // Archived pages are idle; settings/library/meetings differ only in their page.
        var meetings = DemoStages.For("meetings");
        Assert.Equal(DemoPage.Meetings, meetings.Page);
        Assert.False(meetings.IsRunning);
        var library = DemoStages.For("library");
        Assert.Equal(DemoPage.Library, library.Page);
        Assert.False(library.IsRunning);
        var settings = DemoStages.For("settings");
        Assert.Equal(DemoPage.Settings, settings.Page);
        Assert.False(settings.IsRunning);
        var home = DemoStages.For("home");
        Assert.Equal(DemoPage.Home, home.Page);
        Assert.False(home.IsRunning);
    }

    [Fact]
    public void OldDemoScriptNamesMapLikeAppSectionDemoName()
    {
        // Models.swift AppSection(demoName:): assistant/live/prepare are home, materials is
        // library, history is meetings, and an unknown section falls back to home.
        Assert.Equal(DemoPage.Home, DemoStages.For("assistant").Page);
        Assert.Equal(DemoPage.Home, DemoStages.For("live").Page);
        Assert.Equal(DemoPage.Home, DemoStages.For("prepare").Page);
        Assert.Equal(DemoPage.Library, DemoStages.For("materials").Page);
        Assert.Equal(DemoPage.Meetings, DemoStages.For("history").Page);
        Assert.Equal(DemoStages.Home, DemoStages.For("bogus").Name);
        Assert.Equal(DemoPage.Home, DemoStages.For("bogus").Page);
        Assert.False(DemoStages.For("bogus").IsRunning);
    }

    [Fact]
    public void WorkNotesCaptureUsesTheDemoNotesDefault()
    {
        // Mac script: capture notes-work meetings "$appearance" zh-Hans "demoNotes=work".
        var stage = DemoStages.MeetingsWithWorkNotes();
        Assert.Equal(DemoPage.Meetings, stage.Page);
        Assert.Equal("work", stage.Defaults["demoNotes"]);
        Assert.Equal("meetings", stage.Name);
    }

    // ---- island states (NEXTTHOUGHT_DEMO_ISLAND_PHASE) -------------------------------

    [Fact]
    public void IslandPhasesMatchSnapshotIsland()
    {
        Assert.Equal(new[] { "cue", "open", "compact", "hearing", "deck", "launcher", "bilingual", "pinned", "slide" },
            DemoIslands.Names.ToArray());

        var cue = DemoIslands.For("cue");
        Assert.Equal(DemoScript.LiveQuestion, cue.Question);
        Assert.Equal(DemoScript.LiveAnswer, cue.Answer);
        Assert.False(cue.AlwaysOpen);
        Assert.False(cue.Pinned);

        Assert.True(DemoIslands.For("open").AlwaysOpen);
        Assert.Equal("", DemoIslands.For("compact").Answer);
        Assert.Equal(DemoScript.LivePartialTranscript, DemoIslands.For("hearing").ExtraTranscriptLine);
        Assert.True(DemoIslands.For("deck").Pinned);
        Assert.True(DemoIslands.For("launcher").Hovering);

        var bilingual = DemoIslands.For("bilingual");
        Assert.Equal(DemoScript.IslandBilingualQuestion, bilingual.Question);
        Assert.Equal(DemoScript.IslandBilingualAnswer, bilingual.Answer);
        Assert.Equal(DemoScript.IslandBilingualTranslation, bilingual.Translation);
        Assert.True(bilingual.AlwaysOpen);

        // "pinned": the same answer pinned open while the examiner starts a new turn.
        var pinned = DemoIslands.For("pinned");
        Assert.True(pinned.AlwaysOpen);
        Assert.Equal("I am not fully convinced by the term physics informed. Many model-based methods already use the array response.",
            pinned.ExtraTranscriptLine);
        Assert.Equal("我不太认同“物理信息”这个说法。很多基于模型的方法已经用了阵列响应。", pinned.ExtraTranscriptTranslation);

        // "slide": presenting, following a deck on the other display.
        var slide = DemoIslands.For("slide");
        Assert.True(slide.Presenting);
        Assert.True(slide.Pinned);
        Assert.Equal("后端二面 · 项目介绍.pptx", slide.SlideDocument);
        Assert.Equal(4, slide.SlidePage);
        Assert.Equal(12, slide.SlidePageCount);
        Assert.Equal("缓存一致性：先更新数据库，再删除缓存", slide.SlideText);
        Assert.Equal(4, slide.SlidePoints.Count);
        Assert.Equal("先说规模：日均 2 亿次读取，P99 从 120 ms 降到 18 ms", slide.SlidePoints[0]);
        Assert.Equal("我负责方案设计、灰度上线和回滚预案", slide.SlidePoints[3]);
        Assert.Equal("讲完数字，接着讲上线时踩过的坑", slide.SlideTransition);
    }

    [Fact]
    public void UnknownIslandPhaseFallsBackToCue()
    {
        Assert.Equal(DemoIslandPhase.Cue, DemoIslands.For("nonsense").Phase);
        Assert.Equal(DemoIslandPhase.Cue, DemoIslands.For(null).Phase);
    }

    // ---- demo films (DemoFilm.swift) --------------------------------------------------

    [Fact]
    public void IslandFilmTimelineMatchesDemoFilmSwift()
    {
        var film = DemoFilms.Island();
        Assert.Equal("island", film.Name);
        Assert.Equal(20d, film.Fps);
        Assert.Equal(12.6, film.Seconds);

        DemoFilmStep At(double second, DemoFilmAction action) =>
            Assert.Single(film.Steps.Where(step => step.Action == action && Math.Abs(step.At - second) < 1e-9));
        Assert.Equal(DemoScript.LivePartialTranscript, At(1.0, DemoFilmAction.Candidate).Text);
        Assert.Equal(DemoScript.LiveQuestion, At(2.6, DemoFilmAction.Question).Text);
        Assert.Equal(DemoScript.LiveAnswer, At(5.2, DemoFilmAction.Answer).Text);
        Assert.NotNull(At(6.6, DemoFilmAction.HoverOn));
        Assert.NotNull(At(8.4, DemoFilmAction.HoverOff));
        Assert.NotNull(At(9.2, DemoFilmAction.Collapse));
        Assert.NotNull(At(10.0, DemoFilmAction.Pin));
        Assert.NotNull(At(11.8, DemoFilmAction.Unpin));

        // The answer arrives a few characters at a time: 30 steps from 2.7 s, growing prefixes.
        var drafts = film.Steps.Where(step => step.Action == DemoFilmAction.AnswerDraft).ToList();
        Assert.Equal(30, drafts.Count);
        Assert.Equal(2.7 + 0.075, drafts[0].At, 9);
        Assert.Equal(2.7 + 30 * 0.075, drafts[^1].At, 9);
        Assert.Equal(DemoScript.LiveAnswer, drafts[^1].Text);
        Assert.All(drafts, draft => Assert.StartsWith(draft.Text, DemoScript.LiveAnswer));
        for (var index = 1; index < drafts.Count; index++)
            Assert.True(drafts[index].Text.Length >= drafts[index - 1].Text.Length);
        Assert.Equal(film.Steps.OrderBy(step => step.At).Select(step => step.At), film.Steps.Select(step => step.At));
    }

    [Fact]
    public void HomeFilmTimelineMatchesDemoFilmSwift()
    {
        var film = DemoFilms.Home();
        Assert.Equal("home", film.Name);
        Assert.Equal(15d, film.Fps);
        Assert.Equal(7.6, film.Seconds);
        Assert.Equal(6, film.Steps.Count);
        Assert.Equal(new[] { "labMeeting", "academic", "groupProject", "client", "lesson", "interview" },
            film.Steps.Select(step => step.Text).ToArray());
        for (var index = 0; index < film.Steps.Count; index++)
            Assert.Equal(1.0 + index * 1.05, film.Steps[index].At, 9);
        Assert.All(film.Steps, step => Assert.Equal(DemoFilmAction.Scene, step.Action));
    }

    // ---- data counts stay the Mac demo's ---------------------------------------------

    [Fact]
    public void DemoDataCountsMatchTheMacDemo()
    {
        var demo = DemoScript.Create(new DateTimeOffset(2026, 11, 20, 15, 32, 0, TimeSpan.FromHours(8)));
        // Three workspaces: interview (1 meeting, 4 materials), academic (0 meetings, 2 materials),
        // business (1 meeting, 0 materials); two archived meetings in the archive.
        Assert.Equal(3, demo.Series.Count);
        Assert.Equal(1, demo.InterviewSeries.SessionIds.Count);
        Assert.Equal(1, demo.BusinessSeries.SessionIds.Count);
        Assert.Empty(demo.AcademicSeries.SessionIds);
        Assert.Equal(4, demo.InterviewSeries.Documents.Count);
        Assert.Equal(2, demo.AcademicSeries.Documents.Count);
        // Review session: 6 transcript lines, 2 answers, 6 questions, ready summary.
        Assert.Equal(6, demo.Review.Lines.Count);
        Assert.Equal(2, demo.Review.Answers.Count);
        Assert.Equal(6, demo.Review.Meta.QuestionCount);
        Assert.Equal("ready", demo.Review.Meta.SummaryState);
        // Weekly meeting: Mac writes transcriptCount 4 while the demo detail carries 5 lines.
        Assert.Equal(4, demo.Weekly.Meta.TranscriptCount);
        Assert.Equal(5, demo.Weekly.Lines.Count);
        // Live page: 6 entries (one of them the echo line), answer history of 2.
        Assert.Equal(6, demo.Live.Lines.Count);
        Assert.Single(demo.Live.Lines, line => line.SuspectedEcho == true);
        Assert.Equal(2, demo.Live.AnswerHistory.Count);
        Assert.Equal(3, demo.Skills.Count);
        Assert.Equal(6, demo.Materials.Count);
    }

    // ---- the shell uses the staged model ----------------------------------------------

    [Fact]
    public void DemoShellAppliesMacStagesAndFilmsThroughTheSharedModel()
    {
        var root = Root();
        var state = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "AppState.Demo.cs"));
        Assert.Contains("DemoStages.For", state);
        Assert.Contains("ApplyDemoStage", state);
        Assert.Contains("DemoStages.MeetingsWithWorkNotes", state);
        Assert.Contains("demoNotes", state);
        Assert.DoesNotContain("StartDemoFeeder", state);

        var content = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "DemoContent.cs"));
        Assert.Contains("DemoStages.Names", content);
        Assert.Contains("DemoFilms.For", content);
        Assert.Contains("DemoIslands.For", content);

        var island = File.ReadAllText(Path.Combine(root, "Windows", "src", "NextThought.Desktop", "Views", "CueIslandWindow.axaml.cs"));
        Assert.Contains("DemoIslands.For", island);
        // The island phase content comes from the shared demo data, not from local literals.
        Assert.DoesNotContain("温度补偿之后残差还剩多少", island);
    }
}
