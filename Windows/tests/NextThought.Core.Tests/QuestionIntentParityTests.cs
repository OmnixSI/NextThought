using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from QuestionIntentTests.swift: the labeled hand-written corpus, the intent
/// classes, the stricter lesson-mode threshold, and the candidate tracker's stability,
/// duplicate, fragment and lead-in rules.
/// </summary>
public sealed class QuestionIntentParityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(50_000);

    /// <summary>Labeled corpus covering the cases called out in the audit: Chinese questions
    /// without “？”, rhetorical/self-answered questions, fillers, first-person narration,
    /// follow-ups and code-switching.</summary>
    private static readonly IReadOnlyList<QuestionDetectionSample> QuestionCorpus =
    [
        // Explicit questions
        new("How does a database index improve lookup speed", true),
        new("What are the trade-offs between Redis and Memcached?", true),
        new("Why did you choose Kafka over RabbitMQ", true),
        new("Can you walk me through your last project", true),
        new("Have you ever dealt with a production outage", true),
        new("Is there a way to make this query faster", true),
        new("So how would you scale this service to ten million users", true),
        new("Tell me about a time you disagreed with your manager", true),
        new("Could you explain the CAP theorem", true),
        new("Which data structure would you use here?", true),
        new("数据库索引有什么缺点", true),
        new("你为什么离开上一家公司", true),
        new("这个方案的瓶颈在哪里", true),
        new("你能不能讲一下 Redis 的持久化机制", true),
        new("介绍一下你自己", true),
        new("请解释一下进程和线程的区别", true),
        new("这个 database index 是怎么提升 lookup speed 的", true),
        new("你们团队是怎么做 code review 的", true),
        new("如果流量突然翻倍你会怎么处理", true),
        new("你对我们公司有了解吗", true),
        new("这个结论成立吗", true),
        new("哪种排序算法是稳定的", true),
        new("说说你对微服务的理解", true),
        new("举个例子说明一下", true),
        new("线程池的核心参数有哪些", true),
        new("你觉得这个设计合理吗", true),
        new("大家有没有问题", true),
        new("这道题的时间复杂度是多少", true),
        new("那缺点呢", true),
        new("为什么", true),
        new("然后呢", true),
        new("What about the cost", true),
        new("why not", true),
        new("Compare TCP and UDP for me", true),
        new("下一步我们是不是应该先上线灰度", true),
        // Statements, narration, rhetorical questions and fillers
        new("The database index improves lookup speed", false),
        new("这个 database index 会提升 lookup speed", false),
        new("我来介绍一下今天的议程", false),
        new("Let me explain how the cache works", false),
        new("为什么呢？因为哈希表的查询是常数时间", false),
        new("怎么说呢，这个项目其实挺复杂的", false),
        new("没什么问题，我们继续", false),
        new("I don't know why the build failed yesterday", false),
        new("今天我们讲一下动态规划", false),
        new("我们先看一下这个图", false),
        new("Okay thanks everyone", false),
        new("下周三之前把文档发给我", false),
        new("好的，没问题", false),
        new("This is the final version of the design", false),
        new("Let's move on to the next item", false),
        new("我觉得这个方案可以", false),
        new("嗯，对，就是这样", false),
        new("The answer is forty two", false),
        new("上次会议的结论是先做性能测试", false),
        new("We will ship it next sprint", false),
        new("我给大家讲讲这个算法的思路", false),
        new("So the key insight is that we cache aggressively", false),
        new("不管怎么样，先把它跑起来", false),
        new("Thanks for having me today", false),
        new("这个函数返回一个列表", false)
    ];

    [Fact]
    public void CorpusRecallAndFalseTriggerBudget()
    {
        var report = QuestionDetectionEvaluator.Evaluate(QuestionCorpus, MeetingMode.Meeting);
        // Targets for the first, rule-based layer. Raise them as the corpus grows.
        Assert.True(report.Recall >= 0.9, report.ToString());
        Assert.True(report.FalseTriggers <= 2, report.ToString());
    }

    [Fact]
    public void ClassifiesIntents()
    {
        Assert.Equal(UtteranceIntent.ExplanationRequest, QuestionIntentClassifier.Assess("介绍一下你自己").Intent);
        Assert.Equal(UtteranceIntent.Question, QuestionIntentClassifier.Assess("Why did you choose Swift?").Intent);
        Assert.Equal(UtteranceIntent.FollowUp, QuestionIntentClassifier.Assess("那缺点呢").Intent);
        Assert.Equal(UtteranceIntent.Incomplete, QuestionIntentClassifier.Assess("Can you").Intent);
        Assert.Equal(UtteranceIntent.Incomplete, QuestionIntentClassifier.Assess("如果流量突然翻倍，").Intent);
        Assert.Equal(UtteranceIntent.Statement, QuestionIntentClassifier.Assess("我来介绍一下今天的议程").Intent);
    }

    [Fact]
    public void LessonModeIsStricterForRhetoricalQuestions()
    {
        var lesson = QuestionIntentClassifier.Assess("为什么呢？因为哈希表的查询是常数时间", MeetingMode.Lesson);
        Assert.True(!lesson.IsActionable ||
                    lesson.Score < QuestionIntentClassifier.Thresholds(MeetingMode.Lesson).Actionable);
    }

    [Fact]
    public void StreamingRevisionsUpdateOneCandidateAndTriggerOnce()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        var utterance = Guid.NewGuid();

        var first = tracker.Ingest("How does", false, utterance, Now);
        Assert.Equal(CandidateKind.Updated, first.Kind);
        var second = tracker.Ingest("How does a database index work", false, utterance, Now.AddSeconds(0.3));
        Assert.Equal(CandidateKind.Updated, second.Kind);
        Assert.Equal(first.Candidate!.Id, second.Candidate!.Id);
        Assert.Equal(1, second.Candidate.Revision);
        // Not yet stable.
        Assert.Equal(CandidateKind.None, tracker.Tick(Now.AddSeconds(0.5)).Kind);
        var final = tracker.Ingest("How does a database index work?", true, utterance, Now.AddSeconds(1));
        Assert.Equal(CandidateKind.Trigger, final.Kind);
        Assert.Equal(CandidateKind.None,
            tracker.Ingest("How does a database index work?", true, Guid.NewGuid(), Now.AddSeconds(3)).Kind);
    }

    [Fact]
    public void StablePartialTriggersEarlyAndFinalDoesNotRepeat()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        var utterance = Guid.NewGuid();
        tracker.Ingest("你为什么离开上一家公司？", false, utterance, Now);
        Assert.Equal(CandidateKind.Trigger, tracker.Tick(Now.AddSeconds(1.5)).Kind);
        Assert.Equal(CandidateKind.None,
            tracker.Ingest("你为什么离开上一家公司？", true, utterance, Now.AddSeconds(2)).Kind);
    }

    [Fact]
    public void FragmentsJoinAcrossFinalSegments()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        Assert.Equal(CandidateKind.Updated,
            tracker.Ingest("Can you", true, Guid.NewGuid(), Now).Kind);
        var joined = tracker.Ingest("explain this design", true, Guid.NewGuid(), Now.AddSeconds(2));
        Assert.Equal(CandidateKind.Trigger, joined.Kind);
        Assert.Equal("Can you explain this design", joined.Candidate!.Text);
    }

    [Fact]
    public void StaleFragmentIsDropped()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        tracker.Ingest("Can you", true, Guid.NewGuid(), Now);
        Assert.Equal(CandidateKind.None,
            tracker.Ingest("The meeting has ended", true, Guid.NewGuid(), Now.AddSeconds(9)).Kind);
    }

    [Fact]
    public void StatementsNeverCallTheModel()
    {
        var tracker = new QuestionCandidateTracker(MeetingMode.Meeting);
        var utterance = Guid.NewGuid();
        Assert.Equal(CandidateKind.None, tracker.Ingest("我们先看一下这个图", false, utterance, Now).Kind);
        Assert.Equal(CandidateKind.None,
            tracker.Ingest("我们先看一下这个图", true, utterance, Now.AddSeconds(1)).Kind);
    }
}
