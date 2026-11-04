import Testing
@testable import InterviewHackerCore

struct OnboardingFlowTests {
    @Test func stepsRunInTheDesignedOrder() {
        #expect(OnboardingStep.allCases == [.welcome, .sample, .sound, .voice, .answers, .materials, .start])
        #expect(OnboardingStep.count == 7)
        #expect(OnboardingStep.welcome.rawValue == 0 && OnboardingStep.voice.rawValue == 3
                && OnboardingStep.answers.rawValue == 4 && OnboardingStep.start.rawValue == 6)
    }

    @Test func nextAndPreviousStopAtTheEnds() {
        #expect(OnboardingStep.welcome.previous == nil)
        #expect(OnboardingStep.welcome.next == .sample)
        #expect(OnboardingStep.start.next == nil)
        #expect(OnboardingStep.start.previous == .materials)
        #expect(OnboardingStep.start.isLast && !OnboardingStep.materials.isLast)
        #expect(OnboardingStep.sound.position == 3)
        #expect(OnboardingStep.sound.next == .voice && OnboardingStep.voice.next == .answers)
        #expect(OnboardingStep.answers.previous == .voice)
    }

    @Test func storedStepIsClampedIntoRange() {
        #expect(OnboardingStep.clamped(-4) == .welcome)
        #expect(OnboardingStep.clamped(2) == .sound)
        #expect(OnboardingStep.clamped(3) == .voice)
        #expect(OnboardingStep.clamped(6) == .start)
        #expect(OnboardingStep.clamped(99) == .start)
    }

    @Test func onlyOptionalStepsCanBeSkipped() {
        let skippable = OnboardingStep.allCases.filter(\.isSkippable)
        #expect(skippable == [.sample, .voice, .answers, .materials])
    }

    @Test func everyStepHasATimeEstimate() {
        #expect(OnboardingStep.allCases.allSatisfy { $0.estimatedSeconds > 0 })
        #expect(OnboardingStep.allCases.reduce(0) { $0 + $1.estimatedSeconds } == 315)
    }

    @Test func materialsOfferListsOnlyReadyComponents() {
        let both = OnboardingMaterialsOffer(ocrReady: true, semanticReady: true)
        #expect(both.found == [.ocr, .semantic] && both.missing.isEmpty)
        let none = OnboardingMaterialsOffer(ocrReady: false, semanticReady: false)
        #expect(none.found.isEmpty && none.missing == [.ocr, .semantic])
        let ocrOnly = OnboardingMaterialsOffer(ocrReady: true, semanticReady: false)
        #expect(ocrOnly.found == [.ocr] && ocrOnly.missing == [.semantic])
        #expect(ocrOnly.isReady(.ocr) && !ocrOnly.isReady(.semantic))
    }

    @Test func materialsOfferPendingIsWhatIsReadyButNotInUse() {
        let both = OnboardingMaterialsOffer(ocrReady: true, semanticReady: true)
        #expect(both.pending(usingOCR: false, usingSemantic: false) == [.ocr, .semantic])
        #expect(both.pending(usingOCR: true, usingSemantic: false) == [.semantic])
        #expect(both.pending(usingOCR: false, usingSemantic: true) == [.ocr])
        #expect(both.pending(usingOCR: true, usingSemantic: true).isEmpty)
        // Nothing found: nothing to switch on, whatever is in use.
        let none = OnboardingMaterialsOffer(ocrReady: false, semanticReady: false)
        #expect(none.pending(usingOCR: false, usingSemantic: false).isEmpty)
        let semanticOnly = OnboardingMaterialsOffer(ocrReady: false, semanticReady: true)
        #expect(semanticOnly.pending(usingOCR: false, usingSemantic: false) == [.semantic])
    }
}
