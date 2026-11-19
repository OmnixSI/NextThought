namespace NextThought.Core;

/// <summary>
/// Publishes validated session folders into the archive and rolls back already-published folders
/// when a later one fails (port of SessionArchiveMigrationPublisher).
/// </summary>
public static class SessionArchiveMigrationPublisher
{
    public sealed class RecoveryRequired(string stagingDirectory, string operationFailure,
        IReadOnlyList<string> rollbackFailures) : Exception(
        "Archive migration rollback needs recovery. The staged backup remains at " + stagingDirectory + ". "
        + string.Join("; ", new[] { operationFailure }.Concat(rollbackFailures)))
    {
        public string StagingDirectory { get; } = stagingDirectory;
        public string OperationFailure { get; } = operationFailure;
        public IReadOnlyList<string> RollbackFailures { get; } = rollbackFailures;
    }

    /// <summary>
    /// A leftover staging root means an earlier migration could not finish its rollback or was
    /// interrupted; callers report recovery state instead of starting another replacement.
    /// </summary>
    public static string? PendingRecoveryDirectory(string root)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(root));
        if (parent is null || !Directory.Exists(parent)) return null;
        var prefix = "." + Path.GetFileName(Path.GetFullPath(root)) + "-migration-";
        return Directory.EnumerateDirectories(parent)
            .FirstOrDefault(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal));
    }

    public static void Publish(
        IReadOnlyList<string> sessionIds,
        string root,
        Action<string, string> stage,
        Action<string, string> validate,
        Func<string, string, bool>? canReplaceExisting = null,
        Action<string, string>? move = null,
        Action? commit = null)
    {
        if (sessionIds.Distinct(StringComparer.Ordinal).Count() != sessionIds.Count
            || sessionIds.Any(id => id.Length == 0 || id.Contains('/') || id.Contains('\\') || id is "." or ".."))
            throw new ArgumentException("会话 id 不能重复或用作目录名。", nameof(sessionIds));

        Directory.CreateDirectory(root);
        var fullRoot = Path.GetFullPath(root);
        var parent = Path.GetDirectoryName(fullRoot)!;
        var stagingRoot = Path.Combine(parent, "." + Path.GetFileName(fullRoot) + "-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(stagingRoot);
        var preserveStagingRoot = false;
        var backupsRoot = Path.Combine(stagingRoot, "backups");
        try
        {
            Directory.CreateDirectory(backupsRoot);
            foreach (var id in sessionIds)
            {
                var stagedFolder = Path.Combine(stagingRoot, id);
                Directory.CreateDirectory(stagedFolder);
                stage(id, stagedFolder);
                validate(id, stagedFolder);
                var destination = Path.Combine(fullRoot, id);
                if (Directory.Exists(destination) && !(canReplaceExisting?.Invoke(id, destination) ?? false))
                    throw new IOException("归档里已经有这个会议：" + id);
            }

            var published = new List<string>();
            var backups = new List<(string Backup, string Destination)>();
            try
            {
                foreach (var id in sessionIds)
                {
                    var stagedFolder = Path.Combine(stagingRoot, id);
                    var destination = Path.Combine(fullRoot, id);
                    if (Directory.Exists(destination))
                    {
                        if (!(canReplaceExisting?.Invoke(id, destination) ?? false))
                            throw new IOException("归档里已经有这个会议：" + id);
                        var backup = Path.Combine(backupsRoot, id);
                        backups.Add((backup, destination));
                        Move(destination, backup, move);
                    }
                    published.Add(destination);
                    Move(stagedFolder, destination, move);
                }
                commit?.Invoke();
            }
            catch (Exception operation)
            {
                var failures = new List<string>();
                foreach (var destination in Enumerable.Reverse(published))
                {
                    if (!Directory.Exists(destination)) continue;
                    try { Directory.Delete(destination, true); }
                    catch (Exception deleteError) { failures.Add("Could not remove published folder " + destination + ": " + deleteError.Message); }
                }
                foreach (var backup in Enumerable.Reverse(backups))
                {
                    if (!Directory.Exists(backup.Backup)) continue;
                    if (Directory.Exists(backup.Destination))
                    {
                        failures.Add("Could not restore " + backup.Destination + ": destination still exists");
                        continue;
                    }
                    try { Move(backup.Backup, backup.Destination, move); }
                    catch (Exception restoreError) { failures.Add("Could not restore " + backup.Destination + ": " + restoreError.Message); }
                }
                if (failures.Count > 0)
                {
                    preserveStagingRoot = true;
                    throw new RecoveryRequired(stagingRoot, operation.Message, failures);
                }
                throw;
            }
        }
        finally
        {
            if (!preserveStagingRoot)
            {
                try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); } catch { }
            }
        }
    }

    private static void Move(string source, string destination, Action<string, string>? move)
    {
        if (move is not null)
        {
            move(source, destination);
            return;
        }
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination, overwrite: false);
    }
}
