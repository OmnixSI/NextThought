import Testing
@testable import InterviewHackerCore

struct TranscriptDeduplicatorTests {
    @Test func trimsRepeatedEnglishWordsAtSegmentBoundary() {
        #expect(TranscriptDeduplicator.removeRepeatedPrefix(
            previous: "We should use a cache",
            incoming: "a cache for repeated requests") == "for repeated requests")
    }

    @Test func trimsRepeatedChineseCharactersAtSegmentBoundary() {
        #expect(TranscriptDeduplicator.removeRepeatedPrefix(
            previous: "这个方案通过缓存提升速度",
            incoming: "缓存提升速度，减少重复请求") == "减少重复请求")
    }

    @Test func keepsTextWithoutAUsefulOverlap() {
        #expect(TranscriptDeduplicator.removeRepeatedPrefix(
            previous: "The first topic is caching",
            incoming: "A separate question about storage") == "A separate question about storage")
    }
}
