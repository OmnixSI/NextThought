using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the series/experience part of Workspace.swift.</summary>
public sealed class WorkspaceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nt-workspace-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(directory, true); } catch { /* best effort */ }
    }

    [Fact]
    public void SeriesNamesAreUniqueCaseInsensitively()
    {
        var data = WorkspaceData.Empty.AddSeries("硕士论文").AddSeries("硕士论文").AddSeries("  ");
        Assert.Single(data.Series);
        Assert.Equal("硕士论文", data.Series[0].Name);
    }

    [Fact]
    public void ASessionBelongsToOneSeriesAtMost()
    {
        var data = WorkspaceData.Empty.AddSeries("A").AddSeries("B");
        var a = data.Series[0].Id;
        var b = data.Series[1].Id;
        data = data.Assign("s1", a);
        Assert.Equal(a, data.SeriesOfSession("s1")!.Id);
        data = data.Assign("s1", b);
        Assert.Equal(b, data.SeriesOfSession("s1")!.Id);
        Assert.Empty(data.Find(a)!.SessionIds);
        Assert.Empty(WorkspaceData.Empty.Assign("s1", "missing").Series);
    }

    [Fact]
    public void LessonsAreAppendedAndRemoved()
    {
        var data = WorkspaceData.Empty.AddSeries("组会");
        var id = data.Series[0].Id;
        data = data.AddLesson(id, " 先说结论，再说依据 ", "s1").AddLesson(id, "把实验和论证分开讲");
        Assert.Equal(2, data.Find(id)!.Lessons.Count);
        Assert.Equal("先说结论，再说依据", data.Find(id)!.Lessons[0].Text);
        Assert.Equal("s1", data.Find(id)!.Lessons[0].SourceSessionId);
        data = data.RemoveLesson(id, data.Find(id)!.Lessons[0].Id);
        Assert.Single(data.Find(id)!.Lessons);
        Assert.Single(data.AddLesson(id, "   ").Find(id)!.Lessons);
    }

    [Fact]
    public void RenameRefusesDuplicates()
    {
        var data = WorkspaceData.Empty.AddSeries("A").AddSeries("B");
        var a = data.Series[0].Id;
        data = data.Rename(a, "B");
        Assert.Equal("A", data.Find(a)!.Name);
        data = data.Rename(a, "C");
        Assert.Equal("C", data.Find(a)!.Name);
    }

    [Fact]
    public void StoreRoundTripsAndRecoversFromDamage()
    {
        var store = new WorkspaceStore(directory);
        Assert.Empty(store.Load().Series);
        var data = WorkspaceData.Empty.AddSeries("开题").AddLesson(WorkspaceData.Empty.AddSeries("x").Series[0].Id, "x");
        var id = WorkspaceData.Empty.AddSeries("开题").Series[0].Id;
        var saved = WorkspaceData.Empty.AddSeries("开题");
        saved = saved.Assign("s1", saved.Series[0].Id).AddLesson(saved.Series[0].Id, "一条经验", "s1");
        store.Save(saved);
        var loaded = store.Load();
        Assert.Single(loaded.Series);
        Assert.Equal("开题", loaded.Series[0].Name);
        Assert.Equal("s1", loaded.Series[0].SessionIds[0]);
        Assert.Equal("一条经验", loaded.Series[0].Lessons[0].Text);

        // Per-series files are authoritative now; a damaged legacy copy is ignored.
        File.WriteAllText(store.Path_, "{ not json");
        Assert.Single(store.Load().Series);
        Directory.Delete(Path.Combine(directory, "Workspaces"), true);
        Assert.Empty(store.Load().Series);
        _ = data;
        _ = id;
    }

    [Fact]
    public void StoreKeepsMacSceneSetupFields()
    {
        var id = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var json = "{\"series\":[{\"id\":\"" + id + "\",\"name\":\"客户会\",\"createdAt\":\"2026-01-01T00:00:00Z\","
            + "\"sessionIDs\":[],\"lessons\":[],\"mode\":\"client\",\"style\":\"detailed\","
            + "\"preferences\":\"先讲风险\",\"notes\":\"内部备注\",\"customTypeName\":\"投标复盘\","
            + "\"customBaseMode\":\"client\",\"archived\":true,\"fields\":{\"客户\":\"某公司\"},\"standing\":true,"
            + "\"seriesItemSourceTombstones\":[{\"itemID\":\"item\",\"sourceSessionID\":\"session\"}],"
            + "\"stageReview\":\"整体准备充分\",\"stageReviewAt\":\"2026-02-01T08:00:00Z\","
            + "\"glossary\":{\"entries\":[{\"canonical\":\"Kubernetes\",\"kind\":\"product\"}]}}]}";
        var store = new WorkspaceStore(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(store.Path_)!);
        File.WriteAllText(store.Path_, json);
        var series = store.Load().Series.Single();
        Assert.Equal("client", series.Mode);
        Assert.Equal("detailed", series.Style);
        Assert.Equal("先讲风险", series.Preferences);
        Assert.Equal("内部备注", series.Notes);
        Assert.Equal("投标复盘", series.CustomTypeName);
        Assert.Equal("client", series.CustomBaseMode);
        Assert.True(series.Archived);
        Assert.Equal("某公司", series.Fields!["客户"]);
        Assert.True(series.Standing);
        Assert.Equal(GlossaryKind.Product, series.Glossary!.Entries.Single().Kind);
        Assert.Equal("item", series.SeriesItemSourceTombstones!.Single().ItemId);
        Assert.Equal("session", series.SeriesItemSourceTombstones.Single().SourceSessionId);
        Assert.Equal("整体准备充分", series.StageReview);
        Assert.Equal(DateTimeOffset.Parse("2026-02-01T08:00:00Z"), series.StageReviewAt);

        store.Save(new WorkspaceData([series]));
        var saved = File.ReadAllText(Path.Combine(directory, "Workspaces", id + ".json"));
        Assert.Contains("\"customTypeName\"", saved);
        Assert.Contains("\"glossary\"", saved);
        Assert.Contains("\"sourceKind\": \"product\"", saved);
        Assert.Contains("\"seriesItemSourceTombstones\"", saved);
        Assert.Contains("\"itemID\"", saved);
        Assert.Contains("\"stageReview\"", saved);
    }

    [Fact]
    public void StoreKeepsTheMacSkillIDs()
    {
        var skillId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var data = new WorkspaceData([
            new WorkspaceSeries("s1", "客户会", DateTimeOffset.UnixEpoch, [], []) { SkillIds = [skillId] }
        ]);
        var store = new WorkspaceStore(directory);
        store.Save(data);
        var loaded = store.Load();
        Assert.Equal([skillId], loaded.Series.Single().SkillIds);
        Assert.Contains("\"skillIDs\"", File.ReadAllText(store.Path_));
    }

    [Fact]
    public void LessonRevisionIdUsesTheMacByteOrder()
    {
        var lesson = new WorkspaceLesson { Id = "lesson-1", Text = "经验", Revision = 1, UpdatedAt = DateTimeOffset.UnixEpoch };
        Assert.Equal("b32341f9-421d-583f-9842-8766e74cf164", WorkspaceLessonHistory.CurrentRevision(lesson).Id);
    }

    [Fact]
    public void ArchiveJsonKeepsChineseReadable()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { text = "中文" }, JsonDefaults.ArchiveOptions);
        Assert.Contains("中文", json);
        Assert.DoesNotContain("\\u4e2d", json);
    }

    [Fact]
    public void StoreWritesOneFilePerSeriesAndLoadsItAfterLegacyFileIsGone()
    {
        var store = new WorkspaceStore(directory);
        var data = WorkspaceData.Empty.AddSeries("开题").AddSeries("答辩");
        var id = data.Series[0].Id;
        store.Save(data);
        var seriesFile = Path.Combine(directory, "Workspaces", id.ToUpperInvariant() + ".json");
        Assert.True(File.Exists(seriesFile));
        File.Delete(store.Path_);
        var loaded = store.Load();
        Assert.Equal(2, loaded.Series.Count);
        Assert.Contains(loaded.Series, series => series.Name == "开题");

        store.Save(new WorkspaceData([data.Series[0]]));
        Assert.False(File.Exists(Path.Combine(directory, "Workspaces", data.Series[1].Id.ToUpperInvariant() + ".json")));
    }

    [Fact]
    public void SanitiseKeepsOneSeriesPerSession()
    {
        var store = new WorkspaceStore(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(store.Path_)!);
        File.WriteAllText(store.Path_, """
            {"series": [
              {"id": "a", "name": "A", "createdAt": "2026-01-01T00:00:00+00:00", "sessionIds": ["s1", "s1"], "lessons": []},
              {"id": "b", "name": "B", "createdAt": "2026-01-01T00:00:00+00:00", "sessionIds": ["s1", "s2"], "lessons": [{"id": "l", "text": "", "createdAt": "2026-01-01T00:00:00+00:00"}]}
            ]}
            """);
        var loaded = store.Load();
        Assert.Equal(["s1"], loaded.Series[0].SessionIds);
        Assert.Equal(["s2"], loaded.Series[1].SessionIds);
        Assert.Empty(loaded.Series[1].Lessons);
    }
}
