using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// task-62/E18: the series page's record tab matches meetings through WorkspaceSeries.SessionIds,
/// which is [JsonIgnore] because it is Windows grouping state. WorkspaceStore must still write it:
/// before this test the save/load round trip silently dropped a series' meetings, so the series
/// record list (and the knowledge-review self-test) came back empty after a reload.
/// </summary>
public sealed class WorkspaceSessionRoundTripTests
{
    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "nt-workspace-sessions-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SavingAndLoadingAWorkspaceKeepsTheSeriesMeetings()
    {
        var root = NewRoot();
        try
        {
            var series = WorkspaceSeries.Create("E18 round trip") with { SessionIds = ["first-id", "second-id"] };
            new WorkspaceStore(root).Save(new WorkspaceData([series]));

            var reloaded = new WorkspaceStore(root).Load();
            var found = Assert.Single(reloaded.Series);
            Assert.Equal(series.Id, found.Id);
            Assert.Equal(["first-id", "second-id"], found.SessionIds);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void TheLegacySingleFileAlsoKeepsTheSeriesMeetings()
    {
        var root = NewRoot();
        try
        {
            var series = WorkspaceSeries.Create("E18 legacy") with { SessionIds = ["meeting-id"] };
            new WorkspaceStore(root).Save(new WorkspaceData([series]));
            // A Mac-style loader may fall back to the legacy aggregate file; it has to carry the ids too.
            var legacy = Path.Combine(root, "Workspace", "workspace.json");
            Assert.True(File.Exists(legacy));
            Assert.Contains("sessionIds", File.ReadAllText(legacy), StringComparison.Ordinal);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
