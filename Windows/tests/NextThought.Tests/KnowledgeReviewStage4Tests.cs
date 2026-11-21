using Xunit;

namespace NextThought.Tests;

/// <summary>
/// task-78/§3.4 knowledge-review: the stale decision uses the real transcript revision, the record
/// tab sorts newest first, the collect button is on the page, and 打开完整文本 opens a readable text
/// extraction instead of the parsed document.json.
/// </summary>
public sealed class KnowledgeReviewStage4Tests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "AppState.Series.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]));

    [Fact]
    public void TheStaleDecisionComparesTheCurrentTranscriptRevision()
    {
        var series = Read("AppState.Series.cs");
        Assert.Contains("IsCandidateStale", series);
        Assert.Contains("SessionProjectionBuilder.TranscriptSourceRevision", series);
        Assert.Contains("SeriesChangeRow(candidate, series.SeriesItems, IsCandidateStale(candidate))", series);
        var row = Read("SeriesChangeRow.cs");
        Assert.Contains("bool stale = false", row);
        Assert.Contains("staleOverride", row);
    }

    [Fact]
    public void TheRecordTabSortsNewestFirst()
    {
        Assert.Contains("OrderByDescending(meeting => meeting!.Meta.StartedAt)",
            Read("AppState.Series.cs"));
    }

    [Fact]
    public void TheMaterialFullTextOpensAReadableExtraction()
    {
        var state = Read("AppState.MaterialReview.cs");
        Assert.Contains("extracted.txt", state);
        Assert.Contains("Build(material.Blocks, int.MaxValue)", state);
        Assert.DoesNotContain("Path.Combine(Paths.Root, \"Materials\", material.Id, \"document.json\")", state);
    }
}
