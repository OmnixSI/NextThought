import AppKit
import Foundation
import NextThoughtCore
import UniformTypeIdentifiers

/// Session recording and the after-session summary.
@MainActor
extension AppState {
    // MARK: Recording hooks

    func sessionContextSnapshot(for workspace: Workspace) -> SessionContextSnapshot {
        SessionContextSnapshot(mode: workspace.mode, style: workspace.style, preferences: workspace.preferences,
                               profile: workspace.materialText, documents: workspace.documents.filter(\.enabled))
    }

    @discardableResult
    func updateArchiveContext(_ workspace: Workspace, sessionID: UUID) -> Bool {
        archive.updateContext(sessionContextSnapshot(for: workspace), for: sessionID)
    }

    func beginArchive(startedAt: Date) {
        guard recordSessions else { activeArchiveID = nil; return }
        // The start flow prepares the workspace after applying this meeting's
        // carry choices. Persist that same snapshot so the archive records the
        // context the meeting actually used.
        let workspace = preparedWorkspace ?? activeWorkspace
        var meta = SessionMeta(startedAt: startedAt, mode: workspace.mode,
                               title: plannedMeeting?.title.flatMap { $0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : $0 },
                               audioSource: isOfflineMeeting ? String(localized: "线下 · 麦克风") : audioSourceTitle(for: selectedTargetID),
                               recognizer: recognizerDescription,
                               answerModel: currentAnswerModel,
                               workspaceID: workspace.id, workspaceName: workspace.name)
        if workspace.mode == .custom { meta.typeName = workspace.typeDisplayName }
        if isOfflineMeeting { meta.inPerson = true }
        archive.begin(meta, context: sessionContextSnapshot(for: workspace))
        activeArchiveID = meta.id
    }

    /// `endedAt`: the end of the last words when the meeting ended by itself after silence.
    func finishArchive(endedAt: Date? = nil, summarize: Bool = true) {
        guard activeArchiveID != nil else { return }
        activeArchiveID = nil
        guard let meta = archive.finish(at: endedAt ?? Date()) else { return }
        DataBackup.afterSession(sessionID: meta.id)
        // 结束就是纪要: the same meeting, now as its notes page.
        historyWorkspaceFilter = nil
        historySelection = meta.id
        section = .meetings
        if autoSummarize && summarize { generateSummary(for: meta.id) }
    }

    func recordTranscript(_ entry: TranscriptEntry) {
        guard activeArchiveID != nil else { return }
        archive.append(SessionTranscriptLine(id: entry.id, date: entry.date, speaker: entry.speaker, text: entry.text,
                                             translatedText: entry.translatedText, startedAt: entry.startedAt, endedAt: entry.endedAt))
    }

    func receiveLiveTranslation(_ text: String, job: LiveTranslationJob) {
        guard job.isFinal, let entryID = job.entryID else { return }
        let translation = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !translation.isEmpty else { return }
        let inputRevision = entries.first(where: { $0.id == entryID })
            .map { SessionProjectionBuilder.transcriptRevision(id: $0.id, text: $0.text) }
        if let index = entries.firstIndex(where: { $0.id == entryID }) {
            entries[index].translatedText = translation
        }
        if let sessionID = job.sessionID {
            archive.appendTranslation(sessionID: sessionID, lineID: entryID, text: translation, inputRevision: inputRevision)
        }
    }

    /// Opens an archived meeting from a series source reference.
    func openArchivedMeeting(_ sessionID: UUID) {
        guard archive.sessions.contains(where: { $0.id == sessionID }) else { return }
        historyWorkspaceFilter = nil
        historySelection = sessionID
        section = .meetings
    }

    func recordQuestion(_ candidate: QuestionCandidate, disposition: String) {
        recordQuestion(text: candidate.text, leadIn: candidate.leadIn, intent: candidate.assessment.intent.rawValue,
                       score: candidate.assessment.score, disposition: disposition)
    }

    func recordQuestion(text: String, leadIn: String?, intent: String, score: Double, disposition: String) {
        if !questionTexts.contains(text) {
            questionTexts.append(text)
            if questionTexts.count > 60 { questionTexts.removeFirst(questionTexts.count - 60) }
        }
        guard activeArchiveID != nil else { return }
        archive.append(SessionQuestionEvent(date: Date(), text: text, leadIn: leadIn, intent: intent, score: score, disposition: disposition))
    }

