using Xunit;

namespace NextThought.Tests;

/// <summary>
/// §3.5 series page: WorkspacesView's left list search/sort, scene rows with documents/questions/
/// meetings order, and the 记录/资料/经验积累 tabs on the right.
/// </summary>
public sealed class SeriesPageParityTests
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
    public void LeftListHasSearchSortAndSceneRows()
    {
        var view = Read("Views", "SeriesView.axaml");
        Assert.Contains("SeriesQuery", view);
        Assert.Contains("搜索系列或资料", view);
        Assert.Contains("SeriesSortOptions", view);
        Assert.Contains("SeriesSort", view);
        Assert.Contains("SeriesShownRows", view);
        Assert.Contains("SceneGlyph", view);
        Assert.Contains("StatsLine", view);
        var state = Read("AppState.SeriesList.cs");
        Assert.Contains("SeriesSortOptions", state);
        Assert.Contains("最近活动", state);
        Assert.Contains("名称", state);
        Assert.Contains("SeriesShownRows", state);
        Assert.Contains("RefreshSeriesPanel", state);
    }

    [Fact]
    public void DetailHasTheThreeMacTabs()
    {
        var view = Read("Views", "SeriesView.axaml");
        Assert.Contains("TabItem Header=\"{loc:Tr 记录}\"", view);
        Assert.Contains("TabItem Header=\"{loc:Tr 资料}\"", view);
        Assert.Contains("TabItem Header=\"{loc:Tr 经验积累}\"", view);
        Assert.Contains("SeriesMeetings", view);
        Assert.Contains("SelectedSeriesDocuments", view);
        Assert.Contains("SeriesLessons", view);
        Assert.Contains("SeriesBank", view);
        Assert.Contains("OpenSeriesRecord_Click", view);
        var code = Read("Views", "SeriesView.axaml.cs");
        Assert.Contains("OpenSeriesRecordAsync", code);
    }

    [Fact]
    public void RecordRowsShowTimeAndOutcome()
    {
        var view = Read("Views", "SeriesView.axaml");
        Assert.Contains("TimeText", view);
        Assert.Contains("Details", view);
        var row = Read("RecentMeetingRow.cs");
        Assert.Contains("public string TimeText", row);
        Assert.Contains("public string Details", row);
        Assert.Contains("N 份资料", row);
    }
}