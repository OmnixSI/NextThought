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

    @Test func startCanRequestPendingMicrophonePermissionButOfflineCannotSkipDenial() {
        let permissionCanBeRequested = RealtimeSetupReadiness.microphonePermissionAllowsStart(
            isGranted: false, isUndetermined: true)
        let deniedPermissionBlocks = RealtimeSetupReadiness.microphonePermissionAllowsStart(
            isGranted: false, isUndetermined: false)
        let deniedRequiredMicrophone = RealtimeSetupReadiness(
            speech: true, capture: true, microphoneRequired: true, microphone: deniedPermissionBlocks, answers: true, audioSource: true)
        let optionalOnlineMicrophoneDisabled = RealtimeSetupReadiness(
            speech: true, capture: true, microphoneRequired: false, microphone: false, answers: false, audioSource: true)

        #expect(permissionCanBeRequested)
        #expect(RealtimeSetupReadiness(speech: true, capture: true, microphoneRequired: true, microphone: permissionCanBeRequested,
                                        answers: false, audioSource: true).canRecord)
        #expect(!deniedRequiredMicrophone.canRecord)
        #expect(optionalOnlineMicrophoneDisabled.canRecord)
        #expect(RealtimeSetupReadiness.microphoneRequired(microphoneEnabled: false, isOfflineMeeting: true))
        #expect(!RealtimeSetupReadiness.microphoneRequired(microphoneEnabled: false, isOfflineMeeting: false))
    }
}

struct RecordingReadinessTests {
    @Test func coreAudioCannotBeBypassedAndAIIsOptional() {
        for mask in 0..<128 {
            let speech = mask & 1 != 0, capture = mask & 2 != 0, micRequired = mask & 4 != 0
            let mic = mask & 8 != 0, answers = mask & 16 != 0, source = mask & 32 != 0
            let systemAudioRequired = mask & 64 != 0
            let setup = RealtimeSetupReadiness(speech: speech, capture: capture, microphoneRequired: micRequired,
                                               microphone: mic, answers: answers, audioSource: source,
                                               systemAudioRequired: systemAudioRequired)
            #expect(setup.voiceReady == (speech && (!systemAudioRequired || capture) && (!micRequired || mic)))
            #expect(setup.canStart == (speech && (!systemAudioRequired || capture) && (!micRequired || mic)
                                       && (!systemAudioRequired || source)))
            #expect(setup.canUseAI == answers)
        }
    }

    @Test func firstMicrophonePermissionCanBeRequestedButRefusalCannotBeBypassed() {
        for permission in RecordingPermission.allCases {
            let setup = RecordingReadiness(speech: true, capture: false, audioSource: false,
                systemAudioRequired: false, microphoneRequired: true, microphone: permission)
            #expect(setup.canRequestStart == (permission != .denied))
            #expect(setup.canStartRecording == (permission == .granted))
            #expect(setup.issues == (permission == .granted ? [] : [.microphonePermission]))
        }
    }

    @Test func onlineSourceMustStillExistAfterPermissionWasGranted() {
        let setup = RecordingReadiness(speech: true, capture: true, audioSource: false,
            systemAudioRequired: true, microphoneRequired: false, microphone: .denied)
        #expect(!setup.canRequestStart && !setup.canStartRecording)
        #expect(setup.issues == [.audioSource])
        let unasked = RecordingReadiness(speech: true, capture: false, audioSource: false,
            systemAudioRequired: true, microphoneRequired: false, microphone: .undetermined)
        #expect(unasked.canRequestStart && !unasked.canStartRecording)
        #expect(unasked.issues == [.systemAudioPermission])
    }

    @Test func inPersonMeetingNeedsMicrophoneButNotSystemAudio() {
        let offline = RealtimeSetupReadiness(speech: true, capture: false, microphoneRequired: true,
                                             microphone: true, answers: true, audioSource: false,
                                             systemAudioRequired: false)
        let online = RealtimeSetupReadiness(speech: true, capture: false, microphoneRequired: true,
                                            microphone: true, answers: true, audioSource: false)

        #expect(offline.canStart)
        #expect(!online.canStart)
        #expect(!RealtimeSetupReadiness(speech: true, capture: false, microphoneRequired: true,
                                        microphone: false, answers: true, audioSource: false,
                                        systemAudioRequired: false).canStart)
    }
    @Test func optionalDocumentModelsAreNotNeededToStartRealtimeAssistance() {
        let setup = RealtimeSetupReadiness(speech: true, capture: true, microphoneRequired: true, microphone: true, answers: true, audioSource: true)
        let documents = DocumentFeatureAvailability(checking: false, runtime: false, ocr: false, layout: false, embedding: false, codexConnected: false)
        #expect(!documents.ready(.paddle) && !documents.ready(.semantic))
        #expect(setup.canStart)
    }
}
