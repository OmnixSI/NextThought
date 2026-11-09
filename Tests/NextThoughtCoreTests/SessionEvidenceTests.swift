import Foundation
import Testing
@testable import NextThoughtCore

struct SessionEvidenceTests {
    @Test func aValidTimestampAloneNeverClaimsThatAModelFactIsSupported() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(32), speaker: .remote,
                                         text: "我们会等外部数据确认后再提交。")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(90), mode: .meeting)
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "已决定周五提交。", time: "00:32")])])

        let evidence = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: notes, summary: nil)

        #expect(evidence.count == 1)
        #expect(evidence[0].support == .timeOnly)
        #expect(evidence[0].lineID == line.id)
        #expect(evidence[0].claim == "已决定周五提交。")
        #expect(evidence[0].reviewDecision == nil)
        #expect(evidence[0].claimRevision?.count == 64)
        #expect(evidence[0].support == .timeOnly)
        #expect(evidence[0].needsConfirmation)
        #expect(!evidence[0].support.label.contains("已确认"))
        #expect(!evidence[0].support.label.localizedCaseInsensitiveContains("confirmed"))
    }

    @Test func exactQuoteLinksToItsStableLineAndKeepsTheSourceRevision() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(65), speaker: .remote,
                                         text: "等外部数据确认后再提交。")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(100), mode: .meeting)
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "actions", title: "待办", kind: .actions,
                                                                  items: [NotesItem(text: "周五前提交", time: "01:05",
                                                                                    quote: "等外部数据确认后再提交。")])])

        let evidence = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: notes, summary: nil)[0]

        #expect(evidence.support == .quoted)
        #expect(evidence.lineID == line.id)
        #expect(evidence.sourceVariant == .corrected)
        #expect(evidence.sourceRevision == SessionProjectionBuilder.transcriptRevision(id: line.id, text: line.text))
    }

    @Test func repeatedQuoteUsesTheOccurrenceNearestItsTimestamp() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let first = SessionTranscriptLine(date: start.addingTimeInterval(12), speaker: .remote, text: "周五发布新版。")
        let second = SessionTranscriptLine(date: start.addingTimeInterval(82), speaker: .remote, text: "周五发布新版。")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "周五发布新版", time: "01:20",
                                                                                    quote: "周五发布新版。")])])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(100), mode: .meeting)

        let evidence = SessionEvidenceBuilder.build(meta: meta, lines: [first, second], notes: notes, summary: nil)[0]

        #expect(evidence.support == .quoted)
        #expect(evidence.lineID == second.id)
        #expect(evidence.location == "01:22")
    }

    @Test func timestampChoosesNearestOccurrenceAcrossOriginalAndCorrectedVersions() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let nearby = SessionTranscriptLine(date: start.addingTimeInterval(12), speaker: .remote,
                                           text: "缓存鸡穿需要修复。")
        let farther = SessionTranscriptLine(date: start.addingTimeInterval(82), speaker: .remote,
                                            text: "缓存集群已经准备好。")
        let corrections = [
            TranscriptCorrection(line: 0, lineID: nearby.id,
                                 baseRevision: SessionProjectionBuilder.transcriptRevision(id: nearby.id, text: nearby.text),
                                 original: "鸡穿", corrected: "雪崩"),
            TranscriptCorrection(line: 1, lineID: farther.id,
                                 baseRevision: SessionProjectionBuilder.transcriptRevision(id: farther.id, text: farther.text),
                                 original: "集群", corrected: "鸡穿")
        ]
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "缓存鸡穿需要修复", time: "00:12",
                                                                                    quote: "缓存鸡穿")])],
                                    corrections: corrections)
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(100), mode: .meeting)

        let evidence = SessionEvidenceBuilder.build(meta: meta, lines: [nearby, farther], notes: notes, summary: nil)[0]

        #expect(evidence.lineID == nearby.id)
        #expect(evidence.sourceVariant == .original)
    }

    @Test func correctedOnlyQuoteIdentifiesTheCorrectedTranscriptVersion() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(15), speaker: .remote, text: "缓存鸡穿")
        let correction = TranscriptCorrection(line: 0, lineID: line.id,
                                              baseRevision: SessionProjectionBuilder.transcriptRevision(id: line.id, text: line.text),
                                              original: "鸡穿", corrected: "集群")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "使用缓存集群", time: "00:15",
                                                                                    quote: "缓存集群")])],
                                    corrections: [correction])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(60), mode: .meeting)

        let evidence = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: notes, summary: nil)[0]

        #expect(evidence.support == .quoted)
        #expect(evidence.sourceVariant == .corrected)
        #expect(evidence.sourceText == "缓存集群")
        #expect(evidence.sourceRevision == SessionProjectionBuilder.transcriptRevision(id: line.id, text: "缓存集群"))
    }

    @Test func persistedEvidenceDecisionIsBoundToClaimAndBothTranscriptRevisions() throws {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(32), speaker: .remote,
                                         text: "周五前提交接口评审。")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(90), mode: .meeting)
        let itemID = UUID()
        let item = NotesItem(id: itemID, text: "周五前提交接口评审", time: "00:32",
                             quote: "周五前提交接口评审。")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "actions", title: "待办", kind: .actions,
                                                                  items: [item])])
        let original = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: notes, summary: nil)[0]
        let review = SessionEvidenceReview(decision: .supported,
                                           claimRevision: try #require(original.claimRevision),
                                           transcriptSourceRevision: original.transcriptSourceRevision,
                                           lineSourceRevision: try #require(original.sourceRevision))
        let summary = SessionSummary(title: "会议", markdown: "", questions: [], generatedAt: start, model: "test",
                                     transcriptSourceRevision: original.transcriptSourceRevision,
                                     evidenceReviews: [original.id: review])
        let restored = try JSONDecoder().decode(SessionSummary.self, from: JSONEncoder().encode(summary))

        let current = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: notes, summary: restored)[0]
        #expect(current.reviewDecision == .supported)
        #expect(!current.reviewIsStale)

        var changedLine = line
        changedLine.text = "周五前不提交接口评审。"
        let transcriptChanged = SessionEvidenceBuilder.build(meta: meta, lines: [changedLine], notes: notes,
                                                              summary: restored)[0]
        #expect(transcriptChanged.reviewDecision == nil)
        #expect(transcriptChanged.reviewIsStale)

        var changedNotes = notes
        changedNotes.sections[0].items[0].text = "下周再提交接口评审"
        let claimChanged = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: changedNotes,
                                                        summary: restored)[0]
        #expect(claimChanged.reviewDecision == nil)
        #expect(claimChanged.reviewIsStale)
    }

    @Test func unmatchedQuoteCanShowNearbySpeechButRemainsUnverified() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(120), speaker: .remote, text: "本周先完成接口评审。")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(200), mode: .meeting)
        let payload = StructuredNotesPayload(format: .outline,
                                             outline: OutlineNotes(sections: [OutlineSection(title: "决定", at: 120,
                                                                                            points: [OutlinePoint(text: "正式决定周五发布", at: 120)])]))
        let summary = SessionSummary(title: "会议", markdown: "", questions: [], generatedAt: start, model: "test",
                                     structured: payload, structuredAll: [payload])

        let evidence = SessionEvidenceBuilder.build(meta: meta, lines: [line], notes: nil, summary: summary)
        let decision = evidence.first { $0.claim == "正式决定周五发布" }

        #expect(decision?.support == .timeOnly)
        #expect(decision?.lineID == line.id)
        #expect(decision?.sourceText == line.text)
    }

    @Test func structuredEvidenceItemCanBeEditedAndKeepsItsSiblingNodes() throws {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let payload = StructuredNotesPayload(format: .outline,
                                             outline: OutlineNotes(sections: [OutlineSection(
                                                title: "路线图",
                                                points: [OutlinePoint(text: "先完成缓存", at: 24,
                                                                      children: [TimedText(text: "补充监控", at: 30)])])]))
        let summary = SessionSummary(title: "会议", markdown: "", questions: [], generatedAt: start, model: "test",
                                     structured: payload, structuredAll: [payload])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(90), mode: .meeting)
        let references = SessionEvidenceBuilder.build(meta: meta, lines: [], notes: nil, summary: summary)
        let child = references.first { $0.claim == "补充监控" }
        let target = try #require(child?.structuredTarget)

        #expect(target.format == .outline)
        #expect(target.path == "section:0:point:0:child:0")
        #expect(!target.payloadRevision.isEmpty)
        var edited = summary
        #expect(SessionEvidenceEditor.updateStructuredNote(in: &edited,
                                                            target: target,
                                                            text: "补充告警监控"))
        #expect(edited.payload(for: .outline)?.outline?.sections[0].points[0].text == "先完成缓存")
        #expect(edited.payload(for: .outline)?.outline?.sections[0].points[0].children[0].text == "补充告警监控")
        #expect(SessionEvidenceBuilder.build(meta: meta, lines: [], notes: nil, summary: edited)
            .contains(where: { $0.claim == "补充告警监控" }))

        var regeneratedOutline = payload.outline!
        regeneratedOutline.sections[0].points[0].children[0].text = "新生成的行动项"
        var regeneratedPayload = payload
        regeneratedPayload.outline = regeneratedOutline
        var regenerated = summary
        regenerated.store(regeneratedPayload)
        #expect(!SessionEvidenceEditor.updateStructuredNote(in: &regenerated,
                                                              target: target,
                                                              text: "旧编辑不能覆盖新内容"))
        #expect(regenerated.payload(for: .outline)?.outline?.sections[0].points[0].children[0].text == "新生成的行动项")

        var unchanged = summary
        #expect(!SessionEvidenceEditor.updateStructuredNote(in: &unchanged,
                                                             target: SessionStructuredNotesTarget(format: .outline,
                                                                                                  path: "section:9",
                                                                                                  payloadRevision: target.payloadRevision),
                                                             text: "不存在"))
    }
}
