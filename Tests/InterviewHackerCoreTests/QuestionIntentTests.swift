import Foundation
import Testing
@testable import InterviewHackerCore

/// Labeled, hand-written corpus covering the cases called out in the audit:
/// Chinese questions without “？”, rhetorical/self-answered questions, fillers,
/// first-person narration, follow-ups and code-switching.
let questionCorpus: [QuestionDetectionEvaluator.Sample] = [
    // Explicit questions
    .init("How does a database index improve lookup speed", true),
    .init("What are the trade-offs between Redis and Memcached?", true),
    .init("Why did you choose Kafka over RabbitMQ", true),
    .init("Can you walk me through your last project", true),
    .init("Have you ever dealt with a production outage", true),
    .init("Is there a way to make this query faster", true),
    .init("So how would you scale this service to ten million users", true),
    .init("Tell me about a time you disagreed with your manager", true),
    .init("Could you explain the CAP theorem", true),
    .init("Which data structure would you use here?", true),
    .init("数据库索引有什么缺点", true),
    .init("你为什么离开上一家公司", true),
    .init("这个方案的瓶颈在哪里", true),
    .init("你能不能讲一下 Redis 的持久化机制", true),
    .init("介绍一下你自己", true),
    .init("请解释一下进程和线程的区别", true),
    .init("这个 database index 是怎么提升 lookup speed 的", true),
    .init("你们团队是怎么做 code review 的", true),
    .init("如果流量突然翻倍你会怎么处理", true),
    .init("你对我们公司有了解吗", true),
    .init("这个结论成立吗", true),
    .init("哪种排序算法是稳定的", true),
    .init("说说你对微服务的理解", true),
    .init("举个例子说明一下", true),
    .init("线程池的核心参数有哪些", true),
    .init("你觉得这个设计合理吗", true),
    .init("大家有没有问题", true),
    .init("这道题的时间复杂度是多少", true),
    .init("那缺点呢", true),
    .init("为什么", true),
    .init("然后呢", true),
    .init("What about the cost", true),
    .init("why not", true),
    .init("Compare TCP and UDP for me", true),
    .init("下一步我们是不是应该先上线灰度", true),
    // Statements, narration, rhetorical questions and fillers
    .init("The database index improves lookup speed", false),
    .init("这个 database index 会提升 lookup speed", false),
    .init("我来介绍一下今天的议程", false),
    .init("Let me explain how the cache works", false),
    .init("为什么呢？因为哈希表的查询是常数时间", false),
    .init("怎么说呢，这个项目其实挺复杂的", false),
    .init("没什么问题，我们继续", false),
    .init("I don't know why the build failed yesterday", false),
    .init("今天我们讲一下动态规划", false),
    .init("我们先看一下这个图", false),
    .init("Okay thanks everyone", false),
    .init("下周三之前把文档发给我", false),
    .init("好的，没问题", false),
    .init("This is the final version of the design", false),
    .init("Let's move on to the next item", false),
    .init("我觉得这个方案可以", false),
    .init("嗯，对，就是这样", false),
    .init("The answer is forty two", false),
    .init("上次会议的结论是先做性能测试", false),
    .init("We will ship it next sprint", false),
    .init("我给大家讲讲这个算法的思路", false),
    .init("So the key insight is that we cache aggressively", false),
    .init("不管怎么样，先把它跑起来", false),
    .init("Thanks for having me today", false),
    .init("这个函数返回一个列表", false),
]

struct QuestionIntentTests {
    let now = Date(timeIntervalSince1970: 50_000)

    @Test func corpusRecallAndFalseTriggerBudget() {
        let report = QuestionDetectionEvaluator.evaluate(questionCorpus, mode: .meeting)
        // Targets for the first, rule-based layer. Raise them as the corpus grows.
        #expect(report.recall >= 0.9, "\(report)")
        #expect(report.falseTriggers <= 2, "\(report)")
    }

    @Test func classifiesIntents() {
        #expect(QuestionIntentClassifier.assess("介绍一下你自己").intent == .explanationRequest)
        #expect(QuestionIntentClassifier.assess("Why did you choose Swift?").intent == .question)
        #expect(QuestionIntentClassifier.assess("那缺点呢").intent == .followUp)
        #expect(QuestionIntentClassifier.assess("Can you").intent == .incomplete)
        #expect(QuestionIntentClassifier.assess("如果流量突然翻倍，").intent == .incomplete)
        #expect(QuestionIntentClassifier.assess("我来介绍一下今天的议程").intent == .statement)
    }

    @Test func lessonModeIsStricterForRhetoricalQuestions() {
        let lesson = QuestionIntentClassifier.assess("为什么呢？因为哈希表的查询是常数时间", mode: .lesson)
        #expect(!lesson.intent.isActionable || lesson.score < QuestionIntentClassifier.Thresholds.forMode(.lesson).actionable)
    }

    @Test func streamingRevisionsUpdateOneCandidateAndTriggerOnce() {
        var tracker = QuestionCandidateTracker()
        guard case let .updated(first) = tracker.ingest("How does", isFinal: false, utteranceKey: "u1", now: now) else {
            Issue.record("expected incomplete candidate"); return
        }
        guard case let .updated(second) = tracker.ingest("How does a database index work", isFinal: false, utteranceKey: "u1", now: now.addingTimeInterval(0.3)) else {
            Issue.record("expected update"); return
        }
        #expect(first.id == second.id)
        #expect(second.revision == 1)
        // Not yet stable.
        #expect(tracker.tick(now: now.addingTimeInterval(0.5)) == .none)
        guard case .trigger = tracker.ingest("How does a database index work?", isFinal: true, utteranceKey: "u1", now: now.addingTimeInterval(1)) else {
            Issue.record("final question should trigger"); return
        }
        #expect(tracker.ingest("How does a database index work?", isFinal: true, utteranceKey: "u2", now: now.addingTimeInterval(3)) == .none)
    }

    @Test func stablePartialTriggersEarlyAndFinalDoesNotRepeat() {
        var tracker = QuestionCandidateTracker()
        _ = tracker.ingest("你为什么离开上一家公司？", isFinal: false, utteranceKey: "k", now: now)
        guard case .trigger = tracker.tick(now: now.addingTimeInterval(1.5)) else {
            Issue.record("stable strong partial should trigger"); return
        }
        #expect(tracker.ingest("你为什么离开上一家公司？", isFinal: true, utteranceKey: "k", now: now.addingTimeInterval(2)) == .none)
    }

    @Test func fragmentsJoinAcrossFinalSegments() {
        var tracker = QuestionCandidateTracker()
        #expect({ if case .updated = tracker.ingest("Can you", isFinal: true, now: now) { return true }; return false }())
        guard case let .trigger(candidate) = tracker.ingest("explain this design", isFinal: true, now: now.addingTimeInterval(2)) else {
            Issue.record("joined fragment should trigger"); return
        }
        #expect(candidate.text == "Can you explain this design")
    }

    @Test func staleFragmentIsDropped() {
        var tracker = QuestionCandidateTracker()
        _ = tracker.ingest("Can you", isFinal: true, now: now)
        #expect(tracker.ingest("The meeting has ended", isFinal: true, now: now.addingTimeInterval(9)) == .none)
    }

    @Test func statementsNeverCallTheModel() {
        var tracker = QuestionCandidateTracker()
        #expect(tracker.ingest("我们先看一下这个图", isFinal: false, utteranceKey: "s", now: now) == .none)
        #expect(tracker.ingest("我们先看一下这个图", isFinal: true, utteranceKey: "s", now: now.addingTimeInterval(1)) == .none)
    }
}
