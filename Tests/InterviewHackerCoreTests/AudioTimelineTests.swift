import Foundation
import Testing
@testable import InterviewHackerCore

/// The short audio window behind "区分对方的每个人": a sentence is cut out again by the start and end the
/// recognizer reported. Sample `i` of a stream holds the value `i / 1000`, so a cut can be checked by eye.
struct AudioTimelineTests {
    let origin = Date(timeIntervalSince1970: 1_000)

    /// One second of the stream, second number `second` (16 000 samples).
    private func second(_ second: Int) -> [Int16] {
        (0..<16_000).map { Int16((second * 16_000 + $0) / 1_000) }
    }

    private func value(_ index: Int) -> Float { Float(Int16(index / 1_000)) / 32_768 }

    @Test func cutsASentenceByTime() throws {
        let timeline = AudioTimeline()
        for s in 0..<3 { timeline.append(second(s), origin: origin) }
        let clip = try #require(timeline.clip(from: origin.addingTimeInterval(1.0), to: origin.addingTimeInterval(1.5)))
        #expect(clip.count == 8_000)
        #expect(clip.first == value(16_000))      // sample 16 000 -> 16
        #expect(clip.last == value(23_999))       // sample 23 999 -> 23
        #expect(timeline.heldSeconds == 3)
    }

    @Test func chunksOfAnySizeJoinUp() throws {
        let timeline = AudioTimeline()
        let all = (0..<3).flatMap { second($0) }
        // The capture services hand over 1 600-sample (100 ms) chunks.
        var index = 0
        while index < all.count {
            timeline.append(Array(all[index..<min(index + 1_600, all.count)]), origin: origin)
            index += 1_600
        }
        let clip = try #require(timeline.clip(from: origin.addingTimeInterval(0.5), to: origin.addingTimeInterval(2.5)))
        #expect(clip.count == 32_000)
        #expect(clip.first == value(8_000))
        #expect(clip.last == value(39_999))
    }

    @Test func oldAudioIsForgotten() throws {
        let timeline = AudioTimeline(retentionSeconds: 2)
        for s in 0..<10 { timeline.append(second(s), origin: origin) }
        // Held: the last 2 s of the 10 (trimmed back to the retention once 1 s of slack was used).
        #expect(timeline.heldSeconds == 2)
        let recent = try #require(timeline.clip(from: origin.addingTimeInterval(8), to: origin.addingTimeInterval(10)))
        #expect(recent.count == 32_000)
        #expect(recent.first == value(128_000))
        #expect(timeline.clip(from: origin, to: origin.addingTimeInterval(2)) == nil)
    }

    @Test func tooLittleCoverageGivesNothing() throws {
        let timeline = AudioTimeline()
        timeline.append(second(0), origin: origin)      // 1 s held
        // Asking for 2 s of which 1 s exists: 50 % < the default 60 %.
        #expect(timeline.clip(from: origin.addingTimeInterval(-1), to: origin.addingTimeInterval(1)) == nil)
        let partial = try #require(timeline.clip(from: origin.addingTimeInterval(-1), to: origin.addingTimeInterval(1), minimumCoverage: 0.4))
        #expect(partial.count == 16_000)
        #expect(partial.first == value(0))
        // Nothing wanted, or backwards.
        #expect(timeline.clip(from: origin.addingTimeInterval(1), to: origin.addingTimeInterval(1)) == nil)
        #expect(timeline.clip(from: origin.addingTimeInterval(2), to: origin.addingTimeInterval(1)) == nil)
    }

    @Test func aLongSentenceIsCutToItsLastTwentySeconds() throws {
        let timeline = AudioTimeline()
        for s in 0..<30 { timeline.append(second(s), origin: origin) }
        let clip = try #require(timeline.clip(from: origin, to: origin.addingTimeInterval(30)))
        #expect(clip.count == 320_000)
        #expect(clip.first == value(160_000))
        #expect(clip.last == value(479_999))
        let shorter = try #require(timeline.clip(from: origin, to: origin.addingTimeInterval(30), maxSeconds: 5))
        #expect(shorter.count == 80_000)
    }

    @Test func aResumedStreamHasItsOwnTimeline() throws {
        let timeline = AudioTimeline()
        let later = origin.addingTimeInterval(100)
        timeline.append(second(0), origin: origin)
        timeline.append(second(0), origin: later)       // after a pause the stream starts over
        timeline.append(second(1), origin: later)
        // A late sentence of the first stream still finds its audio.
        let first = try #require(timeline.clip(from: origin.addingTimeInterval(0.25), to: origin.addingTimeInterval(0.75)))
        #expect(first.count == 8_000)
        #expect(first.first == value(4_000))
        let resumed = try #require(timeline.clip(from: later.addingTimeInterval(1.0), to: later.addingTimeInterval(1.5)))
        #expect(resumed.count == 8_000)
        #expect(resumed.first == value(16_000))
        // Between the two streams there is no audio.
        #expect(timeline.clip(from: origin.addingTimeInterval(50), to: origin.addingTimeInterval(51)) == nil)
    }

    @Test func silenceIsTrimmedOffBothEnds() {
        // 1 s of silence, 1 s at 0.1, 1 s of silence: 150 windows of 320 samples; speech is windows 50...99.
        let clip = [Float](repeating: 0, count: 16_000) + [Float](repeating: 0.1, count: 16_000) + [Float](repeating: 0, count: 16_000)
        let trimmed = AudioTimeline.trimmingSilence(clip)
        // Kept: windows 48...101 (two padding windows each side) = 54 * 320.
        #expect(trimmed.count == 17_280)
        #expect(trimmed[0] == 0 && trimmed[640] == 0.1 && trimmed[trimmed.count - 641] == 0.1 && trimmed.last == 0)
    }

    @Test func trimmingKeepsSpeechRunningToTheEdges() {
        let speech = [Float](repeating: 0.05, count: 16_000)
        #expect(AudioTimeline.trimmingSilence(speech) == speech)
        // Leading silence only: the tail is speech to the last sample, so it is kept whole.
        let lead = [Float](repeating: 0, count: 8_000) + speech
        #expect(AudioTimeline.trimmingSilence(lead).count == 16_000 + 640)
    }

    @Test func silentOrTinyClips() {
        #expect(AudioTimeline.trimmingSilence([Float](repeating: 0.0005, count: 32_000)).isEmpty)
        #expect(AudioTimeline.trimmingSilence([Float](repeating: 0, count: 32_000)).isEmpty)
        let tiny = [Float](repeating: 0.3, count: 900)
        #expect(AudioTimeline.trimmingSilence(tiny) == tiny)
    }

    @Test func resetForgetsEverything() {
        let timeline = AudioTimeline()
        timeline.append(second(0), origin: origin)
        timeline.reset()
        #expect(timeline.heldSeconds == 0)
        #expect(timeline.clip(from: origin, to: origin.addingTimeInterval(1)) == nil)
        timeline.append([], origin: origin)
        #expect(timeline.heldSeconds == 0)
    }
}
