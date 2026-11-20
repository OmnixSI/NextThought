using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B4: translated from WorkspaceTests.swift staleBackupCannotRestoreDefaultDeletedLessonsOrTheirSourceIDs.</summary>
public sealed class BackupMergeTests
{
    [Fact]
    public void StaleBackupCannotRestoreDefaultDeletedLessonsOrTheirSourceIds()
    {
        var workspaceId = Guid.NewGuid().ToString("D");
        var deleted = Guid.NewGuid().ToString("D");
        var remaining = Guid.NewGuid().ToString("D");
        var deletedLessonId = Guid.NewGuid().ToString("D");
        var sharedLessonId = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);
        var oldBackup = new WorkspaceSeries(workspaceId, "系列", now, [deleted, remaining],
        [
            new WorkspaceLesson { Id = deletedLessonId, Text = "只由已删除会议支持的经验", SourceSessionId = deleted, SourceSessionIds = [deleted], CreatedAt = now, UpdatedAt = now },
            new WorkspaceLesson { Id = sharedLessonId, Text = "多场会议共同支持的经验", SourceSessionId = deleted, SourceSessionIds = [deleted, remaining], CreatedAt = now, UpdatedAt = now }
        ]);

        var (deletedSeries, changed) = WorkspaceSourceIntegrity.RemoveMeetings(oldBackup, [deleted], now: now.AddSeconds(10));
        Assert.True(changed);
        Assert.Equal([sharedLessonId], deletedSeries.Lessons.Select(lesson => lesson.Id));
        Assert.Equal([remaining], deletedSeries.Lessons[0].SourceSessionIds);
        Assert.Equal([deleted], deletedSeries.SourceDeletionTombstones);

        var staleBackup = oldBackup with
        {
            Lessons =
            [
                oldBackup.Lessons[0] with { Revision = 5, Text = "已删除会议中的更新经验", UpdatedAt = now.AddSeconds(100) },
                oldBackup.Lessons[1] with { Revision = 7, UpdatedAt = now.AddSeconds(100) }
            ]
        };

        var merged = Assert.Single(BackupMerge.Workspaces([deletedSeries], [staleBackup]).Merged);
        Assert.Equal([sharedLessonId], merged.Lessons.Select(lesson => lesson.Id));
        Assert.Equal([remaining], merged.Lessons[0].SourceSessionIds);
        Assert.Equal(remaining, merged.Lessons[0].SourceSessionId);
        Assert.Equal([deleted], merged.SourceDeletionTombstones);
        var encoded = System.Text.Json.JsonSerializer.Serialize(merged, JsonDefaults.Options);
        Assert.DoesNotContain("已删除会议中的更新经验", encoded);

