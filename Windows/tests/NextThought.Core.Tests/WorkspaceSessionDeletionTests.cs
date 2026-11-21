using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for removing a meeting from a series while keeping knowledge honest.</summary>
public sealed class WorkspaceSessionDeletionTests
{
    private static readonly string Deleted = Guid.NewGuid().ToString("D");
    private static readonly string Kept = Guid.NewGuid().ToString("D");

    private static WorkspaceSeries Series(params WorkspaceLesson[] lessons) => new(
        "series", "组会", DateTimeOffset.UtcNow, [Deleted, Kept], lessons)
    {
        KnowledgeCandidates =
        [
            new WorkspaceKnowledgeCandidate { SourceSessionId = Deleted, Status = WorkspaceKnowledgeStatus.Pending },
            new WorkspaceKnowledgeCandidate { SourceSessionId = Kept, Status = WorkspaceKnowledgeStatus.Pending }
        ],
        QuestionBank =
        [
            new QuestionBankItem { Id = "q-deleted", Question = "删除的问题", SourceSessionId = Deleted },
            new QuestionBankItem { Id = "q-kept", Question = "保留的问题", SourceSessionId = Kept }
        ],
        SeriesItems =
        [
            new WorkspaceSeriesItem
            {
                Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "决定", SourceSessionId = Deleted,
                EvidenceId = "e", ClaimRevision = "c", SourceRevision = "s", SourceQuote = "原话", Location = "05:20"
            },
            new WorkspaceSeriesItem
            {
                Kind = WorkspaceSeriesItem.KindValue.Action, Text = "待办", SourceSessionId = Kept,
                EvidenceId = "e2", ClaimRevision = "c", SourceRevision = "s"
            }
        ]
    };

    [Fact]
    public void QuestionsFromTheDeletedMeetingLeaveTheBank()
    {
        var result = WorkspaceSessionDeletion.Apply(Series(), Deleted);
        var question = Assert.Single(result.Series.QuestionBank);
        Assert.Equal("q-kept", question.Id);
        Assert.Equal(1, result.RemovedQuestions);
        Assert.Contains(Deleted, result.Series.SourceDeletionTombstones!);
    }

    [Fact]
    public void NonMemberIsUnchanged()
    {
        var result = WorkspaceSessionDeletion.Apply(Series(), Guid.NewGuid().ToString("D"));
        Assert.Equal(0, result.RemovedSessions);
        Assert.Equal(2, result.Series.SessionIds.Count);
    }

    [Fact]
    public void MembershipIsRemovedAndTombstonedOnce()
    {
        var first = WorkspaceSessionDeletion.Apply(Series(), Deleted);
        Assert.Single(first.Series.SessionIds);
        Assert.Equal([Deleted], first.Series.SourceDeletionTombstones);
        Assert.Equal(1, first.RemovedSessions);
        var second = WorkspaceSessionDeletion.Apply(first.Series, Kept);
        Assert.Equal([Deleted, Kept], second.Series.SourceDeletionTombstones);
    }

    [Fact]
    public void SingleSourceLessonsDisappearButSharedOnesRemain()
    {
        var only = new WorkspaceLesson { Text = "只有这场", SourceSessionId = Deleted, SourceSessionIds = [Deleted] };
        var shared = new WorkspaceLesson { Text = "两场都有", SourceSessionIds = [Deleted, Kept], SourceSessionId = Deleted };
        var result = WorkspaceSessionDeletion.Apply(Series(only, shared), Deleted);
        Assert.Equal(1, result.RemovedLessons);
        var lesson = Assert.Single(result.Series.Lessons);
        Assert.Equal("两场都有", lesson.Text);
        Assert.Equal([Kept], lesson.SourceSessionIds);
        Assert.Equal(Kept, lesson.SourceSessionId);
    }

    [Fact]
    public void RetainedLessonsKeepTheirTextAndMarkTheMissingSource()
    {
        var only = new WorkspaceLesson { Text = "手动保留", SourceSessionId = Deleted, SourceSessionIds = [Deleted] };
        var result = WorkspaceSessionDeletion.Apply(Series(only), Deleted, retainLessons: true);
        Assert.Equal(0, result.RemovedLessons);
        var lesson = Assert.Single(result.Series.Lessons);
        Assert.True(lesson.IndependentlyRetained);
        Assert.Equal([Deleted], lesson.SourceMissingSessionIds);
        Assert.Equal("手动保留", lesson.Text);
    }

    [Fact]
    public void CandidatesFromTheDeletedMeetingAreDropped()
    {
        var result = WorkspaceSessionDeletion.Apply(Series(), Deleted);
        Assert.Equal(1, result.RemovedCandidates);
        var candidate = Assert.Single(result.Series.KnowledgeCandidates);
        Assert.Equal(Kept, candidate.SourceSessionId);
    }

    [Fact]
    public void ItemsKeepTextButLoseTheirQuoteAndLocation()
    {
        var result = WorkspaceSessionDeletion.Apply(Series(), Deleted);
        Assert.Equal(1, result.MarkedItems);
        var decision = result.Series.SeriesItems[0];
        Assert.True(decision.SourceMissing);
        Assert.Null(decision.SourceQuote);
        Assert.Null(decision.Location);
        Assert.Equal("决定", decision.Text);
        Assert.NotEqual(true, result.Series.SeriesItems[1].SourceMissing);
    }

    [Fact]
    public void QuestionBankSurvivesDeletion()
    {
        var series = Series() with { QuestionBank = [new QuestionBankItem { Question = "问", Answer = "答" }] };
        var result = WorkspaceSessionDeletion.Apply(series, Deleted);
        Assert.Single(result.Series.QuestionBank);
    }
}
