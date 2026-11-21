using System.IO.Compression;
using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// Stage 4 §6.4/J3/J4: the home page must carry the 3.4.0 statistics section, the continue-series
/// row stats, mac wording (系列, not 系列) and the scene-chip/placeholder presentation. These
/// tests are written first and fail until the page is reworked.
/// </summary>
public sealed class HomeStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "HomeView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    // ---- J3: statistics -----------------------------------------------------------------

    [Fact]
    public void TheHomePageShowsTheUseStatisticsSection()
    {
        var home = Read("Views", "HomeView.axaml");
        Assert.Contains("<home:HomeStats", home);
        var stats = Read("Views", "Home", "HomeStats.axaml");
        Assert.Contains("{loc:Tr 用了多少}", stats);
        Assert.Contains("{loc:Tr 本月}", stats);
        Assert.Contains("{loc:Tr 累计}", stats);
        Assert.Contains("{loc:Tr 场会议}", stats);
        Assert.Contains("{loc:Tr 记录时长}", stats);
        Assert.Contains("{loc:Tr 听到的提问}", stats);
        Assert.Contains("{loc:Tr 给出的回答}", stats);
        Assert.Contains("{Binding HomeStatsMeetings}", stats);
        Assert.Contains("{Binding HomeStatsDuration}", stats);
        Assert.Contains("{Binding HomeStatsQuestions}", stats);
        Assert.Contains("{Binding HomeStatsAnswers}", stats);
        Assert.Contains("{Binding HomeStatsIsMonth", stats);
        Assert.Contains("{Binding HomeStatsIsAll", stats);
        Assert.Contains("时长按每场从开始记录到结束计算；暂停的时间也算在内。", stats);
        var state = Read("AppState.Home.cs");
        Assert.Contains("HomeStatsAllTime", state);
        Assert.Contains("MeetingStats.Of", state);
    }

    [Fact]
    public async Task ImportedAllScenariosStatsMatchTheHandCalculatedMacNumber()
    {
        // Hand calculation from the four all-scenarios meetings (fixture meta.json):
        // 周三组会 45 s + 分工会 0 s + 识别模型例会 45 s + final translation 0 s = 90 s,
        // questions 1 + 0 + 1 + 0 = 2, answers 0 + 0 + 0 + 0 = 0, all in November 2026.
        var work = Path.Combine(Path.GetTempPath(), "nt-j3-stats-" + Guid.NewGuid().ToString("N"));
        try
        {
            var fixture = Path.Combine(Root(), "Windows", "tests", "Fixtures", "mac-backup", "nt-mac-fixtures-26.5.2-20261121.zip");
            var extract = Path.Combine(work, "extract");
            Directory.CreateDirectory(extract);
            ZipFile.ExtractToDirectory(fixture, extract);
            var archive = Path.Combine(extract, "nt-fixtures", "ntbackup", "all-scenarios.ntbackup");
            var paths = new AppPaths(Path.Combine(work, "data"));
            await new BackupService(paths).ImportAsync(archive);
            var metas = new SessionStore(paths).List();
            Assert.Equal(4, metas.Count);

            var month = MeetingStats.Month(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            var stats = MeetingStats.Of(metas, month);
            Assert.Equal(4, stats.Meetings);
            Assert.Equal(90, stats.Seconds);
            Assert.Equal(2, stats.Questions);
            Assert.Equal(0, stats.Answers);
            Assert.Equal("1 分钟", stats.DurationText);
        }
        finally { try { Directory.Delete(work, true); } catch { } }
    }

    // ---- J4: series wording and next meeting -------------------------------------------

    [Fact]
    public void TheHomeSeriesSectionUsesTheMacWordingForSeries()
    {
        var continueSeries = Read("Views", "Home", "ContinueSeries.axaml");
        Assert.Contains("继续一个系列", continueSeries);
        Assert.Contains("全部系列", continueSeries);
        Assert.DoesNotContain("资料夹", continueSeries);
        var rows = Read("AppState.Home.cs");
        Assert.Contains("MeetingStats.Of", rows);
        Assert.Contains("还没开过", rows);
        Assert.DoesNotContain("资料夹", rows);
    }

    [Fact]
    public void NoInterfaceTextUsesTheOldSeriesWord()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Desktop("Views"), "*.axaml", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(Desktop(), "*.cs", SearchOption.TopDirectoryOnly)))
        {
            if (Path.GetFileName(file) is "UiTextMac.g.cs" or "UiTextWindows.cs") continue;
            if (File.ReadAllText(file).Contains("资料夹", StringComparison.Ordinal)) offenders.Add(Path.GetFileName(file));
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void TheSeriesPageHasAStartNextMeetingAction()
    {
        var series = Read("Views", "SeriesView.axaml");
        Assert.Contains("开下一场", series);
        Assert.Contains("OpenNextMeetingInSeriesCommand", series);
        var state = Read("AppState.Series.cs");
        Assert.Contains("OpenNextMeetingInSeries", state);
        Assert.Contains("PrepareNextMeetingInSeries", state);
    }

    // ---- §6.4: presentation -------------------------------------------------------------

    [Fact]
    public void OnlyTheSelectedSceneChipCarriesABackground()
    {
        var styles = Read("Styles", "Controls.axaml");
        Assert.Contains("Button.scene", styles);
        var sceneStyle = Section(styles, "Button.scene\"");
        Assert.Contains("Background\" Value=\"Transparent\"", sceneStyle);
        var composer = Read("Views", "Home", "StartComposer.axaml");
        // The placeholder must be larger/darker than the default watermark.
        Assert.Contains("PART_Watermark", composer);
        Assert.Contains("FontSize\" Value=\"19\"", composer);
        // The three moment dots take the scene pigment (set from the code-behind).
        Assert.Contains("MomentsList", composer);
        var code = Read("Views", "Home", "StartComposer.axaml.cs");
        Assert.Contains("SceneTheme.For(state.SelectedMode).Tint", code);
        Assert.Contains("Ellipse", code);
        // The prepare action is the outlined secondary button, start is primary with the red dot.
        Assert.Contains("Classes=\"secondary\"", composer);
        Assert.Contains("IconArrowRight", Read("Views", "Home", "ContinueSeries.axaml"));
    }

    private static string Section(string text, string marker)
    {
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        var end = text.IndexOf("</Style>", start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..(end + 8)];
    }
}
