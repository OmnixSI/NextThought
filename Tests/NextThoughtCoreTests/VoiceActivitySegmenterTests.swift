import Testing
@testable import NextThoughtCore

struct VoiceActivitySegmenterTests {
    @Test func waitsForSpeechAndFinalizesAfterPause() {
        var segmenter = VoiceActivitySegmenter()
        let silence = Array(repeating: Int16(0), count: 8_000)
        let speech = Array(repeating: Int16(5_000), count: 8_000)

        #expect(segmenter.append(silence).isEmpty)
        #expect(segmenter.append(speech).isEmpty)
        #expect(segmenter.append(speech).isEmpty)
        #expect(segmenter.append(silence).isEmpty)
        let events = segmenter.append(silence)
        #expect(events.count == 1)
        #expect(events[0].kind == .final)
        #expect(events[0].samples.count >= 24_000)
        #expect(events[0].startSample < events[0].endSample)
    }

    @Test func emitsTemporaryPartialBeforeFinal() {
        var segmenter = VoiceActivitySegmenter()
        let speech = Array(repeating: Int16(5_000), count: 8_000)
        var kinds: [SpeechSegmentEvent.Kind] = []

        for _ in 0..<10 {
            kinds.append(contentsOf: segmenter.append(speech).map(\.kind))
        }
        kinds.append(segmenter.flush()?.kind ?? .partial)

        #expect(kinds.contains(.partial))
        #expect(kinds.last == .final)
    }

    @Test func carriesOverlapWhenContinuousSpeechReachesLimit() {
        var segmenter = VoiceActivitySegmenter()
        let speech = Array(repeating: Int16(5_000), count: 8_000)
        var finalEvents: [SpeechSegmentEvent] = []

        for _ in 0..<40 {
            finalEvents.append(contentsOf: segmenter.append(speech).filter { $0.kind == .final })
        }

        #expect(finalEvents.count >= 1)
        #expect(finalEvents[0].samples.count >= 192_000)
        #expect(finalEvents[0].samples.count <= 200_000)
        #expect(finalEvents.count >= 1)
        #expect(finalEvents[0].endSample > finalEvents[0].startSample)
    }

    @Test func filtersSilenceAndVeryShortVoice() {
        var segmenter = VoiceActivitySegmenter()
        let silence = Array(repeating: Int16(0), count: 8_000)
        let shortSpeech = Array(repeating: Int16(5_000), count: 2_000)

        #expect(segmenter.append(silence).isEmpty)
        #expect(segmenter.append(shortSpeech).isEmpty)
        #expect(segmenter.flush() == nil)
    }

    @Test func resetDiscardsPendingAudio() {
        var segmenter = VoiceActivitySegmenter()
        let speech = Array(repeating: Int16(5_000), count: 16_000)
        #expect(segmenter.append(speech).isEmpty)
        segmenter.reset()
        #expect(segmenter.flush() == nil)
    }

    @Test func retainsWholeFirstSpeechChunkAndPreRoll() {
        var segmenter = VoiceActivitySegmenter()
        _ = segmenter.append(Array(repeating: Int16(0), count: 8_000))
        let firstWord = Array(repeating: Int16(5_000), count: 8_000)
        _ = segmenter.append(firstWord)
        let event = segmenter.flush()
        #expect(event?.samples.count == 12_000)
        #expect(event?.samples.suffix(8_000) == firstWord[...])
        #expect(event?.startSample == 4_000)
        #expect(event?.endSample == 16_000)
    }

    @Test func continuousSpeechUsesHalfSecondOverlap() {
        var segmenter = VoiceActivitySegmenter()
        var first: SpeechSegmentEvent?
        for _ in 0..<120 {
            let events = segmenter.append(Array(repeating: Int16(5_000), count: 1_600))
            if let event = events.first(where: { $0.kind == .final }) { first = event }
        }
        _ = segmenter.append(Array(repeating: Int16(5_000), count: 1_600))
        let tail = segmenter.flush()
        #expect(first?.endSample == 192_000)
        #expect(tail?.startSample == 184_000)
        #expect(tail?.samples.count == 9_600)
        #expect(first?.id != tail?.id)
    }
}