        var replayed = Assert.Single(BackupMerge.Workspaces([merged], [staleBackup]).Merged);
        Assert.Equal(merged, replayed);
    }

    [Fact]
    public void MergeKeepsLessonDeletionTombstonesAuthoritativeAndIdempotent()
    {
        var workspaceId = Guid.NewGuid().ToString("D");
        var lessonId = Guid.NewGuid().ToString("D");
        var deletedSession = Guid.NewGuid().ToString("D");
        var createdAt = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000).AddSeconds(-100);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);
        var original = new WorkspaceSeries(workspaceId, "系列", createdAt, [deletedSession],
        [
            new WorkspaceLesson { Id = lessonId, Text = "保留的经验", SourceSessionId = deletedSession,
                SourceSessionIds = [deletedSession], CreatedAt = createdAt, UpdatedAt = now, Revision = 1 }
        ]);
        var (retained, changed) = WorkspaceSourceIntegrity.RemoveMeetings(original, [deletedSession],
            retainAcceptedContent: true, now: now.AddSeconds(10));
        Assert.True(changed);
        var retainedLesson = Assert.Single(retained.Lessons);
        Assert.True(retainedLesson.IndependentlyRetained);
        Assert.Null(retainedLesson.SourceSessionId);
        Assert.Null(retainedLesson.SourceSessionIds);
        Assert.Equal([deletedSession], retainedLesson.SourceMissingSessionIds);

        var merged = Assert.Single(BackupMerge.Workspaces([retained], [original]).Merged);
        var mergedLesson = Assert.Single(merged.Lessons);
        Assert.Equal("保留的经验", mergedLesson.Text);
        Assert.Null(mergedLesson.SourceSessionId);
        Assert.Null(mergedLesson.SourceSessionIds);
        Assert.Equal([deletedSession], mergedLesson.SourceMissingSessionIds);
        Assert.True(mergedLesson.IndependentlyRetained);
        Assert.Single(mergedLesson.History);
        Assert.Null(mergedLesson.History[0].SourceSessionId);
        Assert.Null(mergedLesson.History[0].SourceSessionIds);
        Assert.Equal([deletedSession], mergedLesson.History[0].SourceMissingSessionIds);

        var replayed = Assert.Single(BackupMerge.Workspaces([merged], [original]).Merged);
        Assert.Equal(merged, replayed);
    }


    [Fact]
    public void MergeDoesNotResurrectDefaultDeletedSeriesContent()
    {
        var workspaceId = Guid.NewGuid().ToString("D");
        var itemId = Guid.NewGuid().ToString("D");
        var deletedSession = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);
        var acceptedAt = now.AddSeconds(-30);
        var receipt = new WorkspaceSeriesChangeReceipt(Guid.NewGuid(), "decision-candidate",
            WorkspaceSeriesChangeDecision.Add, acceptedAt, itemId);
        var accepted = new WorkspaceSeriesChangeCandidate
        {
            Id = "decision-candidate", Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "改为周一提交", SourceSessionId = deletedSession, EvidenceId = "decision-1",
            ClaimRevision = "claim-1", SourceRevision = "line-1", SourceQuote = "周一之前提交",
            Location = "01:25", TranscriptSourceRevision = "transcript-1",
            Status = WorkspaceSeriesChangeCandidate.StatusValue.Accepted, Receipt = receipt, CreatedAt = acceptedAt
        };
        var oldItem = new WorkspaceSeriesItem
        {
            Id = itemId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "改为周一提交",
            SourceSessionId = deletedSession, EvidenceId = "decision-1", ClaimRevision = "claim-1",
            SourceRevision = "line-1", SourceQuote = "周一之前提交", Location = "01:25",
            TranscriptSourceRevision = "transcript-1", ConfirmedAt = acceptedAt, UpdatedAt = acceptedAt
        };
        var oldBackup = new WorkspaceSeries(workspaceId, "系列", acceptedAt, [], [])
        {
            SeriesItems = [oldItem], SeriesChangeCandidates = [accepted]
        };
        var (deleted, changed) = WorkspaceSourceIntegrity.RemoveMeetings(oldBackup, [deletedSession], now: now.AddSeconds(10));
        Assert.True(changed);
        Assert.Empty(deleted.SeriesItems);
        var receiptTombstone = Assert.Single(deleted.SeriesChangeCandidates);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Accepted, receiptTombstone.Status);
        Assert.Equal(receipt, receiptTombstone.Receipt);
        Assert.True(receiptTombstone.SourceMissing);
        Assert.Equal("", receiptTombstone.Text);
        Assert.Null(receiptTombstone.SourceQuote);

        var concurrentItem = oldItem with
        {
            Text = "仍按周五提交", Revision = 4, UpdatedAt = now.AddSeconds(100), SourceQuote = "周五之前提交"
        };
        var concurrentCandidate = accepted with
        {
            Text = "仍按周五提交", SourceQuote = "周五之前提交",
            Receipt = new WorkspaceSeriesChangeReceipt(Guid.NewGuid(), accepted.Id,
                WorkspaceSeriesChangeDecision.Replace, now.AddSeconds(100), itemId)
        };
        var concurrentBackup = new WorkspaceSeries(workspaceId, "系列备份", now.AddSeconds(100), [], [])
        {
            SeriesItems = [concurrentItem], SeriesChangeCandidates = [concurrentCandidate]
        };
        var merged = Assert.Single(BackupMerge.Workspaces([deleted], [concurrentBackup]).Merged);
        Assert.Empty(merged.SeriesItems);
        var mergedTombstone = Assert.Single(merged.SeriesChangeCandidates);
        Assert.True(mergedTombstone.SourceMissing);
        Assert.Equal("", mergedTombstone.Text);
        Assert.Null(mergedTombstone.SourceQuote);
        Assert.Null(mergedTombstone.TranscriptSourceRevision);
        Assert.Equal(itemId, mergedTombstone.Receipt?.ResultItemId);

        var replayed = Assert.Single(BackupMerge.Workspaces([merged], [concurrentBackup]).Merged);
        Assert.Equal(merged, replayed);
    }

}
