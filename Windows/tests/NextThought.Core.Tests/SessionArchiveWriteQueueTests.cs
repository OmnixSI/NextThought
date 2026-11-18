using System.Text;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the bounded archive write queue and its loss accounting.</summary>
public sealed class SessionArchiveWriteQueueTests
{
    private static byte[] Data(int length) => Encoding.UTF8.GetBytes(new string('x', length));

    [Fact]
    public void EnqueueTracksRevisionsAndBytes()
    {
        var queue = new SessionArchiveWriteQueue();
        var first = queue.Enqueue("a", Data(3));
        var second = queue.Enqueue("b", Data(5), SessionArchiveWriteMode.Replace);
        Assert.True(first.Queued);
        Assert.True(second.Queued);
        Assert.Equal(1, first.Revision);
        Assert.Equal(2, second.Revision);
        Assert.Equal(2, queue.Entries.Count);
        Assert.Equal(8, queue.PendingBytes);
        Assert.Equal(2, queue.Snapshot.PendingEvents);
    }

    [Fact]
    public void EventLimitOverflowsAndRecordsLoss()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingEvents: 2);
        queue.Enqueue("a", Data(1));
        queue.Enqueue("b", Data(1));
        var rejected = queue.Enqueue("c", Data(1));
        Assert.False(rejected.Queued);
        Assert.True(queue.HasPermanentFailure);
        Assert.Equal(1, queue.UnqueuedWriteCount);
        Assert.Equal("待保存队列已满；新写入没有入队。", queue.LastFailure);
    }

    [Fact]
    public void ByteLimitRejectsTheWriteThatWouldExceedIt()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingBytes: 10);
        Assert.True(queue.Enqueue("a", Data(6)).Queued);
        Assert.False(queue.Enqueue("b", Data(6)).Queued);
        Assert.Single(queue.Entries);
    }

    [Fact]
    public void DataLossCanOnlyBeAcknowledgedAfterDraining()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingEvents: 1);
        queue.Enqueue("a", Data(1));
        queue.Enqueue("b", Data(1));
        Assert.False(queue.CanAcknowledgeDataLoss);
        queue.Acknowledge(1);
        Assert.True(queue.CanAcknowledgeDataLoss);
        Assert.True(queue.AcknowledgeDataLossAndResume());
        Assert.False(queue.HasPermanentFailure);
        Assert.Equal(1, queue.AcknowledgedUnqueuedWriteCount);
        Assert.True(queue.Enqueue("c", Data(1)).Queued);
    }

    [Fact]
    public void AcknowledgeOnlyAcceptsAContiguousPrefix()
    {
        var queue = new SessionArchiveWriteQueue();
        queue.Enqueue("a", Data(1));
        queue.Enqueue("b", Data(1));
        queue.Enqueue("c", Data(1));
        queue.Acknowledge(2);
        Assert.Single(queue.Entries);
        Assert.Equal(3, queue.Entries[0].Revision);
        Assert.Equal(2, queue.PersistedRevision);
        queue.Acknowledge(2);
        Assert.Equal(2, queue.PersistedRevision);
        queue.Acknowledge(3);
        Assert.Empty(queue.Entries);
        Assert.NotNull(queue.LastPersistedAt);
    }

    [Fact]
    public void PermanentFailureOutlivesTransientOnes()
    {
        var queue = new SessionArchiveWriteQueue();
        queue.Fail("disk full", permanent: true);
        queue.Fail("retrying");
        Assert.True(queue.HasPermanentFailure);
        Assert.Equal("disk full", queue.LastFailure);
    }

    [Fact]
    public void DiscardPendingReplacementKeepsTheOverflowAccounting()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingEvents: 1);
        queue.Enqueue("a", Data(1));
        queue.Enqueue("b", Data(1));
        Assert.True(queue.Enqueue("a", Data(1), SessionArchiveWriteMode.Replace).Queued == false);
        queue.DiscardPendingForReplacement();
        Assert.Empty(queue.Entries);
        Assert.Equal(0, queue.PendingBytes);
        Assert.Equal(0, queue.SubmittedRevision);
        Assert.True(queue.HasPermanentFailure);
        Assert.True(queue.Snapshot.CanAcknowledgeDataLoss);
    }

    [Fact]
    public void DiscardWithoutLossClearsTheQueueAndFailures()
    {
        var queue = new SessionArchiveWriteQueue();
        queue.Enqueue("a", Data(4));
        queue.Fail("transient");
        queue.DiscardPendingForReplacement();
        Assert.Empty(queue.Entries);
        Assert.False(queue.HasPermanentFailure);
        Assert.Null(queue.LastFailure);
        Assert.Equal(queue.PersistedRevision, queue.SubmittedRevision);
    }

    [Fact]
    public void SnapshotReflectsEveryCounter()
    {
        var queue = new SessionArchiveWriteQueue(maximumPendingEvents: 1);
        queue.Enqueue("a", Data(2));
        queue.Enqueue("b", Data(2));
        var snapshot = queue.Snapshot;
        Assert.Equal(1, snapshot.SubmittedRevision);
        Assert.Equal(0, snapshot.PersistedRevision);
        Assert.Equal(1, snapshot.PendingEvents);
        Assert.Equal(2, snapshot.PendingBytes);
        Assert.Equal(1, snapshot.UnqueuedWriteCount);
        Assert.Equal(0, snapshot.AcknowledgedUnqueuedWriteCount);
        Assert.False(snapshot.CanAcknowledgeDataLoss);
    }
}
