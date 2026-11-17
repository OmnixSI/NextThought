import Foundation
import Testing
@testable import NextThoughtCore

struct SessionExportTests {
    @Test func subtitleExportUsesProjectionAndReportsEstimatedEnds() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let first = SessionTranscriptLine(date: start.addingTimeInterval(1), speaker: .remote,
                                          text: "原始识别", translatedText: "source text",
                                          startedAt: start.addingTimeInterval(1))
        let second = SessionTranscriptLine(date: start.addingTimeInterval(4), speaker: .me,
                                           text: "校正后的内容", startedAt: start.addingTimeInterval(4),
                                           endedAt: start.addingTimeInterval(5))
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [], corrections: [TranscriptCorrection(line: 1, original: "校正后的内容",
                                                                                     corrected: "当前校正版本")])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(6), mode: .meeting)
        let projection = SessionProjectionBuilder.build(meta: meta, lines: [first, second], summary: nil, notes: notes)

        let result = SessionSubtitleExporter.export(meta: meta, projection: projection, format: .vtt)

        #expect(result.text.hasPrefix("WEBVTT\n\n"))
        #expect(result.text.contains("00:00:01.000 --> 00:00:02.200"))
        #expect(result.text.contains("远端：原始识别") || result.text.contains("对方：原始识别"))
        #expect(result.text.contains("当前校正版本"))
        #expect(result.estimatedTimingCount == 1)
    }

    @Test func subtitleExportLabelsStaleTranslations() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(10), speaker: .remote,
                                         text: "校正文本", translatedText: "old translation",
                                         translationInputRevision: "old-source")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [], corrections: [TranscriptCorrection(line: 0, original: "校正文本", corrected: "新校正文本")])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(20), mode: .meeting)
        let projection = SessionProjectionBuilder.build(meta: meta, lines: [line], summary: nil, notes: notes)

        let result = SessionSubtitleExporter.export(meta: meta, projection: projection, format: .srt)

        #expect(result.text.contains("译文（可能已过期）：old translation"))
        #expect(result.staleTranslationCount == 1)
    }

    @Test func subtitleLanguageChoiceDoesNotLeakOrDuplicateTranslations() throws {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(2), speaker: .remote,
                                         text: "原文内容", translatedText: "translated text",
                                         startedAt: start.addingTimeInterval(2), endedAt: start.addingTimeInterval(4))
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(5), mode: .meeting)
        let source = SessionProjectionBuilder.build(meta: meta, lines: [line], options: .init(languageView: .source))
        let translated = SessionProjectionBuilder.build(meta: meta, lines: [line], options: .init(languageView: .translated))
        let bilingual = SessionProjectionBuilder.build(meta: meta, lines: [line], options: .init(languageView: .bilingual))

        let sourceExport = SessionSubtitleExporter.export(meta: meta, projection: source, format: .srt)
        let translatedExport = SessionSubtitleExporter.export(meta: meta, projection: translated, format: .srt)
        let bilingualExport = SessionSubtitleExporter.export(meta: meta, projection: bilingual, format: .vtt)

        #expect(sourceExport.text.contains("原文内容"))
        #expect(!sourceExport.text.contains("translated text"))
        #expect(translatedExport.text.contains("译文：translated text"))
        #expect(!translatedExport.text.contains("原文内容"))
        let sourcePosition = try #require(bilingualExport.text.range(of: "原文内容")?.lowerBound)
        let translationPosition = try #require(bilingualExport.text.range(of: "译文：translated text")?.lowerBound)
        #expect(sourcePosition < translationPosition)
        #expect(bilingualExport.text.components(separatedBy: "translated text").count == 2)
    }

    @Test func translatedOnlyMarkdownLabelsStaleTranslations() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let lineID = UUID()
        let line = SessionTranscriptLine(id: lineID, date: start.addingTimeInterval(5), speaker: .remote,
                                         text: "修正后的中文", translatedText: "old translation",
                                         translationInputRevision: "old-source")
        let correction = TranscriptCorrection(line: 0, lineID: lineID,
                                              baseRevision: SessionProjectionBuilder.transcriptRevision(id: lineID, text: line.text),
                                              original: "修正后的中文", corrected: "当前修正后的中文")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [], corrections: [correction])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(20), mode: .meeting)
        let projection = SessionProjectionBuilder.build(meta: meta, lines: [line], notes: notes,
                                                       options: .init(languageView: .translated))

        let text = SessionMarkdownExporter.export(meta: meta, projection: projection)

        #expect(text.contains("译文（待更新）：old translation"))
        #expect(!text.contains("当前修正后的中文"))
    }

    @Test func markdownAndSubtitlesRespectSelectedOriginalOrCorrectedProjection() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let lineID = UUID()
        let line = SessionTranscriptLine(id: lineID, date: start.addingTimeInterval(12), speaker: .remote,
                                         text: "王小明说周五提交。", translatedText: "Wang said Friday.",
                                         translationInputRevision: SessionProjectionBuilder.transcriptRevision(id: lineID, text: "王小明说周五提交。"),
                                         startedAt: start.addingTimeInterval(12), endedAt: start.addingTimeInterval(14))
        let correction = TranscriptCorrection(line: 0, lineID: lineID,
                                              baseRevision: SessionProjectionBuilder.transcriptRevision(id: lineID, text: line.text),
                                              original: "周五", corrected: "下周一")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "交付计划",
                                    sections: [], corrections: [correction])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(20), mode: .meeting)
        let corrected = SessionProjectionBuilder.build(meta: meta, lines: [line], notes: notes,
                                                       options: .init(transcriptVariant: .corrected, languageView: .source))
        let original = SessionProjectionBuilder.build(meta: meta, lines: [line], notes: notes,
                                                      options: .init(transcriptVariant: .original, languageView: .source))

        let correctedMarkdown = SessionMarkdownExporter.export(meta: meta, projection: corrected)
        let originalMarkdown = SessionMarkdownExporter.export(meta: meta, projection: original)
        let correctedVTT = SessionSubtitleExporter.export(meta: meta, projection: corrected, format: .vtt).text
        let originalVTT = SessionSubtitleExporter.export(meta: meta, projection: original, format: .vtt).text

        #expect(correctedMarkdown.contains("王小明说下周一提交。"))
        #expect(!correctedMarkdown.contains("王小明说周五提交。"))
        #expect(originalMarkdown.contains("王小明说周五提交。"))
        #expect(!originalMarkdown.contains("王小明说下周一提交。"))
        #expect(correctedVTT.contains("王小明说下周一提交。"))
        #expect(!correctedVTT.contains("王小明说周五提交。"))
        #expect(originalVTT.contains("王小明说周五提交。"))
        #expect(!originalVTT.contains("王小明说下周一提交。"))
    }

    @Test func markdownFixturePreservesChineseNamesLongURLsNestedListsAndTables() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let longURL = "https://example.invalid/releases/nextthought/2026/11/09/" + String(repeating: "review-and-approval-", count: 12) + "?owner=wang-xiaoming&reviewer=chen-xiaoyu&build=20261109"
        let summary = SessionSummary(title: "王小明与陈晓雨的交付复盘",
                                     markdown: """
                                     ## 决定与行动
                                     - 王小明负责预发布验证。
                                       - 陈晓雨复核长会记录。
                                         - 如果失败，先保留旧版并联系值班负责人。

                                     | 负责人 | 交付物 | 日期 |
                                     | --- | --- | --- |
                                     | 王小明 | 预发布验证清单 | 周五 |
                                     | 陈晓雨 | 双语会议摘要 | 下周一 |

                                     ## 参考链接
                                     \(longURL)
                                     """,
                                     questions: [], generatedAt: start, model: "fixture")
        let line = SessionTranscriptLine(date: start.addingTimeInterval(6), speaker: .remote,
                                         text: "王小明确认预发布验证从周五开始。")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        let projection = SessionProjectionBuilder.build(meta: meta, lines: [line], summary: summary)

        let markdown = SessionMarkdownExporter.export(meta: meta, projection: projection)

        #expect(markdown.contains("王小明与陈晓雨的交付复盘"))
        #expect(markdown.contains("王小明负责预发布验证。"))
        #expect(markdown.contains("陈晓雨复核长会记录。"))
        #expect(markdown.contains("如果失败，先保留旧版并联系值班负责人。"))
        #expect(markdown.contains("| 王小明 | 预发布验证清单 | 周五 |"))
        #expect(markdown.contains("| 陈晓雨 | 双语会议摘要 | 下周一 |"))
        #expect(markdown.contains(longURL))
        #expect(markdown.contains("王小明确认预发布验证从周五开始。"))
    }
}

