import Foundation
import Testing
@testable import InterviewHackerCore

struct DecisionSpeedTests {
    private func speed(_ samples: [Double]) -> DecisionSpeed { DecisionSpeed(samples: samples) }

    @Test func tiersFollowTheMedian() {
        #expect(speed([]).tier == nil)
        #expect(speed([10, 12]).tier == nil)
        #expect(speed([8, 9, 14, 12, 10]).tier == .instant)
        #expect(speed([40, 50, 60]).tier == .fast)
        #expect(speed([120, 150, 110]).tier == .slow)
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
        let slow = speed([120, 140, 160, 150])
        #expect(slow.budget(for: .beforeAnswer) == nil)
        #expect(slow.budget(for: .ruleTrigger) == nil)
        #expect(slow.budget(for: .uncertainQuestion) != nil)
        let tooSlow = speed([300, 320, 350])
        #expect(tooSlow.budget(for: .uncertainQuestion) == nil)
        #expect(tooSlow.budget(for: .background) != nil)
        #expect(tooSlow.backgroundSlowdown > slow.backgroundSlowdown)
    }

    @Test func missesCountAsTheDeadline() {
        var speed = DecisionSpeed()
        for _ in 0..<3 { speed.record(ms: 20) }
        #expect(speed.tier == .instant)
        // Calls that never return in time must not leave a slow Mac looking fast.
        for _ in 0..<4 { speed.recordMiss(deadlineMs: 300) }
        #expect(speed.tier == .tooSlow)
        #expect(speed.budget(for: .beforeAnswer) == nil)
    }

    @Test func keepsOnlyRecentCalls() {
        var speed = DecisionSpeed()
        for _ in 0..<100 { speed.record(ms: 500) }
        for _ in 0..<DecisionSpeed.window { speed.record(ms: 15) }
        #expect(speed.samples.count == DecisionSpeed.window)
        #expect(speed.tier == .instant)
    }

    @Test func unmeasuredUsesShortDefaults() {
        let none = DecisionSpeed()
        #expect(none.budget(for: .beforeAnswer) == 0.12)
        #expect(none.budget(for: .ruleTrigger) == 0.3)
        #expect(none.budget(for: .background) == 0.8)
    }
}
