using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Swift parity: Tests/NextThoughtCoreTests/DocumentJobTests.swift.
/// The Windows port replaces the Mac SQLite store with the durable JSON <see cref="DocumentJobQueue"/>.
/// </summary>
public sealed class DocumentJobParityTests
{
    [Fact]
    public void JobsKeepWorkspaceAndRecoverWithoutLosingFailures()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-document-job-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DocumentJobQueue(root);
            var workspace = Guid.NewGuid().ToString("D");
            var first = DocumentJob.Create(workspace, "paper.pdf") with
            {
                OriginalPath = "/selected/original.pdf",
                Stage = DocumentJob.StageValue.Ocr,
                Completed = 17,
                Total = 80
            };
            var failed = DocumentJob.Create(Guid.NewGuid().ToString("D"), "bad.pdf") with
            {
                Stage = DocumentJob.StageValue.Failed,
                Message = "PDF 已加密"
            };
            Assert.True(store.Enqueue(first));
            Assert.True(store.Enqueue(failed));

            var reopened = new DocumentJobQueue(root);
            var recovered = reopened.AllJobs(recoverInterrupted: true);
            Assert.Equal(2, recovered.Count);
            Assert.Equal(workspace, recovered.Single(job => job.Id == first.Id).WorkspaceId);
            Assert.Equal(DocumentJob.StageValue.Queued, recovered.Single(job => job.Id == first.Id).Stage);
            Assert.Equal(17, recovered.Single(job => job.Id == first.Id).Completed);
            Assert.Equal("PDF 已加密", recovered.Single(job => job.Id == failed.Id).Message);
            Assert.Equal(DocumentJob.StageValue.Failed, recovered.Single(job => job.Id == failed.Id).Stage);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact(Skip = "missing API: DocumentJobStore.all(recoverInterrupted:) preserving a committed document's .embedding stage (C# DocumentJobQueue.AllJobs resets every non-terminal stage to Queued)")]
    public void CommittedDocumentResumesAtEmbeddingInsteadOfDuplicateImport()
    {
    }

    [Fact]
    public void StagedWorkspaceDeletionCanRestoreQueueRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "nt-document-job-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DocumentJobQueue(root);
            var job = DocumentJob.Create(Guid.NewGuid().ToString("D"), "meeting.pdf") with
            {
                OriginalPath = "/selected/meeting.pdf",
                Stage = DocumentJob.StageValue.Ready,
                UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(100)
            };
            Assert.True(store.Enqueue(job));

            Assert.True(store.Delete([job.Id]));
            var staleSave = job with
            {
                Message = "late save",
                UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(200)
            };
            Assert.False(store.Update(staleSave));
            Assert.Empty(store.All);

            Assert.True(store.RestoreDeleted([job]));
            Assert.Equal([job], store.All);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact(Skip = "missing API: SQLiteRowReader.collect(step:makeRow:)")]
    public void JournalRowReaderThrowsInsteadOfTreatingSQLiteErrorsAsEndOfRows()
    {
    }

    [Fact(Skip = "missing API: DocumentJobStore.recoverWorkspaceDeletionTransactions(at:readJournals:)")]
    public void FailedJournalQueryAbortsStartupRecoveryBeforeTouchingStagedFiles()
    {
    }

    [Fact(Skip = "missing API: DocumentContent.rawPages")]
    public void OldDocumentContentDecodesWithoutNewRawText()
    {
    }
}
