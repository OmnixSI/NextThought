using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for merging series imported from a backup.</summary>
public sealed class BackupWorkspaceMergeTests
{
    private static WorkspaceSeries Series(string id, string name, DateTimeOffset created, params WorkspaceLesson[] lessons) =>
        new(id, name, created, ["s1"], lessons);

    [Fact]
    public void NewSeriesAreAdded()
    {
        var result = BackupMerge.Workspaces([], [Series("a", "系列 A", DateTimeOffset.UtcNow)]);
        Assert.Single(result.Merged);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void NewerIncomingWinsAndUnionsKnowledge()
    {
        var older = Series("a", "旧名", DateTimeOffset.UtcNow.AddDays(-5),
            new WorkspaceLesson { Id = "l1", Text = "旧经验", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5) });
        var newer = Series("a", "新名", DateTimeOffset.UtcNow.AddDays(-1),
            new WorkspaceLesson { Id = "l2", Text = "新经验", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1) })
            with
        {
            SessionIds = ["s2"],
            SourceDeletionTombstones = ["s3"]
        };
        var result = BackupMerge.Workspaces([older], [newer]);
        var merged = Assert.Single(result.Merged);
        Assert.Equal("新名", merged.Name);
        Assert.Equal(["s1", "s2"], merged.SessionIds);
        Assert.Equal(["旧经验", "新经验"], merged.Lessons.Select(lesson => lesson.Text));
        Assert.Equal(["s3"], merged.SourceDeletionTombstones);
        Assert.Equal(1, result.Updated);
    }

    [Fact]
    public void OlderIncomingDoesNotOverwriteNewerWorkButStillUnions()
    {
        var current = Series("a", "本地新名", DateTimeOffset.UtcNow,
            new WorkspaceLesson { Id = "l1", Text = "本地经验" }) with { UseKnowledge = true };
        var oldBackup = Series("a", "备份旧名", DateTimeOffset.UtcNow.AddDays(-10),
            new WorkspaceLesson { Id = "l0", Text = "备份经验", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10) })
            with { UseKnowledge = false };
        var merged = Assert.Single(BackupMerge.Workspaces([current], [oldBackup]).Merged);
        Assert.Equal("本地新名", merged.Name);
        Assert.True(merged.UseKnowledge);
        Assert.Equal(["本地经验", "备份经验"], merged.Lessons.Select(lesson => lesson.Text));
    }

    [Fact]
    public void TombstonesRemoveKnowledgeResurrectedByAnOldBackup()
    {
        var current = Series("a", "当前", DateTimeOffset.UtcNow) with
        {
            SourceDeletionTombstones = ["old-meeting"],
            QuestionBank = [new QuestionBankItem { Id = "q1", SourceSessionId = "keep" }]
        };
        var backup = Series("a", "旧备份", DateTimeOffset.UtcNow.AddDays(-3)) with
        {
            SessionIds = ["old-meeting", "keep"],
            QuestionBank =
            [
                new QuestionBankItem { Id = "q1", SourceSessionId = "keep" },
                new QuestionBankItem { Id = "q2", SourceSessionId = "old-meeting" }
            ],
            KnowledgeCandidates = [new WorkspaceKnowledgeCandidate { Id = "c1", SourceSessionId = "old-meeting" }]
        };
        var merged = Assert.Single(BackupMerge.Workspaces([current], [backup]).Merged);
        Assert.DoesNotContain("old-meeting", merged.SessionIds);
        Assert.DoesNotContain(merged.QuestionBank, question => question.Id == "q2");
        Assert.Single(merged.QuestionBank);
        Assert.Empty(merged.KnowledgeCandidates);
    }

    [Fact]
    public void IdenticalSeriesAreSkipped()
    {
        var series = Series("a", "系列", DateTimeOffset.UtcNow);
        var result = BackupMerge.Workspaces([series], [series]);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void UnionDeduplicatesById()
    {
        var existing = Series("a", "系列", DateTimeOffset.UtcNow,
            new WorkspaceLesson { Id = "l1", Text = "一" }) with
        {
            KnowledgeCandidates = [new WorkspaceKnowledgeCandidate { Id = "c1", SourceSessionId = "s1" }]
        };
        var incoming = existing with
        {
            Lessons = [new WorkspaceLesson { Id = "l1", Text = "重复" }, new WorkspaceLesson { Id = "l2", Text = "二" }],
            KnowledgeCandidates =
            [
                new WorkspaceKnowledgeCandidate { Id = "c1", SourceSessionId = "s1" },
                new WorkspaceKnowledgeCandidate { Id = "c2", SourceSessionId = "s1" }
            ]
        };
        var touched = incoming with { CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        var merged = Assert.Single(BackupMerge.Workspaces([existing], [touched]).Merged);
        Assert.Equal(2, merged.Lessons.Count);
        Assert.Equal(2, merged.KnowledgeCandidates.Count);
        Assert.Single(merged.KnowledgeCandidates, candidate => candidate.Id == "c1");
    }
}
