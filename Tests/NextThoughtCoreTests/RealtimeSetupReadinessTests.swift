import Testing
@testable import NextThoughtCore

struct RealtimeSetupReadinessTests {
    @Test func recordingRequiresVoiceAndAudioButNotAnswerService() {
        for mask in 0..<64 {
            let speech = mask & 1 != 0, capture = mask & 2 != 0, micRequired = mask & 4 != 0
            let mic = mask & 8 != 0, answers = mask & 16 != 0, source = mask & 32 != 0
            let setup = RealtimeSetupReadiness(speech: speech, capture: capture, microphoneRequired: micRequired, microphone: mic, answers: answers, audioSource: source)
            #expect(setup.voiceReady == (speech && capture && (!micRequired || mic)))
            #expect(setup.canRecord == (speech && capture && (!micRequired || mic) && source))
            #expect(setup.canStart == setup.canRecord)
            #expect(setup.canAnswer == answers)
        }
    }
    @Test func optionalDocumentModelsAreNotNeededToStartRealtimeAssistance() {
        let setup = RealtimeSetupReadiness(speech: true, capture: true, microphoneRequired: true, microphone: true, answers: true, audioSource: true)
        let documents = DocumentFeatureAvailability(checking: false, runtime: false, ocr: false, layout: false, embedding: false, codexConnected: false)
        #expect(!documents.ready(.paddle) && !documents.ready(.semantic))
        #expect(setup.canStart)
    }
}
