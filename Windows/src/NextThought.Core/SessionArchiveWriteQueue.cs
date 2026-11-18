namespace NextThought.Core;

public sealed record SessionPersistenceSnapshot(
    int SubmittedRevision,
    int PersistedRevision,
    int PendingEvents,
    int PendingBytes,
    DateTimeOffset? LastPersistedAt,
    string? LastFailure,
    int UnqueuedWriteCount,
    int AcknowledgedUnqueuedWriteCount,
    bool CanAcknowledgeDataLoss);

/// <summary>
/// A bounded in-memory queue of archive writes (port of SessionArchiveWriteQueue). It never grows
/// without bound: overflow is recorded explicitly and can only be acknowledged after every
/// accepted write has been saved.
/// </summary>
public sealed class SessionArchiveWriteQueue
{
    public sealed record Entry(int Revision, string FileName, byte[] Data, SessionArchiveWriteMode Mode = SessionArchiveWriteMode.Append);

    public readonly record struct EnqueueResult(bool Queued, int Revision);

    public int MaximumPendingBytes { get; }
    public int MaximumPendingEvents { get; }
    private readonly List<Entry> entries = [];
    private long pendingBytes;
    private int submittedRevision;
    private int persistedRevision;
    private bool overflowed;
    private bool permanentFailure;
    private string? permanentFailureMessage;

    public SessionArchiveWriteQueue(int maximumPendingBytes = 8 * 1024 * 1024, int maximumPendingEvents = 16_384)
    {
        MaximumPendingBytes = Math.Max(1, maximumPendingBytes);
        MaximumPendingEvents = Math.Max(1, maximumPendingEvents);
    }

    public IReadOnlyList<Entry> Entries => entries;
    public int SubmittedRevision => submittedRevision;
    public int PersistedRevision => persistedRevision;
    public long PendingBytes => pendingBytes;
    public DateTimeOffset? LastPersistedAt { get; private set; }
    public string? LastFailure { get; private set; }
    public int UnqueuedWriteCount { get; private set; }
    public int AcknowledgedUnqueuedWriteCount { get; private set; }
    public bool HasPermanentFailure => permanentFailure;

    /// <summary>The user can acknowledge missing writes only after every accepted write is saved.</summary>
    public bool CanAcknowledgeDataLoss => overflowed && entries.Count == 0;

    public EnqueueResult Enqueue(string fileName, byte[] data, SessionArchiveWriteMode mode = SessionArchiveWriteMode.Append)
    {
        if (overflowed)
        {
            RecordUnqueuedWrite();
            return new EnqueueResult(false, 0);
        }
        if (entries.Count >= MaximumPendingEvents || data.LongLength > MaximumPendingBytes - pendingBytes)
        {
            RecordUnqueuedWrite();
            return new EnqueueResult(false, 0);
        }
        submittedRevision++;
        entries.Add(new Entry(submittedRevision, fileName, data, mode));
        pendingBytes += data.LongLength;
        return new EnqueueResult(true, submittedRevision);
    }

    /// <summary>Accepts the recorded loss explicitly and reopens the queue; the count stays visible.</summary>
    public bool AcknowledgeDataLossAndResume()
    {
        if (!CanAcknowledgeDataLoss) return false;
        overflowed = false;
        permanentFailure = false;
        permanentFailureMessage = null;
        LastFailure = null;
        AcknowledgedUnqueuedWriteCount = UnqueuedWriteCount;
        return true;
    }

    /// <summary>Acknowledges only a completed prefix; entries after a partial batch failure remain.</summary>
    public void Acknowledge(int throughRevision, DateTimeOffset? at = null)
    {
        if (throughRevision <= persistedRevision) return;
        var completedCount = entries.TakeWhile(entry => entry.Revision <= throughRevision).Count();
        if (completedCount == 0 || entries[completedCount - 1].Revision != throughRevision) return;
        foreach (var entry in entries.Take(completedCount)) pendingBytes -= entry.Data.LongLength;
        entries.RemoveRange(0, completedCount);
        persistedRevision = throughRevision;
        LastPersistedAt = at ?? DateTimeOffset.UtcNow;
        if (entries.Count == 0 && !permanentFailure) LastFailure = null;
    }

    public void Fail(string message, bool permanent = false)
    {
        if (permanent)
        {
            permanentFailure = true;
            permanentFailureMessage = message;
        }
        LastFailure = permanentFailureMessage ?? message;
    }

    /// <summary>A newer full snapshot supersedes every pending replacement snapshot.</summary>
    public void DiscardPendingForReplacement()
    {
        entries.Clear();
        pendingBytes = 0;
        submittedRevision = persistedRevision;
        if (UnqueuedWriteCount > AcknowledgedUnqueuedWriteCount)
        {
            overflowed = true;
            permanentFailure = true;
            permanentFailureMessage = "待保存队列已满；部分新写入没有入队。";
            LastFailure = permanentFailureMessage;
        }
        else
        {
            overflowed = false;
            permanentFailure = false;
            permanentFailureMessage = null;
            LastFailure = null;
        }
    }

    public SessionPersistenceSnapshot Snapshot => new(submittedRevision, persistedRevision, entries.Count,
        (int)Math.Min(int.MaxValue, pendingBytes), LastPersistedAt, LastFailure, UnqueuedWriteCount,
        AcknowledgedUnqueuedWriteCount, CanAcknowledgeDataLoss);

    private void RecordUnqueuedWrite()
    {
        UnqueuedWriteCount++;
        overflowed = true;
        permanentFailure = true;
        permanentFailureMessage = "待保存队列已满；新写入没有入队。";
        LastFailure = permanentFailureMessage;
    }
}
