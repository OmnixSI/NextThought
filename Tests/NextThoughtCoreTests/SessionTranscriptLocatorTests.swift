import Foundation
import Testing
@testable import NextThoughtCore

struct SessionTranscriptLocatorTests {
    private let start = Date(timeIntervalSince1970: 1_800_000_000)

    @Test func distantAndGapReferencesDoNotBorrowTheNearestLine() {
        let lines = [SessionTranscriptLine(date: start, speaker: .room, text: "开场"),
                     SessionTranscriptLine(date: start.addingTimeInterval(175), speaker: .room, text: "02:55 的原话")]
        #expect(SessionTranscriptLocator.index(offsetSeconds: 18 * 60 + 22, startedAt: start, lines: lines) == nil)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 60, startedAt: start, lines: lines) == nil)
        #expect(SessionTranscriptLocator.index(offsetSeconds: -1, startedAt: start, lines: lines) == nil)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 175, startedAt: start, lines: []) == nil)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 178, startedAt: start, lines: lines) == 1)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 179, startedAt: start, lines: lines) == nil)
    }

    @Test func realUtteranceIntervalsResolveWithOnlyASmallBoundaryTolerance() {
        let lines = [SessionTranscriptLine(date: start.addingTimeInterval(10), speaker: .room, text: "持续的一句话",
                                           startedAt: start.addingTimeInterval(10), endedAt: start.addingTimeInterval(30)),
                     SessionTranscriptLine(date: start.addingTimeInterval(80), speaker: .room, text: "另一个话题")]
        #expect(SessionTranscriptLocator.index(offsetSeconds: 25, startedAt: start, lines: lines) == 0)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 33, startedAt: start, lines: lines) == 0)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 34, startedAt: start, lines: lines) == nil)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 60, startedAt: start, lines: lines) == nil)
    }

    @Test func stableLineIDsTakePrecedenceAndMissingIDsCannotGuess() {
        let line = SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .room, text: "已标记的原话")
        #expect(SessionTranscriptLocator.index(lineID: line.id, offsetSeconds: 1102, startedAt: start, lines: [line]) == 0)
        #expect(SessionTranscriptLocator.index(lineID: UUID(), offsetSeconds: 1102, startedAt: start, lines: [line]) == nil)
        #expect(SessionTranscriptLocator.index(lineID: UUID(), startedAt: start, lines: [line]) == nil)
        #expect(SessionTranscriptLocator.index(lineID: UUID(), offsetSeconds: 5, startedAt: start, lines: [line]) == 0)
    }

    @Test func malformedIntervalsDoNotExpandTheAcceptedRange() {
        let line = SessionTranscriptLine(date: start.addingTimeInterval(20), speaker: .room, text: "旧锚点",
                                         startedAt: start.addingTimeInterval(30), endedAt: start.addingTimeInterval(10))
        #expect(SessionTranscriptLocator.index(offsetSeconds: 25, startedAt: start, lines: [line]) == nil)
        #expect(SessionTranscriptLocator.index(offsetSeconds: 20, startedAt: start, lines: [line]) == 0)
    }
}
