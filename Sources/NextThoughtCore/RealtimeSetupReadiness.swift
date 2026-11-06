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
    public var coreReady: Bool { voiceReady && answers }
    public var canStart: Bool { coreReady && audioSource }
}
