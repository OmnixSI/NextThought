import Foundation

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
}

public struct VoiceActivitySegmenter {
    private let sampleRate = 16_000
    private let threshold = 0.012
    private var totalSamples = 0
    private var currentID: UUID?
    private var startSample = 0
    private var samples: [Int16] = []
    private var preRoll: [Int16] = []
    private var silenceSamples = 0
    private var speechSamples = 0
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
        let isSpeech = level >= threshold

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
        if samples.count - lastPartialCount >= 64_000, samples.count >= 32_000, let currentID {
            events.append(makeEvent(.partial, id: currentID))
            lastPartialCount = samples.count
        }

        let reachedSilence = silenceSamples >= sampleRate
        let reachedLimit = samples.count >= sampleRate * 12
        if (reachedSilence || reachedLimit), let currentID {
            if speechSamples >= sampleRate / 2 {
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
                self.lastPartialCount = overlap.count
            } else {
                resetCurrent()
            }
        }
        return events
    }

    public mutating func flush() -> SpeechSegmentEvent? {
        guard speechSamples >= sampleRate / 2, let currentID else {
            resetCurrent()
            return nil
        }
        let event = makeEvent(.final, id: currentID)
        resetCurrent()
        return event
    }

    public mutating func reset() {
        totalSamples = 0
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
        lastPartialCount = 0
        startSample = totalSamples
    }
}
