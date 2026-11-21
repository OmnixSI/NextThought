using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>A7: series membership lives in each meeting's meta.json, not in a Windows-only list.</summary>
public sealed class WorkspaceMembershipTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "nt-workspace-a7-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AImportedMacMeetingJoinsItsSeriesFromMeta()
    {
        var root = Root();
        try
        {
            var seriesId = Guid.NewGuid().ToString("D");
            var sessionId = Guid.NewGuid().ToString("D");
            Directory.CreateDirectory(Path.Combine(root, "Workspaces"));
            Directory.CreateDirectory(Path.Combine(root, "Sessions", sessionId.ToUpperInvariant()));
            File.WriteAllText(Path.Combine(root, "Workspaces", seriesId.ToUpperInvariant() + ".json"),
                "{\"id\":\"" + seriesId + "\",\"name\":\"\u8BFA\u8A00\",\"createdAt\":\"2025-01-01T00:00:00Z\",\"lessons\":[]}");
            File.WriteAllText(Path.Combine(root, "Sessions", sessionId.ToUpperInvariant(), "meta.json"),
                "{\"id\":\"" + sessionId + "\",\"startedAt\":\"2025-01-02T00:00:00Z\",\"workspaceID\":\"" + seriesId + "\",\"workspaceName\":\"\u8BFA\u8A00\"}");

            var loaded = new WorkspaceStore(root).Load();
            var series = Assert.Single(loaded.Series);
            Assert.Contains(series.SessionIds, id => string.Equals(id, sessionId, StringComparison.OrdinalIgnoreCase));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void OldSessionIdListsMigrateIntoMetaOnce()
    {
        var root = Root();
        try
        {
            var seriesId = Guid.NewGuid().ToString("D");
            var sessionId = Guid.NewGuid().ToString("D");
            Directory.CreateDirectory(Path.Combine(root, "Workspaces"));
            Directory.CreateDirectory(Path.Combine(root, "Sessions", sessionId.ToUpperInvariant()));
            File.WriteAllText(Path.Combine(root, "Workspaces", seriesId.ToUpperInvariant() + ".json"),
                "{\"id\":\"" + seriesId + "\",\"name\":\"\u8BFA\u8A00\",\"createdAt\":\"2025-01-01T00:00:00Z\",\"sessionIds\":[\"" + sessionId + "\"],\"lessons\":[]}");
            File.WriteAllText(Path.Combine(root, "Sessions", sessionId.ToUpperInvariant(), "meta.json"),
                "{\"id\":\"" + sessionId + "\",\"startedAt\":\"2025-01-02T00:00:00Z\",\"title\":\"t\"}");

            var store = new WorkspaceStore(root);
            var loaded = store.Load();
            Assert.Single(loaded.Series);
            using (var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Sessions", sessionId.ToUpperInvariant(), "meta.json"))))
            {
                Assert.Equal(seriesId, meta.RootElement.GetProperty("workspaceID").GetString());
                Assert.Equal("\u8BFA\u8A00", meta.RootElement.GetProperty("workspaceName").GetString());
                Assert.True(meta.RootElement.TryGetProperty("title", out _));
            }

            File.WriteAllText(Path.Combine(root, "Sessions", sessionId.ToUpperInvariant(), "meta.json"),
                "{\"id\":\"" + sessionId + "\",\"startedAt\":\"2025-01-02T00:00:00Z\",\"workspaceID\":\"" + seriesId + "\",\"workspaceName\":\"\u8BFA\u8A00\"}");
            store.Save(loaded);
            using var seriesJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Workspaces", seriesId.ToUpperInvariant() + ".json")));
            Assert.False(seriesJson.RootElement.TryGetProperty("sessionIds", out _));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