@Suite struct NotesShapeTests {
    private func item(_ text: String, _ topic: String? = nil) -> NotesItem { NotesItem(text: text, topic: topic) }

    @Test func markdownIsSavedAsMd() {
        #expect(SessionExportFormat.markdown.fileExtension == "md")
        #expect(SessionExportFormat.docx.fileExtension == "docx")
        #expect(SessionExportFormat.srt.fileExtension == "srt")
    }

    @Test func pointsOfOneTopicSitTogether() {
        let tidy = NotesShape.tidy([item("a1", "上线顺序"), item("b1", "资金安排"), item("a2", "上线顺序："), item("b2", " 资金安排")],
                                   kind: .items, isGap: false)
        #expect(tidy.map(\.text) == ["a1", "a2", "b1", "b2"])
        let groups = NotesShape.groups(tidy)
        #expect(groups.map(\.topic) == ["上线顺序", "资金安排"])
        #expect(groups.map(\.items.count) == [2, 2])
    }

    @Test func aListWithoutRealTopicsStaysFlat() {
        // One topic for everything, or topics on some items only, is no grouping.
        #expect(NotesShape.tidy([item("a", "会议"), item("b", "会议")], kind: .items, isGap: false).allSatisfy { $0.topic == nil })
        #expect(NotesShape.tidy([item("a", "x"), item("b"), item("c", "y")], kind: .items, isGap: false).allSatisfy { $0.topic == nil })
    }

