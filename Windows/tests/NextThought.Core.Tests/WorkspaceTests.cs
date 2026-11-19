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

        File.WriteAllText(store.Path_, "{ not json");
        Assert.Empty(store.Load().Series);
        _ = data;
        _ = id;
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
