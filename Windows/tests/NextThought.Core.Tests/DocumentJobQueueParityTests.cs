using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B20: queue recovery, tombstones and no silent loss of unfinished jobs.</summary>
public sealed class DocumentJobQueueParityTests
{
    private static string TempDir()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nt-jobs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static DocumentJob Job(string name, DocumentJob.StageValue stage = DocumentJob.StageValue.Receiving) =>
        DocumentJob.Create(Guid.NewGuid().ToString("D"), name) with
        {
            Stage = stage,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000),
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)
        };

    [Fact]
    public void ReopeningResetsUnfinishedStagesButKeepsProgressAndFailures()
    {
        var dir = TempDir();
        var first = Job("paper.pdf") with
        {
            Stage = DocumentJob.StageValue.Ocr, Completed = 17, Total = 80
        };
        var failed = Job("bad.pdf") with
        {
            Stage = DocumentJob.StageValue.Failed, Message = "PDF 已加密"
        };
        var queue = new DocumentJobQueue(dir);
        Assert.True(queue.Enqueue(first));
        Assert.True(queue.Enqueue(failed));

        var reopened = new DocumentJobQueue(dir);
        var recovered = reopened.AllJobs(recoverInterrupted: true);
        Assert.Equal(2, recovered.Count);
        var recoveredFirst = Assert.Single(recovered, job => job.Id == first.Id);
        Assert.Equal(DocumentJob.StageValue.Queued, recoveredFirst.Stage);
        Assert.Equal(17, recoveredFirst.Completed);
        var recoveredFailed = Assert.Single(recovered, job => job.Id == failed.Id);
        Assert.Equal(DocumentJob.StageValue.Failed, recoveredFailed.Stage);
        Assert.Equal("PDF 已加密", recoveredFailed.Message);
    }

    [Fact]
    public void DeletedRowsAreTombstonedAndCanBeRestored()
    {
        var dir = TempDir();
        var queue = new DocumentJobQueue(dir);
        var job = Job("meeting.pdf", DocumentJob.StageValue.Ready) with
        {
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(100)
        };
        Assert.True(queue.Enqueue(job));
        Assert.True(queue.Delete([job.Id]));
        Assert.Empty(queue.All);

        var late = job with { Message = "late save", UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(200) };
        queue.Update(late);
        Assert.Empty(queue.All);

        Assert.True(queue.RestoreDeleted([job]));
        Assert.Equal(job, Assert.Single(queue.All));
    }

    [Fact]
    public void DamagedQueueFileIsNeverOverwritten()
    {
        var dir = TempDir();
        var path = System.IO.Path.Combine(dir, "document-jobs.json");
        File.WriteAllText(path, "{not json");
        var queue = new DocumentJobQueue(dir);
        Assert.Throws<InvalidDataException>(() => queue.Enqueue(Job("paper.pdf")));
        Assert.Equal("{not json", File.ReadAllText(path));
    }

    [Fact]
    public void CapacityPrunesOnlyTerminalRows()
    {
        var dir = TempDir();
        var queue = new DocumentJobQueue(dir);
        for (var index = 0; index < 200; index++)
        {
            var terminal = Job("done-" + index, DocumentJob.StageValue.Ready) with
            {
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 + index),
                UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 + index)
            };
            queue.Enqueue(terminal);
        }
        for (var index = 0; index < 5; index++)
        {
            var running = Job("running-" + index, DocumentJob.StageValue.Ocr) with
            {
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000 + index),
                UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000 + index)
            };
            queue.Enqueue(running);
        }
        Assert.Equal(200, queue.Count);
        Assert.Equal(5, queue.All.Count(job => !job.IsTerminal));
    }
}
