using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for the knowledge half of Workspace.swift (KnowledgeMerger, review receipts, NotesCarry).</summary>
public sealed class KnowledgeMergerTests
{
    private static WorkspaceSeries Series(params WorkspaceLesson[] lessons) =>
        WorkspaceSeries.Create("面试") with { Lessons = lessons };

    private static WorkspaceSeries WithBank(WorkspaceSeries series, QuestionBankItem item) =>
        series with { QuestionBank = [item] };

    [Fact]
    public void SimilarityIsOneForIdenticalTextAndLowForUnrelatedText()
    {
        Assert.Equal(1.0, KnowledgeMerger.Similarity("先说结论再说依据", "先说结论再说依据"));
        Assert.Equal(0.0, KnowledgeMerger.Similarity("今天天气不错", "数据库索引优化"));
        Assert.True(KnowledgeMerger.Similarity("先说结论，再说依据", "先说结论再说依据") >= 0.6);
    }

    [Fact]
    public void QuestionIntentKeepsWhyAndHowApart()
    {
        Assert.Equal("why", KnowledgeMerger.QuestionIntent("为什么用 Redis"));
        Assert.Equal("how", KnowledgeMerger.QuestionIntent("怎么用 Redis"));
        Assert.False(KnowledgeMerger.SimilarQuestion("为什么用 Redis", "怎么用 Redis"));
        Assert.True(KnowledgeMerger.SimilarQuestion("怎么用 Redis", "如何用 Redis"));
        Assert.True(KnowledgeMerger.SimilarQuestion("请问怎么用 Redis", "怎样用 Redis"));
    }

    [Fact]
    public void QuestionKeyDropsPolitenessAndFillers()
    {
        var chinese = KnowledgeMerger.QuestionKey("请问你一下，怎么用 Redis");
        Assert.DoesNotContain("请问", chinese);
        Assert.DoesNotContain("怎么", chinese);
        Assert.Contains("redis", chinese);
        Assert.Contains("redis", KnowledgeMerger.QuestionKey("can you tell me about Redis"));
    }

    [Fact]
    public void CollectingTheSameSummaryTwiceAddsNothingTheSecondTime()
    {
        var series = Series();
        var lessons = new[] { "先说结论，再说依据" };
        var bank = new[] { new QuestionBankEntry("为什么用 Redis", "因为读多写少") };

        var first = KnowledgeMerger.CollectCandidates(series, lessons, bank, "s1");
        Assert.Equal(2, first.Added.Count);
        Assert.Equal(2, first.Series.KnowledgeCandidates.Count);

        var second = KnowledgeMerger.CollectCandidates(first.Series, lessons, bank, "s1");
        Assert.Empty(second.Added);
        Assert.Equal(2, second.Series.KnowledgeCandidates.Count);
    }

    [Fact]
    public void ShortLessonsAndAnswersLessQuestionsAreDropped()
    {
        var (series, added) = KnowledgeMerger.CollectCandidates(
            Series(),
            ["短"],
            [new QuestionBankEntry("什么问题", "")],
            "s1");
        Assert.Empty(added);
        Assert.Empty(series.KnowledgeCandidates);
    }

    [Fact]
    public void AcceptingALessonCreatesItWithItsSource()
    {
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(), ["先说结论，再说依据"], [], "s1");
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(series, added[0], WorkspaceKnowledgeDecision.Add, DateTimeOffset.UtcNow);

