using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §6.7 E1: HistoryView (所有记录) is rebuilt against Sources/NextThought/HistoryView.swift.
/// Mac has no screenshot for this window, so the contract is the Swift source: a 270pt session
/// list beside the archive overview, the filter bar, series groups, and SessionRow's dot/time/
/// details/summary-icon layout. These are source assertions; the light/dark evidence lives in
/// validation/screens/history/.
/// </summary>
public sealed class HistoryViewParityTests
{
    private static string Windows()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return Path.Combine(directory.FullName, "Windows");
        }
        throw new DirectoryNotFoundException("repository root");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Windows(), "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void HistoryHasTheMacTwoPaneLayout()
    {
        var view = Read("Views", "HistoryView.axaml");
        // HistoryView.body: sessionList 270pt wide, hairline, archive overview on the right.
        Assert.Contains("270", view);
        Assert.Contains("HistoryGroups", view);
        Assert.Contains("本机记录", view);
        // sessionListTitle: 所有记录 in the left pane and the breadcrumb on the right.
        Assert.Contains("所有记录", view);
        Assert.Contains("会议系列", view);
        Assert.Contains("批量删除", view);
        Assert.Contains("全选当前结果", view);
        Assert.Contains("删除所选", view);
        Assert.Contains("完成", view);
        Assert.Contains("没有符合条件的记录", view);
    }

    [Fact]
    public void HistoryFilterBarMatchesTheSwiftFilterBar()
    {
        var view = Read("Views", "HistoryView.axaml");
        Assert.Contains("HistoryQuery", view);
        Assert.Contains("搜索全部转写、纪要、笔记", view);
        foreach (var choice in new[] { "HistoryDateChoices", "HistoryLengthChoices", "HistorySortChoices" })
            Assert.Contains(choice, view);
        // §6.1: the filter controls are the design-system MenuPicker, not bare Fluent ComboBoxes.
        Assert.Contains("MenuPicker", view);
        Assert.Contains("HistorySummary", view);
        Assert.Contains("HistoryHitPosition", view);
    }

    [Fact]
    public void SessionRowMatchesHistoryViewSwift()
    {
        var view = Read("Views", "HistoryView.axaml");
        // SessionRow: mode tint dot, title, summary icon, MeetingOutcome.when on the right,
        // details caption under the title, selected background uses Sunken.
        Assert.Contains("ModeTintBrush", view);
        Assert.Contains("TimeText", view);
        Assert.Contains("Details", view);
        Assert.Contains("IconSparkle", view);
        Assert.Contains("IconExclamationmarkCircle", view);
        Assert.Contains("Sunken", view);
        Assert.Contains("InteractiveRowBody", view);
    }

    [Fact]
    public void RowViewModelExposesTheMacSessionRowFields()
    {
        var row = File.ReadAllText(Path.Combine(Windows(), "src", "NextThought.Desktop", "RecentMeetingRow.cs"));
        foreach (var member in new[] { "ModeTintBrush", "TimeText", "Details", "HasReadySummary", "HasPendingSummary", "HasFailedSummary" })
            Assert.Contains(member, row);
        Assert.Contains("MeetingOutcome.When", row);
        Assert.Contains("MeetingOutcome.Line", row);
        Assert.Contains("SceneTheme.For", row);
    }
}
