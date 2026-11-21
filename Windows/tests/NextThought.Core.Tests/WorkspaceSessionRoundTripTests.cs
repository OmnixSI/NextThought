using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// task-62/E18: the series page's record tab matches meetings through WorkspaceSeries.SessionIds.
/// A7/A6 says that list is Windows grouping state: Mac workspace files must never carry it (the old
/// migration test asserts exactly that), so it lives in WindowsState/workspace-sessions.json and is
/// merged back after Load. These tests pin the sidecar round trip and the clean Mac files.
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

            var sidecar = Path.Combine(root, "WindowsState", "workspace-sessions.json");
            Assert.True(File.Exists(sidecar));
            Assert.Contains("first-id", File.ReadAllText(sidecar), StringComparison.Ordinal);

            var reloaded = new WorkspaceStore(root).Load();
            var found = Assert.Single(reloaded.Series);
            Assert.Equal(series.Id, found.Id);
            Assert.Equal(["first-id", "second-id"], found.SessionIds);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void TheMacWorkspaceFilesStayFreeOfSessionIds()
    {
        var root = NewRoot();
        try
        {
            var series = WorkspaceSeries.Create("E18 mac files") with { SessionIds = ["meeting-id"] };
            new WorkspaceStore(root).Save(new WorkspaceData([series]));

            var perSeries = Path.Combine(root, "Workspaces",
                Guid.Parse(series.Id).ToString("D").ToUpperInvariant() + ".json");
            Assert.True(File.Exists(perSeries));
            Assert.DoesNotContain("sessionIds", File.ReadAllText(perSeries), StringComparison.Ordinal);

            var legacy = Path.Combine(root, "Workspace", "workspace.json");
            Assert.True(File.Exists(legacy));
            Assert.DoesNotContain("sessionIds", File.ReadAllText(legacy), StringComparison.Ordinal);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
