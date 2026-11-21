using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// J8/D1 gap closure: the Mac 3.4.0 demo-only entries that were still missing —
/// NEXTTHOUGHT_DEMO_VOICES (in-room multi-voice demo meeting), NEXTTHOUGHT_DEMO_CHURN
/// (live-page render counters), and the `live-notes` / `backup-fixtures` self-test entry
/// points from SelfTest.swift / BackupFixtureSelfTest.swift, plus the `--demo --self-test=demo`
/// regression. Written first, then the implementations follow.
/// </summary>
public sealed class GapClosureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    // ---- ① DemoMode NEXTTHOUGHT_DEMO_VOICES=1 -------------------------------------------------

    [Fact]
    public void TheDemoCanRunTheWeeklyMeetingInARoomWithThreeVoices()
    {
        var now = new DateTimeOffset(2026, 11, 21, 14, 0, 0, TimeSpan.FromHours(8));
        var dataset = DemoScript.Create(now, voices: true);
        Assert.Equal(12, dataset.Weekly.Lines.Count);
        Assert.All(dataset.Weekly.Lines, line => Assert.Equal("room", line.Speaker));
        Assert.Equal(new int?[] { 1, 1, 2, 1, 2, 1, 3, 2, 1, 2, 1, 2 }, dataset.Weekly.Lines.Select(line => line.Voice).ToArray());
        Assert.True(dataset.Weekly.Meta.InPerson);
        Assert.Equal("线下 · 麦克风", dataset.Weekly.Meta.AudioSource);
        Assert.Equal("老师", dataset.Weekly.Meta.VoiceNames!["1"]);
        // The normal demo meeting is untouched.
        var normal = DemoScript.Create(now);
        Assert.Equal(5, normal.Weekly.Lines.Count);
        Assert.False(normal.Weekly.Meta.InPerson);
        Assert.Null(normal.Weekly.Meta.VoiceNames);
    }

    [Fact]
    public void TheDemoVoicesSwitchIsADemoOnlyStartupFlag()
    {
        var startup = Read("StartupOptions.cs");
        Assert.Contains("DemoVoices", startup);
        Assert.Contains("--demo-voices", startup);
        Assert.Contains("DemoChurn", startup);
        Assert.Contains("--demo-churn=", startup);
        var content = Read("DemoContent.cs");
        Assert.Contains("DemoScript.Create(DateTimeOffset.Now, StartupOptions.DemoVoices)", content);
        var demo = Read("AppState.Demo.cs");
        Assert.Contains("StartupOptions.DemoVoices", demo);
    }

    // ---- ③/⑦ live render counters and the churn entry ----------------------------------------

    [Fact]
    public void TheLivePageCountsHowOftenItsHeavyPartsAreRebuilt()
    {
        var counters = Read("Views", "Live", "LiveRenderCount.cs");
        foreach (var counter in new[] { "TopBar", "MoreMenu", "Transcript", "RightColumn", "Partial", "Line", "Page" })
            Assert.Contains(counter, counters);
        Assert.Contains("Reset", counters);
        var live = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("LiveRenderCount", live);
        var churn = Read("SelfTest", "Checks", "RunLiveChurnCheckAsync.cs");
        Assert.Contains("LiveRenderCount", churn);
        Assert.Contains("churn", churn);
    }

    [Fact]
    public void TheDemoChurnEntryWritesTheMacStyleMeasurementFile()
    {
        var main = Read("Views", "MainWindow.axaml.cs");
        Assert.Contains("StartupOptions.DemoChurn", main);
        Assert.Contains("RunDemoChurnAsync", main);
    }

    // ---- ④ live-notes / backup-fixtures self-test entries ------------------------------------

    [Fact]
    public void TheSelfTestRegistryHasTheRemainingMacEntries()
    {
        Assert.True(File.Exists(Path.Combine(Desktop("SelfTest", "Checks"), "RunLiveNotesCheckAsync.cs")));
        Assert.True(File.Exists(Path.Combine(Desktop("SelfTest", "Checks"), "RunBackupFixturesCheckAsync.cs")));
        Assert.True(File.Exists(Path.Combine(Desktop("SelfTest", "Checks"), "RunLiveChurnCheckAsync.cs")));
        var liveNotes = File.ReadAllText(Path.Combine(Desktop("SelfTest", "Checks"), "RunLiveNotesCheckAsync.cs"));
        Assert.Contains("LiveNotesPrompt.Parse", liveNotes);
        Assert.Contains("LiveNotesSchedule.IsDue", liveNotes);
        Assert.Contains("InstallDemoLiveNotes", liveNotes + File.ReadAllText(Path.Combine(Desktop("AppState.LiveNotes.cs"))));
        Assert.Contains("DemoStages.LiveNotes", liveNotes);
        var fixtures = File.ReadAllText(Path.Combine(Desktop("SelfTest", "Checks"), "RunBackupFixturesCheckAsync.cs"));
        Assert.Contains("BackupService", fixtures);
        Assert.Contains("CreateAsync", fixtures);
        Assert.Contains("ImportAsync", fixtures);
    }

    // ---- demo self-check regression ----------------------------------------------------------

    [Fact]
    public void TheDemoSelfCheckLoadsTheDemoBeforeAsserting()
    {
        var check = Read("SelfTest", "Checks", "RunDemoCheckAsync.cs");
        Assert.Contains("LoadDemo()", check);
    }
}
