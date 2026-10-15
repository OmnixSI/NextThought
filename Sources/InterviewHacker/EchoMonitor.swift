import Foundation
import InterviewHackerCore

/// Thread-safe wrapper around `CrossTalkMonitor`, fed from both audio queues. It also
/// keeps the latest input levels for the meters, read by the UI at a fixed rate
/// instead of publishing every audio chunk.
final class EchoMonitor: @unchecked Sendable {
    private let lock = NSLock()
    private var monitor = CrossTalkMonitor()
    private var systemLevel: Float = 0
    private var microphoneLevel: Float = 0
    private var echo = false

    func reset() {
        lock.withLock { monitor.reset(); systemLevel = 0; microphoneLevel = 0; echo = false }
    }

    func system(_ samples: [Int16]) {
        let level = Self.level(samples)
        let now = ProcessInfo.processInfo.systemUptime
        lock.withLock {
            monitor.appendSystem(samples, at: now)
            systemLevel = max(level, systemLevel * 0.6)
        }
    }

    func microphone(_ samples: [Int16]) {
        let level = Self.level(samples)
        let now = ProcessInfo.processInfo.systemUptime
        lock.withLock {
            echo = monitor.appendMicrophone(samples, at: now)
            microphoneLevel = max(level, microphoneLevel * 0.6)
        }
    }

    /// The microphone currently carries the speakers' sound rather than the user.
    var microphoneIsEcho: Bool { lock.withLock { echo } }

    /// 0…1 levels for the meters (decaying peak of a dB scale).
    var levels: (system: Float, microphone: Float) {
        lock.withLock {
            let result = (systemLevel, microphoneLevel)
            systemLevel *= 0.85; microphoneLevel *= 0.85
            return result
        }
    }

    private static func level(_ samples: [Int16]) -> Float {
        guard !samples.isEmpty else { return 0 }
        var total: Float = 0
        for sample in samples { let value = Float(sample) / 32_768; total += value * value }
        let rms = sqrt(total / Float(samples.count))
        // -60 dB … 0 dB → 0 … 1
        let decibels = 20 * log10(max(rms, 1e-6))
        return max(0, min(1, (decibels + 60) / 60))
    }
}
