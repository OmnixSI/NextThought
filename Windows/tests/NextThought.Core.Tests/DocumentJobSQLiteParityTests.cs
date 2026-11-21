using System.Text.Json;
using Xunit;
using static NextThought.Core.Tests.MacDocumentJobSQLite;

namespace NextThought.Core.Tests;

/// <summary>
/// J9: the two SQLite-specific tests of Tests/NextThoughtCoreTests/DocumentJobTests.swift,
/// translated verbatim against the test-project-only Mac-format helper
/// (<see cref="MacDocumentJobSQLite"/>). Microsoft.Data.Sqlite is a test-project dependency only:
/// the shipped Windows app uses the JSON queue (DocumentJobQueue), whose equivalent reliability is
/// covered by DocumentJobQueueParityTests. These tests assert Mac SQLite journal semantics
/// (a SQLITE_BUSY journal query aborts startup recovery before any staged file is touched, then a
/// clean retry restores the files and queue rows); they do not assert Windows-specific behaviour.
/// </summary>
public sealed class DocumentJobSQLiteParityTests
{
    [Fact]
    public void JournalRowReaderThrowsInsteadOfTreatingSQLiteErrorsAsEndOfRows()
    {
        var results = new Queue<int>([SqliteRow, SqliteRow, SqliteDone]);
        var rowValue = 0;
        var rows = SQLiteRowReader.Collect(
            step: () => results.Dequeue(),
            makeRow: () => ++rowValue);
        Assert.Equal(new[] { 1, 2 }, rows);

        results = new Queue<int>([SqliteRow, SqliteBusy]);
        rowValue = 0;
        var receivedBusy = false;
        try
        {
            _ = SQLiteRowReader.Collect(step: () => results.Dequeue(), makeRow: () => ++rowValue);
        }
        catch (SQLiteQueryFailure error)
        {
            receivedBusy = error.ResultCode == SqliteBusy;
        }
        Assert.True(receivedBusy);
        Assert.Equal(1, rowValue);
    }

    [Fact]
    public void FailedJournalQueryAbortsStartupRecoveryBeforeTouchingStagedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "journal-query-failure-" + Guid.NewGuid().ToString("N"));
        var databaseUrl = Path.Combine(root, "Preparation", "jobs.sqlite");
        var stagingRoot = Path.Combine(root, ".workspace-delete");
        var source = Path.Combine(root, "Workspaces", "workspace.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "workspace");

            var job = new MacDocumentJob
            {
                WorkspaceId = Guid.NewGuid(),
                FileName = "document.pdf",
                CreatedAt = DateTimeOffset.UnixEpoch
            };
            var moves = MacFileStaging.Manifest([source], stagingRoot);
            var journal = new MacJournal
            {
                WorkspaceId = job.WorkspaceId,
                StagingRoot = stagingRoot,
                Jobs = [job],
                Moves = moves
            };
            MacDocumentJobSQLite.Save(databaseUrl, job);
            MacDocumentJobSQLite.StageWorkspaceDeletion(databaseUrl, journal);
            MacFileStaging.Stage(moves);

            var failedClosed = false;
            try
            {
                MacDocumentJobSQLite.RecoverWorkspaceDeletionTransactions(
                    databaseUrl, () => throw new SQLiteQueryFailure(SqliteBusy));
            }
            catch (SQLiteQueryFailure error)
            {
                failedClosed = error.ResultCode == SqliteBusy;
            }
            Assert.True(failedClosed);
            Assert.False(File.Exists(source));
            Assert.True(File.Exists(moves[0].Staged));
            Assert.Empty(MacDocumentJobSQLite.All(databaseUrl));
            var journals = MacDocumentJobSQLite.WorkspaceDeletionJournals(databaseUrl);
            Assert.Equal(JsonSerializer.Serialize(journal), JsonSerializer.Serialize(Assert.Single(journals)));

            MacDocumentJobSQLite.RecoverWorkspaceDeletionTransactions(databaseUrl);
            Assert.True(File.Exists(source));
            Assert.Equal(new[] { job }, MacDocumentJobSQLite.All(databaseUrl));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* best effort */ }
        }
    }
}