    @Test func openQuestionsDoNotBuryTheDecisions() {
        let many = (1...12).map { item("未决 \($0)") }
        #expect(NotesShape.tidy(many, kind: .items, isGap: true).count == NotesShape.maximumGaps)
        #expect(NotesShape.tidy((1...30).map { item("要点 \($0)") }, kind: .items, isGap: false).count == NotesShape.maximumPoints)
        // Action items and question replays are things that happened: all kept.
        #expect(NotesShape.tidy(many, kind: .actions, isGap: false).count == 12)
    }

    @Test func topicsShowAsLabelsInMarkdown() {
        let section = NotesSectionResult(id: "points", title: "要点", kind: .items,
                                         items: [NotesItem(text: "先上线 SaaS", time: "12:19", topic: "上线顺序"), NotesItem(text: "收入先投入研发", topic: "资金安排")])
        #expect(NotesMarkdown.lines(section, english: false) == ["**上线顺序**", "", "- 先上线 SaaS [12:19]", "", "**资金安排**", "", "- 收入先投入研发"])
        let flat = NotesSectionResult(id: "points", title: "要点", kind: .items, items: [NotesItem(text: "一条")])
        #expect(NotesMarkdown.lines(flat, english: false) == ["- 一条"])
    }

    @Test func thePromptAsksForShortGroupedNotes() {
        let prompt = NotesPrompt.organizeSystem(template: .builtIn(.general), english: false, meetingDate: Date(timeIntervalSince1970: 1_794_000_000), inPerson: false)
        #expect(prompt.contains("topic"))
        #expect(prompt.contains("最多 5 条"))
        #expect(!prompt.contains("详细"))
    }
}
