import AppKit
import Foundation
import InterviewHackerCore
import UniformTypeIdentifiers

/// Session recording and the after-session summary.
@MainActor
extension AppState {
    // MARK: Recording hooks

    func beginArchive(startedAt: Date) {
        guard recordSessions else { activeArchiveID = nil; return }
        let meta = SessionMeta(startedAt: startedAt, mode: sessionMode,
                               audioSource: captureTargets.first { $0.id == selectedTargetID }?.title ?? "",
                               recognizer: recognizerDescription,
                               answerModel: currentAnswerModel)
        let context = SessionContextSnapshot(mode: sessionMode, style: answerStyle, preferences: answerPreferences,
                                             profile: profile, documents: documents.filter(\.enabled))
        archive.begin(meta, context: context)
        activeArchiveID = meta.id
    }

    func finishArchive() {
        guard activeArchiveID != nil else { return }
        activeArchiveID = nil
        guard let meta = archive.finish() else { return }
        if autoSummarize { generateSummary(for: meta.id) }
    }

    func recordTranscript(_ entry: TranscriptEntry) {
        guard activeArchiveID != nil else { return }
        archive.append(SessionTranscriptLine(id: entry.id, date: entry.date, speaker: entry.speaker, text: entry.text,
                                             startedAt: entry.startedAt, endedAt: entry.endedAt))
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
                                          seconds: record.seconds, firstTokenSeconds: record.firstTokenSeconds, automatic: record.automatic))
    }

    private var recognizerDescription: String {
        switch transcriptionBackend {
        case "zipformer": localStreamingModel.title + (refinerModel != nil ? " + 整句精修" : "")
        case "cloud": "云端 · \(cloudProvider.title)"
        default: "Apple Speech"
        }
    }

    // MARK: Summary

    /// Writes the summary and question list with the configured answer engine. Runs in
    /// the background on its own client, so it never interrupts a live answer.
    func generateSummary(for id: UUID) {
        guard !archive.summarizing.contains(id), var meta = archive.sessions.first(where: { $0.id == id }) else { return }
        let model = currentAnswerModel
        guard !model.isEmpty else {
            meta.summaryState = .failed; meta.summaryError = "未选择回答模型，无法生成摘要。"
            archive.update(meta); return
        }
        archive.summarizing.insert(id)
        meta.summaryState = .pending; meta.summaryError = nil
        archive.update(meta)
        let backend = answerBackend
        let reasoning = codexReasoning
        Task {
            defer { archive.summarizing.remove(id) }
            do {
                await archive.flush()
                guard let detail = await archive.detail(id), !detail.lines.isEmpty else { throw SummaryError.emptyTranscript }
                let material = SessionSummaryPrompt.Material(meta: detail.meta, lines: detail.lines, answers: detail.answers,
                                                             detected: detail.questions, context: detail.context)
                let chunks = SessionSummaryPrompt.chunks(SessionSummaryPrompt.transcriptText(detail.lines, start: detail.meta.startedAt))
                let markdown: String
                if chunks.count == 1 {
                    markdown = try await summarize(user: SessionSummaryPrompt.full(material), backend: backend, model: model, reasoning: reasoning)
                } else {
                    var partials: [String] = []
                    for (index, chunk) in chunks.enumerated() {
                        partials.append(try await summarize(user: SessionSummaryPrompt.partial(chunk, index: index, count: chunks.count, material: material),
                                                            backend: backend, model: model, reasoning: reasoning))
                    }
                    markdown = try await summarize(user: SessionSummaryPrompt.merge(partials, material: material), backend: backend, model: model, reasoning: reasoning)
                }
                let parsed = SessionSummaryParser.parse(markdown)
                let summary = SessionSummary(title: parsed.title ?? detail.meta.displayTitle, markdown: markdown,
                                             questions: parsed.questions, generatedAt: Date(), model: model)
                archive.saveSummary(summary, for: id)
                Diagnostics.record("summary.completed", ["questions": parsed.questions.count, "chunks": chunks.count])
            } catch {
                if var failed = archive.sessions.first(where: { $0.id == id }) {
                    failed.summaryState = .failed
                    failed.summaryError = error.localizedDescription
                    archive.update(failed)
                }
                Diagnostics.record("summary.failed")
            }
        }
    }

    private func summarize(user: String, backend: AnswerBackend, model: String, reasoning: String) async throws -> String {
        let request = AnswerRequest(system: SessionSummaryPrompt.system, user: user, model: model, effort: reasoning, imageURL: nil)
        switch backend {
        case .openAICompatible:
            return try await summaryAPIClient.answer(request, baseURL: apiBaseURL, apiKey: apiKey) { _ in }
        case .codexExec, .codexAppServer:
            // A separate one-shot process: the persistent service stays free for live answers.
            return try await summaryCodex.run(prompt: request.fullPrompt, model: model, reasoning: reasoning, timeout: 600) { _ in }
        }
    }

    // MARK: History actions

    func exportSession(_ id: UUID) {
        Task {
            await archive.flush()
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
    var errorDescription: String? { "这次会话没有转录内容，无法生成摘要。" }
}