        Assert.NotNull(receipt);
        Assert.True(receipt!.SourceCounted);
        var lesson = Assert.Single(reviewed.Lessons);
        Assert.Equal("先说结论，再说依据", lesson.Text);
        Assert.Equal("s1", lesson.SourceSessionId);
        Assert.Equal([scope("s1")], lesson.SourceSessionIds!.Select(scope).ToArray());
        Assert.Equal(WorkspaceKnowledgeStatus.Accepted, reviewed.KnowledgeCandidates[0].Status);
        Assert.Equal(lesson.Id, receipt.ResultItemId);
    }

    [Fact]
    public void AcceptingASimilarLessonAddsTheSourceInsteadOfDuplicatingText()
    {
        var existing = WorkspaceLesson.Create("先说结论，再说依据", "s0");
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(existing), ["先说结论再说依据"], [], "s1");
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(series, added[0], WorkspaceKnowledgeDecision.Add, DateTimeOffset.UtcNow);

        Assert.NotNull(receipt);
        var lesson = Assert.Single(reviewed.Lessons);
        Assert.Equal(existing.Id, lesson.Id);
        Assert.Equal(2, lesson.Revision);
        Assert.Contains("s0", lesson.SourceSessionIds!);
        Assert.Contains("s1", lesson.SourceSessionIds!);
        Assert.Single(lesson.History);
    }

    [Fact]
    public void RejectingRecordsAReceiptWithoutTouchingKnowledge()
    {
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(), ["先说结论，再说依据"], [], "s1");
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(series, added[0], WorkspaceKnowledgeDecision.Reject, DateTimeOffset.UtcNow);

        Assert.NotNull(receipt);
        Assert.False(receipt!.SourceCounted);
        Assert.Empty(reviewed.Lessons);
        Assert.Equal(WorkspaceKnowledgeStatus.Rejected, reviewed.KnowledgeCandidates[0].Status);
    }

    [Fact]
    public void ReplacingAnAnswerKeepsTheOldOneInHistory()
    {
        var item = new QuestionBankItem
        {
            Id = Guid.NewGuid().ToString("D"),
            Question = "为什么用 Redis",
            Answer = "快",
            AnswerRevision = 0,
            TimesAsked = 1,
            SourceSessionId = "s0",
            AnswerSourceSessionId = "s0",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var (series, added) = KnowledgeMerger.CollectCandidates(WithBank(Series(), item), [], [new QuestionBankEntry("为什么用 Redis", "因为读多写少")], "s1");
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(series, added[0], WorkspaceKnowledgeDecision.ReplaceAnswer, DateTimeOffset.UtcNow);

        Assert.NotNull(receipt);
        var bank = Assert.Single(reviewed.QuestionBank);
        Assert.Equal("因为读多写少", bank.Answer);
        Assert.Equal(1, bank.AnswerRevision);
        var old = Assert.Single(bank.AnswerHistory!);
        Assert.Equal("快", old.Answer);
        Assert.Equal("s0", old.SourceSessionId);
    }

    [Fact]
    public void ReplacingAgainstAStaleAnswerRevisionIsRefused()
    {
        var item = new QuestionBankItem
        {
            Id = Guid.NewGuid().ToString("D"),
            Question = "为什么用 Redis",
            Answer = "快",
            AnswerRevision = 3,
            SourceSessionId = "s0",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var (series, added) = KnowledgeMerger.CollectCandidates(WithBank(Series(), item), [], [new QuestionBankEntry("为什么用 Redis", "因为读多写少")], "s1");
        // Someone advanced the accepted answer after the suggestion was queued.
        var stale = series.KnowledgeCandidates[0] with { BaseAnswerRevision = 2 };
        var queued = series with { KnowledgeCandidates = [stale] };
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(queued, added[0], WorkspaceKnowledgeDecision.ReplaceAnswer, DateTimeOffset.UtcNow);

        Assert.Null(receipt);
        Assert.Equal("快", reviewed.QuestionBank[0].Answer);
        Assert.Equal(WorkspaceKnowledgeStatus.Pending, reviewed.KnowledgeCandidates[0].Status);
    }

    [Fact]
    public void AcceptingAQuestionCountsSourcesAndTimesAsked()
    {
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(), [], [new QuestionBankEntry("为什么用 Redis", "因为读多写少")], "s1");
        var (reviewed, receipt) = KnowledgeMerger.ApplyCandidate(series, added[0], WorkspaceKnowledgeDecision.Add, DateTimeOffset.UtcNow);

        Assert.NotNull(receipt);
        var bank = Assert.Single(reviewed.QuestionBank);
        Assert.Equal(1, bank.TimesAsked);
        Assert.Equal("s1", bank.AnswerSourceSessionId);
        Assert.Equal(0, bank.AnswerRevision);
    }

    [Fact]
    public void ApplyingTheSameCandidateTwiceReturnsTheSameReceipt()
    {
        var (series, added) = KnowledgeMerger.CollectCandidates(Series(), ["先说结论，再说依据"], [], "s1");
        var operation = Guid.NewGuid();
        var (first, receipt) = KnowledgeMerger.ApplyCandidate(series, added[0], WorkspaceKnowledgeDecision.Add, DateTimeOffset.UtcNow, operation);
        var (second, again) = KnowledgeMerger.ApplyCandidate(first, added[0], WorkspaceKnowledgeDecision.Add, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(receipt, again);
        Assert.Single(second.Lessons);
        Assert.Equal(operation, again!.OperationId);
    }

    [Fact]
    public void EditingAQuestionBumpsRevisionAndKeepsTheOldAnswer()
    {
        var item = new QuestionBankItem
        {
            Id = Guid.NewGuid().ToString("D"),
            Question = "为什么用 Redis",
            Answer = "快",
            TimesAsked = 1,
            SourceSessionId = "s0",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var edited = KnowledgeMerger.EditQuestionBankItem(item, "为什么用 Redis", "因为读多写少", DateTimeOffset.UtcNow);

        Assert.Equal(1, edited.AnswerRevision);
        Assert.Null(edited.AnswerSourceSessionId);
        Assert.Equal("快", Assert.Single(edited.AnswerHistory!).Answer);

        var (restored, changed) = KnowledgeMerger.RestoreAnswerRevision(edited, edited.AnswerHistory![0].Id, DateTimeOffset.UtcNow);
        Assert.True(changed);
        Assert.Equal("快", restored.Answer);
        Assert.Equal(2, restored.AnswerRevision);
    }

    [Fact]
    public void NotesCarryProducesPrefixedLessonsAndQuestionBankEntries()
    {
        var template = NotesTemplate.BuiltIn(NotesTemplateKind.Interview);
        var reply = """
            {"title": "二面复盘", "sections": [
              {"id": "questions", "items": [
                {"text": "为什么用 Redis", "answer": "快", "verdict": "improve", "better": "因为读多写少，且能接受短暂不一致"}
              ]},
              {"id": "improve", "items": ["把实验和论证分开讲"]},
              {"id": "focus", "items": ["下一轮可能追问缓存一致性"]}
            ]}
            """;
        var doc = NotesJson.Parse(reply, template);
        Assert.NotNull(doc);

        var (lessons, bank) = NotesKnowledge.Extract(doc!, template);
        Assert.Contains("把实验和论证分开讲", lessons);
        Assert.Contains("[下一轮] 下一轮可能追问缓存一致性", lessons);
        var entry = Assert.Single(bank);
        Assert.Equal("为什么用 Redis", entry.Question);
        Assert.Equal("因为读多写少，且能接受短暂不一致", entry.Answer);
    }

    [Fact]
    public void AcceptedKnowledgeIsWrittenWithMacFieldNamesAndReloads()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-knowledge-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WorkspaceStore(directory);
            var data = WorkspaceData.Empty.AddSeries("面试").Assign("s1", WorkspaceData.Empty.AddSeries("x").Series[0].Id);
            var series = data.Series[0];
            var (collected, added) = KnowledgeMerger.CollectCandidates(series, ["先说结论，再说依据"], [], "s1");
            var (reviewed, _) = KnowledgeMerger.ApplyCandidate(collected, added[0], WorkspaceKnowledgeDecision.Add, DateTimeOffset.UtcNow);
            data = data.WithSeries(reviewed);
            store.Save(data);

            var json = File.ReadAllText(store.Path_);
            Assert.Contains("\"sourceSessionID\"", json);
            Assert.Contains("\"knowledgeCandidates\"", json);

            var loaded = store.Load();
            var lesson = Assert.Single(loaded.Series[0].Lessons);
            Assert.Equal("先说结论，再说依据", lesson.Text);
            Assert.Single(loaded.Series[0].KnowledgeCandidates);
            Assert.Equal(WorkspaceKnowledgeStatus.Accepted, loaded.Series[0].KnowledgeCandidates[0].Status);
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ExperiencePromptPutsPinnedLessonsFirstAndHonoursTheLimit()
    {
        var pinned = WorkspaceLesson.Create("钉住的经验", "s1") with { Pinned = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-3) };
        var newest = WorkspaceLesson.Create("最新的经验", "s1") with { CreatedAt = DateTimeOffset.UtcNow };
        var series = Series(pinned, newest);

        var text = WorkspaceKnowledgePrompt.ExperienceNotes(series);
        Assert.StartsWith("- 钉住的经验", text);
        Assert.Contains("- 最新的经验", text);
        Assert.Equal("", WorkspaceKnowledgePrompt.ExperienceNotes(series with { UseKnowledge = false }));
        Assert.Equal("", WorkspaceKnowledgePrompt.ExperienceNotes(series, 3));
    }

    private static string scope(string id) => id;
}
