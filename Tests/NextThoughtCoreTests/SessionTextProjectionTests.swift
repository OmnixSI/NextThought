import Foundation
import Testing
@testable import NextThoughtCore

struct SessionTextProjectionTests {
    let start = Date(timeIntervalSince1970: 1_800_000_000)

    @Test func correctionsFollowStableLineIDsAndRejectChangedSources() {
        let first = SessionTranscriptLine(id: UUID(), date: start, speaker: .remote, text: "用 Redis 缓存数据")
        let second = SessionTranscriptLine(id: UUID(), date: start.addingTimeInterval(3), speaker: .remote, text: "Redis 需要一致性校验")
        let anchored = TranscriptCorrection(line: 0, lineID: second.id,
                                            baseRevision: SessionProjectionBuilder.transcriptRevision(id: second.id, text: second.text),
                                            original: "一致性", corrected: "缓存一致性")

        let reordered = NotesCorrections.apply([anchored], to: [second, first])
        #expect(reordered[0].text == "Redis 需要缓存一致性校验")
        #expect(reordered[1].text == first.text)

        var changed = second
        changed.text = "Redis 需要新的校验"
        let rejected = NotesCorrections.apply([anchored], to: [changed])
        #expect(rejected[0].text == changed.text)
    }

    @Test func multipleCorrectionsShareTheSameOriginalRevision() {
        let line = SessionTranscriptLine(id: UUID(), date: start, speaker: .remote, text: "错误词和错误名")
        let revision = SessionProjectionBuilder.transcriptRevision(id: line.id, text: line.text)
        let corrections = [
            TranscriptCorrection(line: 0, lineID: line.id, baseRevision: revision,
                                 original: "错误词", corrected: "正确词"),
            TranscriptCorrection(line: 0, lineID: line.id, baseRevision: revision,
                                 original: "错误名", corrected: "正确名")
        ]

        #expect(NotesCorrections.apply(corrections, to: [line])[0].text == "正确词和正确名")
        #expect(NotesCorrections.valid(corrections, lines: [line]).count == 2)
    }

