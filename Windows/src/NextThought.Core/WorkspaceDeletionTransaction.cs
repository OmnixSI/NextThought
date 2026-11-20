namespace NextThought.Core;

public sealed record WorkspaceDeletionFileMove(string Source, string Staged);

public sealed record WorkspaceDeletionJournal
{
    public enum PhaseValue
    {
        Staging,
        Committed
    }

    [System.Text.Json.Serialization.JsonPropertyName("transactionID")]
    public string TransactionId { get; init; } = Guid.NewGuid().ToString("D");

    [System.Text.Json.Serialization.JsonPropertyName("workspaceID")]
    public string WorkspaceId { get; init; } = "";
    public PhaseValue Phase { get; init; } = PhaseValue.Staging;
    public string StagingRoot { get; init; } = "";
    public IReadOnlyList<WorkspaceDeletionFileMove> Moves { get; init; } = [];
    public IReadOnlyList<DocumentJob> Jobs { get; init; } = [];
}

/// <summary>
/// Reversible file staging for a workspace deletion (port of WorkspaceDeletionFileStaging).
/// Document-job bookkeeping and shared-original accounting stay app-side in this port.
/// </summary>
public static class WorkspaceDeletionFileStaging
{
    /// <summary>
    /// Builds a durable ordered move manifest. Pass the workspace record last so its absence
    /// marks the point at which recovery can treat the deletion as committed.
    /// </summary>
    public static IReadOnlyList<WorkspaceDeletionFileMove> Manifest(IReadOnlyList<string> sources, string stagingRoot)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var moves = new List<WorkspaceDeletionFileMove>();
        var index = 0;
        foreach (var source in sources)
        {
            var full = Path.GetFullPath(source);
            if (!seen.Add(full)) continue;
            var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            moves.Add(new WorkspaceDeletionFileMove(full, Path.Combine(stagingRoot, index + "-" + name)));
            index++;
        }
        return moves;
    }

    /// <summary>
    /// Removes an original from the move set if a current, non-deleted job or workspace still
    /// references its hash/path. Call immediately before staging, after all awaits that could add
    /// a reference (port of excludingSharedOriginals).
    /// </summary>
    public static IReadOnlyList<WorkspaceDeletionFileMove> ExcludingSharedOriginals(
        IReadOnlyList<WorkspaceDeletionFileMove> moves,
        IReadOnlyList<DocumentJob> deletingJobs,
        IReadOnlyList<DocumentJob> currentJobs,
        IReadOnlySet<string> currentWorkspaceSourceHashes,
        string originalsRoot)
    {
        var deletingIds = deletingJobs.Select(job => job.Id).ToHashSet(StringComparer.Ordinal);
        var remainingJobs = currentJobs.Where(job => !deletingIds.Contains(job.Id)).ToList();
        var retainedHashes = remainingJobs.Select(job => job.SourceHash).Where(hash => hash is { Length: > 0 })
            .Select(hash => hash!).ToHashSet(StringComparer.Ordinal);
        retainedHashes.UnionWith(currentWorkspaceSourceHashes);
        var retainedPaths = remainingJobs.Select(job => job.OriginalPath).Where(path => path is { Length: > 0 })
            .Select(path => Path.GetFullPath(path!)).ToHashSet(StringComparer.Ordinal);
        var originalsPrefix = Path.GetFullPath(originalsRoot).TrimEnd(Path.DirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
        var deletingHashesByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var job in deletingJobs)
        {
            if (job.OriginalPath is { Length: > 0 } path && job.SourceHash is { Length: > 0 } hash)
                deletingHashesByPath[Path.GetFullPath(path)] = hash;
        }

        return moves.Where(move =>
        {
            var path = Path.GetFullPath(move.Source);
            if (!path.StartsWith(originalsPrefix, StringComparison.OrdinalIgnoreCase)) return true;
            if (!deletingHashesByPath.TryGetValue(path, out var hash)) return true;
            return !retainedPaths.Contains(path) && !retainedHashes.Contains(hash);
        }).ToList();
    }

    public static void Stage(IReadOnlyList<WorkspaceDeletionFileMove> moves, Action<string, string>? mover = null)
    {
        if (moves.Count == 0) return;
        var stagingRoot = Path.GetDirectoryName(moves[0].Staged);
        if (stagingRoot is null) return;
        Directory.CreateDirectory(stagingRoot);
        foreach (var move in moves)
        {
            if (!File.Exists(move.Source) && !Directory.Exists(move.Source)) continue;
            var parent = Path.GetDirectoryName(move.Staged);
            if (parent is not null) Directory.CreateDirectory(parent);
            if (mover is null) Move(move.Source, move.Staged); else mover(move.Source, move.Staged);
        }
    }

    /// <summary>Restores whichever paths were already moved; untouched sources are skipped.</summary>
    public static void Rollback(IReadOnlyList<WorkspaceDeletionFileMove> moves, string? explicitStagingRoot = null)
    {
        foreach (var move in moves.Reverse())
        {
            if (!File.Exists(move.Staged) && !Directory.Exists(move.Staged)) continue;
            if (File.Exists(move.Source) || Directory.Exists(move.Source))
                throw new IOException("回滚目标已存在：" + move.Source);
            var parent = Path.GetDirectoryName(move.Source);
            if (parent is not null) Directory.CreateDirectory(parent);
            Move(move.Staged, move.Source);
        }
        var stagingRoot = explicitStagingRoot ?? (moves.Count > 0 ? Path.GetDirectoryName(moves[0].Staged) : null);
        if (stagingRoot is not null && Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
    }

    /// <summary>Discards staged files after a committed deletion; a failure can be retried later.</summary>
    public static void RemoveStagedFiles(string stagingRoot)
    {
        if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
    }

    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination, overwrite: false);
    }
}

