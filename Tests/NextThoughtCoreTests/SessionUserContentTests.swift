import Foundation
import Testing
@testable import NextThoughtCore

struct SessionUserContentTests {
    private func temporaryDirectory() throws -> URL {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }

    @Test func schemaOneDecisionsWithoutIDsHaveAStableFallback() throws {
        let lineID = UUID()
        let legacy: [String: Any] = ["schemaVersion": 1, "correctionDecisions": [[
            "lineID": lineID.uuidString, "original": "Mamba", "corrected": "曼巴", "reverted": true
        ]]]
        let data = try JSONSerialization.data(withJSONObject: legacy)
        let first = try JSONLines.decoder().decode(SessionUserContent.self, from: data)
        let second = try JSONLines.decoder().decode(SessionUserContent.self, from: data)
        #expect(first.correctionDecisions.first?.id == second.correctionDecisions.first?.id)
        #expect(first.correctionDecisions.first?.lineID == lineID)
        #expect(first.editedSummary == nil)
        #expect(first.hasContent)
        let roundTrip = try JSONLines.decoder().decode(SessionUserContent.self, from: JSONLines.encoder().encode(first))
        #expect(roundTrip.correctionDecisions.first?.id == first.correctionDecisions.first?.id)
    }

    @Test func fullSummaryOverrideKeepsStructuredPayloadAcrossAIDraftReplacement() throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }
        let generatedAt = Date(timeIntervalSince1970: 1_800_000_000)
        var summary = SessionSummary(title: "Edited", markdown: "User-edited summary", questions: [], generatedAt: generatedAt, model: "test")
        summary.store(StructuredNotesPayload(format: .outline, outline: OutlineNotes(sections: [
            OutlineSection(title: "Edited section", points: [OutlinePoint(text: "User-edited point")])
        ]), transcriptSourceRevision: "source-v1"))
        var content = SessionUserContent()
        content.editedSummary = summary
        _ = try SessionUserContentStore.save(content, to: directory)
        let replacement = SessionSummary(title: "New AI", markdown: "New AI original", questions: [], generatedAt: generatedAt, model: "test")
        try JSONLines.encoder().encode(replacement).write(to: directory.appendingPathComponent("summary.json"))
        let reopened = try SessionUserContentStore.load(from: directory)
        #expect(reopened.hasContent)
        #expect(reopened.editedSummary?.markdown == "User-edited summary")
        #expect(reopened.editedSummary?.payload(for: .outline)?.outline?.sections.first?.points.first?.text == "User-edited point")
        #expect(reopened.editedSummary?.payload(for: .outline)?.transcriptSourceRevision == "source-v1")
    }

    @Test func userDraftRoundTripsIndependentlyOfAIResults() throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }
        let line = UUID(), item = NotesItem(text: "确认过的结论", time: "00:05")
        var content = SessionUserContent()
        content.personalNotes = "下次验证消融实验"
        content.highlights = [.init(lineID: line, offsetSeconds: 5, quote: "导师的建议")]
        content.editedNotes = StructuredNotes(templateID: "lab", templateName: "组会", language: "zh", title: "实验讨论",
                                            sections: [.init(id: "results", title: "研究进展", kind: .items, items: [item])])
        let saved = try SessionUserContentStore.save(content, to: directory)
        #expect(saved.revision == 1)
        let replacementAI = Data("{\"title\":\"新的AI结果\"}".utf8)
        try replacementAI.write(to: directory.appendingPathComponent("notes.json"))
        let reopened = try SessionUserContentStore.load(from: directory)
        #expect(reopened.revision == saved.revision)
        #expect(reopened.personalNotes == saved.personalNotes)
        #expect(reopened.editedNotes?.sections.first?.items.first?.id == item.id)
        #expect(reopened.highlights.first?.lineID == line)
        #expect(reopened.hasContent)
        #expect(throws: (any Error).self) { try SessionUserContentStore.save(content, to: directory) }
        #expect(try SessionUserContentStore.load(from: directory).revision == saved.revision)
    }

    @Test func explicitCorrectionSurvivesRegenerationAndLineReordering() {
        let first = SessionTranscriptLine(date: Date(), speaker: .room, text: "研究的是 Mamba 结构")
        let second = SessionTranscriptLine(date: Date(), speaker: .room, text: "开始实验")
        var content = SessionUserContent()
        content.correctionDecisions = [.init(lineID: first.id, original: "Mamba", corrected: "曼巴", reverted: true)]
        let regenerated = [TranscriptCorrection(line: 1, original: "Mamba", corrected: "另一种错误译名")]
        let corrections = content.corrections(generated: regenerated, lines: [second, first])
        #expect(corrections.count == 1)
        #expect(corrections[0].reverted)
        #expect(NotesCorrections.apply(corrections, to: [second, first])[1].text == first.text)
    }

    @Test func corruptContentIsNeverSilentlyOverwritten() throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }
        let damaged = Data("{incomplete".utf8)
        try damaged.write(to: directory.appendingPathComponent(SessionUserContent.filename))
        #expect(throws: (any Error).self) { try SessionUserContentStore.save(.init(), to: directory) }
        #expect(try Data(contentsOf: directory.appendingPathComponent(SessionUserContent.filename)) == damaged)
    }

    @Test func failedWriteDoesNotPublishASavedRevision() throws {
        let directory = try temporaryDirectory()
        defer { try? FileManager.default.removeItem(at: directory) }
        let blocked = directory.appendingPathComponent("blocked")
        try Data("existing file".utf8).write(to: blocked)
        var content = SessionUserContent(); content.personalNotes = "不得丢失的笔记"
        #expect(throws: (any Error).self) { try SessionUserContentStore.save(content, to: blocked) }
        #expect(content.revision == 0)
        #expect(try String(contentsOf: blocked, encoding: .utf8) == "existing file")
    }

    @Test func oldContextsDecodeAndIncompleteAnswersAreExcludedFromNotesInput() throws {
        let context = SessionContextSnapshot(mode: .labMeeting, style: .detailed, preferences: "", profile: "", documents: [])
        let decoded = try JSONLines.decoder().decode(SessionContextSnapshot.self, from: JSONLines.encoder().encode(context))
        #expect(decoded.effectiveMode == nil)
        let start = Date()
        let complete = SessionAnswerEvent(date: start, question: "完整问题", answer: "完整答案", model: "m", automatic: false)
        let interrupted = SessionAnswerEvent(date: start, question: "中断问题", answer: "尚未完成", model: "m", automatic: false, completion: .interrupted)
        let material = SessionSummaryPrompt.Material(meta: .init(startedAt: start, mode: .labMeeting), lines: [], answers: [complete, interrupted], detected: [], context: decoded)
        #expect(material.answers == [complete])
        let exported = SessionMarkdownExporter.export(meta: material.meta, summary: nil, lines: [], answers: [interrupted], detected: [])
        #expect(exported.contains("未完成的回答片段"))
        let unknown = try JSONDecoder().decode(AnswerCompletion.self, from: Data("\"new-status\"".utf8))
        #expect(unknown == .interrupted)
    }
}
