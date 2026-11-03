import Testing
@testable import InterviewHackerCore

struct LiveTranslationRevisionGateTests {
    @Test func continuousRecognitionStillDispatchesPartialTranslations() {
        var throttle = LiveTranslationPartialThrottle(interval: 0.5)

        let first = throttle.shouldDispatch(at: 0.0)
        let beforeInterval = throttle.shouldDispatch(at: 0.2)
        let stillBeforeInterval = throttle.shouldDispatch(at: 0.4)
        let atInterval = throttle.shouldDispatch(at: 0.5)
        let afterResetInterval = throttle.shouldDispatch(at: 0.7)
        let nextInterval = throttle.shouldDispatch(at: 1.0)

        #expect(first)
        #expect(!beforeInterval)
        #expect(!stillBeforeInterval)
        #expect(atInterval)
        #expect(!afterResetInterval)
        #expect(nextInterval)
    }

    @Test func firstPartialCanDispatchImmediatelyAfterAnUtteranceReset() {
        var throttle = LiveTranslationPartialThrottle(interval: 0.5)
        let first = throttle.shouldDispatch(at: 0.0)
        let throttled = throttle.shouldDispatch(at: 0.1)
        throttle.reset()
        let afterReset = throttle.shouldDispatch(at: 0.2)

        #expect(first)
        #expect(!throttled)
        #expect(afterReset)
    }

    @Test func partialTranslationCanAppearWhileRecognitionKeepsUpdating() {
        var gate = LiveTranslationRevisionGate()
        let first = gate.revise()
        let second = gate.revise()
        let acceptedFirst = gate.accept(first)
        let acceptedSecond = gate.accept(second)

        #expect(!gate.isLatest(first))
        #expect(gate.isLatest(second))
        #expect(acceptedFirst)
        #expect(acceptedSecond)
    }

    @Test func olderTranslationCannotReplaceANewerResult() {
        var gate = LiveTranslationRevisionGate()
        let first = gate.revise()
        let second = gate.revise()
        let acceptedSecond = gate.accept(second)
        let acceptedFirst = gate.accept(first)

        #expect(acceptedSecond)
        #expect(!acceptedFirst)
    }

    @Test func clearingAnUtteranceRejectsItsLateTranslation() {
        var gate = LiveTranslationRevisionGate()
        let old = gate.revise()
        gate.invalidate()
        let current = gate.revise()
        let acceptedOld = gate.accept(old)
        let acceptedCurrent = gate.accept(current)

        #expect(!acceptedOld)
        #expect(acceptedCurrent)
    }
}