/// <summary>
/// Runs a reversible preparation step, checks authorization after its suspension points, and
/// only then enters the commit section (port of WorkspaceDeletionTransaction).
/// </summary>
public static class WorkspaceDeletionTransaction
{
    /// <returns>True when the commit section ran; false when authorization declined.</returns>
    public static async Task<bool> Execute<TPlan>(
        Func<Task<TPlan>> prepare,
        Func<TPlan, bool> isAuthorized,
        Func<TPlan, Task> rollback,
        Func<TPlan, Task> commit,
        Func<TPlan, Task>? stage = null)
    {
        var plan = await prepare();
        if (!isAuthorized(plan))
        {
            await rollback(plan);
            return false;
        }
        try
        {
            if (stage is not null) await stage(plan);
        }
        catch
        {
            await rollback(plan);
            throw;
        }
        if (!isAuthorized(plan))
        {
            await rollback(plan);
            return false;
        }
        try
        {
            await commit(plan);
            return true;
        }
        catch
        {
            await rollback(plan);
            throw;
        }
    }
}

/// <summary>B22: durable JSON job store with workspace-deletion journals.</summary>
public sealed class DocumentJobStore
{
    private readonly string directory;
    private readonly DocumentJobQueue queue;
    private readonly string journalsPath;

    public DocumentJobStore(string path)
    {
        directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory!);
        queue = new DocumentJobQueue(directory);
        journalsPath = Path.Combine(directory, "workspace-deletions.json");
    }

    public void Save(DocumentJob job)
    {
        if (!queue.Update(job)) queue.Enqueue(job);
    }

    public IReadOnlyList<DocumentJob> All() => queue.All;

    public void StageWorkspaceDeletion(WorkspaceDeletionJournal journal)
    {
        if (journal.Jobs.Count > 0) queue.Delete(journal.Jobs.Select(job => job.Id).ToList());
        var journals = Journals();
        journals.Add(journal);
        WriteJournals(journals);
    }

    public void RestoreWorkspaceDeletionRows(string transactionId)
    {
        var journals = Journals();
        var journal = journals.FirstOrDefault(entry => entry.TransactionId == transactionId);
        if (journal is null) return;
        foreach (var job in journal.Jobs) queue.RestoreDeleted([job]);
        journals.RemoveAll(entry => entry.TransactionId == transactionId);
        WriteJournals(journals);
    }

    public IReadOnlyList<WorkspaceDeletionJournal> WorkspaceDeletionJournals() => Journals();

    public static void MarkWorkspaceDeletionCommitted(string transactionId, string path)
    {
        var store = new DocumentJobStore(path);
        var journals = store.Journals();
        var index = journals.FindIndex(entry => entry.TransactionId == transactionId);
        if (index >= 0) journals[index] = journals[index] with { Phase = WorkspaceDeletionJournal.PhaseValue.Committed };
        store.WriteJournals(journals);
    }

    public static void RecoverWorkspaceDeletionTransactions(string path)
    {
        var store = new DocumentJobStore(path);
        foreach (var journal in store.Journals())
        {
            if (journal.Phase == WorkspaceDeletionJournal.PhaseValue.Staging)
            {
                WorkspaceDeletionFileStaging.Rollback(journal.Moves, journal.StagingRoot);
                foreach (var job in journal.Jobs) store.queue.RestoreDeleted([job]);
            }
            else
            {
                WorkspaceDeletionFileStaging.RemoveStagedFiles(journal.StagingRoot);
            }
        }
        store.WriteJournals([]);
    }

    private List<WorkspaceDeletionJournal> Journals()
    {
        if (!File.Exists(journalsPath)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<WorkspaceDeletionJournal>>(
                File.ReadAllText(journalsPath), JsonDefaults.Options) ?? [];
        }
        catch { return []; }
    }

    private void WriteJournals(List<WorkspaceDeletionJournal> journals)
    {
        if (journals.Count == 0) { if (File.Exists(journalsPath)) File.Delete(journalsPath); return; }
        File.WriteAllText(journalsPath, System.Text.Json.JsonSerializer.Serialize(journals, JsonDefaults.Options));
    }
}
