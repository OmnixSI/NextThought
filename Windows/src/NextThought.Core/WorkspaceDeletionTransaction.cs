namespace NextThought.Core;

public sealed record WorkspaceDeletionFileMove(string Source, string Staged);

public sealed record WorkspaceDeletionJournal
{
    public enum PhaseValue
    {
        Staging,
        Committed
    }

    public string TransactionId { get; init; } = Guid.NewGuid().ToString("D");
    public string WorkspaceId { get; init; } = "";
    public PhaseValue Phase { get; init; } = PhaseValue.Staging;
    public string StagingRoot { get; init; } = "";
    public IReadOnlyList<WorkspaceDeletionFileMove> Moves { get; init; } = [];
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

    public static void Stage(IReadOnlyList<WorkspaceDeletionFileMove> moves)
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
            Move(move.Source, move.Staged);
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
