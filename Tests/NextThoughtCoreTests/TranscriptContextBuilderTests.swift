import Foundation
import Testing
@testable import NextThoughtCore

struct TranscriptContextBuilderTests {
    @Test func keepsOnlyTheMostRecentFiveMinutesInOrder() {
        let now = Date(timeIntervalSince1970: 10_000)
        let entries: [(date: Date, text: String)] = [
            (now.addingTimeInterval(-301), "old"),
            (now.addingTimeInterval(-200), "first"),
            (now, "second")
        ]
        #expect(TranscriptContextBuilder.recent(entries, now: now) == "first\nsecond")
    }
}
