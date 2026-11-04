import Foundation

/// The last minute or so of one audio channel (16 kHz mono), addressed by wall-clock time, so a
/// finished sentence can be cut out again by the start and end the recognizer reported for it.
///
/// The capture services hand the recognizer 16 kHz chunks together with the `origin` of the
/// stream, and every recognizer reports a sentence as `origin + sample index / 16 000`; this
/// buffer keeps the same bookkeeping, so the times match exactly. The audio exists only in memory,
/// only for the voiceprint of that sentence, and is never written anywhere.
///
/// Fed from a capture queue and read from the main actor, hence the lock.
public final class AudioTimeline: @unchecked Sendable {
    public static let sampleRate = 16_000

    /// Samples of one stream (one `origin`). A pause and resume starts a new stream.
    private struct Run {
        var origin: Date
        /// Stream index of `samples[0]`.
        var firstSample: Int
        var samples: [Int16]
    }

    private let lock = NSLock()
    private var runs: [Run] = []
    private let retentionSamples: Int
    /// How far past the retention a run may grow before it is trimmed (trimming is O(n)).
    private let slackSamples: Int

    public init(retentionSeconds: Double = 60) {
        retentionSamples = Int(retentionSeconds * Double(Self.sampleRate))
        slackSamples = max(Self.sampleRate, retentionSamples / 6)
    }

    /// Appends the next chunk of the stream that began at `origin`.
    public func append(_ samples: [Int16], origin: Date) {
        guard !samples.isEmpty else { return }
        lock.lock()
        defer { lock.unlock() }
        if let last = runs.indices.last, runs[last].origin == origin {
            runs[last].samples.append(contentsOf: samples)
            let excess = runs[last].samples.count - retentionSamples
            if excess > slackSamples {
                runs[last].samples.removeFirst(excess)
                runs[last].firstSample += excess
            }
        } else {
            runs.append(Run(origin: origin, firstSample: 0, samples: samples))
            // The previous stream may still deliver a last sentence after a pause; older ones are gone.
            if runs.count > 2 { runs.removeFirst(runs.count - 2) }
        }
    }

    /// The audio between `start` and `end` as floats in -1...1, or nil when too little of it is
    /// still held (`minimumCoverage` is the share of the wanted span that must be present).
    /// A span longer than `maxSeconds` is cut to its last `maxSeconds`: 20 s is plenty to know a
    /// voice, and the end of a long sentence is the part most likely still in the buffer.
    public func clip(from start: Date, to end: Date, maxSeconds: Double = 20, minimumCoverage: Double = 0.6) -> [Float]? {
        guard end > start else { return nil }
        let wantedEnd = end
        let wantedStart = max(start, end.addingTimeInterval(-maxSeconds))
        lock.lock()
        defer { lock.unlock() }
        var best: (run: Int, lower: Int, upper: Int)?
        var wanted = 0
        for (index, run) in runs.enumerated() {
            let rate = Double(Self.sampleRate)
            let lowerWanted = Int((wantedStart.timeIntervalSince(run.origin) * rate).rounded())
            let upperWanted = Int((wantedEnd.timeIntervalSince(run.origin) * rate).rounded())
            wanted = max(wanted, upperWanted - lowerWanted)
            let lower = max(lowerWanted, run.firstSample)
            let upper = min(upperWanted, run.firstSample + run.samples.count)
            guard upper > lower else { continue }
            if upper - lower > (best.map { $0.upper - $0.lower } ?? 0) { best = (index, lower, upper) }
        }
        guard let best, wanted > 0, Double(best.upper - best.lower) >= minimumCoverage * Double(wanted) else { return nil }
        let run = runs[best.run]
        let slice = run.samples[(best.lower - run.firstSample)..<(best.upper - run.firstSample)]
        return slice.map { Float($0) / 32_768 }
    }

    /// Cuts the quiet lead-in and tail off a clip. A sentence's reported span starts where the
    /// previous one ended, so it can carry seconds of silence that would only blur the voiceprint
    /// and the length used to decide whether a line is long enough to start a new voice.
    /// Returns an empty array for a clip that is silent all through; clips under 80 ms are kept as they are.
    public static func trimmingSilence(_ samples: [Float], window: Int = 320, paddingWindows: Int = 2) -> [Float] {
        let windows = samples.count / window
        guard windows >= 4 else { return samples }
        var levels = [Float](repeating: 0, count: windows)
        var peak: Float = 0
        for w in 0..<windows {
            var sum: Float = 0
            for i in (w * window)..<((w + 1) * window) { sum += samples[i] * samples[i] }
            levels[w] = (sum / Float(window)).squareRoot()
            peak = max(peak, levels[w])
        }
        // Below this the whole clip is room noise; above it, "quiet" means a tenth of the loudest 20 ms.
        guard peak >= 0.002 else { return [] }
        let threshold = max(0.002, peak * 0.1)
        guard let first = levels.firstIndex(where: { $0 >= threshold }), let last = levels.lastIndex(where: { $0 >= threshold }) else { return [] }
        let lower = max(0, first - paddingWindows) * window
        let upperWindow = last + 1 + paddingWindows
        let upper = upperWindow >= windows ? samples.count : upperWindow * window
        return Array(samples[lower..<upper])
    }

    /// Forgets everything (a new meeting).
    public func reset() {
        lock.lock()
        defer { lock.unlock() }
        runs.removeAll()
    }

    /// Seconds of audio currently held, across streams.
    public var heldSeconds: Double {
        lock.lock()
        defer { lock.unlock() }
        return Double(runs.reduce(0) { $0 + $1.samples.count }) / Double(Self.sampleRate)
    }
}