    @Test func projectionUnifiesCorrectionsTranslationsAnswersAndNotes() {
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(90), mode: .meeting)
        let lineID = UUID()
        let line = SessionTranscriptLine(id: lineID, date: start.addingTimeInterval(12), speaker: .remote,
                                        text: "周五交付", translatedText: "Deliver Friday",
                                        translationInputRevision: SessionProjectionBuilder.transcriptRevision(id: lineID, text: "周五交付"))
        let stableCorrection = TranscriptCorrection(line: 0, lineID: line.id,
                                                    baseRevision: SessionProjectionBuilder.transcriptRevision(id: line.id, text: line.text),
                                                    original: "周五", corrected: "周一")
        let notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "交付安排",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "周一交付", time: "00:12", quote: "周五交付")])],
                                    corrections: [stableCorrection])
        let answer = SessionAnswerEvent(date: start.addingTimeInterval(14), question: "什么时候交付？",
                                        answer: "周一", model: "test", automatic: false)

        let projection = SessionProjectionBuilder.build(meta: meta, lines: [line], answers: [answer],
                                                        summary: SessionSummary(title: "交付", markdown: "## 摘要\n周一交付。",
                                                                                questions: [], generatedAt: start, model: "test"),
                                                        notes: notes)

        #expect(projection.transcript[0].displayText == "周一交付")
        #expect(projection.transcript[0].originalText == "周五交付")
        #expect(projection.transcript[0].translationIsStale)
        #expect(projection.transcript[0].id == line.id)
        #expect(projection.transcript[0].sourceRevision == SessionProjectionBuilder.transcriptRevision(id: line.id, text: "周一交付"))
        #expect(projection.searchableText.contains { $0.kind == .transcript && $0.text == "周五交付" && $0.lineID == line.id })
        #expect(projection.searchableText.contains { $0.kind == .correctedTranscript && $0.text == "周一交付" && $0.lineID == line.id })
        #expect(projection.searchableText.contains { $0.kind == .translation && $0.text == "Deliver Friday" && $0.lineID == line.id })
        #expect(projection.searchableText.first { $0.kind == .translation }?.isStale == true)
        #expect(projection.searchableText.contains { $0.kind == .answer && $0.text == "周一" && $0.sourceID == answer.id })
        #expect(projection.searchableText.contains { $0.kind == .notes && $0.text == "周一交付" })
        #expect(projection.searchableText.contains { $0.kind == .summary && $0.text.contains("周一交付") })
        #expect(projection.summary?.title == "交付")
        #expect(projection.revision.count == 64)
    }

    @Test func markdownExportUsesTheSameCorrectedProjectionAsTheNotesPage() {
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        let lineID = UUID()
        let line = SessionTranscriptLine(id: lineID, date: start.addingTimeInterval(5), speaker: .remote, text: "缓存鸡穿",
                                         translatedText: "Cache failure", translationInputRevision: SessionProjectionBuilder.transcriptRevision(id: lineID, text: "缓存鸡穿"))
        let correction = TranscriptCorrection(line: 0, lineID: line.id,
                                              baseRevision: SessionProjectionBuilder.transcriptRevision(id: line.id, text: line.text),
                                              original: "鸡穿", corrected: "击穿")
        let notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "缓存",
                                    sections: [], corrections: [correction])
        let text = SessionMarkdownExporter.export(meta: meta, summary: nil, lines: [line], answers: [], detected: [], notes: notes)

        #expect(text.contains("**对方**：缓存击穿"))
        #expect(!text.contains("**对方**：缓存鸡穿"))
        #expect(text.contains("*译文（待更新）：* Cache failure"))
    }

    @Test func markdownExportUsesEditedStructuredNotesInsteadOfOlderGeneratedText() {
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .remote, text: "会议里确认继续观察。")
        let editedNotes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "交付安排",
                                          sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                        items: [NotesItem(text: "用户改过的最终措辞")])],
                                          transcriptSourceRevision: SessionProjectionBuilder.transcriptSourceRevision(meta: meta,
                                                                                                                     lines: [line]))
        let olderSummary = SessionSummary(title: "交付安排", markdown: "## 决定\n模型原先写的措辞",
                                          questions: [], generatedAt: start, model: "test")
        let projection = SessionProjectionBuilder.build(meta: meta, lines: [line], summary: olderSummary, notes: editedNotes)

        let text = SessionMarkdownExporter.export(meta: meta, projection: projection)

        #expect(projection.notes == editedNotes)
        #expect(text.contains("用户改过的最终措辞"))
        #expect(!text.contains("模型原先写的措辞"))
        #expect(text.contains("会议里确认继续观察。"))
    }

    @Test func noteAndSummarySearchHitsExposeTheirStaleSourceRevision() {
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .remote, text: "周一提交")
        let notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "安排",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "周五提交")])],
                                    transcriptSourceRevision: "previous-transcript")
        let summary = SessionSummary(title: "旧摘要", markdown: "周五提交", questions: [], generatedAt: start,
                                     model: "test", transcriptSourceRevision: "previous-transcript")

        let projection = SessionProjectionBuilder.build(meta: meta, lines: [line], summary: summary, notes: notes)

        #expect(projection.searchableText.contains { $0.kind == .notes && $0.text == "周五提交" && $0.isStale })
        #expect(projection.searchableText.contains { $0.kind == .summary && $0.text == "周五提交" && $0.isStale })
        #expect(projection.summary?.sourceStatus == .stale)
        #expect(SessionMarkdownExporter.export(meta: meta, projection: projection).contains("较旧的转写版本"))
    }

    @Test func projectionSearchHitKeepsLineIdentityAcrossReordering() {
        let target = SessionTranscriptLine(id: UUID(), date: start.addingTimeInterval(8), speaker: .remote, text: "目标句子")
        let other = SessionTranscriptLine(id: UUID(), date: start, speaker: .me, text: "另一句")
        let first = SessionProjectionBuilder.build(meta: SessionMeta(startedAt: start, mode: .meeting), lines: [target, other])
        let hit = first.searchableText.first { $0.text == "目标句子" }
        let reordered = SessionProjectionBuilder.build(meta: SessionMeta(startedAt: start, mode: .meeting), lines: [other, target])

        #expect(hit?.lineID == target.id)
        #expect(hit?.id == "line:\(target.id.uuidString):original")
        #expect(reordered.searchableText.first { $0.id == hit?.id }?.lineID == target.id)
    }

    @Test func translationSearchRevisionChangesWhenTheTranslationChanges() {
        let lineID = UUID()
        let sourceRevision = SessionProjectionBuilder.transcriptRevision(id: lineID, text: "原文")
        let first = SessionTranscriptLine(id: lineID, date: start, speaker: .remote, text: "原文",
                                         translatedText: "First", translationInputRevision: sourceRevision)
        let updated = SessionTranscriptLine(id: lineID, date: start, speaker: .remote, text: "原文",
                                           translatedText: "Updated", translationInputRevision: sourceRevision)
        let firstHit = SessionProjectionBuilder.build(meta: SessionMeta(startedAt: start, mode: .meeting), lines: [first])
            .searchableText.first { $0.kind == .translation }
        let updatedHit = SessionProjectionBuilder.build(meta: SessionMeta(startedAt: start, mode: .meeting), lines: [updated])
            .searchableText.first { $0.kind == .translation }

        #expect(firstHit?.id == updatedHit?.id)
        #expect(firstHit?.sourceRevision != updatedHit?.sourceRevision)
    }
}
