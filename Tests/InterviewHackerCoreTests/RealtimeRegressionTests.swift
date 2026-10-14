import Foundation
import Testing
@testable import InterviewHackerCore

struct RealtimeRegressionTests {
    let now = Date(timeIntervalSince1970: 10_000)
    @Test func duplicateExpiresAndFollowUpsWorkWithoutPunctuation() {
        var detector = QuestionDetector()
        #expect(detector.detect("然后呢", now: now) == "然后呢")
        #expect(detector.detect("然后呢", now: now.addingTimeInterval(1)) == nil)
        #expect(detector.detect("然后呢", now: now.addingTimeInterval(16)) == "然后呢")
        #expect(detector.detect("why", now: now) == "why")
    }
    @Test func staleFragmentsDoNotAttachToLaterStatements() {
        var detector = QuestionDetector()
        #expect(detector.detect("Can you", now: now) == nil)
        #expect(detector.detect("The meeting has ended", now: now.addingTimeInterval(9)) == nil)
    }
    @Test func differentQuestionsWithSimilarWordsAreNotDeleted() {
        var detector = QuestionDetector()
        #expect(detector.detect("Why does cache A outperform cache B?", now: now) != nil)
        #expect(detector.detect("Why does cache B outperform cache A?", now: now) != nil)
    }
    @Test func manualTriggerUsesLiveTextAndBypassesQuestionHeuristics() {
        #expect(ManualQuestionBuilder.make(typed: "  Custom  ", partial: "live", recent: ["old"]) == "Custom")
        #expect(ManualQuestionBuilder.make(typed: "", partial: "ordinary statement", recent: [])?.contains("ordinary statement") == true)
        #expect(ManualQuestionBuilder.make(typed: "", partial: "", recent: []) == nil)
    }
    @Test func repeatedSpeechIsOnlyTrimmedForOverlappingAudio() {
        #expect(TranscriptDeduplicator.removeOverlappingPrefix(previous: "Why?", incoming: "Why?", previousEnd: now, incomingStart: now.addingTimeInterval(1)) == "Why?")
        #expect(TranscriptDeduplicator.removeOverlappingPrefix(previous: "Why?", incoming: "Why?", previousEnd: now, incomingStart: now.addingTimeInterval(-1)) == "")
    }
    @Test func retrievalSupportsChineseEnglishPageReferencesAndDisabledFiles() {
        let docs = [ContextDocument(name: "notes.pdf", pages: ["网络延迟主要由传输和排队组成。", "数据库索引减少查询时间。"]),
                    ContextDocument(name: "JD.md", pages: ["Kubernetes reliability and deployment"], enabled: false)]
        let index = SessionContextIndex(documents: docs)
        let matches = index.retrieve(question: "数据库索引有什么用")
        #expect(matches.first?.source == "notes.pdf")
        #expect(matches.first?.page == 2)
        #expect(index.retrieve(question: "Kubernetes").isEmpty)
        #expect(index.retrieve(question: "unrelatedword").isEmpty)
        #expect(index.retrieve(question: "索引", maximumCharacters: 3).reduce(0) { $0 + $1.text.count } <= 3)
    }
    @Test func customPreferencesAreSeparateAndFreshContextIsKept() throws {
        let prompt = AnswerPromptBuilder.make(question: "why", context: String(repeating: "old ", count: 4_000) + "LATEST", profile: "data", turns: [], hasScreenshot: false, preferences: "Use English", mode: .lesson)
        let json = String(prompt.dropFirst(AnswerPromptBuilder.instructions.count + 2))
        let object = try #require(JSONSerialization.jsonObject(with: Data(json.utf8)) as? [String: Any])
        #expect(object["user_preferences"] as? String == "Use English")
        #expect(object["session_mode"] as? String == "lesson")
        #expect((object["recent_transcript"] as? String)?.hasSuffix("LATEST") == true)
    }
    @Test func rejectsShortcutCollisionsAndUnmodifiedKeys() {
        #expect(ShortcutBinding.validate(["a": .init(keyCode: 36), "b": .init(keyCode: 36)]) != nil)
        #expect(!ShortcutBinding(keyCode: 0, command: false).isSafe)
        #expect(ShortcutBinding.validate(["a": .init(keyCode: 36), "b": .init(keyCode: 35)]) == nil)
    }
    @Test func overlapAloneIsNotFlushedAsANewUtterance() {
        var segmenter = VoiceActivitySegmenter()
        for _ in 0..<120 { _ = segmenter.append(Array(repeating: Int16(5_000), count: 1_600)) }
        #expect(segmenter.flush() == nil)
    }
}
