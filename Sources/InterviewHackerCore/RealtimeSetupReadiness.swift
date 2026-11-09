import Foundation

public enum RecordingPermission: String, Sendable, CaseIterable {
    case undetermined, granted, denied
}

/// Only the capabilities needed to capture a meeting. AI services and document
/// enhancement are deliberately independent of this decision.
public struct RecordingReadiness: Sendable {
    public enum Issue: String, Sendable, Equatable {
        case recognition, systemAudioPermission, audioSource, microphonePermission
    }
    public var speech: Bool
    public var capture: Bool
    public var audioSource: Bool
    public var systemAudioRequired: Bool
    public var microphoneRequired: Bool
    public var microphone: RecordingPermission

    public init(speech: Bool, capture: Bool, audioSource: Bool, systemAudioRequired: Bool,
                microphoneRequired: Bool, microphone: RecordingPermission) {
        self.speech = speech; self.capture = capture; self.audioSource = audioSource
        self.systemAudioRequired = systemAudioRequired
        self.microphoneRequired = microphoneRequired; self.microphone = microphone
    }
    public var issues: [Issue] {
        var result: [Issue] = []
        if !speech { result.append(.recognition) }
        if systemAudioRequired {
            if !capture { result.append(.systemAudioPermission) }
            else if !audioSource { result.append(.audioSource) }
        }
        if microphoneRequired && microphone != .granted { result.append(.microphonePermission) }
        return result
    }
    public var canStartRecording: Bool { issues.isEmpty }
    /// An unasked permission is requested by the shared start flow. An explicit
    /// refusal or a missing recognizer/source needs a repair action first.
    public var canRequestStart: Bool {
        speech && (!systemAudioRequired || !capture || audioSource)
            && (!microphoneRequired || microphone != .denied)
    }
}

public struct RealtimeSetupReadiness: Sendable {
    public var speech: Bool
    public var capture: Bool
    public var microphoneRequired: Bool
    public var microphone: Bool
    public var answers: Bool
    public var audioSource: Bool
    /// Online meetings need the system-audio capture permission and a selected target.
    /// In-person meetings use only the microphone, so those checks do not apply.
    public var systemAudioRequired: Bool

    public init(speech: Bool, capture: Bool, microphoneRequired: Bool, microphone: Bool, answers: Bool,
                audioSource: Bool, systemAudioRequired: Bool = true) {
        self.speech = speech; self.capture = capture; self.microphoneRequired = microphoneRequired
        self.microphone = microphone; self.answers = answers; self.audioSource = audioSource
        self.systemAudioRequired = systemAudioRequired
    }
    public var systemAudioReady: Bool { !systemAudioRequired || (capture && audioSource) }
    public var voiceReady: Bool { speech && (!systemAudioRequired || capture) && (!microphoneRequired || microphone) }
    /// Retained for existing callers; a recording never depends on an AI service.
    public var coreReady: Bool { voiceReady }
    public var canStart: Bool { coreReady && (!systemAudioRequired || audioSource) }
    public var canUseAI: Bool { answers }
}
