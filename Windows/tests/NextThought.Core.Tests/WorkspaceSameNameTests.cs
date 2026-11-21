using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// J9: the WorkspaceTests.swift tests whose C# counterpart had a different method name. Each test
/// keeps the Swift name and the Swift intent; the C# port is functional (series records are
/// immutable and returned) instead of the Swift inout style, and ids are strings instead of UUIDs.
/// </summary>
public sealed class WorkspaceSameNameTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_900_000_000);

    private static string NewId() => Guid.NewGuid().ToString("D");

    private static WorkspaceSeries Series(string name = "系列", params WorkspaceLesson[] lessons) =>
        WorkspaceSeries.Create(name) with { Lessons = lessons };

    /// <summary>Value comparison for records that hold lists (record equality compares lists by reference).</summary>
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonDefaults.Options);

    // --- modes and materials -------------------------------------------------

    [Fact]
    public void LegacyDefaultAnswerRequirementsFollowModeChanges()
    {
        var oldDefaults = new (MeetingMode Mode, string Value)[]
        {
            (MeetingMode.Meeting,
                "我是会议参与者。回答聚焦决策、风险和下一步行动，给出可以直接说出口的简短发言；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。"),
            (MeetingMode.Client,
                "我是我方代表，正在和客户沟通。回答聚焦客户的需求和顾虑，给出可以直接说出口的简短回应；涉及价格、时间和承诺时以资料为准，不确定的说“会后确认”，不替公司做资料里没有的承诺。"),
            (MeetingMode.General,
                "直接回答当前问题：先给一句可以说出口的结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。")
        };
        foreach (var (mode, oldValue) in oldDefaults)
        {
            var profile = ModeProfile.For(mode);
            Assert.True(profile.MatchesDefaultTemplatePreferences(oldValue));
            Assert.True(profile.MatchesDefaultTemplatePreferences(profile.TemplatePreferences));
            Assert.False(profile.MatchesDefaultTemplatePreferences("我自己的回答要求"));
        }
    }

    [Fact]
    public void WorkspaceDecodesOlderFilesAndBuildsMaterial()
    {
        var id = NewId();
        var json = $$"""{"id":"{{id}}","name":"字节二面","mode":"interview","createdAt":"2026-01-01T00:00:00Z","lessons":[]}""";
        var workspace = JsonSerializer.Deserialize<WorkspaceSeries>(json, JsonDefaults.Options)!;
        Assert.Equal("字节二面", workspace.Name);
        Assert.True(workspace.UseKnowledge);
        var prepared = workspace with
        {
            Fields = new Dictionary<string, string>
            {
                ["company"] = "字节跳动",
                ["position"] = "后端工程师",
                ["legacy"] = "旧字段"
            },
            Notes = "重点准备缓存"
        };
        var text = prepared.MaterialText();
        Assert.StartsWith("应聘公司：字节跳动\n岗位：后端工程师", text);
        Assert.Contains("legacy：旧字段", text);
        Assert.EndsWith("重点准备缓存", text);
    }

    [Fact]
    public void QuestionBankIsSearchableAndCategoriesLabelSources()
    {
        var workspace = WorkspaceSeries.Create("面试") with
        {
            Documents =
            [
                new ContextDocument { Name = "resume.pdf", Pages = ["负责订单服务 Redis 缓存层"], Category = "简历" }
            ],
            QuestionBank =
            [
                new QuestionBankItem { Question = "缓存和数据库怎么保证一致", Answer = "先更新数据库再删除缓存，binlog 兜底" }
            ]
        };
        var index = new SessionContextIndex(workspace.RetrievalDocuments);
        var hits = index.Retrieve("你们缓存一致性怎么保证");
        Assert.Contains(hits, hit => hit.Source == "题库（历次复盘）");
        Assert.Equal("resume.pdf", index.Retrieve("Redis 缓存层").First().Source);
        var disabled = workspace with { UseKnowledge = false };
        Assert.Single(disabled.RetrievalDocuments);
        Assert.Equal("", disabled.ExperienceNotes());
    }

    // --- knowledge candidates -------------------------------------------------

    [Fact]
    public void KnowledgeCandidateAcceptanceRequiresItsArchivedSummaryAndTranscriptRevision()
    {
        var session = NewId();
        var revision = "transcript-v1";
        var summary = new SessionSummary
        {
            Title = "复盘",
            TranscriptSourceRevision = revision,
            Lessons = ["先说结论，再展开证据"],
            Bank = [new BankEntry { Question = "如何处理缓存击穿？", Answer = "互斥重建" }]
        };
        var bank = summary.Bank!.Select(entry => new QuestionBankEntry(entry.Question, entry.Answer)).ToList();
        var (collected, added) = KnowledgeMerger.CollectCandidates(Series("面试"), summary.Lessons!.ToList(), bank,
            session, Now, revision);
        var candidate = collected.KnowledgeCandidates!.First(item => item.Id == added[0]);
        Assert.True(KnowledgeMerger.MatchesCurrentSummary(candidate, summary, session, revision));
        Assert.False(KnowledgeMerger.MatchesCurrentSummary(candidate, summary, session, "transcript-v2"));
        var changed = summary with { Lessons = ["新转写对应的新结论"] };
        Assert.False(KnowledgeMerger.MatchesCurrentSummary(candidate, changed, session, revision));
    }

    [Fact]
    public void AcceptedLessonEditsPersistSourceVersionsAndRestoreWithoutTranscriptData()
    {
        var sourceA = NewId();
        var sourceB = NewId();
        var initialTime = Now.AddSeconds(-100);
        var lesson = new WorkspaceLesson
        {
            Id = NewId(),
            Text = "先讲结论",
            SourceSessionId = sourceA,
            SourceSessionIds = [sourceA, sourceB],
            CreatedAt = initialTime,
            UpdatedAt = initialTime
        };

        var edited = WorkspaceLessonHistory.EditText(lesson, "先讲结论，再给证据", Now)!;
        Assert.Equal(2, edited.Revision);
        var oldRevision = Assert.Single(edited.History);
        Assert.Equal(1, oldRevision.Revision);
        Assert.Equal("先讲结论", oldRevision.Text);
        Assert.Equal(sourceA, oldRevision.SourceSessionId);
        Assert.Equal([sourceA, sourceB], oldRevision.SourceSessionIds);

        var encoded = JsonSerializer.SerializeToUtf8Bytes(edited, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<WorkspaceLesson>(encoded, JsonDefaults.Options)!;
        Assert.Equal(Json(edited.History), Json(decoded.History));
        var restored = WorkspaceLessonHistory.Restore(decoded, oldRevision.Id, Now.AddSeconds(10))!;
        Assert.Equal("先讲结论", restored.Text);
        Assert.Equal(3, restored.Revision);
        Assert.Contains(restored.History, revision => revision.Text == "先讲结论，再给证据" && revision.Revision == 2);

        var workspace = Series("项目复盘", restored);
        var (retainedSeries, changed) = WorkspaceSourceIntegrity.RemoveMeetings(workspace, [sourceA, sourceB],
            retainAcceptedContent: true, now: Now.AddSeconds(20));
        Assert.True(changed);
        var retained = Assert.Single(retainedSeries.Lessons);
        Assert.True(retained.IndependentlyRetained);
        Assert.Equal(new[] { sourceA, sourceB }.OrderBy(id => id), (retained.SourceMissingSessionIds ?? []).OrderBy(id => id));
        Assert.Null(retained.SourceSessionId);
        Assert.Null(retained.SourceSessionIds);

        var latestHistory = retained.History.First(revision => revision.Text == "先讲结论，再给证据");
        var persisted = JsonSerializer.SerializeToUtf8Bytes(retainedSeries, JsonDefaults.Options);
        var reopened = JsonSerializer.Deserialize<WorkspaceSeries>(persisted, JsonDefaults.Options)!;
        var reopenedLesson = WorkspaceLessonHistory.Restore(reopened.Lessons[0], latestHistory.Id, Now.AddSeconds(30))!;
        Assert.Equal("先讲结论，再给证据", reopenedLesson.Text);
        Assert.Null(reopenedLesson.SourceSessionId);
        Assert.True(reopenedLesson.IndependentlyRetained);
        Assert.Equal(new[] { sourceA, sourceB }.OrderBy(id => id),
            (reopenedLesson.SourceMissingSessionIds ?? []).OrderBy(id => id));
    }

    [Fact]
    public void AcceptingAnotherSourceCreatesAnAuditableLessonRevision()
    {
        var sourceA = NewId();
        var sourceB = NewId();
        var start = Now.AddSeconds(-10);
        var lesson = new WorkspaceLesson
        {
            Id = NewId(),
            Text = "先讲结论",
            SourceSessionId = sourceA,
            SourceSessionIds = [sourceA],
            CreatedAt = start,
            UpdatedAt = start
        };

        var (added, changed) = WorkspaceLessonHistory.AddSources(lesson, [sourceB], Now);
        Assert.True(changed);
        Assert.Equal(2, added.Revision);
        Assert.Equal(new[] { sourceA, sourceB }.OrderBy(id => id), added.SourceSessionIds!.OrderBy(id => id));
        var old = Assert.Single(added.History);
        Assert.Equal(1, old.Revision);
        Assert.Equal("先讲结论", old.Text);
        Assert.Equal(sourceA, old.SourceSessionId);

        var (again, changedAgain) = WorkspaceLessonHistory.AddSources(added, [sourceB], Now.AddSeconds(1));
        Assert.False(changedAgain);
        Assert.Equal(2, again.Revision);

        var encoded = JsonSerializer.SerializeToUtf8Bytes(added, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<WorkspaceLesson>(encoded, JsonDefaults.Options)!;
        Assert.Equal(Json(added.History), Json(decoded.History));
        Assert.Equal(2, decoded.Revision);
        Assert.Equal(added.SourceSessionIds!.OrderBy(id => id), decoded.SourceSessionIds!.OrderBy(id => id));
    }

    [Fact]
    public void HistoricalLessonSourceKeepsTheDeletionRetentionChoiceAvailable()
    {
        var deletedSession = NewId();
        var remainingSession = NewId();
        var oldRevision = new WorkspaceLessonRevision
        {
            Id = "old", Revision = 1, Text = "旧版经验来自被删除的会议",
            SourceSessionId = deletedSession, SourceSessionIds = [deletedSession],
            RecordedAt = Now.AddSeconds(-20)
        };
        var lesson = new WorkspaceLesson
        {
            Id = NewId(),
            Text = "当前经验由另一场会议支持",
            SourceSessionId = remainingSession,
            SourceSessionIds = [remainingSession],
            CreatedAt = Now.AddSeconds(-10),
            UpdatedAt = Now,
            Revision = 2,
            History = [oldRevision]
        };
        var original = Series("系列", lesson);
        Assert.True(WorkspaceSourceIntegrity.HasRetainableAcceptedContent(original, [deletedSession]));

        var (removed, removedChanged) = WorkspaceSourceIntegrity.RemoveMeetings(original, [deletedSession],
            retainAcceptedContent: false, now: Now.AddSeconds(1));
        Assert.True(removedChanged);
        Assert.Equal(lesson.Text, removed.Lessons[0].Text);
        Assert.Empty(removed.Lessons[0].History);

        var (retained, retainedChanged) = WorkspaceSourceIntegrity.RemoveMeetings(original, [deletedSession],
            retainAcceptedContent: true, now: Now.AddSeconds(1));
        Assert.True(retainedChanged);
        var retainedRevision = Assert.Single(retained.Lessons[0].History);
        Assert.Equal(oldRevision.Text, retainedRevision.Text);
        Assert.Null(retainedRevision.SourceSessionId);
        Assert.Null(retainedRevision.SourceSessionIds);
        Assert.Equal([deletedSession], retainedRevision.SourceMissingSessionIds);
    }

    [Fact]
    public void KnowledgeCandidatesStayPendingUntilAcceptedAndRepeatedReviewsDoNotCountTwice()
    {
        var earlierSession = NewId();
        var session = NewId();
        var workspace = Series("面试") with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Question = "缓存一致性怎么保证？", Answer = "旧答法保留", TimesAsked = 2,
                    SourceSessionId = earlierSession, UpdatedAt = Now
                }
            ]
        };
        var lessons = new[] { "先说结论再展开", "先说结论再展开。" };
        var bank = new[] { new QuestionBankEntry("缓存一致性怎么保证？", "先更新库再删缓存") };
        var (first, firstAdded) = KnowledgeMerger.CollectCandidates(workspace, lessons, bank, session, Now);
        Assert.Equal(2, firstAdded.Count);
        var single = Assert.Single(first.QuestionBank);
        Assert.Equal(2, single.TimesAsked);
        Assert.Equal("旧答法保留", single.Answer);

        var (again, againAdded) = KnowledgeMerger.CollectCandidates(first, ["先说结论再展开"], bank, session, Now);
        Assert.Empty(againAdded);

        var candidate = first.KnowledgeCandidates!.First(item => item.Kind == WorkspaceKnowledgeKind.Question);
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(first, candidate.Id,
            WorkspaceKnowledgeDecision.KeepExisting, Now);
        Assert.NotNull(receipt);
        Assert.Equal("旧答法保留", reviewed.QuestionBank[0].Answer);
        Assert.Equal(3, reviewed.QuestionBank[0].TimesAsked);
        Assert.Equal(new[] { earlierSession, session }.OrderBy(id => id),
            reviewed.QuestionBank[0].SourceSessionIds!.OrderBy(id => id));

        var (_, retry) = KnowledgeMerger.ApplyCandidate(reviewed, candidate.Id,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now.AddSeconds(1));
        Assert.Equal(receipt, retry);
        Assert.Equal(3, reviewed.QuestionBank[0].TimesAsked);

        var restored = JsonSerializer.Deserialize<WorkspaceSeries>(
            JsonSerializer.SerializeToUtf8Bytes(reviewed, JsonDefaults.Options), JsonDefaults.Options)!;
        var (_, afterRestart) = KnowledgeMerger.ApplyCandidate(restored, candidate.Id,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now.AddSeconds(2));
        Assert.Equal(receipt, afterRestart);
        Assert.Equal(3, restored.QuestionBank[0].TimesAsked);
    }

    [Fact]
    public void ReplacingAnAnswerRequiresFreshRevisionAndKeepsTheOldAnswer()
    {
        var workspace = Series("面试") with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = NewId(), Question = "Redis 缓存雪崩怎么处理", Answer = "人工维护的答法",
                    TimesAsked = 4, UpdatedAt = Now
                }
            ]
        };
        var session = NewId();
        var bank = new[] { new QuestionBankEntry("Redis 缓存雪崩如何处理", "分批过期并加随机值") };
        var (collected, added) = KnowledgeMerger.CollectCandidates(workspace, [], bank, session, Now);
        var candidateId = added[0];
        var edited = collected with
        {
            QuestionBank =
            [
                KnowledgeMerger.EditQuestionBankItem(collected.QuestionBank[0], "Redis 缓存雪崩如何处理",
                    "用户后来手动调整的答法", Now.AddSeconds(1))
            ]
        };
        var (stale, staleReceipt) = KnowledgeMerger.ApplyCandidate(edited, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now);
        Assert.Null(staleReceipt);
        var refreshed = KnowledgeMerger.RefreshCandidateBase(stale, candidateId)!;
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(refreshed, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now);
        Assert.NotNull(receipt);
        Assert.Equal(WorkspaceKnowledgeDecision.ReplaceAnswer, receipt!.Decision);
        Assert.Equal("分批过期并加随机值", reviewed.QuestionBank[0].Answer);
        Assert.Equal(session, reviewed.QuestionBank[0].AnswerSourceSessionId);
        Assert.Equal(new[] { "人工维护的答法", "用户后来手动调整的答法" },
            reviewed.QuestionBank[0].AnswerHistory!.Select(revision => revision.Answer));
        Assert.Equal(5, reviewed.QuestionBank[0].TimesAsked);
    }

    [Fact]
    public void ReplacingAnAnswerPreservesItsSourceAttribution()
    {
        var originalQuestionSession = NewId();
        var answerSession = NewId();
        var candidateSession = NewId();
        var workspace = Series("面试") with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = NewId(), Question = "Redis 缓存雪崩怎么处理", Answer = "原答法", TimesAsked = 2,
                    SourceSessionId = originalQuestionSession,
                    SourceSessionIds = [originalQuestionSession, answerSession],
                    AnswerSourceSessionId = answerSession, AnswerRevision = 2, UpdatedAt = Now
                }
            ]
        };
        var (collected, added) = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "新答法")], candidateSession, Now);
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(collected, added[0],
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now.AddSeconds(1));
        Assert.NotNull(receipt);
        Assert.Equal("新答法", reviewed.QuestionBank[0].Answer);
        Assert.Equal(candidateSession, reviewed.QuestionBank[0].AnswerSourceSessionId);
        var old = Assert.Single(reviewed.QuestionBank[0].AnswerHistory!);
        Assert.Equal("原答法", old.Answer);
        Assert.Equal(answerSession, old.SourceSessionId);
    }

    [Fact]
    public void StaleKeepExistingRequiresReviewOfTheCurrentQuestionRevision()
    {
        var existingSession = NewId();
        var candidateSession = NewId();
        var workspace = Series("面试") with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = NewId(), Question = "Redis 缓存雪崩怎么处理", Answer = "原答法", TimesAsked = 2,
                    SourceSessionId = existingSession, SourceSessionIds = [existingSession]
                }
            ]
        };
        var (collected, added) = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "候选答法")], candidateSession, Now);
        var candidateId = added[0];
        var edited = collected with
        {
            QuestionBank =
            [
                KnowledgeMerger.EditQuestionBankItem(collected.QuestionBank[0], "Redis 缓存雪崩怎么处理",
                    "刚刚手动更新的答法", Now.AddSeconds(1))
            ]
        };
        var (stale, staleReceipt) = KnowledgeMerger.ApplyCandidate(edited, candidateId,
            WorkspaceKnowledgeDecision.KeepExisting, Now);
        Assert.Null(staleReceipt);
        Assert.Equal("刚刚手动更新的答法", stale.QuestionBank[0].Answer);
        Assert.Equal(2, stale.QuestionBank[0].TimesAsked);

        var refreshed = KnowledgeMerger.RefreshCandidateBase(stale, candidateId)!;
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(refreshed, candidateId,
            WorkspaceKnowledgeDecision.KeepExisting, Now);
        Assert.NotNull(receipt);
        Assert.True(receipt!.SourceCounted);
        Assert.Equal("刚刚手动更新的答法", reviewed.QuestionBank[0].Answer);
        Assert.Equal(3, reviewed.QuestionBank[0].TimesAsked);
        Assert.Equal(new[] { existingSession, candidateSession }.OrderBy(id => id),
            (reviewed.QuestionBank[0].SourceSessionIds ?? []).OrderBy(id => id));
    }

    [Fact]
    public void KeepingAnExistingLessonIsRejectedWhenNoSimilarLessonRemains()
    {
        var workspace = Series("面试");
        var (collected, added) = KnowledgeMerger.CollectCandidates(workspace, ["面试回答先说结论，再按证据展开"],
            [], NewId(), Now);
        var candidateId = added[0];
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(collected, candidateId,
            WorkspaceKnowledgeDecision.KeepExisting, Now);
        Assert.Null(receipt);
        Assert.Empty(reviewed.Lessons);
        Assert.Equal(WorkspaceKnowledgeStatus.Pending, reviewed.KnowledgeCandidates![0].Status);
    }

    [Fact]
    public void EditingQuestionWordingInvalidatesOpenReplacementWithoutErasingProvenance()
    {
        var sourceSession = NewId();
        var currentSession = NewId();
        var oldRevision = new WorkspaceAnswerRevision
        {
            Id = "old", Answer = "更早的答法", RecordedAt = Now.AddSeconds(-10)
        };
        var workspace = Series("面试") with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = NewId(), Question = "Redis 缓存雪崩怎么处理", Answer = "人工维护的答法",
                    TimesAsked = 4, SourceSessionId = sourceSession, SourceSessionIds = [sourceSession],
                    AnswerHistory = [oldRevision], AnswerRevision = 3, UpdatedAt = Now
                }
            ]
        };
        var (collected, added) = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "分批过期并加随机值")], currentSession, Now);
        var candidateId = added[0];
        var edited = collected with
        {
            QuestionBank =
            [
                KnowledgeMerger.EditQuestionBankItem(collected.QuestionBank[0], "Redis 缓存雪崩如何处理",
                    "人工维护的答法", Now.AddSeconds(1))
            ]
        };
        Assert.Equal(4, edited.QuestionBank[0].AnswerRevision);
        Assert.Equal(new[] { oldRevision }, edited.QuestionBank[0].AnswerHistory!);
        Assert.Equal(new[] { sourceSession }, edited.QuestionBank[0].SourceSessionIds!);
        var (stale, staleReceipt) = KnowledgeMerger.ApplyCandidate(edited, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now);
        Assert.Null(staleReceipt);
        var refreshed = KnowledgeMerger.RefreshCandidateBase(stale, candidateId)!;

        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(refreshed, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now);
        Assert.NotNull(receipt);
        Assert.True(receipt!.SourceCounted);
        Assert.Equal("分批过期并加随机值", reviewed.QuestionBank[0].Answer);
        Assert.Equal(new[] { "更早的答法", "人工维护的答法" },
            reviewed.QuestionBank[0].AnswerHistory!.Select(revision => revision.Answer));
        Assert.Contains(currentSession, reviewed.QuestionBank[0].SourceSessionIds!);
    }

    [Fact]
    public void WhyAndHowQuestionsRemainSeparateCandidates()
    {
        var workspace = Series("面试");
        var (_, added) = KnowledgeMerger.CollectCandidates(workspace, [], [
            new QuestionBankEntry("为什么选择 Redis？", "需要低延迟缓存"),
            new QuestionBankEntry("如何使用 Redis？", "设置过期时间并处理并发")
        ], NewId(), Now);
        Assert.Equal(2, added.Count);
        Assert.Equal(2, added.Distinct().Count());
    }

    // --- deletion retention and backup merge ---------------------------------

    [Fact]
    public void BackupMergeKeepsAndDeterministicallyResolvesReceiptsAcrossDeletedCandidates()
    {
        var workspaceId = NewId();
        var sourceSession = NewId();
        var itemId = NewId();
        const string candidateId = "deleted-candidate";
        var firstReceipt = new WorkspaceSeriesChangeReceipt(
            Guid.NewGuid(), candidateId, WorkspaceSeriesChangeDecision.Add, Now, itemId);
        var laterReceipt = new WorkspaceSeriesChangeReceipt(
            Guid.NewGuid(), candidateId, WorkspaceSeriesChangeDecision.Reject, Now.AddSeconds(10), null);
        var receiptless = new WorkspaceSeriesChangeCandidate
        {
            Id = candidateId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "",
            SourceSessionId = sourceSession, EvidenceId = "", ClaimRevision = "", SourceRevision = "",
            SourceMissing = true, Status = WorkspaceSeriesChangeCandidate.StatusValue.Superseded
        };
        var acceptedTombstone = receiptless with
        {
            Status = WorkspaceSeriesChangeCandidate.StatusValue.Accepted, Receipt = firstReceipt
        };
        var deletedWorkspace = new WorkspaceSeries(workspaceId, "系列", Now, [])
        {
            SeriesChangeCandidates = [receiptless]
        };
        var acceptedBackup = new WorkspaceSeries(workspaceId, "系列备份", Now, [])
        {
            SeriesChangeCandidates = [acceptedTombstone]
        };

        var merged = Assert.Single(BackupMerge.Workspaces([deletedWorkspace], [acceptedBackup]).Merged);
        var candidate = Assert.Single(merged.SeriesChangeCandidates);
        Assert.True(candidate.SourceMissing);
        Assert.Equal("", candidate.Text);
        Assert.Null(candidate.SourceQuote);
        Assert.Equal(firstReceipt, candidate.Receipt);
        var (replayedSeries, replayed) = WorkspaceSeriesChangeMerger.Apply(merged, candidateId,
            WorkspaceSeriesChangeDecision.Add, operationId: firstReceipt.OperationId);
        Assert.Equal(firstReceipt, replayed);
        Assert.Equal(firstReceipt, replayedSeries.SeriesChangeCandidates[0].Receipt);

        var rejectedTombstone = acceptedTombstone with
        {
            Receipt = laterReceipt, Status = WorkspaceSeriesChangeCandidate.StatusValue.Rejected
        };
        var rejectedBackup = new WorkspaceSeries(workspaceId, "系列备份", Now, [])
        {
            SeriesChangeCandidates = [rejectedTombstone]
        };
        var conflicted = Assert.Single(BackupMerge.Workspaces([merged], [rejectedBackup]).Merged);
        var conflictedCandidate = Assert.Single(conflicted.SeriesChangeCandidates);
        Assert.True(conflictedCandidate.SourceMissing);
        Assert.Equal(laterReceipt, conflictedCandidate.Receipt);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Rejected, conflictedCandidate.Status);
        Assert.Equal(new[] { firstReceipt }, conflictedCandidate.ReceiptHistory!);

        var (_, replayAdd) = WorkspaceSeriesChangeMerger.Apply(conflicted, candidateId,
            WorkspaceSeriesChangeDecision.Add, operationId: firstReceipt.OperationId);
        Assert.Equal(firstReceipt, replayAdd);
        var (_, replayReject) = WorkspaceSeriesChangeMerger.Apply(conflicted, candidateId,
            WorkspaceSeriesChangeDecision.Reject, operationId: laterReceipt.OperationId);
        Assert.Equal(laterReceipt, replayReject);

        var reversed = Assert.Single(BackupMerge.Workspaces([rejectedBackup], [merged]).Merged);
        var reversedCandidate = Assert.Single(reversed.SeriesChangeCandidates);
        Assert.Equal(laterReceipt, reversedCandidate.Receipt);
        Assert.Equal(new[] { firstReceipt }, reversedCandidate.ReceiptHistory!);
        Assert.True(reversedCandidate.SourceMissing);
        Assert.Equal("", reversedCandidate.Text);
    }

    [Fact]
    public void BackupMergePreservesExplicitSeriesRetentionWithoutRevivingItsSource()
    {
        var workspaceId = NewId();
        var itemId = NewId();
        var deletedSession = NewId();
        var acceptedAt = Now.AddSeconds(-30);
        var receipt = new WorkspaceSeriesChangeReceipt(
            Guid.NewGuid(), "retained-decision", WorkspaceSeriesChangeDecision.Add, acceptedAt, itemId);
        var candidate = new WorkspaceSeriesChangeCandidate
        {
            Id = "retained-decision", Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "改为周一提交",
            SourceSessionId = deletedSession, EvidenceId = "decision-2", ClaimRevision = "claim-2",
            SourceRevision = "line-2", SourceQuote = "周一之前提交", Location = "02:10",
            TranscriptSourceRevision = "transcript-2", Status = WorkspaceSeriesChangeCandidate.StatusValue.Accepted,
            Receipt = receipt, CreatedAt = acceptedAt
        };
        var item = new WorkspaceSeriesItem
        {
            Id = itemId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "改为周一提交",
            SourceSessionId = deletedSession, EvidenceId = "decision-2", ClaimRevision = "claim-2",
            SourceRevision = "line-2", SourceQuote = "周一之前提交", Location = "02:10",
            TranscriptSourceRevision = "transcript-2", ConfirmedAt = acceptedAt
        };
        var original = new WorkspaceSeries(workspaceId, "系列", acceptedAt, [])
        {
            SeriesItems = [item], SeriesChangeCandidates = [candidate]
        };
        var (retained, changed) = WorkspaceSourceIntegrity.RemoveMeetings(original, [deletedSession],
            retainAcceptedContent: true, now: Now.AddSeconds(10));
        Assert.True(changed);
        var retainedBackup = new WorkspaceSeries(workspaceId, "系列", acceptedAt, [])
        {
            SeriesItems = [item], SeriesChangeCandidates = [candidate]
        };
        var merged = Assert.Single(BackupMerge.Workspaces([retained], [retainedBackup]).Merged);
        var mergedItem = Assert.Single(merged.SeriesItems);
        Assert.Equal("改为周一提交", mergedItem.Text);
        Assert.True(mergedItem.SourceMissing);
        Assert.True(mergedItem.IndependentlyRetained);
        Assert.Null(mergedItem.SourceQuote);
        Assert.Null(mergedItem.Location);
        Assert.Null(mergedItem.TranscriptSourceRevision);
        var mergedCandidate = Assert.Single(merged.SeriesChangeCandidates);
        Assert.True(mergedCandidate.SourceMissing);
        Assert.Equal("", mergedCandidate.Text);
        Assert.Null(mergedCandidate.SourceQuote);
        Assert.Null(mergedCandidate.TranscriptSourceRevision);
    }

    [Fact]
    public void ExplicitSeriesRetentionWinsAgainstDefaultDeleteInEitherBackupOrder()
    {
        var workspaceId = NewId();
        var itemId = NewId();
        var deletedSession = NewId();
        var item = new WorkspaceSeriesItem
        {
            Id = itemId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "正式改为周一提交",
            SourceSessionId = deletedSession, EvidenceId = "decision", ClaimRevision = "claim",
            SourceRevision = "line", SourceQuote = "周一原话", Location = "01:10",
            TranscriptSourceRevision = "transcript", Revision = 3
        };
        var original = new WorkspaceSeries(workspaceId, "系列", Now, [])
        {
            SeriesItems = [item]
        };
        var (retained, _) = WorkspaceSourceIntegrity.RemoveMeetings(original, [deletedSession],
            retainAcceptedContent: true, now: Now.AddSeconds(1));
        var (defaultDeleted, _) = WorkspaceSourceIntegrity.RemoveMeetings(original, [deletedSession],
            retainAcceptedContent: false, now: Now.AddSeconds(1));

        var keptFirst = Assert.Single(BackupMerge.Workspaces([retained], [defaultDeleted]).Merged);
        var deletedFirst = Assert.Single(BackupMerge.Workspaces([defaultDeleted], [retained]).Merged);
        var keptFirstItem = Assert.Single(keptFirst.SeriesItems);
        var deletedFirstItem = Assert.Single(deletedFirst.SeriesItems);
        Assert.Equal(Json(keptFirstItem), Json(deletedFirstItem));
        Assert.Equal("正式改为周一提交", keptFirstItem.Text);
        Assert.True(keptFirstItem.SourceMissing);
        Assert.True(keptFirstItem.IndependentlyRetained);
        Assert.Null(keptFirstItem.SourceQuote);
        Assert.Null(keptFirstItem.Location);
        Assert.Null(keptFirstItem.TranscriptSourceRevision);
    }

    [Fact]
    public void HigherLiveSeriesRevisionWinsOverRetainedStaleTextAndKeepsItInHistory()
    {
        var workspaceId = NewId();
        var itemId = NewId();
        var deletedSession = NewId();
        var liveSession = NewId();
        var retainedItem = new WorkspaceSeriesItem
        {
            Id = itemId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "保留的旧结论",
            SourceSessionId = deletedSession, EvidenceId = "old-evidence", ClaimRevision = "old-claim",
            SourceRevision = "old-line", SourceQuote = "旧会议原话", Location = "00:40",
            TranscriptSourceRevision = "old-transcript", Revision = 2
        };
        var retainedWorkspace = new WorkspaceSeries(workspaceId, "系列", Now, [])
        {
            SeriesItems = [retainedItem]
        };
        (retainedWorkspace, _) = WorkspaceSourceIntegrity.RemoveMeetings(retainedWorkspace, [deletedSession],
            retainAcceptedContent: true, now: Now.AddSeconds(10));

        var liveItem = new WorkspaceSeriesItem
        {
            Id = itemId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "新会议确认的结论",
            SourceSessionId = liveSession, EvidenceId = "live-evidence", ClaimRevision = "live-claim",
            SourceRevision = "live-line", SourceQuote = "新会议原话", Location = "02:00",
            TranscriptSourceRevision = "live-transcript", Revision = 3
        };
        var liveWorkspace = new WorkspaceSeries(workspaceId, "系列", Now, [])
        {
            SeriesItems = [liveItem]
        };

        var forward = Assert.Single(BackupMerge.Workspaces([retainedWorkspace], [liveWorkspace]).Merged);
        var reverse = Assert.Single(BackupMerge.Workspaces([liveWorkspace], [retainedWorkspace]).Merged);
        var forwardItem = Assert.Single(forward.SeriesItems);
        var reverseItem = Assert.Single(reverse.SeriesItems);
        Assert.Equal(Json(forwardItem), Json(reverseItem));
        Assert.Equal("新会议确认的结论", forwardItem.Text);
        Assert.Equal(liveSession, forwardItem.SourceSessionId);
        Assert.Equal(3, forwardItem.Revision);
        Assert.True(forwardItem.SourceMissing != true);
        Assert.True(forwardItem.IndependentlyRetained != true);
        var retainedHistory = Assert.Single(forwardItem.History,
            revision => revision.Text == "保留的旧结论");
        Assert.Equal(deletedSession, retainedHistory.SourceSessionId);
        Assert.True(retainedHistory.SourceMissing);
        Assert.Null(retainedHistory.SourceQuote);
        Assert.Null(retainedHistory.Location);
        Assert.Null(retainedHistory.TranscriptSourceRevision);
    }

    [Fact]
    public void ExplicitDeletionRetentionKeepsAcceptedTextWithAnAccurateSourceTombstone()
    {
        var deletedSession = NewId();
        var itemId = NewId();
        var oldRevision = new WorkspaceSeriesItemRevision
        {
            Id = "old", Revision = 1, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "先按周五提交",
            SourceSessionId = deletedSession, EvidenceId = "old-evidence", ClaimRevision = "old-claim",
            SourceRevision = "old-line", SourceQuote = "周五之前提交", Location = "00:15",
            TranscriptSourceRevision = "old-transcript"
        };
        var receipt = new WorkspaceSeriesChangeReceipt(
            Guid.NewGuid(), "accepted-candidate", WorkspaceSeriesChangeDecision.Add, Now, itemId);
        var candidate = new WorkspaceSeriesChangeCandidate
        {
            Id = "accepted-candidate", Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "正式改为周一提交",
            SourceSessionId = deletedSession, EvidenceId = "current-evidence", ClaimRevision = "current-claim",
            SourceRevision = "current-line", SourceQuote = "周一之前提交", Location = "01:25",
            TranscriptSourceRevision = "current-transcript",
            Status = WorkspaceSeriesChangeCandidate.StatusValue.Accepted, Receipt = receipt
        };
        var workspace = new WorkspaceSeries(NewId(), "项目交付", Now, [])
        {
            Lessons =
            [
                new WorkspaceLesson
                {
                    Id = NewId(), Text = "已确认的独立经验", SourceSessionId = deletedSession,
                    CreatedAt = Now, UpdatedAt = Now
                }
            ],
            SeriesItems =
            [
                new WorkspaceSeriesItem
                {
                    Id = itemId, Kind = WorkspaceSeriesItem.KindValue.Decision, Text = "正式改为周一提交",
                    SourceSessionId = deletedSession, EvidenceId = "current-evidence",
                    ClaimRevision = "current-claim", SourceRevision = "current-line",
                    SourceQuote = "周一之前提交", Location = "01:25",
                    TranscriptSourceRevision = "current-transcript", Revision = 2, History = [oldRevision],
                    UpdatedAt = Now
                }
            ],
            SeriesChangeCandidates = [candidate]
        };

        var (retained, changed) = WorkspaceSourceIntegrity.RemoveMeetings(workspace, [deletedSession],
            retainAcceptedContent: true, now: Now.AddSeconds(1));
        Assert.True(changed);
        var retainedLesson = Assert.Single(retained.Lessons);
        Assert.Equal("已确认的独立经验", retainedLesson.Text);
        Assert.True(retainedLesson.IndependentlyRetained);
        Assert.Null(retainedLesson.SourceSessionId);
        Assert.Null(retainedLesson.SourceSessionIds);
        Assert.Equal(new[] { deletedSession }, retainedLesson.SourceMissingSessionIds);

        var retainedItem = Assert.Single(retained.SeriesItems);
        Assert.Equal("正式改为周一提交", retainedItem.Text);
        Assert.True(retainedItem.SourceMissing);
        Assert.True(retainedItem.IndependentlyRetained);
        Assert.Equal("", retainedItem.EvidenceId);
        Assert.Equal("", retainedItem.ClaimRevision);
        Assert.Equal("", retainedItem.SourceRevision);
        Assert.Null(retainedItem.TranscriptSourceRevision);
        Assert.Null(retainedItem.SourceQuote);
        Assert.Null(retainedItem.Location);
        Assert.Equal("先按周五提交", retainedItem.History[0].Text);
        Assert.True(retainedItem.History[0].SourceMissing);
        Assert.Null(retainedItem.History[0].TranscriptSourceRevision);
        Assert.Null(retainedItem.History[0].SourceQuote);
        Assert.Null(retainedItem.History[0].Location);

        var retainedCandidate = Assert.Single(retained.SeriesChangeCandidates);
        Assert.Equal(WorkspaceSeriesChangeCandidate.StatusValue.Accepted, retainedCandidate.Status);
        Assert.Equal(receipt, retainedCandidate.Receipt);
        Assert.True(retainedCandidate.SourceMissing);
        Assert.Equal("", retainedCandidate.Text);
        Assert.Null(retainedCandidate.SourceQuote);
        Assert.Null(retainedCandidate.Location);
        Assert.Null(retainedCandidate.TranscriptSourceRevision);
        var (_, replayed) = WorkspaceSeriesChangeMerger.Apply(retained, "accepted-candidate",
            WorkspaceSeriesChangeDecision.Replace, operationId: Guid.NewGuid());
        Assert.Equal(receipt, replayed);
        var (_, changedAgain) = WorkspaceSourceIntegrity.RemoveMeetings(retained, [deletedSession],
            retainAcceptedContent: true, now: Now.AddSeconds(2));
        Assert.False(changedAgain);
    }

    [Fact]
    public void StageReviewKnowledgeTracksEveryContributingMeeting()
    {
        // Fixed ids like the Swift test: the candidate's primary source is the smallest id.
        var deletedSession = "00000000-0000-0000-0000-000000000001";
        var remainingSession = "00000000-0000-0000-0000-000000000002";
        var sources = new[]
        {
            new WorkspaceKnowledgeCandidateSource
            {
                SessionId = deletedSession, StartedAt = Now,
                TranscriptSourceRevision = "transcript-1", ProjectionRevision = "projection-1"
            },
            new WorkspaceKnowledgeCandidateSource
            {
                SessionId = remainingSession, StartedAt = Now.AddSeconds(1),
                TranscriptSourceRevision = "transcript-2", ProjectionRevision = "projection-2"
            }
        };
        var workspace = WorkspaceSeries.Create("阶段复盘");
        var (collected, added) = KnowledgeMerger.CollectCandidates(workspace,
            ["跨会复盘经验能帮助下一步准备"],
            [new QuestionBankEntry("阶段复盘候选问题？", "由多场会议共同总结的回答")],
            null, Now, contributingSources: sources, originId: workspace.Id);
        Assert.Equal(2, added.Count);
        var lessonCandidate = collected.KnowledgeCandidates!.First(item => item.Kind == WorkspaceKnowledgeKind.Lesson);
        var questionCandidate = collected.KnowledgeCandidates!.First(item => item.Kind == WorkspaceKnowledgeKind.Question);
        Assert.Null(lessonCandidate.SourceSessionId);
        Assert.Equal(new[] { deletedSession, remainingSession }.OrderBy(id => id),
            lessonCandidate.ContributingSessionIds.OrderBy(id => id));

        var (pending, pendingChanged) = WorkspaceSourceIntegrity.RemoveMeetings(collected, [deletedSession],
            now: Now.AddSeconds(1));
        Assert.True(pendingChanged);
        Assert.Empty(pending.KnowledgeCandidates ?? []);
        var (_, rejected) = KnowledgeMerger.ApplyCandidate(pending, lessonCandidate.Id,
            WorkspaceKnowledgeDecision.Add, Now);
        Assert.Null(rejected);

        var (withLesson, lessonReceipt) = KnowledgeMerger.ApplyCandidate(collected, lessonCandidate.Id,
            WorkspaceKnowledgeDecision.Add, Now);
        Assert.NotNull(lessonReceipt);
        var (reviewed, questionReceipt) = KnowledgeMerger.ApplyCandidate(withLesson, questionCandidate.Id,
            WorkspaceKnowledgeDecision.Add, Now);
        Assert.NotNull(questionReceipt);
        var (removed, removedChanged) = WorkspaceSourceIntegrity.RemoveMeetings(reviewed, [deletedSession],
            now: Now.AddSeconds(1));
        Assert.True(removedChanged);
        Assert.Equal(new[] { remainingSession }, removed.Lessons.First().SourceSessionIds);
        Assert.Equal(new[] { remainingSession }, removed.QuestionBank.First().SourceSessionIds);
        Assert.Equal("", removed.QuestionBank.First().Answer);
        Assert.Equal(1, removed.QuestionBank.First().TimesAsked);
        var retainedCandidates = removed.KnowledgeCandidates!;
        Assert.Equal(2, retainedCandidates.Count);
        Assert.All(retainedCandidates, candidate =>
        {
            Assert.NotNull(candidate.Receipt);
            Assert.Equal(WorkspaceKnowledgeStatus.Accepted, candidate.Status);
        });
        Assert.All(retainedCandidates, candidate =>
        {
            Assert.Equal("", candidate.Text);
            Assert.Null(candidate.ProposedAnswer);
        });
        Assert.All(retainedCandidates, candidate =>
            Assert.Equal(new[] { remainingSession }, candidate.ContributingSessionIds));
        Assert.DoesNotContain("由多场会议共同总结的回答",
            string.Join("\n", removed.RetrievalDocuments.SelectMany(document => document.Pages)));
    }
}
