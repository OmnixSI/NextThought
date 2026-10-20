import Testing
@testable import InterviewHackerCore

struct RealtimeSetupReadinessTests {
    @Test func coreVoiceAndConversationCannotBeBypassed() {
        for mask in 0..<64 {
            let speech = mask & 1 != 0, capture = mask & 2 != 0, micRequired = mask & 4 != 0
            let mic = mask & 8 != 0, answers = mask & 16 != 0, source = mask & 32 != 0
            let setup = RealtimeSetupReadiness(speech: speech, capture: capture, microphoneRequired: micRequired, microphone: mic, answers: answers, audioSource: source)
            #expect(setup.voiceReady == (speech && capture && (!micRequired || mic)))
            #expect(setup.canStart == (speech && capture && (!micRequired || mic) && answers && source))
        }
    }
    @Test func optionalDocumentModelsAreNotNeededToStartRealtimeAssistance() {
        let setup = RealtimeSetupReadiness(speech: true, capture: true, microphoneRequired: true, microphone: true, answers: true, audioSource: true)
        let documents = DocumentFeatureAvailability(checking: false, runtime: false, ocr: false, layout: false, embedding: false, codexConnected: false)
        #expect(!documents.ready(.paddle) && !documents.ready(.semantic))
        #expect(setup.canStart)
    }
}
