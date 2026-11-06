import Foundation

public enum SpeechAudioSource: String, Sendable, Equatable {
    case system, microphone
}

public struct SpeechSegmentEvent: Sendable, Equatable {
    public enum Kind: Sendable, Equatable {
        case partial
        case final
    }

    public var kind: Kind
    public var id: UUID
    public var startSample: Int
    public var endSample: Int
    public var samples: [Int16]
    public var streamStartedAt: Date? = nil
    public var source: SpeechAudioSource = .system
}

public struct VoiceActivitySegmenter {
    private let sampleRate = 16_000
    /// Base RMS threshold; raised automatically above a slowly tracked noise floor so
    /// quiet remote voices are not lost and steady background noise does not count.
    private let baseThreshold = 0.008
    private var noiseFloor = 0.0
    /// Silence that ends an utterance. Shorter than the previous 1 s for faster turns.
    public var endSilenceSamples = 11_200
    /// New audio needed between live partial previews.
    public var partialInterval = 24_000
    private var totalSamples = 0
    private var currentID: UUID?
    private var startSample = 0
    private var samples: [Int16] = []
    private var preRoll: [Int16] = []
    private var silenceSamples = 0
    private var speechSamples = 0
    private var hasNewSpeech = false
    private var lastPartialCount = 0

    public init() {}

    public mutating func append(_ newSamples: [Int16]) -> [SpeechSegmentEvent] {
        guard !newSamples.isEmpty else { return [] }
        totalSamples += newSamples.count

        var squareTotal = 0.0
        for sample in newSamples {
            let value = Double(sample) / 32_768
            squareTotal += value * value
        }
        let level = sqrt(squareTotal / Double(newSamples.count))
        let threshold = max(baseThreshold, min(0.03, noiseFloor * 3))
        let isSpeech = level >= threshold
        // Floor follower: drops quickly to quieter chunks, rises slowly (~1 min) so a
        // constant background hum is eventually ignored without swallowing speech.
        if noiseFloor == 0 { noiseFloor = min(level, 0.01) }
        else if level < noiseFloor { noiseFloor = noiseFloor * 0.8 + level * 0.2 }
        else { noiseFloor = noiseFloor * 0.998 + level * 0.002 }
        if isSpeech { hasNewSpeech = true }

        if currentID == nil {
            if !isSpeech {
                preRoll.append(contentsOf: newSamples)
                if preRoll.count > 4_000 {
                    preRoll.removeFirst(preRoll.count - 4_000)
                }
                return []
            }
            currentID = UUID()
            samples = preRoll
            samples.append(contentsOf: newSamples)
            startSample = totalSamples - samples.count
            preRoll.removeAll(keepingCapacity: true)
            silenceSamples = 0
            speechSamples = newSamples.count
            lastPartialCount = 0
        } else {
            samples.append(contentsOf: newSamples)
            if isSpeech {
                silenceSamples = 0
                speechSamples += newSamples.count
            } else {
                silenceSamples += newSamples.count
            }
        }

        var events: [SpeechSegmentEvent] = []
        if isSpeech, samples.count - lastPartialCount >= partialInterval, samples.count >= 16_000, let currentID {
            events.append(makeEvent(.partial, id: currentID))
            lastPartialCount = samples.count
        }

        let reachedSilence = silenceSamples >= endSilenceSamples
        let reachedLimit = samples.count >= sampleRate * 12
        if (reachedSilence || reachedLimit), let currentID {
            if speechSamples >= sampleRate / 2, hasNewSpeech {
                events.append(makeEvent(.final, id: currentID))
            }
            if reachedLimit && isSpeech {
                let overlapCount = min(sampleRate / 2, samples.count)
                let overlap = Array(samples.suffix(overlapCount))
                self.currentID = UUID()
                self.samples = overlap
                self.startSample = totalSamples - overlap.count
                self.silenceSamples = 0
                self.speechSamples = overlap.count
                self.hasNewSpeech = false
                self.lastPartialCount = overlap.count
            } else {
                resetCurrent()
            }
        }
        return events
    }

    public mutating func flush() -> SpeechSegmentEvent? {
        guard hasNewSpeech, speechSamples >= sampleRate / 2, let currentID else {
            resetCurrent()
            return nil
        }
        let event = makeEvent(.final, id: currentID)
        resetCurrent()
        return event
    }

    public mutating func reset() {
        totalSamples = 0
        noiseFloor = 0
        resetCurrent()
        preRoll.removeAll(keepingCapacity: false)
    }

    private func makeEvent(_ kind: SpeechSegmentEvent.Kind, id: UUID) -> SpeechSegmentEvent {
        SpeechSegmentEvent(
            kind: kind,
            id: id,
            startSample: startSample,
            endSample: startSample + samples.count,
            samples: samples)
    }

    private mutating func resetCurrent() {
        currentID = nil
        samples.removeAll(keepingCapacity: true)
        silenceSamples = 0
        speechSamples = 0
        hasNewSpeech = false
        lastPartialCount = 0
        startSample = totalSamples
    }
}
