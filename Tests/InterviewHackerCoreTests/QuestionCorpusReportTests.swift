import Foundation
import Testing
@testable import InterviewHackerCore

struct QuestionCorpusReportTests {
    let now = Date(timeIntervalSince1970: 80_000)

    @Test func realisticRecognizerOutputCorpus() {
        for mode in [SessionMode.interview, .meeting] {
            let tuned = QuestionDetectionEvaluator.evaluate(extendedQuestionCorpus, mode: mode)
            #expect(tuned.recall >= 0.95, "\(mode) \(tuned)")
            #expect(tuned.falseTriggers <= 1, "\(mode) \(tuned)")
            let heldOut = QuestionDetectionEvaluator.evaluate(heldOutQuestionCorpus, mode: mode)
            #expect(heldOut.recall >= 0.9, "\(mode) \(heldOut)")
            #expect(heldOut.falseTriggers <= 1, "\(mode) \(heldOut)")
        }
    }

    @Test func fillersAndAudioChecks() {
        #expect(QuestionIntentClassifier.assess("嗯 那个 你们的 QPS 大概有多少").intent.isActionable)
        #expect(QuestionIntentClassifier.assess("um so what's your take on this").intent.isActionable)
        #expect(!QuestionIntentClassifier.assess("can you hear me now").intent.isActionable)
        #expect(!QuestionIntentClassifier.assess("能听到我说话吗").intent.isActionable)
        // Word fillers are only stripped when separated: “对于…” stays intact.
        #expect(QuestionIntentClassifier.strippingFillers("对于这个问题你怎么看") == "对于这个问题你怎么看")
        #expect(QuestionIntentClassifier.strippingFillers("嗯 好的 那下一个问题") == "那下一个问题")
    }

    @Test func questionEndingPartialTriggersSooner() {
        var tracker = QuestionCandidateTracker(mode: .interview)
        _ = tracker.ingest("你为什么离开上一家公司？", isFinal: false, utteranceKey: "a", now: now)
        guard case .trigger = tracker.tick(now: now.addingTimeInterval(0.7)) else {
            Issue.record("a partial that ends like a question should trigger after ~0.6 s"); return
        }
    }

    @Test func extendedFinalSupersedesPrematureTrigger() {
        var tracker = QuestionCandidateTracker(mode: .interview)
        _ = tracker.ingest("你怎么看微服务？", isFinal: false, utteranceKey: "u", now: now)
        guard case let .trigger(early) = tracker.tick(now: now.addingTimeInterval(0.8)) else {
            Issue.record("expected early trigger"); return
        }
        guard case let .trigger(final) = tracker.ingest("你怎么看微服务？特别是拆分粒度和数据一致性怎么处理", isFinal: true, utteranceKey: "u", now: now.addingTimeInterval(3)) else {
            Issue.record("extended final should trigger again"); return
        }
        #expect(final.supersedes == early.id)
    }

    @Test func precedingStatementsBecomeLeadIn() {
        var tracker = QuestionCandidateTracker(mode: .interview)
        #expect(tracker.ingest("我看你简历上写了 Kafka", isFinal: true, utteranceKey: "s", now: now) == .none)
        guard case let .trigger(candidate) = tracker.ingest("讲讲你是怎么用的", isFinal: true, utteranceKey: "q", now: now.addingTimeInterval(2)) else {
            Issue.record("expected trigger"); return
        }
        #expect(candidate.leadIn == "我看你简历上写了 Kafka")
        #expect(candidate.fullText == "我看你简历上写了 Kafka讲讲你是怎么用的" || candidate.fullText.contains("Kafka"))
        // Old statements are not attached.
        _ = tracker.ingest("今天天气不错", isFinal: true, utteranceKey: "s2", now: now.addingTimeInterval(10))
        guard case let .trigger(later) = tracker.ingest("你最大的缺点是什么", isFinal: true, utteranceKey: "q2", now: now.addingTimeInterval(30)) else {
            Issue.record("expected trigger"); return
        }
        #expect(later.leadIn == nil)
    }
}
