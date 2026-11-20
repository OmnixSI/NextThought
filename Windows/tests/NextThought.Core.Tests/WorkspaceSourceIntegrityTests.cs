using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>B3: translated from WorkspaceTests.swift deletingMeetingSourcesRemovesDependentKnowledgeAndKeepsOtherSources.</summary>
public sealed class WorkspaceSourceIntegrityTests
{
    [Fact]
    public void DeletingMeetingSourcesRemovesDependentKnowledgeAndKeepsOtherSources()
    {
        var deleted = Guid.NewGuid().ToString("D");
        var remaining = Guid.NewGuid().ToString("D");
        var unrelated = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000);
        var deletedRevision = new WorkspaceAnswerRevision
        {
            Answer = "已删除来源旧答法", RecordedAt = now, SourceSessionId = deleted
        };
        var remainingRevision = new WorkspaceAnswerRevision
        {
            Answer = "仍有效的历史答法", RecordedAt = now, SourceSessionId = remaining
        };
        var friday = new WorkspaceSeriesItemRevision
        {
            Revision = 1, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "暂定周五",
            SourceSessionId = deleted, EvidenceId = "friday", ClaimRevision = "claim-friday",
            SourceRevision = "line-friday", SourceQuote = "周五原话", Location = "00:12"
        };
        var workspace = new WorkspaceSeries("series", "项目交付", now,
            [deleted, remaining, unrelated],
            [
                new WorkspaceLesson { Text = "只被删会议中总结的经验", SourceSessionId = deleted, CreatedAt = now, UpdatedAt = now },
                new WorkspaceLesson
                {
                    Text = "多场会议共同支持的经验", SourceSessionId = deleted,
                    SourceSessionIds = [deleted, remaining], CreatedAt = now, UpdatedAt = now
                }
            ])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Question = "被删会议独有的问题？", Answer = "被删会议独有答法",
                    SourceSessionId = deleted, SourceSessionIds = [deleted], AnswerSourceSessionId = deleted
                },
                new QuestionBankItem
                {
                    Question = "多场会议重复提问？", Answer = "由被删会议提供的答法", TimesAsked = 2,
                    SourceSessionId = deleted, SourceSessionIds = [deleted, remaining],
                    AnswerSourceSessionId = deleted, AnswerHistory = [deletedRevision, remainingRevision]
                },
                new QuestionBankItem
                {
                    Question = "仍有效的问题？", Answer = "有效来源的答法", TimesAsked = 2,
                    SourceSessionId = deleted, SourceSessionIds = [deleted, remaining],
                    AnswerSourceSessionId = remaining
                }
            ],
            KnowledgeCandidates =
            [
                new WorkspaceKnowledgeCandidate
                {
                    Id = "deleted", LogicalItemId = "deleted", Kind = WorkspaceKnowledgeKind.Lesson,
                    Text = "待审阅经验", SourceSessionId = deleted
                },
                new WorkspaceKnowledgeCandidate
                {
                    Id = "other", LogicalItemId = "other", Kind = WorkspaceKnowledgeKind.Lesson,
                    Text = "其他会议经验", SourceSessionId = unrelated
                }
            ],
            SeriesItems =
            [
                new WorkspaceSeriesItem
                {
                    Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "正式改为周一",
                    SourceSessionId = deleted, EvidenceId = "monday", ClaimRevision = "claim-monday",
                    SourceRevision = "line-monday", SourceQuote = "周一原话", Location = "01:20",
                    TranscriptSourceRevision = "transcript-monday", Revision = 2, History = [friday]
                }
            ],
            SeriesChangeCandidates =
            [
                new WorkspaceSeriesChangeCandidate
                {
                    Id = "deleted-series", Kind = WorkspaceSeriesItem.KindValue.Action, Text = "已失效的行动",
                    SourceSessionId = deleted, EvidenceId = "action", ClaimRevision = "claim",
                    SourceRevision = "line", TranscriptSourceRevision = "transcript"
                }
            ]
        };

        var (after, changed) = WorkspaceSourceIntegrity.RemoveMeetings(workspace, [deleted], now: now.AddSeconds(1));
        Assert.True(changed);
        Assert.Single(after.Lessons);
        Assert.Equal("多场会议共同支持的经验", after.Lessons[0].Text);
        Assert.Equal(remaining, after.Lessons[0].SourceSessionId);
        Assert.Equal([remaining], after.Lessons[0].SourceSessionIds);
        Assert.Contains("多场会议共同支持的经验", after.ExperienceNotes());
        Assert.DoesNotContain("只被删会议中总结的经验", after.ExperienceNotes());

        Assert.Equal(2, after.QuestionBank.Count);
        Assert.Equal("多场会议重复提问？", after.QuestionBank[0].Question);
        Assert.Equal([remaining], after.QuestionBank[0].SourceSessionIds);
        Assert.Equal(1, after.QuestionBank[0].TimesAsked);
        Assert.Equal("", after.QuestionBank[0].Answer);
        Assert.Equal("仍有效的历史答法", Assert.Single(after.QuestionBank[0].AnswerHistory!).Answer);
        Assert.Equal("有效来源的答法", after.QuestionBank[1].Answer);

        Assert.Equal("other", Assert.Single(after.KnowledgeCandidates).Id);
        var tombstone = Assert.Single(after.SeriesChangeCandidates);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Superseded, tombstone.Status);
        Assert.True(tombstone.SourceMissing);
        Assert.Equal("", tombstone.Text);
        Assert.Null(tombstone.SourceQuote);
        Assert.Null(tombstone.TranscriptSourceRevision);
        Assert.Empty(after.SeriesItems);
        Assert.Contains(deleted, after.SourceDeletionTombstones!);

        var (again, changedAgain) = WorkspaceSourceIntegrity.RemoveMeetings(after, [deleted], now: now.AddSeconds(2));
        Assert.False(changedAgain);
        Assert.Equal(after, again);
    }
}
