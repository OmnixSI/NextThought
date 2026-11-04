import Testing
@testable import InterviewHackerCore

struct OnboardingFlowTests {
    @Test func stepsRunInTheDesignedOrder() {
        #expect(OnboardingStep.allCases == [.welcome, .sample, .sound, .answers, .materials, .start])
        #expect(OnboardingStep.count == 6)
        #expect(OnboardingStep.welcome.rawValue == 0 && OnboardingStep.answers.rawValue == 3 && OnboardingStep.start.rawValue == 5)
    }

    @Test func nextAndPreviousStopAtTheEnds() {
        #expect(OnboardingStep.welcome.previous == nil)
        #expect(OnboardingStep.welcome.next == .sample)
        #expect(OnboardingStep.start.next == nil)
        #expect(OnboardingStep.start.previous == .materials)
        #expect(OnboardingStep.start.isLast && !OnboardingStep.materials.isLast)
        #expect(OnboardingStep.sound.position == 3)
    }

    @Test func storedStepIsClampedIntoRange() {
        #expect(OnboardingStep.clamped(-4) == .welcome)
        #expect(OnboardingStep.clamped(2) == .sound)
        #expect(OnboardingStep.clamped(5) == .start)
        #expect(OnboardingStep.clamped(99) == .start)
    }

    @Test func onlyOptionalStepsCanBeSkipped() {
        let skippable = OnboardingStep.allCases.filter(\.isSkippable)
        #expect(skippable == [.sample, .answers, .materials])
    }

    @Test func everyStepHasATimeEstimate() {
        #expect(OnboardingStep.allCases.allSatisfy { $0.estimatedSeconds > 0 })
        #expect(OnboardingStep.allCases.reduce(0) { $0 + $1.estimatedSeconds } == 270)
    }
}
