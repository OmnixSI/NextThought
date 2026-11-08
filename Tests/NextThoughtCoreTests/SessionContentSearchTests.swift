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
        #expect(corrected?.kind == .correctedTranscript)
        #expect(corrected?.location == "01:21")
        #expect(translated?.kind == .translation)
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

    @Test func glossaryVariantsRemainSearchableAndMarkedAsMisheard() {
        let terms = Glossary(entries: [GlossaryEntry(canonical: "Kubernetes", variants: ["K8s"])]).search(for: "Kubernetes")
        let hit = SessionContentSearch.firstHit(in: [SessionSearchField(kind: .transcript, text: "准备把 K8s 放进集群")], terms: terms)
        #expect(hit?.matchedForm == "K8s")
        #expect(hit?.isMisheard == true)
        #expect(hit?.excerpt.contains("K8s") == true)
    }
}