    func recordAnswer(_ record: AnswerRecord) {
        guard activeArchiveID != nil else { return }
        archive.append(SessionAnswerEvent(id: record.id, date: record.date, question: record.question, answer: record.answer, model: record.model,
                                          seconds: record.seconds, firstTokenSeconds: record.firstTokenSeconds, automatic: record.automatic,
                                          screenshotPath: record.screenshotPath, webAnswer: record.webAnswer, webSources: record.webSources, evidence: record.evidence))
    }

    private var recognizerDescription: String {
        switch transcriptionBackend {
        case "zipformer": localStreamingModel.title + (refinerModel != nil ? " + 整句精修" : "")
        case "cloud": "云端 · \(cloudProvider.title)"
        default: "Apple Speech"
        }
    }

    // MARK: Summary
    // The notes pipeline (generateSummary) lives in AppState+Notes.swift.

    // MARK: History actions

    func exportSession(_ id: UUID) {
        Task {
            guard await archive.flush(sessionID: id) else {
                let reason = archive.persistenceBySession[id]?.lastFailure
                errorMessage = "导出失败：会议内容尚未全部保存。" + (reason.map { "\n" + $0 } ?? "请重试保存后再导出。")
                return
            }
            guard let detail = await archive.detail(id) else {
                errorMessage = "无法读取这场会议，暂时不能导出。"
                return
            }
            sessionExportRequest = SessionExportRequest(detail: detail)
        }
    }

    func copySessionMarkdown(_ detail: SessionDetail) {
        let text = SessionMarkdownExporter.export(meta: detail.meta, summary: detail.summary, lines: detail.lines,
                                                  answers: detail.answers, detected: detail.questions, notes: detail.notes)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }

    /// Save a person's evidence decision only if the claim and transcript versions still
    /// match what the page displayed. A transcript edit invalidates the old review.
    func reviewSessionEvidence(sessionID: UUID, evidenceID: String, expectedClaimRevision: String,
                               expectedTranscriptRevision: String, expectedLineRevision: String,
                               decision: SessionEvidenceDecision?) {
        Task {
            await StructuredNotesJobs.shared.exclusive {
                guard await archive.flush(sessionID: sessionID),
                      let detail = await archive.detail(sessionID),
                      var summary = detail.summary,
                      let current = SessionEvidenceBuilder.build(meta: detail.meta, lines: detail.lines,
                                                                 notes: detail.notes, summary: detail.summary)
                        .first(where: { $0.id == evidenceID }),
                      current.claimRevision == expectedClaimRevision,
                      current.transcriptSourceRevision == expectedTranscriptRevision,
                      current.sourceRevision == expectedLineRevision,
                      current.lineID != nil,
                      current.sourceText != nil else {
                    errorMessage = "原话或纪要刚刚有变化，请刷新后重新核对。"
                    return
                }

                var reviews = summary.evidenceReviews ?? [:]
                if let decision {
                    reviews[evidenceID] = SessionEvidenceReview(
                        decision: decision,
                        claimRevision: expectedClaimRevision,
                        transcriptSourceRevision: expectedTranscriptRevision,
                        lineSourceRevision: expectedLineRevision)
                } else {
                    reviews[evidenceID] = nil
                }
                summary.evidenceReviews = reviews.isEmpty ? nil : reviews
                archive.saveSummary(summary, for: sessionID)
                guard await archive.flush(sessionID: sessionID) else {
                    errorMessage = "核对结果暂未保存；请重试保存后再离开此页。"
                    return
                }
                archive.notesRevision += 1
                Diagnostics.record("notes.evidence_reviewed", ["decision": decision?.rawValue ?? "cleared"])
            }
        }
    }
}

enum SummaryError: LocalizedError {
    case emptyTranscript
    case archiveNotSaved(String?)
    var errorDescription: String? {
        switch self {
        case .emptyTranscript:
            "这次会话没有转录内容，无法生成摘要。"
        case .archiveNotSaved(let reason):
            "会议归档尚未全部保存，暂时无法生成已保存的纪要。" + (reason.map { "\n" + $0 } ?? "")
        }
    }
}
