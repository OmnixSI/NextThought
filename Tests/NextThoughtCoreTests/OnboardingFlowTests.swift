import Testing
@testable import NextThoughtCore

struct OnboardingFlowTests {
    @Test func stepsRunInTheDesignedOrder() {
        #expect(OnboardingStep.allCases == [.scene, .sound, .answers, .start])
        #expect(OnboardingStep.count == 4)
        #expect(OnboardingStep.scene.rawValue == 0 && OnboardingStep.answers.rawValue == 2 && OnboardingStep.start.rawValue == 3)
    }

    @Test func nextAndPreviousStopAtTheEnds() {
        #expect(OnboardingStep.scene.previous == nil)
        #expect(OnboardingStep.scene.next == .sound)
        #expect(OnboardingStep.start.next == nil)
        #expect(OnboardingStep.start.previous == .answers)
        #expect(OnboardingStep.start.isLast && !OnboardingStep.answers.isLast)
        #expect(OnboardingStep.sound.position == 2)
    }

    @Test func storedStepIsClampedIntoRange() {
        #expect(OnboardingStep.clamped(-4) == .scene)
        #expect(OnboardingStep.clamped(2) == .answers)
        #expect(OnboardingStep.clamped(3) == .start)
        #expect(OnboardingStep.clamped(99) == .start)
    }

    @Test func onlyTheAnswerServiceCanBeSkipped() {
        #expect(OnboardingStep.allCases.filter(\.isSkippable) == [.answers])
    }

    @Test func everyStepHasATimeEstimate() {
        #expect(OnboardingStep.allCases.allSatisfy { $0.estimatedSeconds > 0 })
    }

    @Test func resumeNamesOnlyWhatIsStillOpen() {
        #expect(OnboardingPending.open(soundReady: false, answersReady: false) == [.sound, .answers])
        #expect(OnboardingPending.open(soundReady: true, answersReady: false) == [.answers])
        #expect(OnboardingPending.open(soundReady: false, answersReady: true) == [.sound])
        #expect(OnboardingPending.open(soundReady: true, answersReady: true).isEmpty)
    }
}
