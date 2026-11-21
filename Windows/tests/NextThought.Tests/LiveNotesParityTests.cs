using Xunit;

namespace NextThought.Tests;

/// <summary>
/// J1: the live-notes column is wired to Core LiveNotes / LiveNotesPrompt / LiveNotesSchedule
/// (LiveNotesEngine.swift): about every 45 s one request rewrites the notes, the first three lines
/// bring the first notes, settled topics are never sent back, an unreadable reply keeps the notes on
/// show, and what the last update added carries the scene's colour. The demo notes are the Mac
/// demo's own (DemoMode.swift installDemo), which is what live-notes-*.png shows.
/// </summary>
public sealed class LiveNotesParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void TheEngineUsesTheCoreSchedulePromptAndParser()
    {
        var engine = Read("AppState.LiveNotes.cs");
        Assert.Contains("LiveNotesSchedule.IsDue", engine);
        Assert.Contains("LiveNotesPrompt.User", engine);
        Assert.Contains("LiveNotesPrompt.Parse", engine);
        Assert.Contains("LiveNotesPrompt.System", engine);
        Assert.Contains("LiveNotesStatus.Updating", engine);
        Assert.Contains("LiveNotesStatus.Failed", engine);
        Assert.Contains("LiveNotesStatus.NoEngine", engine);
        Assert.Contains("liveNotesInFlight", engine);
        Assert.Contains("next.Statements", engine);
        // An unreadable reply keeps the notes on show (Mac: no assignment, status = .failed).
        Assert.Contains("if (next is null)", engine);
        // 45 s / first 3 lines / 20 s / 2 editable topics / 12 topics are the Core constants.
        var core = File.ReadAllText(Path.Combine(Root(), "Windows", "src", "NextThought.Core", "LiveNotes.cs"));
        Assert.Contains("public const double Interval = 45;", core);
        Assert.Contains("public const int FirstLines = 3;", core);
        Assert.Contains("public const double FirstWait = 20;", core);
        Assert.Contains("public const int EditableTopics = 2;", core);
        Assert.Contains("public const int MaxTopics = 12;", core);
        Assert.Contains("notes.Covered = lines;", core);
    }

    [Fact]
    public void TheColumnShowsSummaryTopicsDecisionsAndTodos()
    {
        var live = Read("Views", "LiveView.axaml");
        Assert.Contains("{Binding LiveNotesSummaryText}", live);
        Assert.Contains("{Binding LiveNotesTopicRows}", live);
        Assert.Contains("{Binding LiveNotesDecisionRows}", live);
        Assert.Contains("{Binding LiveNotesTodoRows}", live);
        Assert.Contains("{loc:Tr 定下来的}", live);
        Assert.Contains("{loc:Tr 要做的}", live);
        Assert.Contains("{Binding Fresh}", live);
        Assert.Contains("{Binding LiveNotesIsUpdating}", live);
        Assert.Contains("{Binding LiveNotesStatusIsWarning}", live);
        Assert.Contains("{Binding LiveNotesHintText}", live);
        Assert.Contains("{Binding LiveNotesCanUpdate}", live);
        Assert.Contains("LiveSceneTintBrush", live);
        var engine = Read("AppState.LiveNotes.cs");
        Assert.Contains("public IReadOnlyList<LiveNotesTopicRow> LiveNotesTopicRows", engine);
        Assert.Contains("public IReadOnlyList<LiveNotesDecisionRow> LiveNotesDecisionRows", engine);
        Assert.Contains("public IReadOnlyList<LiveNotesTodoRow> LiveNotesTodoRows", engine);
        Assert.Contains("public string LiveNotesStatusText", engine);
        Assert.Contains("这次没更新成功，稍后会再试", engine);
        Assert.Contains("还没有连接回答服务，写不了实时纪要", engine);
        Assert.Contains("更新于 %@ · 约每 45 秒一次", engine);
    }

    [Fact]
    public void TheDemoShowsTheMacDemoNotes()
    {
        var engine = Read("AppState.LiveNotes.cs");
        Assert.Contains("面试进行到系统设计：先聊了订单缓存层的项目，现在在追问数据库和缓存的一致性。", engine);
        Assert.Contains("项目经历", engine);
        Assert.Contains("缓存一致性", engine);
        Assert.Contains("下一步讲热点 key 的处理", engine);
        Assert.Contains("会后补一张读写时序图", engine);
        Assert.Contains("面试官追问：删除缓存失败怎么办", engine);
        var demo = File.ReadAllText(Path.Combine(Root(), "Sources", "NextThought", "DemoMode.swift"));
        Assert.Contains("面试进行到系统设计：先聊了订单缓存层的项目，现在在追问数据库和缓存的一致性。", demo);
        Assert.Contains("会后补一张读写时序图", demo);
        var check = Read("SelfTest", "Checks", "RunLiveUiCheckAsync.cs");
        Assert.Contains("InstallDemoLiveNotes", check);
        Assert.Contains("LiveNotesHasContent", check);
        Assert.Contains("live-notes-light.png", check);
    }

    [Fact]
    public void TheScheduleMatchesTheMacConstants()
    {
        // Mirrors LiveNotesSchedule.isDue (LiveNotes.swift).
        var empty = new NextThought.Core.LiveNotes();
        Assert.False(NextThought.Core.LiveNotesSchedule.IsDue(empty, 2, 0));
        Assert.True(NextThought.Core.LiveNotesSchedule.IsDue(empty, 3, 0));
        Assert.True(NextThought.Core.LiveNotesSchedule.IsDue(empty, 1, 21));
        var notes = new NextThought.Core.LiveNotes
        {
            Summary = "在谈订单缓存层",
            Topics = [new NextThought.Core.LiveNotes.Topic("项目经历", ["缓存层"])],
            Covered = 4
        };
        Assert.False(NextThought.Core.LiveNotesSchedule.IsDue(notes, 4, 999));
        Assert.False(NextThought.Core.LiveNotesSchedule.IsDue(notes, 5, 44.9));
        Assert.True(NextThought.Core.LiveNotesSchedule.IsDue(notes, 5, 45));
    }

    [Fact]
    public void TheLiveNotesScreenshotsExist()
    {
        var directory = Path.Combine(Root(), "Windows", "validation", "screens", "live");
        Assert.True(File.Exists(Path.Combine(directory, "live-notes-light.png")), "live-notes-light.png");
        Assert.True(File.Exists(Path.Combine(directory, "live-notes-dark.png")), "live-notes-dark.png");
    }
}
