import Foundation

public struct RealtimeSetupReadiness: Sendable {
    public var speech: Bool
    public var capture: Bool
    public var microphoneRequired: Bool
    public var microphone: Bool
    public var answers: Bool
    public var audioSource: Bool

    public init(speech: Bool, capture: Bool, microphoneRequired: Bool, microphone: Bool, answers: Bool, audioSource: Bool) {
        self.speech = speech; self.capture = capture; self.microphoneRequired = microphoneRequired
        self.microphone = microphone; self.answers = answers; self.audioSource = audioSource
    }
    public var voiceReady: Bool { speech && capture && (!microphoneRequired || microphone) }
    /// Recording needs a working voice path and an available audio source. The
    /// answer service is an independent capability and must not block capture.
    public var canRecord: Bool { voiceReady && audioSource }
    public var canAnswer: Bool { answers }

    /// Kept as the existing setup API; "start" refers to starting a recording.
    public var canStart: Bool { canRecord }
}
