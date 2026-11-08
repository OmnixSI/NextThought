import Foundation
import Testing
@testable import NextThoughtCore

struct SessionContentSearchTests {
    @Test func searchesEveryMeetingIncludingThe201stAnd501st() {
        let typedTerm = "needle-only-in-transcript"
        let terms = Glossary().search(for: typedTerm)
        let documents = (1...501).map { number in
            SessionSearchDocument(
                sessionID: UUID(),
                fields: number == 201 || number == 501
                    ? [SessionSearchField(kind: .transcript, text: "会中原话：\(typedTerm)-meeting-\(number)", location: "12:34")]
                    : [SessionSearchField(kind: .transcript, text: "普通会议第 \(number) 场")])
        }

        let results = SessionContentSearch.search(documents, terms: terms)

        #expect(results.count == 2)
        #expect(results[0].hit.excerpt.contains("meeting-201"))
        #expect(results[0].hit.location == "12:34")
        #expect(results[1].hit.excerpt.contains("meeting-501"))
    }

    @Test func liveSessionUpdatesDoNotChangeTheArchiveSearchStamp() {
        let activeID = UUID()
        var active = SessionMeta(id: activeID, startedAt: Date(timeIntervalSince1970: 1_800_000_000), mode: .meeting)
        let before = SessionSearchArchiveStamp(sessions: [active], activeSessionID: activeID, searchableContentRevision: 0)

        active.transcriptCount += 1
        active.questionCount += 1
        active.answerCount += 1
        let whileRecording = SessionSearchArchiveStamp(sessions: [active], activeSessionID: activeID, searchableContentRevision: 0)
        let afterFinalizing = SessionSearchArchiveStamp(sessions: [active], activeSessionID: nil, searchableContentRevision: 0)

        #expect(whileRecording == before)
        #expect(afterFinalizing != whileRecording)
    }

    @Test func savedArchivedContentChangesTheArchiveSearchStamp() {
        let session = SessionMeta(startedAt: Date(timeIntervalSince1970: 1_800_000_000), endedAt: Date(timeIntervalSince1970: 1_800_000_100), mode: .meeting)
        let before = SessionSearchArchiveStamp(sessions: [session], activeSessionID: nil, searchableContentRevision: 0)
        var updated = session
        updated.summaryState = .ready
        let metadataChange = SessionSearchArchiveStamp(sessions: [updated], activeSessionID: nil, searchableContentRevision: 0)
        let savedContentChange = SessionSearchArchiveStamp(sessions: [session], activeSessionID: nil, searchableContentRevision: 1)

        #expect(metadataChange != before)
        #expect(savedContentChange != before)
    }

    @Test func finalizedSessionStaysOutOfSearchUntilFinalTranslationsDrain() {
        let sessionID = UUID()
        let active = SessionMeta(id: sessionID, startedAt: Date(timeIntervalSince1970: 1_800_000_000), mode: .meeting)
        let beforeStop = SessionSearchArchiveStamp(sessions: [active], activeSessionID: sessionID,
                                                   searchableContentRevision: 0)
        var finalized = active
        finalized.endedAt = Date(timeIntervalSince1970: 1_800_000_100)
        let whileTranslationsDrain = SessionSearchArchiveStamp(sessions: [finalized], activeSessionID: nil,
                                                               pendingTranslationSessionIDs: [sessionID],
                                                               searchableContentRevision: 0)
        let afterDrain = SessionSearchArchiveStamp(sessions: [finalized], activeSessionID: nil,
                                                   searchableContentRevision: 0)

        #expect(whileTranslationsDrain == beforeStop)
        #expect(afterDrain != whileTranslationsDrain)
    }

