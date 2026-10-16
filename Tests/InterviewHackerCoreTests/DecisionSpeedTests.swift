import Foundation
import Testing
@testable import InterviewHackerCore

struct DecisionSpeedTests {
    private func speed(_ samples: [Double]) -> DecisionSpeed { DecisionSpeed(samples: samples) }

    @Test func tiersFollowTheMedian() {
        #expect(speed([]).tier == nil)
        #expect(speed([10, 12]).tier == nil)
        #expect(speed([8, 9, 14, 12, 10]).tier == .instant)
        #expect(speed([25, 30, 35]).tier == .fast)
        #expect(speed([60, 80, 70]).tier == .slow)
        #expect(speed([400, 380, 500]).tier == .tooSlow)
    }

    @Test func fastMacWaitsBrieflyBeforeAnswering() throws {
        let fast = speed([10, 12, 11, 13, 30])
        let before = try #require(fast.budget(for: .beforeAnswer))
        #expect(before >= 0.06 && before <= 0.15)
        let trigger = try #require(fast.budget(for: .ruleTrigger))
        #expect(trigger <= 0.3)
        #expect(try #require(fast.budget(for: .uncertainQuestion)) <= 0.5)
    }

    @Test func slowMacNeverDelaysAnAnswer() {
        let slow = speed([60, 80, 90, 70])
        #expect(slow.budget(for: .beforeAnswer) == nil)
        #expect(slow.budget(for: .ruleTrigger) == nil)
        #expect(slow.budget(for: .uncertainQuestion) != nil)
        let tooSlow = speed([150, 160, 170])
        #expect(tooSlow.budget(for: .uncertainQuestion) == nil)
        #expect(tooSlow.budget(for: .background) != nil)
        #expect(tooSlow.backgroundSlowdown > slow.backgroundSlowdown)
    }

    @Test func missesCountAsTheDeadline() {
        var speed = DecisionSpeed()
        for _ in 0..<3 { speed.record(ms: 10) }
        #expect(speed.tier == .instant)
        // Calls that never return in time must not leave a slow Mac looking fast.
        for _ in 0..<4 { speed.recordMiss(deadlineMs: 300) }
        #expect(speed.tier == .tooSlow)
        #expect(speed.budget(for: .beforeAnswer) == nil)
    }

    @Test func keepsOnlyRecentCalls() {
        var speed = DecisionSpeed()
        for _ in 0..<100 { speed.record(ms: 500) }
        for _ in 0..<DecisionSpeed.window { speed.record(ms: 10) }
        #expect(speed.samples.count == DecisionSpeed.window)
        #expect(speed.tier == .instant)
    }

    /// Laya on an M2 Pro: ~45 ms for one question, ~90 ms for a three-question utterance check.
    @Test func timesAreOneQuestionEquivalents() throws {
        var speed = DecisionSpeed()
        for _ in 0..<5 { speed.record(ms: 45, questions: 1) }
        for _ in 0..<5 { speed.record(ms: 90, questions: 3) }
        #expect(speed.p50 == 45)
        #expect(speed.tier == .fast)
        let trigger = try #require(speed.budget(for: .ruleTrigger, questions: 3))
        #expect(trigger >= 0.18 && trigger <= 0.3)
        // Question kind plus twelve skills would not finish before the answer: skip it.
        #expect(speed.budget(for: .beforeAnswer, questions: 13) == nil)
        #expect(speed.budget(for: .beforeAnswer, questions: 1) != nil)
        #expect(try #require(speed.budget(for: .background, questions: 9)) > speed.budget(for: .background)!)
    }

    @Test func unmeasuredUsesShortDefaults() {
        let none = DecisionSpeed()
        #expect(none.budget(for: .beforeAnswer) == 0.12)
        #expect(none.budget(for: .ruleTrigger) == 0.3)
        #expect(none.budget(for: .background) == 0.8)
    }
}

struct UtteranceVetoTests {
    private func verdict(_ intent: String, _ yes: Double) -> DecisionTasks.UtteranceVerdict? {
        DecisionTasks.utteranceVerdict([
            DecisionTasks.Key.intent: DecisionAnswer(choice: intent, probabilities: [intent: 0.8]),
            DecisionTasks.Key.answerNow: DecisionAnswer(yes: yes),
        ])
    }

    /// Real questions the model labelled correctly but scored low on “answer now” (corpus).
    @Test func lowAnswerScoreAloneDoesNotDeclineARuleTrigger() throws {
        #expect(try #require(verdict("question", 0.11)).declinesTrigger == false)
        #expect(try #require(verdict("incomplete", 0.01)).declinesTrigger == false)
        #expect(try #require(verdict("request", 0.19)).declinesTrigger == false)
    }

    @Test func nonQuestionWithVeryLowScoreDeclines() throws {
        #expect(try #require(verdict("statement", 0.05)).declinesTrigger)
        #expect(try #require(verdict("logistics", 0.1)).declinesTrigger)
        #expect(try #require(verdict("statement", 0.3)).declinesTrigger == false)
    }
}
