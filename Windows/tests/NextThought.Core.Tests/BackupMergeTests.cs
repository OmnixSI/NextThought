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
}
