namespace NextThought.Core;

/// <summary>
/// Byte-bounded LRU cache for history-search snapshots (port of the SessionSearch cache).
/// The active meeting is normally excluded by the caller; this only bounds memory.
/// </summary>
public sealed class SessionSearchCache
{
    private sealed record Entry(SessionSearchSnapshot Snapshot, string Stamp, long Bytes, long Used);

    private readonly Dictionary<Guid, Entry> entries = [];
    private long bytes;
    private long clock;

    public SessionSearchCache(long limitBytes = 64L * 1024 * 1024) =>
        LimitBytes = Math.Max(1, limitBytes);

    public long LimitBytes { get; }
    public long Bytes => bytes;
    public int Count => entries.Count;

    public SessionSearchSnapshot? Get(Guid sessionId)
    {
        if (!entries.TryGetValue(sessionId, out var entry)) return null;
        entries[sessionId] = entry with { Used = ++clock };
        return entry.Snapshot;
    }

    /// <summary>Stores the snapshot under its stamp; an oversized snapshot is refused without evicting others.</summary>
    public bool Add(Guid sessionId, string stamp, SessionSearchSnapshot snapshot)
    {
        if (entries.TryGetValue(sessionId, out var existing))
        {
            if (existing.Stamp == stamp) return true;
            entries.Remove(sessionId);
            bytes -= existing.Bytes;
        }
        var size = snapshot.ByteCount;
        if (size > LimitBytes) return false;
        while (bytes + size > LimitBytes && entries.Count > 0)
        {
            var victim = entries.MinBy(pair => pair.Value.Used);
            entries.Remove(victim.Key);
            bytes -= victim.Value.Bytes;
        }
        entries[sessionId] = new Entry(snapshot, stamp, size, ++clock);
        bytes += size;
        return true;
    }

    /// <summary>Drops a session whose source changed; returns whether it was cached.</summary>
    public bool Remove(Guid sessionId)
    {
        if (!entries.Remove(sessionId, out var entry)) return false;
        bytes -= entry.Bytes;
        return true;
    }

    public void Clear()
    {
        entries.Clear();
        bytes = 0;
    }
}
