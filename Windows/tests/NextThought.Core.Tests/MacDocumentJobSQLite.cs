using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace NextThought.Core.Tests;

/// <summary>
/// J9 (test project only): the Mac SQLite persistence behind DocumentJobs.swift and
/// WorkspaceDeletionTransaction.swift, copied into the test project so the two SQLite-specific
/// Mac tests can be translated verbatim. The Windows app deliberately stores the queue in the JSON
/// <c>DocumentJobQueue</c>; this helper exists only because Microsoft.Data.Sqlite is a
/// test-project dependency and is not shipped.
///
/// Schema copied from Mac <c>JobDatabase</c>:
///   jobs (id TEXT PRIMARY KEY, body BLOB NOT NULL)
///   workspace_deletion_journal (id TEXT PRIMARY KEY, body BLOB NOT NULL)
/// The journal rows carry the JSON snapshots and every file move, exactly like the Mac types.
/// </summary>
internal static class MacDocumentJobSQLite
{
    public const int SqliteRow = 100;
    public const int SqliteDone = 101;
    public const int SqliteBusy = 5;

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public sealed class SQLiteQueryFailure(int resultCode) : Exception
    {
        public int ResultCode { get; } = resultCode;
    }

    /// <summary>Mac SQLiteRowReader: only SQLITE_DONE is a successful end, errors are thrown.</summary>
    public static class SQLiteRowReader
    {
        public static List<Row> Collect<Row>(Func<int> step, Func<Row> makeRow)
        {
            var rows = new List<Row>();
            while (true)
            {
                var code = step();
                if (code == SqliteRow)
                {
                    rows.Add(makeRow());
                    continue;
                }
                if (code == SqliteDone) return rows;
                throw new SQLiteQueryFailure(code);
            }
        }
    }

    public sealed record MacDocumentJob
    {
        [JsonPropertyName("id")] public Guid Id { get; init; } = Guid.NewGuid();
        [JsonPropertyName("workspaceID")] public Guid WorkspaceId { get; init; }
        [JsonPropertyName("fileName")] public string FileName { get; init; } = "";
        [JsonPropertyName("stage")] public string Stage { get; init; } = "queued";
        [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UnixEpoch;
    }

    public sealed record MacFileMove(string Source, string Staged);

    public enum MacJournalPhase
    {
        Staging,
        Committed
    }

    public sealed record MacJournal
    {
        [JsonPropertyName("transactionID")] public Guid TransactionId { get; init; } = Guid.NewGuid();
        [JsonPropertyName("workspaceID")] public Guid WorkspaceId { get; init; }
        public MacJournalPhase Phase { get; init; } = MacJournalPhase.Staging;
        public string StagingRoot { get; init; } = "";
        public IReadOnlyList<MacDocumentJob> Jobs { get; init; } = [];
        public IReadOnlyList<MacFileMove> Moves { get; init; } = [];
    }

    private static SqliteConnection Open(string url)
    {
        var directory = Path.GetDirectoryName(url);
        if (directory is not null) Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = url,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;" +
            "CREATE TABLE IF NOT EXISTS jobs (id TEXT PRIMARY KEY, body BLOB NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS workspace_deletion_journal (id TEXT PRIMARY KEY, body BLOB NOT NULL);";
        command.ExecuteNonQuery();
        return connection;
    }

    public static void Save(string url, MacDocumentJob job)
    {
        using var connection = Open(url);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO jobs(id,body) VALUES($id,$body)";
        command.Parameters.AddWithValue("$id", job.Id.ToString("D").ToUpperInvariant());
        command.Parameters.AddWithValue("$body", JsonSerializer.SerializeToUtf8Bytes(job, Json));
        command.ExecuteNonQuery();
    }

    public static List<MacDocumentJob> All(string url)
    {
        using var connection = Open(url);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT body FROM jobs";
        using var reader = command.ExecuteReader();
        var jobs = new List<MacDocumentJob>();
        while (reader.Read())
        {
            var bytes = (byte[])reader[0];
            jobs.Add(JsonSerializer.Deserialize<MacDocumentJob>(bytes, Json)!);
        }
        return jobs.OrderBy(job => job.CreatedAt).ToList();
    }

    public static void StageWorkspaceDeletion(string url, MacJournal journal)
    {
        if (journal.Phase != MacJournalPhase.Staging) throw new InvalidOperationException("journal is not staging");
        using var connection = Open(url);
        using var transaction = connection.BeginTransaction();
        WriteJournal(connection, transaction, journal);
        DeleteJobRows(connection, transaction, journal.Jobs.Select(job => job.Id));
        transaction.Commit();
    }

    public static List<MacJournal> WorkspaceDeletionJournals(string url)
    {
        using var connection = Open(url);
        return ReadJournals(connection);
    }

