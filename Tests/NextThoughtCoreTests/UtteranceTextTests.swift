import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct UtteranceTextTests {
    @Test func theOfflinePassWinsWhenItCoversWhatWasHeard() {
        let text = UtteranceText.final(streaming: "how does reusing the previous prediction reduce pilot overhead", streamSeconds: 6.1,
                                       refined: "How does reusing the previous prediction reduce pilot overhead?", segmentSeconds: 5.8)
        #expect(text == "How does reusing the previous prediction reduce pilot overhead?")
    }

    @Test func minutesOfQuietSpeechAreNotReplacedByTheirLastSentence() {
        let heard = Array(repeating: "that is all my work and I will introduce the plan for the next year", count: 12).joined(separator: " ")
        let text = UtteranceText.final(streaming: heard, streamSeconds: 395, refined: "So, radio map.", segmentSeconds: 4)
        #expect(text == heard)
    }

    @Test func aShortOfflineTextOfTheSameAudioStillWins() {
        // The stream stutters, the offline pass cleans it up: same audio, fewer words.
        let text = UtteranceText.final(streaming: "嗯 那个 我们 我们 这个 方法 是 是 可以 的", streamSeconds: 5, refined: "我们这个方法是可以的。", segmentSeconds: 4.6)
        #expect(text == "我们这个方法是可以的。")
    }

    @Test func emptySidesFallBack() {
        #expect(UtteranceText.final(streaming: "hello there", streamSeconds: 2, refined: "", segmentSeconds: 2) == "hello there")
        #expect(UtteranceText.final(streaming: "", streamSeconds: 0, refined: "Hello.", segmentSeconds: 2) == "Hello.")
        #expect(UtteranceText.units("XL-MIMO 的 near field 效应") == 7)
    }

    @Test func wordsLeftPendingAreClosedWhenTheSpeakerStopsOrGoesOn() {
        let start = Date(timeIntervalSince1970: 1_000)
        var pending = PendingUtterance()
        #expect(!pending.isOverdue(at: start))
        pending.heard("so the radio map", at: start)
        #expect(!pending.isOverdue(at: start.addingTimeInterval(1)))
        // Stopped: the same words for two and a half seconds.
        #expect(pending.isOverdue(at: start.addingTimeInterval(2.6)))
        // Still talking: words keep changing, but twenty seconds is long enough for a line.
        for second in 1...19 { pending.heard("so the radio map \(second)", at: start.addingTimeInterval(Double(second))) }
        #expect(!pending.isOverdue(at: start.addingTimeInterval(19.5)))
        #expect(pending.isOverdue(at: start.addingTimeInterval(20.1)))
        pending.closed()
        #expect(!pending.isOverdue(at: start.addingTimeInterval(60)))
    }
}
