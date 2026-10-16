import Foundation
import Testing
@testable import InterviewHackerCore

struct EchoSuppressionTests {
    /// Speech-like signal: noise shaped by a random syllable envelope (~4–6 Hz).
    func speech(seconds: Double, seed: UInt64, level: Double = 0.2) -> [Double] {
        var generator = SeededGenerator(seed: seed)
        let count = Int(seconds * 16_000)
        var envelope: [Double] = []
        while envelope.count < count {
            let syllable = Int(Double.random(in: 0.08...0.25, using: &generator) * 16_000)
            let gap = Int(Double.random(in: 0.03...0.15, using: &generator) * 16_000)
            let peak = Double.random(in: 0.4...1, using: &generator)
            envelope += (0..<syllable).map { peak * sin(Double.pi * Double($0) / Double(syllable)) }
            envelope += Array(repeating: 0.01, count: gap)
        }
        return (0..<count).map { envelope[$0] * level * Double.random(in: -1...1, using: &generator) }
    }

    func delayed(_ source: [Double], by delay: Int, gain: Double, plus other: [Double]) -> [Double] {
        var result = other
        for index in delay..<min(source.count, other.count) {
            let echo: Double = source[index - delay] * gain
            result[index] += echo
        }
        return result
    }

    func pcm(_ values: [Double]) -> [Int16] { values.map { Int16(max(-32_767, min(32_767, $0 * 32_767))) } }

    /// Feeds both channels in 100 ms chunks; the mic is delayed by `delay` samples.
    func run(system: [Double], microphone: [Double]) -> (echoRatio: Double, correlation: Double) {
        var monitor = CrossTalkMonitor()
        var echoChunks = 0, speechChunks = 0, last = 0.0
        let chunk = 1_600
        for start in stride(from: 0, to: min(system.count, microphone.count) - chunk, by: chunk) {
            let time = Double(start + chunk) / 16_000
            monitor.appendSystem(pcm(Array(system[start..<start + chunk])), at: time)
            let echo = monitor.appendMicrophone(pcm(Array(microphone[start..<start + chunk])), at: time)
            if monitor.microphoneHasSpeech, start > 16_000 { speechChunks += 1; if echo { echoChunks += 1 } }
            last = monitor.lastCorrelation
        }
        return (speechChunks == 0 ? 0 : Double(echoChunks) / Double(speechChunks), last)
    }

    @Test func delayedQuietCopyIsEcho() {
        let remote = speech(seconds: 6, seed: 1)
        let delay = 2_400 // 150 ms acoustic + driver delay
        let noise = (0..<remote.count).map { _ in Double.random(in: -0.002...0.002) }
        let mic = delayed(remote, by: delay, gain: 0.15, plus: noise)
        let result = run(system: remote, microphone: mic)
        #expect(result.echoRatio > 0.8, "\(result)")
    }

    @Test func independentSpeakerIsNotEcho() {
        let remote = speech(seconds: 6, seed: 2)
        let me = speech(seconds: 6, seed: 3, level: 0.25)
        let result = run(system: remote, microphone: me)
        #expect(result.echoRatio < 0.2, "\(result)")
    }

    @Test func userTalkingOverEchoIsStillTheUser() {
        let remote = speech(seconds: 6, seed: 4)
        let me = speech(seconds: 6, seed: 5, level: 0.3)
        let mic = delayed(remote, by: 2_400, gain: 0.1, plus: me)
        let result = run(system: remote, microphone: mic)
        #expect(result.echoRatio < 0.3, "\(result)")
    }

    @Test func silentSystemNeverMarksEcho() {
        let me = speech(seconds: 4, seed: 6)
        let result = run(system: Array(repeating: 0, count: me.count), microphone: me)
        #expect(result.echoRatio == 0)
    }

    @Test func textFilterCatchesRepeatedWords() {
        #expect(EchoTextFilter.isLikelyEcho("那数据库和缓存怎么保证一致性", recentSystemTexts: ["那数据库和缓存同时更新的时候，你们怎么保证一致性？"]))
        #expect(!EchoTextFilter.isLikelyEcho("我们用的是先更新数据库再删除缓存", recentSystemTexts: ["那数据库和缓存同时更新的时候，你们怎么保证一致性？"]))
        #expect(EchoTextFilter.isLikelyEcho("how do you handle cache invalidation", recentSystemTexts: ["So how do you handle cache invalidation in production?"]))
        #expect(!EchoTextFilter.isLikelyEcho("好的", recentSystemTexts: ["好的"]))
    }

    /// Replies reuse the question's words; they must stay the user's lines.
    @Test func repliesThatReuseWordsAreNotEcho() {
        let now = Date()
        let lines = [TimedLine(text: "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？", start: now.addingTimeInterval(-6), end: now.addingTimeInterval(-2)),
                     TimedLine(text: "你们的缓存是放在 Redis 里吗？", start: now.addingTimeInterval(-12), end: now.addingTimeInterval(-9))]
        #expect(!EchoTextFilter.isEcho(microphone: "我们保证一致性的方式是先更新数据库再删缓存", start: now, end: now.addingTimeInterval(3), system: lines, acousticEchoFraction: 0))
        #expect(!EchoTextFilter.isEcho(microphone: "对，缓存放在 Redis 里", start: now, end: now.addingTimeInterval(2), system: lines, acousticEchoFraction: 0.1))
        // Old union matching would have called this echo (most bigrams appear somewhere above).
        #expect(!EchoTextFilter.isLikelyEcho("你们的数据库缓存是怎么更新的", recentSystemTexts: lines.map(\.text), threshold: 0.8))
    }

    @Test func echoAtTheSameTimeIsCaught() {
        let now = Date()
        let line = TimedLine(text: "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？", start: now, end: now.addingTimeInterval(4))
        // Near-verbatim repeat while the other side speaks: echo even without the audio monitor.
        #expect(EchoTextFilter.isEcho(microphone: "数据库和缓存同时更新的时候你们是怎么保证一致性的", start: now.addingTimeInterval(0.3), end: now.addingTimeInterval(4.2), system: [line], acousticEchoFraction: 0))
        // Partial repeat: echo only if the speakers were heard in the microphone.
        #expect(EchoTextFilter.isEcho(microphone: "缓存和数据库怎么保证一致性", start: now.addingTimeInterval(0.5), end: now.addingTimeInterval(4), system: [line], acousticEchoFraction: 0.7))
        #expect(!EchoTextFilter.isEcho(microphone: "缓存和数据库怎么保证一致性", start: now.addingTimeInterval(0.5), end: now.addingTimeInterval(4), system: [line], acousticEchoFraction: 0))
        // The same words a minute later are the user, not echo.
        #expect(!EchoTextFilter.isEcho(microphone: "数据库和缓存同时更新的时候你们是怎么保证一致性的", start: now.addingTimeInterval(60), end: now.addingTimeInterval(64), system: [line], acousticEchoFraction: 0))
    }
}

struct SeededGenerator: RandomNumberGenerator {
    private var state: UInt64
    init(seed: UInt64) { state = seed &* 0x9E3779B97F4A7C15 | 1 }
    mutating func next() -> UInt64 {
        state ^= state << 13; state ^= state >> 7; state ^= state << 17
        return state
    }
}
