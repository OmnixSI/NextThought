using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the durable JSON document-job queue.</summary>
public sealed class DocumentJobQueueTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nt-job-queue-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static DocumentJob Job(string id, DateTimeOffset created, DocumentJob.StageValue stage = DocumentJob.StageValue.Queued) =>
        DocumentJob.Create("ws", id + ".pdf") with { Id = id, CreatedAt = created, UpdatedAt = created, Stage = stage };

    [Fact]
    public void JobsSurviveAReopen()
    {
        var queue = new DocumentJobQueue(root);
        Assert.True(queue.Enqueue(Job("a", DateTimeOffset.UtcNow)));
        var reopened = new DocumentJobQueue(root);
        Assert.Single(reopened.All);
        Assert.Equal("a", reopened.All[0].Id);
        Assert.True(File.Exists(reopened.FilePath));
    }

    [Fact]
    public void DuplicateIdsAreIgnored()
    {
        var queue = new DocumentJobQueue(root);
        Assert.True(queue.Enqueue(Job("a", DateTimeOffset.UtcNow)));
        Assert.False(queue.Enqueue(Job("a", DateTimeOffset.UtcNow)));
        Assert.Single(queue.All);
    }

    [Fact]
    public void NextIsTheOldestPendingJob()
    {
        var queue = new DocumentJobQueue(root);
        var now = DateTimeOffset.UtcNow;
        queue.Enqueue(Job("new", now));
        queue.Enqueue(Job("old", now.AddMinutes(-5)));
        queue.Enqueue(Job("done", now.AddMinutes(-10), DocumentJob.StageValue.Ready));
        queue.Enqueue(Job("cancelled", now.AddMinutes(-20), DocumentJob.StageValue.Cancelled));
        Assert.Equal("old", queue.Next()?.Id);
        Assert.Equal(["old", "new"], queue.Pending.Select(job => job.Id));
    }

    [Fact]
    public void UpdateAndRemoveMaintainTheFile()
    {
        var queue = new DocumentJobQueue(root);
        queue.Enqueue(Job("a", DateTimeOffset.UtcNow));
        var updated = queue.All[0] with { Stage = DocumentJob.StageValue.Parsing, Completed = 2, Total = 5 };
        Assert.True(queue.Update(updated));
        Assert.False(queue.Update(updated with { Id = "missing" }));
        Assert.Equal(DocumentJob.StageValue.Parsing, new DocumentJobQueue(root).All[0].Stage);
        Assert.True(queue.Remove("a"));
        Assert.False(queue.Remove("a"));
        Assert.Empty(new DocumentJobQueue(root).All);
    }

    [Fact]
    public void ClearEmptiesTheQueue()
    {
        var queue = new DocumentJobQueue(root);
        queue.Enqueue(Job("a", DateTimeOffset.UtcNow));
        queue.Enqueue(Job("b", DateTimeOffset.UtcNow));
        queue.Clear();
        Assert.Equal(0, queue.Count);
        Assert.Empty(new DocumentJobQueue(root).All);
    }
}