    @Test func searchesOriginalCorrectedAndTranslatedTranscriptAsDistinctSources() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(81), speaker: .remote, text: "缓存鸡穿", translatedText: "cache penetration")
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "校对",
                                    sections: [], corrections: [TranscriptCorrection(line: 0, original: "缓存鸡穿", corrected: "缓存击穿")])
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(100), mode: .meeting)

        let fields = SessionContentSearch.fields(meta: meta, lines: [line], questions: [], answers: [], summary: nil, notes: notes)

        let original = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "缓存鸡穿"))
        let corrected = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "缓存击穿"))
        let translated = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "cache penetration"))
        #expect(original?.kind == .transcript)
        #expect(original?.id == "line:\(line.id.uuidString):original")
        #expect(original?.lineID == line.id)
        #expect(original?.sourceRevision == SessionProjectionBuilder.transcriptRevision(id: line.id, text: line.text))
        #expect(corrected?.kind == .correctedTranscript)
        #expect(corrected?.id == "line:\(line.id.uuidString):corrected")
        #expect(corrected?.lineID == line.id)
        #expect(corrected?.location == "01:21")
        #expect(translated?.kind == .translation)
        #expect(translated?.id == "line:\(line.id.uuidString):translation")
        #expect(translated?.lineID == line.id)
        #expect(translated?.isStale == true)
    }

    @Test func alternateTextHitUsesTheOriginalSourceIdentity() {
        let lineID = UUID()
        let field = SessionSearchField(kind: .correctedTranscript, text: "王小明负责周一提交。",
                                       alternateText: "王小明负责周五提交。", id: "line:corrected",
                                       lineID: lineID, sourceRevision: "corrected-revision",
                                       alternateID: "line:original", alternateKind: .transcript,
                                       alternateLineID: lineID, alternateSourceRevision: "original-revision")

        let hit = SessionContentSearch.firstHit(in: [field], terms: Glossary().search(for: "周五提交"))

        #expect(hit?.kind == .transcript)
        #expect(hit?.id == "line:original")
        #expect(hit?.lineID == lineID)
        #expect(hit?.sourceRevision == "original-revision")
        #expect(hit?.excerpt.contains("周五提交") == true)
    }

    @Test func returnsEveryMatchingSourceFieldForPerMeetingNavigation() {
        let terms = Glossary().search(for: "发布计划")
        let first = SessionSearchField(kind: .transcript, text: "发布计划先等数据", id: "line-1")
        let second = SessionSearchField(kind: .notes, text: "发布计划改到周一", id: "note-1")

        let hits = SessionContentSearch.hits(in: [first, second], terms: terms)

        #expect(hits.count == 2)
        #expect(hits.map(\.id) == ["line-1", "note-1"])
        #expect(hits.map(\.kind) == [.transcript, .notes])
    }

    @Test func includesQuestionsAnswersAndStructuredNotes() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let meta = SessionMeta(startedAt: start, mode: .meeting)
        let question = SessionQuestionEvent(date: start.addingTimeInterval(30), text: "什么叫一致性", intent: "why", score: 1, disposition: "manual")
        let answer = SessionAnswerEvent(date: start.addingTimeInterval(40), question: "怎么处理", answer: "逐条检查蓝鲸缓存", model: "test", automatic: true)
        let notes = StructuredNotes(templateID: "general", templateName: "通用", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "actions", title: "待办", kind: .actions,
                                                                  items: [NotesItem(text: "联系海鸥团队", time: "02:15", owner: "小王")])])
        let structured = StructuredNotesPayload(format: .outline,
                                                outline: OutlineNotes(sections: [OutlineSection(title: "风险", at: 345,
                                                                                               points: [OutlinePoint(text: "蜂鸟回滚路径", at: 350)])]))
        let summary = SessionSummary(title: "周会", markdown: "已确认缓存策略", questions: [], generatedAt: start, model: "test",
                                     structured: structured, structuredAll: [structured])
        let fields = SessionContentSearch.fields(meta: meta, lines: [], questions: [question], answers: [answer], summary: summary, notes: notes)

        #expect(SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "一致性"))?.kind == .question)
        #expect(SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "蓝鲸缓存"))?.kind == .answer)
        #expect(SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "海鸥团队"))?.location == "02:15")
        #expect(SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "缓存策略"))?.kind == .notes)
        #expect(SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "蜂鸟回滚"))?.location == "05:50")
    }

    @Test func timedNoteAndStructuredSearchHitsNavigateToNearbyTranscriptLine() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(32), speaker: .remote, text: "原话所在行")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(90), mode: .meeting)
        let notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "纪要",
                                    sections: [NotesSectionResult(id: "actions", title: "待办", kind: .actions,
                                                                  items: [NotesItem(text: "联系外部数据团队", time: "00:32")])])
        let payload = StructuredNotesPayload(format: .outline,
                                             outline: OutlineNotes(sections: [OutlineSection(title: "决定", at: 32,
                                                                                            points: [OutlinePoint(text: "周一提交结果", at: 32)])]))
        let summary = SessionSummary(title: "会议", markdown: "", questions: [], generatedAt: start, model: "test",
                                     structured: payload, structuredAll: [payload])
        let fields = SessionContentSearch.fields(meta: meta, lines: [line], questions: [], answers: [],
                                                 summary: summary, notes: notes)

        let noteHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "外部数据团队"))
        let structuredHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "周一提交结果"))

        #expect(noteHit?.kind == .notes)
        #expect(noteHit?.lineID == line.id)
        #expect(structuredHit?.kind == .notes)
        #expect(structuredHit?.lineID == line.id)
        #expect(structuredHit?.location == "00:32")
    }

    @Test func notesAndSummarySearchHitsPreserveStaleSourceStatus() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .remote, text: "当前会议内容")
        let notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "待核对纪要",
                                    sections: [NotesSectionResult(id: "actions", title: "待办", kind: .items,
                                                                  items: [NotesItem(text: "旧版本待办词")])],
                                    transcriptSourceRevision: "older-transcript-revision")
        let summary = SessionSummary(title: "旧版总结", markdown: "旧版本摘要词", questions: [], generatedAt: start,
                                     model: "test", transcriptSourceRevision: "older-transcript-revision")

        let fields = SessionContentSearch.fields(meta: meta, lines: [line], questions: [], answers: [],
                                                 summary: summary, notes: notes)
        let noteHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "旧版本待办词"))
        let summaryHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "旧版本摘要词"))

        #expect(noteHit?.kind == .notes)
        #expect(noteHit?.sourceStatus == .stale)
        #expect(noteHit?.isStale == true)
        #expect(summaryHit?.kind == .notes)
        #expect(summaryHit?.sourceStatus == .stale)
        #expect(summaryHit?.isStale == true)
    }

    @Test func editedNotesMarkTheirPreviouslyRenderedSummaryAsStaleInSearch() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let line = SessionTranscriptLine(date: start.addingTimeInterval(5), speaker: .remote, text: "当前会议内容")
        let meta = SessionMeta(startedAt: start, endedAt: start.addingTimeInterval(30), mode: .meeting)
        var notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "纪要",
                                   sections: [NotesSectionResult(id: "actions", title: "待办", kind: .actions,
                                                                 items: [NotesItem(text: "原始待办措辞")])])
        let transcriptRevision = SessionProjectionBuilder.transcriptSourceRevision(meta: meta, lines: [line], notes: notes)
        notes.transcriptSourceRevision = transcriptRevision
        let summary = SessionSummary(title: "纪要", markdown: "## 待办\n\n原始待办措辞", questions: [],
                                     generatedAt: start, model: "test", transcriptSourceRevision: transcriptRevision,
                                     structuredNotesSourceRevision: SessionProjectionBuilder.structuredNotesRevision(notes))
        notes.sections[0].items[0].text = "更新后的待办措辞"

        let fields = SessionContentSearch.fields(meta: meta, lines: [line], questions: [], answers: [],
                                                 summary: summary, notes: notes)
        let oldHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "原始待办措辞"))
        let currentHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "更新后的待办措辞"))

        #expect(oldHit?.kind == .notes)
        #expect(oldHit?.sourceStatus == .stale)
        #expect(oldHit?.isStale == true)
        #expect(currentHit?.kind == .notes)
        #expect(currentHit?.sourceStatus == .current)
        #expect(currentHit?.isStale == false)
    }

    @Test func legacyNoteAndSummarySearchHitsKeepUnknownFreshnessDistinctFromStale() {
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let meta = SessionMeta(startedAt: start, mode: .meeting)
        let notes = StructuredNotes(templateID: "general", templateName: "会议", language: "zh", title: "旧纪要",
                                    sections: [NotesSectionResult(id: "decisions", title: "决定", kind: .items,
                                                                  items: [NotesItem(text: "未标记版本的决定")])])
        let summary = SessionSummary(title: "旧摘要", markdown: "未标记版本的摘要", questions: [], generatedAt: start, model: "test")
        let fields = SessionContentSearch.fields(meta: meta, lines: [], questions: [], answers: [],
                                                 summary: summary, notes: notes)

        let noteHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "未标记版本的决定"))
        let summaryHit = SessionContentSearch.firstHit(in: fields, terms: Glossary().search(for: "未标记版本的摘要"))

        #expect(noteHit?.sourceStatus == .unknown)
        #expect(noteHit?.isStale == false)
        #expect(summaryHit?.sourceStatus == .unknown)
        #expect(summaryHit?.isStale == false)
    }

    @Test func glossaryVariantsRemainSearchableAndMarkedAsMisheard() {
        let terms = Glossary(entries: [GlossaryEntry(canonical: "Kubernetes", variants: ["K8s"])]).search(for: "Kubernetes")
        let hit = SessionContentSearch.firstHit(in: [SessionSearchField(kind: .transcript, text: "准备把 K8s 放进集群")], terms: terms)
        #expect(hit?.matchedForm == "K8s")
        #expect(hit?.isMisheard == true)
        #expect(hit?.excerpt.contains("K8s") == true)
    }
}
