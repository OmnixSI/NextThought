using System.Text.Json;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from Tests/NextThoughtCoreTests/WorkspaceTests.swift. The Windows port models the
/// Mac workspace as one <see cref="WorkspaceSeries"/> (lessons, question bank, series items and
/// review candidates live on the series), so the series plays the workspace in every test.
/// </summary>
public sealed class WorkspaceTestsParityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_900_000_000);

    private static string Id() => Guid.NewGuid().ToString("D");

    private static WorkspaceSeries Series(string name, params WorkspaceLesson[] lessons) =>
        new(Id(), name, Now, [], lessons);

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, WorkspaceStore.Options);

    private static List<WorkspaceSeries> Merge(IReadOnlyList<WorkspaceSeries> existing, IReadOnlyList<WorkspaceSeries> incoming) =>
        BackupMerge.Workspaces(existing, incoming).Merged.ToList();

    private static IReadOnlyList<string> SortedIds(params string[] ids) =>
        ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

    [Fact]
    public void ModesHaveProfilesAndOldValuesStillDecode()
    {
        foreach (var mode in SessionModes.BuiltIn)
        {
            var profile = ModeProfile.For(mode.ToMeetingMode());
            Assert.NotEmpty(profile.Fields);
            Assert.NotEmpty(profile.DocumentCategories);
            Assert.NotEmpty(profile.TemplatePreferences);
        }
        var decoded = JsonSerializer.Deserialize<List<SessionMode>>(
            "[\"meeting\",\"interview\",\"lesson\",\"futuremode\"]", JsonDefaults.Options)!;
        Assert.Equal([SessionMode.Meeting, SessionMode.Interview, SessionMode.Lesson, SessionMode.General], decoded);
        Assert.Equal("工作会议", SessionMode.Meeting.Title());
    }

    [Fact]
    public void DivergentSameNumberSeriesSnapshotsHaveStableUniqueReplayableHistoryIDs()
    {
        var workspaceId = Id();
        var itemId = Id();
        var sourceSessionId = Id();
        var owners = new[] { "\u963F\u9752", "\u5C0F\u6797", "Morgan" };
        var snapshots = owners.Select((owner, index) => new WorkspaceSeriesItem
        {
            Id = itemId,
            Kind = WorkspaceSeriesItem.KindValue.Action,
            Text = "\u5468\u4E94\u524D\u63D0\u4EA4\u63A5\u53E3\u8BC4\u5BA1",
            Owner = owner,
            Due = "\u5468\u4E94\u524D",
            ActionState = WorkspaceSeriesItem.ActionStateValue.Open,
            SourceSessionId = sourceSessionId,
            EvidenceId = "action-evidence",
            ClaimRevision = "same-claim",
            SourceRevision = "same-line",
            SourceQuote = "\u5468\u4E94\u524D\u63D0\u4EA4",
            Location = "01:20",
            TranscriptSourceRevision = "same-transcript",
            Revision = 5,
            ConfirmedAt = Now,
            UpdatedAt = Now.AddSeconds(index)
        }).ToList();
        var snapshotIds = snapshots.Select(item => item.RevisionId()).ToList();
        Assert.Equal(3, snapshotIds.Distinct().Count());
        foreach (var item in snapshots)
        {
            var decoded = System.Text.Json.JsonSerializer.Deserialize<WorkspaceSeriesItem>(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item, WorkspaceStore.Options), WorkspaceStore.Options)!;
            Assert.Equal(item.RevisionId(), decoded.RevisionId());
        }

        var workspaces = snapshots.Select(item => new WorkspaceSeries(workspaceId, "\u7CFB\u5217", Now.AddSeconds(100), [], [])
        {
            SeriesItems = [item]
        }).ToList();
        var mergedForward = Merge([workspaces[0]], [workspaces[1], workspaces[2]])[0];
        var mergedReverse = Merge([workspaces[2]], [workspaces[1], workspaces[0]])[0];
        var forwardItem = mergedForward.SeriesItems!.First();
        var reverseItem = mergedReverse.SeriesItems!.First();
        Assert.Equal("Morgan", forwardItem.Owner);
        Assert.Equal("Morgan", reverseItem.Owner);
        Assert.Equal(Json(forwardItem.History), Json(reverseItem.History));
        Assert.Equal(forwardItem.History.Count, forwardItem.History.Select(revision => revision.Id).Distinct().Count());
        Assert.Equal(snapshotIds.Take(2).ToHashSet(), forwardItem.History.Select(revision => revision.Id).ToHashSet());
        Assert.Equal(new HashSet<string?>(owners.Take(2)), forwardItem.History.Select(revision => revision.Owner).ToHashSet());

        var replayed = Merge([mergedForward], [workspaces[1], workspaces[2]])[0];
        var replayedItem = replayed.SeriesItems!.First();
        Assert.Equal(Json(forwardItem.History), Json(replayedItem.History));
        Assert.Equal(replayedItem.History.Count, replayedItem.History.Select(revision => revision.Id).Distinct().Count());
    }

    [Fact]
    public void BackupReimportCannotResurrectDefaultDeletedSeriesItem()
    {
        var workspaceId = "00000000-0000-0000-0000-000000000101";
        var itemId = "00000000-0000-0000-0000-000000000102";
        var deletedSession = "00000000-0000-0000-0000-000000000103";
        var history = new WorkspaceSeriesItemRevision
        {
            Revision = 1,
            Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "此前按周五提交",
            SourceSessionId = deletedSession,
            EvidenceId = "old-evidence",
            ClaimRevision = "old-claim",
            SourceRevision = "old-line",
            SourceQuote = "旧原话周五",
            Location = "00:15",
            TranscriptSourceRevision = "old-transcript",
            RecordedAt = Now
        };
        var staleItem = new WorkspaceSeriesItem
        {
            Id = itemId,
            Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "改为周一提交",
            SourceSessionId = deletedSession,
            EvidenceId = "current-evidence",
            ClaimRevision = "current-claim",
            SourceRevision = "current-line",
            SourceQuote = "新原话周一",
            Location = "01:25",
            TranscriptSourceRevision = "current-transcript",
            Revision = 2,
            History = [history],
            ConfirmedAt = Now,
            UpdatedAt = Now
        };
        var oldBackup = new WorkspaceSeries(workspaceId, "系列", Now, [], []) { SeriesItems = [staleItem] };
        var (deleted, changed) = WorkspaceSourceIntegrity.RemoveMeetings(oldBackup, [deletedSession],
            retainAcceptedContent: false, now: Now.AddSeconds(10));
        Assert.True(changed);
        Assert.Empty(deleted.SeriesItems);
        Assert.Equal([new WorkspaceSeriesItemSourceTombstone { ItemId = itemId, SourceSessionId = deletedSession }],
            deleted.SeriesItemSourceTombstones);

        // Simulate an older device exporting the pre-delete record later, with a
        // higher local revision and backup timestamp. Reimporting it repeatedly
        // must not restore the item, its quote, or its deleted history.
        var newerStaleItem = staleItem with
        {
            Text = "仍按周五提交",
            Revision = 8,
            UpdatedAt = Now.AddSeconds(200),
            SourceQuote = "已删除来源中的新原话",
            History = [history with { Text = "来源中记载的旧决定", SourceQuote = "已删除来源中的历史原话" }]
        };
        var staleBackup = oldBackup with
        {
            CreatedAt = Now.AddSeconds(300),
            SeriesItems = [newerStaleItem]
        };

        var imported = Merge([deleted], [staleBackup])[0];
        Assert.Empty(imported.SeriesItems);
        Assert.Equal(deleted.SeriesItemSourceTombstones, imported.SeriesItemSourceTombstones);
        var importedJson = Json(imported);
        Assert.DoesNotContain("仍按周五提交", importedJson);
        Assert.DoesNotContain("已删除来源中的新原话", importedJson);
        Assert.DoesNotContain("来源中记载的旧决定", importedJson);
        Assert.DoesNotContain("已删除来源中的历史原话", importedJson);

        var replayed = Merge([imported], [staleBackup])[0];
        Assert.Empty(replayed.SeriesItems);
        Assert.Equal(deleted.SeriesItemSourceTombstones, replayed.SeriesItemSourceTombstones);
    }

    [Fact(Skip = "missing API: WorkspaceSeriesChangeMerger.applyCandidate (operation-ID replay)")]
    public void BackupMergeKeepsAndDeterministicallyResolvesReceiptsAcrossDeletedCandidates()
    {
    }

    [Fact(Skip = "missing API: WorkspaceSeriesChangeMerger.applyCandidate (replay of an accepted candidate)")]
    public void ExplicitDeletionRetentionKeepsAcceptedTextWithAnAccurateSourceTombstone()
    {
    }

    [Fact]
    public void StageReviewKnowledgeTracksEveryContributingMeeting()
    {
        var deletedSession = "00000000-0000-0000-0000-000000000001";
        var remainingSession = "00000000-0000-0000-0000-000000000002";
        var sources = new[]
        {
            new WorkspaceKnowledgeCandidateSource
            {
                SessionId = deletedSession,
                StartedAt = Now,
                TranscriptSourceRevision = "transcript-1",
                ProjectionRevision = "projection-1"
            },
            new WorkspaceKnowledgeCandidateSource
            {
                SessionId = remainingSession,
                StartedAt = Now.AddSeconds(1),
                TranscriptSourceRevision = "transcript-2",
                ProjectionRevision = "projection-2"
            }
        };
        var workspace = Series("阶段复盘");
        var collected = KnowledgeMerger.CollectCandidates(workspace,
            ["跨会复盘经验能帮助下一步准备"],
            [new QuestionBankEntry("阶段复盘候选问题？", "由多场会议共同总结的回答")],
            sessionId: null, now: Now, originId: workspace.Id, contributingSources: sources);
        Assert.Equal(2, collected.Added.Count);
        workspace = collected.Series;

        var lessonCandidate = workspace.KnowledgeCandidates!.First(candidate => candidate.Kind == WorkspaceKnowledgeKind.Lesson);
        var questionCandidate = workspace.KnowledgeCandidates!.First(candidate => candidate.Kind == WorkspaceKnowledgeKind.Question);
        Assert.Null(lessonCandidate.SourceSessionId);
        Assert.Equal([deletedSession, remainingSession], lessonCandidate.ContributingSessionIds);

        var (pending, pendingChanged) = WorkspaceSourceIntegrity.RemoveMeetings(workspace, [deletedSession],
            now: Now.AddSeconds(1));
        Assert.True(pendingChanged);
        Assert.Empty(pending.KnowledgeCandidates!);
        Assert.Null(KnowledgeMerger.ApplyCandidate(pending, lessonCandidate.Id,
            WorkspaceKnowledgeDecision.Add, Now).Receipt);

        var lessonApplied = KnowledgeMerger.ApplyCandidate(workspace, lessonCandidate.Id,
            WorkspaceKnowledgeDecision.Add, Now);
        Assert.NotNull(lessonApplied.Receipt);
        workspace = lessonApplied.Series;
        var questionApplied = KnowledgeMerger.ApplyCandidate(workspace, questionCandidate.Id,
            WorkspaceKnowledgeDecision.Add, Now);
        Assert.NotNull(questionApplied.Receipt);
        workspace = questionApplied.Series;
        var (updated, changed) = WorkspaceSourceIntegrity.RemoveMeetings(workspace, [deletedSession],
            now: Now.AddSeconds(1));
        Assert.True(changed);
        Assert.Equal([remainingSession], updated.Lessons.First().SourceSessionIds);
        Assert.Equal([remainingSession], updated.QuestionBank.First().SourceSessionIds);
        Assert.Equal("", updated.QuestionBank.First().Answer);
        Assert.Equal(1, updated.QuestionBank.First().TimesAsked);
        var retainedCandidates = updated.KnowledgeCandidates!;
        Assert.Equal(2, retainedCandidates.Count);
        Assert.All(retainedCandidates, candidate =>
        {
            Assert.NotNull(candidate.Receipt);
            Assert.Equal(WorkspaceKnowledgeStatus.Accepted, candidate.Status);
        });
        Assert.All(retainedCandidates, candidate =>
        {
            Assert.Empty(candidate.Text);
            Assert.Null(candidate.ProposedAnswer);
        });
        Assert.All(retainedCandidates, candidate =>
            Assert.Equal([remainingSession], candidate.ContributingSessionIds));
        Assert.DoesNotContain("由多场会议共同总结的回答",
            string.Concat(updated.RetrievalDocuments.SelectMany(document => document.Pages)));
    }

    [Fact]
    public void KnowledgeCandidatesStayPendingUntilAcceptedAndRepeatedReviewsDoNotCountTwice()
    {
        var workspace = Series("面试");
        var earlierSession = Id();
        var session = Id();
        workspace = workspace with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Question = "缓存一致性怎么保证？",
                    Answer = "旧答法保留",
                    TimesAsked = 2,
                    SourceSessionId = earlierSession,
                    UpdatedAt = Now
                }
            ]
        };
        var first = KnowledgeMerger.CollectCandidates(workspace, ["先说结论再展开", "先说结论再展开。"],
            [new QuestionBankEntry("缓存一致性怎么保证？", "先更新库再删缓存")], session, now: Now);
        Assert.Equal(2, first.Added.Count);
        workspace = first.Series;
        Assert.Single(workspace.QuestionBank);
        Assert.Equal(2, workspace.QuestionBank[0].TimesAsked);
        Assert.Equal("旧答法保留", workspace.QuestionBank[0].Answer);
        var again = KnowledgeMerger.CollectCandidates(workspace, ["先说结论再展开"],
            [new QuestionBankEntry("缓存一致性怎么保证？", "先更新库再删缓存")], session, now: Now);
        Assert.Empty(again.Added);

        var candidate = workspace.KnowledgeCandidates!.First(item => item.Kind == WorkspaceKnowledgeKind.Question);
        var kept = KnowledgeMerger.ApplyCandidate(workspace, candidate.Id,
            WorkspaceKnowledgeDecision.KeepExisting, Now);
        Assert.NotNull(kept.Receipt);
        workspace = kept.Series;
        Assert.Equal("旧答法保留", workspace.QuestionBank[0].Answer);
        Assert.Equal(3, workspace.QuestionBank[0].TimesAsked);
        Assert.Equal(SortedIds(earlierSession, session), workspace.QuestionBank[0].SourceSessionIds);
        var retry = KnowledgeMerger.ApplyCandidate(workspace, candidate.Id,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now.AddSeconds(1));
        Assert.Equal(kept.Receipt, retry.Receipt);
        Assert.Equal(3, workspace.QuestionBank[0].TimesAsked);
        var data = JsonSerializer.Serialize(workspace, WorkspaceStore.Options);
        var restored = JsonSerializer.Deserialize<WorkspaceSeries>(data, WorkspaceStore.Options)!;
        var afterRestart = KnowledgeMerger.ApplyCandidate(restored, candidate.Id,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now.AddSeconds(2));
        Assert.Equal(kept.Receipt, afterRestart.Receipt);
        Assert.Equal(3, afterRestart.Series.QuestionBank[0].TimesAsked);
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
                    Question = "Redis 缓存雪崩怎么处理",
                    Answer = "人工维护的答法",
                    TimesAsked = 4
                }
            ]
        };
        var session = Id();
        var collected = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "分批过期并加随机值")],
            session, now: Now);
        var candidateId = collected.Added.First();
        workspace = collected.Series with
        {
            QuestionBank =
            [
                KnowledgeMerger.EditQuestionBankItem(collected.Series.QuestionBank[0],
                    "Redis 缓存雪崩如何处理", "用户后来手动调整的答法", Now.AddSeconds(1))
            ]
        };
        Assert.Null(KnowledgeMerger.ApplyCandidate(workspace, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now).Receipt);
        var refreshed = KnowledgeMerger.RefreshCandidateBase(workspace, candidateId);
        Assert.NotNull(refreshed);
        var applied = KnowledgeMerger.ApplyCandidate(refreshed!, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now);
        Assert.NotNull(applied.Receipt);
        Assert.Equal(WorkspaceKnowledgeDecision.ReplaceAnswer, applied.Receipt!.Decision);
        Assert.Equal("分批过期并加随机值", applied.Series.QuestionBank[0].Answer);
        Assert.Equal(session, applied.Series.QuestionBank[0].AnswerSourceSessionId);
        Assert.Equal(["人工维护的答法", "用户后来手动调整的答法"],
            applied.Series.QuestionBank[0].AnswerHistory!.Select(revision => revision.Answer));
        Assert.Equal(5, applied.Series.QuestionBank[0].TimesAsked);
    }

    [Fact]
    public void ReplacingAnAnswerPreservesItsSourceAttribution()
    {
        var workspace = Series("面试");
        var originalQuestionSession = Id();
        var answerSession = Id();
        var candidateSession = Id();
        workspace = workspace with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Question = "Redis 缓存雪崩怎么处理",
                    Answer = "原答法",
                    TimesAsked = 2,
                    SourceSessionId = originalQuestionSession,
                    SourceSessionIds = [originalQuestionSession, answerSession],
                    AnswerSourceSessionId = answerSession,
                    AnswerRevision = 2,
                    UpdatedAt = Now
                }
            ]
        };
        var collected = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "新答法")], candidateSession, now: Now);
        var candidateId = collected.Added.First();
        var applied = KnowledgeMerger.ApplyCandidate(collected.Series, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now.AddSeconds(1));
        Assert.NotNull(applied.Receipt);

        var item = applied.Series.QuestionBank[0];
        Assert.Equal("新答法", item.Answer);
        Assert.Equal(candidateSession, item.AnswerSourceSessionId);
        Assert.Single(item.AnswerHistory!);
        Assert.Equal("原答法", item.AnswerHistory![0].Answer);
        Assert.Equal(answerSession, item.AnswerHistory![0].SourceSessionId);
    }

    [Fact]
    public void StaleKeepExistingRequiresReviewOfTheCurrentQuestionRevision()
    {
        var workspace = Series("面试");
        var existingSession = Id();
        var candidateSession = Id();
        workspace = workspace with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Question = "Redis 缓存雪崩怎么处理",
                    Answer = "原答法",
                    TimesAsked = 2,
                    SourceSessionId = existingSession,
                    SourceSessionIds = [existingSession]
                }
            ]
        };
        var collected = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "候选答法")], candidateSession, now: Now);
        var candidateId = collected.Added.First();
        workspace = collected.Series with
        {
            QuestionBank =
            [
                KnowledgeMerger.EditQuestionBankItem(collected.Series.QuestionBank[0],
                    "Redis 缓存雪崩怎么处理", "刚刚手动更新的答法", Now.AddSeconds(1))
            ]
        };

        Assert.Null(KnowledgeMerger.ApplyCandidate(workspace, candidateId,
            WorkspaceKnowledgeDecision.KeepExisting, Now).Receipt);
        Assert.Equal("刚刚手动更新的答法", workspace.QuestionBank[0].Answer);
        Assert.Equal(2, workspace.QuestionBank[0].TimesAsked);
        Assert.Equal([existingSession], workspace.QuestionBank[0].SourceSessionIds);

        var refreshed = KnowledgeMerger.RefreshCandidateBase(workspace, candidateId);
        Assert.NotNull(refreshed);
        var applied = KnowledgeMerger.ApplyCandidate(refreshed!, candidateId,
            WorkspaceKnowledgeDecision.KeepExisting, Now);
        Assert.NotNull(applied.Receipt);
        Assert.True(applied.Receipt!.SourceCounted);
        Assert.Equal("刚刚手动更新的答法", applied.Series.QuestionBank[0].Answer);
        Assert.Equal(3, applied.Series.QuestionBank[0].TimesAsked);
        Assert.Equal(new HashSet<string> { existingSession, candidateSession },
            (applied.Series.QuestionBank[0].SourceSessionIds ?? []).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void KeepingAnExistingLessonIsRejectedWhenNoSimilarLessonRemains()
    {
        var workspace = Series("面试");
        var collected = KnowledgeMerger.CollectCandidates(workspace,
            ["面试回答先说结论，再按证据展开"], [], Id(), now: Now);
        var candidateId = collected.Added.First();

        Assert.Null(KnowledgeMerger.ApplyCandidate(collected.Series, candidateId,
            WorkspaceKnowledgeDecision.KeepExisting, Now).Receipt);
        Assert.Empty(collected.Series.Lessons);
        Assert.Equal(WorkspaceKnowledgeStatus.Pending,
            collected.Series.KnowledgeCandidates!.First().Status);
    }

    [Fact]
    public void EditingQuestionWordingInvalidatesOpenReplacementWithoutErasingProvenance()
    {
        var workspace = Series("面试");
        var sourceSession = Id();
        var currentSession = Id();
        var oldRevision = new WorkspaceAnswerRevision
        {
            Answer = "更早的答法",
            RecordedAt = Now.AddSeconds(-10)
        };
        workspace = workspace with
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Question = "Redis 缓存雪崩怎么处理",
                    Answer = "人工维护的答法",
                    TimesAsked = 4,
                    SourceSessionId = sourceSession,
                    SourceSessionIds = [sourceSession],
                    AnswerHistory = [oldRevision],
                    AnswerRevision = 3,
                    UpdatedAt = Now
                }
            ]
        };
        var collected = KnowledgeMerger.CollectCandidates(workspace, [],
            [new QuestionBankEntry("Redis 缓存雪崩如何处理", "分批过期并加随机值")], currentSession, now: Now);
        var candidateId = collected.Added.First();
        workspace = collected.Series with
        {
            QuestionBank =
            [
                KnowledgeMerger.EditQuestionBankItem(collected.Series.QuestionBank[0],
                    "Redis 缓存雪崩如何处理", "人工维护的答法", Now.AddSeconds(1))
            ]
        };
        Assert.Equal(4, workspace.QuestionBank[0].AnswerRevision);
        Assert.Equal(["更早的答法"], workspace.QuestionBank[0].AnswerHistory!.Select(revision => revision.Answer));
        Assert.Equal([sourceSession], workspace.QuestionBank[0].SourceSessionIds);
        Assert.Null(KnowledgeMerger.ApplyCandidate(workspace, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now).Receipt);
        var refreshed = KnowledgeMerger.RefreshCandidateBase(workspace, candidateId);
        Assert.NotNull(refreshed);

        var applied = KnowledgeMerger.ApplyCandidate(refreshed!, candidateId,
            WorkspaceKnowledgeDecision.ReplaceAnswer, Now);
        Assert.NotNull(applied.Receipt);
        Assert.True(applied.Receipt!.SourceCounted);
        Assert.Equal("分批过期并加随机值", applied.Series.QuestionBank[0].Answer);
        Assert.Equal(["更早的答法", "人工维护的答法"],
            applied.Series.QuestionBank[0].AnswerHistory!.Select(revision => revision.Answer));
        Assert.Contains(currentSession, applied.Series.QuestionBank[0].SourceSessionIds ?? []);
    }

    [Fact]
    public void WhyAndHowQuestionsRemainSeparateCandidates()
    {
        var workspace = Series("面试");
        var collected = KnowledgeMerger.CollectCandidates(workspace, [],
        [
            new QuestionBankEntry("为什么选择 Redis？", "需要低延迟缓存"),
            new QuestionBankEntry("如何使用 Redis？", "设置过期时间并处理并发")
        ], Id(), now: Now);
        Assert.Equal(2, collected.Added.Count);
        Assert.Equal(2, collected.Added.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact(Skip = "missing API: Workspace.knowledgeCandidates optional (absent vs empty)")]
    public void LegacyKnowledgeFieldsRemainReadable()
    {
    }

    [Fact(Skip = "missing API: LegacyWorkspaceView")]
    public void LegacyReaderCanReadNewWorkspaceFields()
    {
    }

    [Fact]
    public void ParsesLessonsAndBankFromReview()
    {
        var markdown = """
        # 标题
        二面
        ## 问题清单
        1. 介绍一下项目 —— 我的回答要点：订单服务
        ## 经验沉淀
        - 先画时序再讲方案
        - 报出 QPS 等具体数字
        ## 题库更新
        - 问：缓存和数据库怎么保证一致？
          答：先更新数据库再删除缓存。
          删除失败用重试队列兜底。
        - 问：为什么选 Kafka
          答：吞吐高，生态成熟。
        ## 复盘建议
        - 多练
        """;
        var parsed = SessionSummaryParser.ParseAll(markdown);
        Assert.Equal("二面", parsed.Title);
        Assert.Single(parsed.Questions);
        Assert.Equal(["先画时序再讲方案", "报出 QPS 等具体数字"], parsed.Lessons);
        Assert.Equal(2, parsed.Bank.Count);
        Assert.Equal("先更新数据库再删除缓存。\n删除失败用重试队列兜底。", parsed.Bank[0].Answer);
        Assert.Equal("为什么选 Kafka", parsed.Bank[1].Question);
        var profile = ModeProfile.For(MeetingMode.Academic);
        Assert.Contains("报告人", SessionSummaryPrompt.System("学术报告", profile.Role, profile.ReviewFocus));
    }

    [Fact(Skip = "missing API: BackupMerge.workspaces imported-series rename (\"（导入）\")")]
    public void BackupMergeNeverOverwritesNewerWork()
    {
    }

    [Fact]
    public void BackupMergePreservesKnowledgeSourcesHistoryAndReviewReceipt()
    {
        var workspaceId = Id();
        var lessonId = Id();
        var questionId = Id();
        var oldSession = Id();
        var sharedSession = Id();
        var currentSession = Id();
        var sharedRevision = new WorkspaceAnswerRevision
        {
            Answer = "共同历史",
            RecordedAt = Now.AddSeconds(-300)
        };
        var olderRevision = new WorkspaceAnswerRevision
        {
            Answer = "备份中的旧答法",
            RecordedAt = Now.AddSeconds(-200),
            SourceSessionId = oldSession
        };
        var currentRevision = new WorkspaceAnswerRevision
        {
            Answer = "本机历史",
            RecordedAt = Now.AddSeconds(-100),
            SourceSessionId = sharedSession
        };
        var current = new WorkspaceSeries(workspaceId, "A", Now, [],
        [
            new WorkspaceLesson
            {
                Id = lessonId,
                Text = "当前经验",
                SourceSessionId = currentSession,
                SourceSessionIds = [sharedSession, currentSession],
                CreatedAt = Now.AddSeconds(-50),
                Pinned = true
            }
        ])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = "缓存雪崩如何处理",
                    Answer = "当前答法",
                    TimesAsked = 5,
                    SourceSessionId = currentSession,
                    SourceSessionIds = [sharedSession, currentSession],
                    AnswerSourceSessionId = currentSession,
                    AnswerHistory = [sharedRevision, currentRevision],
                    AnswerRevision = 4,
                    UpdatedAt = Now
                }
            ]
        };
        var acceptedReceipt = new WorkspaceKnowledgeReceipt
        {
            OperationId = Guid.NewGuid(),
            CandidateId = "same-candidate",
            Decision = WorkspaceKnowledgeDecision.KeepExisting,
            ReviewedAt = Now.AddSeconds(-25),
            ResultItemId = questionId,
            SourceCounted = true
        };
        current = current with
        {
            KnowledgeCandidates =
            [
                new WorkspaceKnowledgeCandidate
                {
                    Id = "same-candidate",
                    LogicalItemId = "question",
                    Kind = WorkspaceKnowledgeKind.Question,
                    Text = "缓存雪崩如何处理",
                    ProposedAnswer = "备选答法"
                }
            ]
        };

        var backup = new WorkspaceSeries(workspaceId, "A backup", Now.AddSeconds(-20), [],
        [
            new WorkspaceLesson
            {
                Id = lessonId,
                Text = "较旧经验",
                SourceSessionId = oldSession,
                SourceSessionIds = [oldSession, sharedSession],
                CreatedAt = Now.AddSeconds(-150)
            }
        ])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = "缓存雪崩如何处理",
                    Answer = "旧答法",
                    TimesAsked = 4,
                    SourceSessionId = oldSession,
                    SourceSessionIds = [oldSession, sharedSession],
                    AnswerSourceSessionId = oldSession,
                    AnswerHistory = [sharedRevision, olderRevision],
                    AnswerRevision = 2,
                    UpdatedAt = Now.AddSeconds(-10)
                }
            ],
            KnowledgeCandidates =
            [
                new WorkspaceKnowledgeCandidate
                {
                    Id = "same-candidate",
                    LogicalItemId = "question",
                    Kind = WorkspaceKnowledgeKind.Question,
                    Text = "缓存雪崩如何处理",
                    ProposedAnswer = "备选答法",
                    Status = WorkspaceKnowledgeStatus.Accepted,
                    Receipt = acceptedReceipt,
                    CreatedAt = Now.AddSeconds(-80)
                }
            ]
        };

        var merged = Merge([current], [backup])[0];
        var lesson = merged.Lessons.First();
        var question = merged.QuestionBank.First();
        var candidate = merged.KnowledgeCandidates!.First();

        Assert.Equal("A", merged.Name);
        Assert.Equal("当前经验", lesson.Text);
        Assert.True(lesson.Pinned);
        Assert.Equal(Now.AddSeconds(-150), lesson.CreatedAt);
        Assert.Equal(new HashSet<string> { sharedSession, currentSession },
            (lesson.SourceSessionIds ?? []).ToHashSet(StringComparer.Ordinal));
        Assert.Contains(lesson.History, revision => revision.Text == "较旧经验"
            && revision.SourceSessionIds!.ToHashSet(StringComparer.Ordinal).SetEquals([oldSession, sharedSession]));
        Assert.Equal("当前答法", question.Answer);
        Assert.Equal(currentSession, question.AnswerSourceSessionId);
        Assert.Equal(5, question.AnswerRevision);
        Assert.Equal(5, question.TimesAsked);
        Assert.Equal(new HashSet<string> { oldSession, sharedSession, currentSession },
            (question.SourceSessionIds ?? []).ToHashSet(StringComparer.Ordinal));
        var preservedBackupAnswer = question.AnswerHistory!.First(revision => revision.Answer == "旧答法");
        Assert.Equal(backup.QuestionBank[0].UpdatedAt, preservedBackupAnswer.RecordedAt);
        Assert.Equal(oldSession, preservedBackupAnswer.SourceSessionId);
        Assert.Equal(new HashSet<string>
        {
            sharedRevision.Id, olderRevision.Id, currentRevision.Id, preservedBackupAnswer.Id
        }, (question.AnswerHistory ?? []).Select(revision => revision.Id).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(WorkspaceKnowledgeStatus.Accepted, candidate.Status);
        Assert.Equal(acceptedReceipt, candidate.Receipt);
        Assert.Equal(questionId, candidate.Receipt?.ResultItemId);
        Assert.Equal(1, BackupMerge.Workspaces([current], [backup]).Updated);

        var replayed = Merge([merged], [backup])[0];
        Assert.Equal("当前经验", replayed.Lessons.First().Text);
        Assert.Single(replayed.Lessons.First().History.Where(revision => revision.Text == "较旧经验"));
    }

    [Fact]
    public void BackupMergePreservesLosingManualAnswerOnceWithItsSourceAndTimestamp()
    {
        var workspaceId = Id();
        var questionId = Id();
        var sourceSession = Id();
        var manualEditAt = Now.AddSeconds(-60);
        var acceptedAt = Now;
        var manualItem = KnowledgeMerger.EditQuestionBankItem(new QuestionBankItem
        {
            Id = questionId,
            Question = "缓存雪崩怎么处理",
            Answer = "初始答法",
            SourceSessionId = sourceSession,
            AnswerSourceSessionId = sourceSession,
            UpdatedAt = Now.AddSeconds(-120)
        }, "缓存雪崩怎么处理", "手动维护的答法", manualEditAt);
        Assert.Equal(["初始答法"], manualItem.AnswerHistory!.Select(revision => revision.Answer));
        Assert.Null(manualItem.AnswerSourceSessionId);
        Assert.Null(manualItem.KnownAnswerSourceSessionId);

        var current = new WorkspaceSeries(workspaceId, "面试", acceptedAt, [], [])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = manualItem.Question,
                    Answer = "已接受的建议",
                    SourceSessionId = sourceSession,
                    AnswerSourceSessionId = Id(),
                    AnswerRevision = 2,
                    UpdatedAt = acceptedAt
                }
            ]
        };
        var backup = new WorkspaceSeries(workspaceId, "面试备份", acceptedAt.AddSeconds(-1), [], [])
        {
            QuestionBank = [manualItem]
        };

        var firstMerge = Merge([current], [backup])[0];
        var firstQuestion = firstMerge.QuestionBank.First();
        var preserved = firstQuestion.AnswerHistory!.First(revision => revision.Answer == "手动维护的答法");
        Assert.Equal("已接受的建议", firstQuestion.Answer);
        Assert.Equal(manualEditAt, preserved.RecordedAt);
        Assert.Null(preserved.SourceSessionId);

        var secondMerge = Merge([firstMerge], [backup])[0];
        var secondQuestion = secondMerge.QuestionBank.First();
        var preservedRevisions = (secondQuestion.AnswerHistory ?? [])
            .Where(revision => revision.Answer == "手动维护的答法").ToList();
        Assert.Single(preservedRevisions);
        Assert.Equal(manualEditAt, preservedRevisions[0].RecordedAt);
        Assert.Null(preservedRevisions[0].SourceSessionId);
    }

    [Fact]
    public void BackupMergeDoesNotRestoreSourceForSameTextManualAnswer()
    {
        var workspaceId = Id();
        var questionId = Id();
        var oldSourceSessionId = Id();
        var current = new WorkspaceSeries(workspaceId, "面试", Now, [], [])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = "如何处理缓存击穿",
                    Answer = "手动维护的答法",
                    SourceSessionId = oldSourceSessionId,
                    AnswerRevision = 1,
                    UpdatedAt = Now
                }
            ]
        };
        var backup = new WorkspaceSeries(workspaceId, "面试备份", Now.AddSeconds(-10), [], [])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = "如何处理缓存击穿",
                    Answer = "手动维护的答法",
                    SourceSessionId = oldSourceSessionId,
                    AnswerSourceSessionId = oldSourceSessionId,
                    UpdatedAt = Now.AddSeconds(-20)
                }
            ]
        };

        var question = Merge([current], [backup])[0].QuestionBank.First();

        Assert.Equal("手动维护的答法", question.Answer);
        Assert.Equal(1, question.AnswerRevision);
        Assert.Null(question.AnswerSourceSessionId);
        Assert.Null(question.KnownAnswerSourceSessionId);
    }

    [Fact]
    public void EditingOnlyLegacyQuestionWordingKeepsAnswerAttribution()
    {
        var sourceSession = Id();
        var item = new QuestionBankItem
        {
            Question = "如何处理缓存击穿",
            Answer = "先加互斥锁",
            SourceSessionId = sourceSession,
            UpdatedAt = Now
        };
        var answer = item.Answer;

        item = KnowledgeMerger.EditQuestionBankItem(item, "缓存击穿如何处理", answer, Now.AddSeconds(1));

        Assert.Equal(1, item.AnswerRevision);
        Assert.Null(item.AnswerHistory);
        Assert.Equal(sourceSession, item.AnswerSourceSessionId);
        Assert.Equal(sourceSession, item.KnownAnswerSourceSessionId);
    }

    [Fact]
    public void BackupMergePreservesWinningLegacyAnswerAttributionWhenAnswersDiverge()
    {
        var workspaceId = Id();
        var questionId = Id();
        var currentSource = Id();
        var backupSource = Id();
        var current = new WorkspaceSeries(workspaceId, "面试", Now, [], [])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = "如何处理缓存击穿",
                    Answer = "当前答法",
                    SourceSessionId = currentSource,
                    UpdatedAt = Now
                }
            ]
        };
        var backup = new WorkspaceSeries(workspaceId, "面试备份", Now.AddSeconds(-10), [], [])
        {
            QuestionBank =
            [
                new QuestionBankItem
                {
                    Id = questionId,
                    Question = "如何处理缓存击穿",
                    Answer = "旧答法",
                    SourceSessionId = backupSource,
                    UpdatedAt = Now.AddSeconds(-20)
                }
            ]
        };

        var merged = Merge([current], [backup])[0];
        var question = merged.QuestionBank.First();
        var preserved = question.AnswerHistory!.First();

        Assert.Equal("当前答法", question.Answer);
        Assert.Equal(1, question.AnswerRevision);
        Assert.Equal(currentSource, question.AnswerSourceSessionId);
        Assert.Equal(currentSource, question.KnownAnswerSourceSessionId);
        Assert.Equal("旧答法", preserved.Answer);
        Assert.Equal(backupSource, preserved.SourceSessionId);
    }

    [Fact]
    public void CurrentSeriesRevisionHasStableIdentityAndDuplicateBackupDoesNotAddHistory()
    {
        var workspaceId = Id();
        var itemId = Id();
        var sessionId = Id();
        var item = new WorkspaceSeriesItem
        {
            Id = itemId,
            Kind = WorkspaceSeriesItem.KindValue.Decision,
            Text = "按周发布",
            SourceSessionId = sessionId,
            EvidenceId = "decision-1",
            ClaimRevision = "claim-1",
            SourceRevision = "line-1",
            ConfirmedAt = Now,
            UpdatedAt = Now
        };

        var current = new WorkspaceSeries(workspaceId, "系列", Now, [], []) { SeriesItems = [item] };
        var backup = new WorkspaceSeries(workspaceId, "系列", Now.AddSeconds(-1), [], [])
        {
            SeriesItems = [item]
        };
        var merged = Merge([current], [backup])[0];
        var mergedItem = merged.SeriesItems!.First();

        Assert.Equal(Json(item), Json(mergedItem));
        Assert.Empty(mergedItem.History);
    }

    [Fact]
    public void ManifestValidationAndSettingsFilter()
    {
        var manifest = new BackupManifest
        {
            Kind = BackupManifest.BackupKind.Full,
            AppVersion = "1.3.0",
            AppBuild = "20",
            IncludesSecrets = false,
            WorkspaceCount = 1,
            SessionCount = 2
        };
        manifest.Validate();
        Assert.Equal("nextthought-backup", manifest.Format);
        var legacy = manifest with { Format = "interviewhacker-backup" };
        legacy.Validate();
        var future = manifest with { Version = 99 };
        var error = Assert.Throws<BackupManifest.ValidationException>(future.Validate);
        Assert.Contains("99", error.Message);
        var filtered = SettingsSnapshot.Filter(new Dictionary<string, object?>
        {
            ["apiModel"] = "x",
            ["selectedTargetID"] = "window:1",
            ["AppleLanguages"] = new[] { "zh" }
        });
        Assert.Equal(["apiModel"], filtered.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Contains("stt.soniox", SettingsSnapshot.SecretAccounts);
    }

    [Fact]
    public void RestoreMapsLegacyAutoApplyLessonsPreferenceToCandidateCollection()
    {
        var legacy = SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoApplyLessons"] = false,
            ["apiModel"] = "legacy-model",
            ["selectedTargetID"] = "window:1"
        });
        Assert.Equal(false, legacy["autoCollectKnowledgeCandidates"]);
        Assert.DoesNotContain("autoApplyLessons", legacy.Keys);
        Assert.Equal("legacy-model", legacy["apiModel"]);
        Assert.DoesNotContain("selectedTargetID", legacy.Keys);

        var current = SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoApplyLessons"] = true,
            ["autoCollectKnowledgeCandidates"] = false
        });
        Assert.Equal(false, current["autoCollectKnowledgeCandidates"]);
        Assert.DoesNotContain("autoApplyLessons", current.Keys);

        var currentOnly = SettingsSnapshot.ValuesForRestore(new Dictionary<string, object?>
        {
            ["autoCollectKnowledgeCandidates"] = true
        });
        Assert.Equal(true, currentOnly["autoCollectKnowledgeCandidates"]);
    }
}
