using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the byte-bounded LRU snapshot cache.</summary>
public sealed class SessionSearchCacheTests
{
    private static SessionSearchSnapshot Snapshot(int textLength) => new(
    [
        new SessionSearchField(SessionSearchFieldKind.Transcript, new string('x', textLength), Id: "line:1")
    ]);

    private static readonly int SnapshotBytes = Snapshot(10).ByteCount;

    [Fact]
    public void GetReturnsAndPromotesTheSnapshot()
    {
        var cache = new SessionSearchCache(1_000);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        cache.Add(first, "a", Snapshot(10));
        cache.Add(second, "b", Snapshot(10));
        Assert.NotNull(cache.Get(first));
        Assert.Null(cache.Get(Guid.NewGuid()));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void LeastRecentlyUsedIsEvictedPastTheLimit()
    {
        var cache = new SessionSearchCache(SnapshotBytes * 2);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        cache.Add(first, "a", Snapshot(10));
        cache.Add(second, "b", Snapshot(10));
        cache.Get(first);
        cache.Add(third, "c", Snapshot(10));
        Assert.NotNull(cache.Get(first));
        Assert.Null(cache.Get(second));
        Assert.NotNull(cache.Get(third));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void OversizedSnapshotsAreRefusedWithoutEvicting()
    {
        var cache = new SessionSearchCache(SnapshotBytes * 2);
        var kept = Guid.NewGuid();
        cache.Add(kept, "a", Snapshot(10));
        Assert.False(cache.Add(Guid.NewGuid(), "b", Snapshot(10_000)));
        Assert.NotNull(cache.Get(kept));
    }

    [Fact]
    public void ReplacingASnapshotUpdatesTheByteCount()
    {
        var cache = new SessionSearchCache(1_000);
        var id = Guid.NewGuid();
        cache.Add(id, "a", Snapshot(10));
        var afterFirst = cache.Bytes;
        cache.Add(id, "b", Snapshot(30));
        Assert.Equal(afterFirst + 20, cache.Bytes);
        cache.Add(id, "b", Snapshot(30));
        Assert.Equal(afterFirst + 20, cache.Bytes);
    }

    [Fact]
    public void RemoveAndClearAccountForBytes()
    {
        var cache = new SessionSearchCache(1_000);
        var id = Guid.NewGuid();
        cache.Add(id, "a", Snapshot(10));
        Assert.True(cache.Remove(id));
        Assert.False(cache.Remove(id));
        Assert.Equal(0, cache.Bytes);
        cache.Add(Guid.NewGuid(), "b", Snapshot(10));
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
    }
}
