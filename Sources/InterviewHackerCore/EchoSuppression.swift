import Foundation

/// Without headphones the other side's voice comes out of the speakers and is picked
/// up again by the microphone. The two channels are recognized independently, so
/// the echo would show up as “我” saying the interviewer's words — and, worse, make
/// the app think the user is talking and hold back automatic answers.
///
/// `CrossTalkMonitor` compares the loudness envelopes of both channels: a microphone
/// signal that follows the system-audio envelope (with a short acoustic/driver delay)
/// is echo; a microphone signal with its own rhythm is the user, even when both
/// sides talk at the same time.
public struct CrossTalkMonitor: Sendable {
    public struct Config: Sendable, Equatable {
        /// Envelope frame length in samples (20 ms at 16 kHz).
        public var frameSamples = 320
        /// Analysis window (frames) for the correlation.
        public var windowFrames = 80
        /// Lags searched, in frames: the system capture can arrive before or after the mic.
        public var minimumLag = -15
        public var maximumLag = 25
        /// Correlation above which the microphone is treated as echo.
        public var echoCorrelation = 0.7
        /// Microphone frames below this RMS are silence.
        public var speechRMS = 0.006
        /// System audio below this RMS cannot produce echo.
        public var systemActiveRMS = 0.004
        public init() {}
    }

    public var config: Config
    private var systemFrames: [Int: Double] = [:]
    private var microphoneFrames: [Int: Double] = [:]
    private var systemRemainder: [Int16] = []
    private var microphoneRemainder: [Int16] = []
    private var systemFrameCursor: Int?
    private var microphoneFrameCursor: Int?
    public private(set) var lastCorrelation = 0.0
    public private(set) var microphoneIsEcho = false
    public private(set) var microphoneHasSpeech = false

    public init(config: Config = Config()) { self.config = config }

    public mutating func reset() {
        systemFrames.removeAll(); microphoneFrames.removeAll()
        systemRemainder.removeAll(); microphoneRemainder.removeAll()
        systemFrameCursor = nil; microphoneFrameCursor = nil
        lastCorrelation = 0; microphoneIsEcho = false; microphoneHasSpeech = false
    }

    /// `time` is the arrival time (seconds, any monotonic clock) of the chunk's last sample.
    public mutating func appendSystem(_ samples: [Int16], at time: TimeInterval) {
        append(samples, at: time, system: true)
    }

    /// Returns whether the microphone currently looks like an echo of the system audio.
    @discardableResult
    public mutating func appendMicrophone(_ samples: [Int16], at time: TimeInterval) -> Bool {
        append(samples, at: time, system: false)
        evaluate()
        return microphoneIsEcho
    }

    private mutating func append(_ samples: [Int16], at time: TimeInterval, system: Bool) {
        var buffer = (system ? systemRemainder : microphoneRemainder) + samples
        let frameCount = buffer.count / config.frameSamples
        let frameDuration = Double(config.frameSamples) / 16_000
        // Frame index of the newest complete frame on a shared 20 ms grid.
        var newest = Int((time / frameDuration).rounded(.down))
        if let cursor = system ? systemFrameCursor : microphoneFrameCursor, newest <= cursor { newest = cursor + frameCount }
        for frame in 0..<frameCount {
            let slice = buffer[(frame * config.frameSamples)..<((frame + 1) * config.frameSamples)]
            var total = 0.0
            for sample in slice { let value = Double(sample) / 32_768; total += value * value }
            let rms = sqrt(total / Double(config.frameSamples))
            let index = newest - (frameCount - 1 - frame)
            if system { systemFrames[index] = rms } else { microphoneFrames[index] = rms }
        }
        buffer.removeFirst(frameCount * config.frameSamples)
        if system { systemRemainder = buffer; systemFrameCursor = newest } else { microphoneRemainder = buffer; microphoneFrameCursor = newest }
        // Keep ~4 s of history.
        let horizon = newest - 200
        if system { systemFrames = systemFrames.filter { $0.key > horizon } } else { microphoneFrames = microphoneFrames.filter { $0.key > horizon } }
    }

    private mutating func evaluate() {
        guard let end = microphoneFrameCursor else { return }
        let window = (end - config.windowFrames + 1)...end
        let mic = window.map { microphoneFrames[$0] ?? 0 }
        let recentMic = mic.suffix(10)
        microphoneHasSpeech = recentMic.contains { $0 >= config.speechRMS }
        guard microphoneHasSpeech else { microphoneIsEcho = false; lastCorrelation = 0; return }

        var best = 0.0
        for lag in config.minimumLag...config.maximumLag {
            // System frame at (t - lag) is compared with microphone frame at t.
            let system = window.map { systemFrames[$0 - lag] ?? 0 }
            guard system.contains(where: { $0 >= config.systemActiveRMS }) else { continue }
            best = max(best, Self.correlation(Self.envelope(mic), Self.envelope(system)))
        }
        lastCorrelation = best
        microphoneIsEcho = best >= config.echoCorrelation
    }

    /// Log-energy envelope; correlation of logs is level-independent (speaker volume,
    /// room gain) and dominated by the syllable rhythm.
    static func envelope(_ values: [Double]) -> [Double] { values.map { log10(max($0, 1e-4)) } }

    static func correlation(_ a: [Double], _ b: [Double]) -> Double {
        guard a.count == b.count, a.count > 2 else { return 0 }
        let meanA = a.reduce(0, +) / Double(a.count), meanB = b.reduce(0, +) / Double(b.count)
        var numerator = 0.0, varianceA = 0.0, varianceB = 0.0
        for index in a.indices {
            let x = a[index] - meanA, y = b[index] - meanB
            numerator += x * y; varianceA += x * x; varianceB += y * y
        }
        guard varianceA > 1e-6, varianceB > 1e-6 else { return 0 }
        return numerator / sqrt(varianceA * varianceB)
    }
}

/// Text-level backstop: a microphone utterance whose words largely repeat what the
/// system channel said a moment ago is the speakers being re-recorded.
public enum EchoTextFilter {
    public static func isLikelyEcho(_ microphoneText: String, recentSystemTexts: [String], threshold: Double = 0.55) -> Bool {
        let mic = units(microphoneText)
        guard mic.count >= 3 else { return false }
        let system = Set(recentSystemTexts.flatMap(units))
        guard !system.isEmpty else { return false }
        let shared = mic.filter { system.contains($0) }.count
        return Double(shared) / Double(mic.count) >= threshold
    }

    /// Character bigrams for CJK, lowercase words for everything else.
    static func units(_ text: String) -> [String] {
        var result: [String] = []
        var cjk: [Character] = []
        var word = ""
        func flushCJK() {
            if cjk.count == 1 { result.append(String(cjk)) }
            if cjk.count > 1 { for index in 0..<(cjk.count - 1) { result.append(String(cjk[index...index + 1])) } }
            cjk.removeAll()
        }
        func flushWord() { if !word.isEmpty { result.append(word) }; word = "" }
        for character in text.lowercased() {
            if character.unicodeScalars.contains(where: { (0x3400...0x9FFF).contains($0.value) }) { flushWord(); cjk.append(character) }
            else if character.isLetter || character.isNumber { flushCJK(); word.append(character) }
            else { flushCJK(); flushWord() }
        }
        flushCJK(); flushWord()
        return result
    }
}
