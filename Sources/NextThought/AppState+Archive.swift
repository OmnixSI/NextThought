import AppKit
import Foundation
import NextThoughtCore
import UniformTypeIdentifiers

/// Session recording and the after-session summary.
@MainActor
extension AppState {
    // MARK: Recording hooks

    func beginArchive(startedAt: Date) {
        guard recordSessions else { activeArchiveID = nil; return }
        let workspace = activeWorkspace
        var meta = SessionMeta(startedAt: startedAt, mode: workspace.mode,
                               title: plannedMeeting?.title.flatMap { $0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : $0 },
                               audioSource: isOfflineMeeting ? String(localized: "线下 · 麦克风") : audioSourceTitle(for: selectedTargetID),
                               recognizer: recognizerDescription,
                               answerModel: currentAnswerModel,
                               workspaceID: workspace.id, workspaceName: workspace.name)
        if workspace.mode == .custom { meta.typeName = workspace.typeDisplayName }
        if isOfflineMeeting { meta.inPerson = true }
        let context = SessionContextSnapshot(mode: workspace.mode, style: workspace.style, preferences: workspace.preferences,
                                             profile: workspace.materialText, documents: workspace.documents.filter(\.enabled))
        archive.begin(meta, context: context)
        activeArchiveID = meta.id
    }

    /// `endedAt`: the end of the last words when the meeting ended by itself after silence.
    func finishArchive(endedAt: Date? = nil, summarize: Bool = true) {
        guard activeArchiveID != nil else { return }
        activeArchiveID = nil
        guard let meta = archive.finish(at: endedAt ?? Date()) else { return }
        DataBackup.afterSession()
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
        if let index = entries.firstIndex(where: { $0.id == entryID }) {
            entries[index].translatedText = translation
        }
        if let sessionID = job.sessionID {
            archive.appendTranslation(sessionID: sessionID, lineID: entryID, text: translation)
        }
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
            guard let detail = await archive.detail(id) else { return }
            let panel = NSSavePanel()
            panel.allowedContentTypes = [UTType(filenameExtension: "md") ?? .plainText]
            let name = (detail.summary?.title ?? detail.meta.displayTitle).replacingOccurrences(of: "/", with: "-")
            panel.nameFieldStringValue = "\(name).md"
            guard panel.runModal() == .OK, let url = panel.url else { return }
            let text = SessionMarkdownExporter.export(meta: detail.meta, summary: detail.summary, lines: detail.lines,
                                                      answers: detail.answers, detected: detail.questions)
            do { try text.write(to: url, atomically: true, encoding: .utf8) }
            catch { errorMessage = "导出失败：\(error.localizedDescription)" }
        }
    }

    func copySessionMarkdown(_ detail: SessionDetail) {
        let text = SessionMarkdownExporter.export(meta: detail.meta, summary: detail.summary, lines: detail.lines,
                                                  answers: detail.answers, detected: detail.questions)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
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