    /// <summary>
    /// Mac DocumentJobStore.recoverWorkspaceDeletionTransactions: prepared transactions restore
    /// files and rows, committed ones discard their staging area. The readJournals seam exists so a
    /// failed journal query can be proven to abort before any staged file or row is touched.
    /// </summary>
    public static void RecoverWorkspaceDeletionTransactions(string url, Func<List<MacJournal>>? readJournals = null)
    {
        using var connection = Open(url);
        var journals = readJournals?.Invoke() ?? ReadJournals(connection);
        foreach (var journal in journals)
        {
            if (journal.Phase == MacJournalPhase.Staging)
            {
                MacFileStaging.Rollback(journal.Moves, journal.StagingRoot);
                using var transaction = connection.BeginTransaction();
                InsertJobs(connection, transaction, journal.Jobs);
                DeleteJournal(connection, transaction, journal.TransactionId);
                transaction.Commit();
            }
            else
            {
                MacFileStaging.RemoveStagedFiles(journal.StagingRoot);
                using var transaction = connection.BeginTransaction();
                DeleteJournal(connection, transaction, journal.TransactionId);
                transaction.Commit();
            }
        }
    }

    private static List<MacJournal> ReadJournals(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT body FROM workspace_deletion_journal";
        using var reader = command.ExecuteReader();
        var journals = new List<MacJournal>();
        while (reader.Read())
        {
            var bytes = (byte[])reader[0];
            journals.Add(JsonSerializer.Deserialize<MacJournal>(bytes, Json)!);
        }
        return journals;
    }

    private static void WriteJournal(SqliteConnection connection, SqliteTransaction transaction, MacJournal journal)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO workspace_deletion_journal(id,body) VALUES($id,$body)";
        command.Parameters.AddWithValue("$id", journal.TransactionId.ToString("D").ToUpperInvariant());
        command.Parameters.AddWithValue("$body", JsonSerializer.SerializeToUtf8Bytes(journal, Json));
        command.ExecuteNonQuery();
    }

    private static void DeleteJournal(SqliteConnection connection, SqliteTransaction transaction, Guid id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM workspace_deletion_journal WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString("D").ToUpperInvariant());
        command.ExecuteNonQuery();
    }

    private static void DeleteJobRows(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM jobs WHERE id=$id";
            command.Parameters.AddWithValue("$id", id.ToString("D").ToUpperInvariant());
            command.ExecuteNonQuery();
        }
    }

    private static void InsertJobs(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<MacDocumentJob> jobs)
    {
        foreach (var job in jobs)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT OR REPLACE INTO jobs(id,body) VALUES($id,$body)";
            command.Parameters.AddWithValue("$id", job.Id.ToString("D").ToUpperInvariant());
            command.Parameters.AddWithValue("$body", JsonSerializer.SerializeToUtf8Bytes(job, Json));
            command.ExecuteNonQuery();
        }
    }
}

/// <summary>Mac WorkspaceDeletionFileStaging: manifest, stage and rollback of the ordered moves.</summary>
internal static class MacFileStaging
{
    /// <summary>Builds the durable ordered move manifest: (index)-(file name) under the staging root.</summary>
    public static List<MacDocumentJobSQLite.MacFileMove> Manifest(IReadOnlyList<string> sources, string stagingRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = sources.Where(source => seen.Add(Path.GetFullPath(source))).ToList();
        return unique.Select((source, index) => new MacDocumentJobSQLite.MacFileMove(
            source, Path.Combine(stagingRoot, $"{index}-{Path.GetFileName(source)}"))).ToList();
    }

    public static void Stage(IReadOnlyList<MacDocumentJobSQLite.MacFileMove> moves)
    {
        if (moves.Count == 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(moves[0].Staged)!);
        foreach (var move in moves.Where(move => File.Exists(move.Source)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(move.Staged)!);
            File.Move(move.Source, move.Staged);
        }
    }

    /// <summary>Restores whichever paths were already moved; untouched sources are skipped.</summary>
    public static void Rollback(IReadOnlyList<MacDocumentJobSQLite.MacFileMove> moves, string explicitStagingRoot)
    {
        for (var index = moves.Count - 1; index >= 0; index--)
        {
            var move = moves[index];
            if (!File.Exists(move.Staged)) continue;
            if (File.Exists(move.Source)) throw new IOException("source still exists: " + move.Source);
            Directory.CreateDirectory(Path.GetDirectoryName(move.Source)!);
            File.Move(move.Staged, move.Source);
        }
        RemoveStagedFiles(explicitStagingRoot);
    }

    /// <summary>Discards the moved files after a committed deletion (journal stays until this works).</summary>
    public static void RemoveStagedFiles(string stagingRoot)
    {
        if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
    }
}
