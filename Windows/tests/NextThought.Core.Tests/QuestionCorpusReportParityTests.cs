using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from QuestionCorpusReportTests.swift: the tuned and held-out recognizer corpora
/// meet their recall / false-trigger budgets in interview and meeting modes, fillers and audio
/// checks behave, question-ending partials trigger sooner, extended finals supersede a premature
/// trigger, and preceding statements become the lead-in.
/// </summary>
public sealed class QuestionCorpusReportParityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(80_000);

    [Fact]
    public void RealisticRecognizerOutputCorpus()
    {
        foreach (var mode in new[] { MeetingMode.Interview, MeetingMode.Meeting })
        {
            var tuned = QuestionDetectionEvaluator.Evaluate(QuestionEvalCorpus.Extended, mode);
            Assert.True(tuned.Recall >= 0.95, $"{mode} {tuned}");
            Assert.True(tuned.FalseTriggers <= 1, $"{mode} {tuned}");

            var heldOut = QuestionDetectionEvaluator.Evaluate(QuestionEvalCorpus.HeldOut, mode);
            Assert.True(heldOut.Recall >= 0.9, $"{mode} {heldOut}");
            Assert.True(heldOut.FalseTriggers <= 1, $"{mode} {heldOut}");
        }
    }

    [Fact]
    public void FillersAndAudioChecks()
    {
        Assert.True(QuestionIntentClassifier.Assess("嗯 那个 你们的 QPS 大概有多少").IsActionable);
        Assert.True(QuestionIntentClassifier.Assess("um so what's your take on this").IsActionable);
        Assert.False(QuestionIntentClassifier.Assess("can you hear me now").IsActionable);
        Assert.False(QuestionIntentClassifier.Assess("能听到我说话吗").IsActionable);
        // Word fillers are only stripped when separated: “对于…” stays intact.
        Assert.Equal("对于这个问题你怎么看", QuestionIntentClassifier.StrippingFillers("对于这个问题你怎么看"));
        Assert.Equal("那下一个问题", QuestionIntentClassifier.StrippingFillers("嗯 好的 那下一个问题"));
    }

    [Fact]
    public void QuestionEndingPartialTriggersSooner()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Interview);
        tracker.Ingest("你为什么离开上一家公司？", false, Guid.NewGuid(), Now);
        Assert.Equal(CandidateKind.Trigger, tracker.Tick(Now.AddSeconds(0.7)).Kind);
    }

    [Fact]
    public void ExtendedFinalSupersedesPrematureTrigger()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Interview);
        var utterance = Guid.NewGuid();
        tracker.Ingest("你怎么看微服务？", false, utterance, Now);
        var early = tracker.Tick(Now.AddSeconds(0.8));
        Assert.Equal(CandidateKind.Trigger, early.Kind);
        var final = tracker.Ingest("你怎么看微服务？特别是拆分粒度和数据一致性怎么处理", true, utterance, Now.AddSeconds(3));
        Assert.Equal(CandidateKind.Trigger, final.Kind);
        Assert.Equal(early.Candidate!.Id, final.Candidate!.Supersedes!.Value);
    }

    [Fact]
    public void PrecedingStatementsBecomeLeadIn()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Interview);
        Assert.Equal(CandidateKind.None,
            tracker.Ingest("我看你简历上写了 Kafka", true, Guid.NewGuid(), Now).Kind);
        var decision = tracker.Ingest("讲讲你是怎么用的", true, Guid.NewGuid(), Now.AddSeconds(2));
        Assert.Equal(CandidateKind.Trigger, decision.Kind);
        var candidate = decision.Candidate!;
        Assert.Equal("我看你简历上写了 Kafka", candidate.LeadIn);
        Assert.True(candidate.FullText == "我看你简历上写了 Kafka讲讲你是怎么用的"
                    || candidate.FullText.Contains("Kafka"));
        // Old statements are not attached.
        tracker.Ingest("今天天气不错", true, Guid.NewGuid(), Now.AddSeconds(10));
        var later = tracker.Ingest("你最大的缺点是什么", true, Guid.NewGuid(), Now.AddSeconds(30));
        Assert.Equal(CandidateKind.Trigger, later.Kind);
        Assert.Null(later.Candidate!.LeadIn);
    }
}
