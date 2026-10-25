import AppKit
import Foundation
import SwiftUI
import InterviewHackerCore

/// The three steps of writing notes, shown on the meeting page while they run.
enum NotesStep: Int, CaseIterable, Identifiable {
    case proofreading, organizing, checking
    var id: Int { rawValue }
    var title: LocalizedStringKey {
        switch self {
        case .proofreading: "校对转写"
        case .organizing: "按模板整理"
        case .checking: "核对"
        }
    }
}

enum NotesError: LocalizedError {
    case unreadable
    var errorDescription: String? { String(localized: "纪要模型没有按要求的格式回答。") }
}

/// The model the notes use: separate from live answers (stronger, slower, no Fast mode).
struct NotesEngine {
    var backend: AnswerBackend
    var model: String
    var reasoning: String
    /// Codex service tier; "default" keeps models that run Fast by default at standard speed.
    var serviceTier: String?
}

/// After the meeting (02-scenarios-and-flows.md, "会后复盘与纪要"): proofread the
/// transcript (no new recognition, no audio is kept), organize it with the meeting
/// type's template, citing a time for every item, then check: drop items with no
/// support, turn relative dates into dates and keep "待确认" marks.
@MainActor
extension AppState {
    // MARK: Settings

    /// Reasoning levels from strongest to weakest.
    static let notesEffortOrder = ["max", "ultra", "xhigh", "high", "medium", "low", "minimal", "none"]

    /// Notes follow the interface language unless set otherwise in Settings → Notes.
    var notesUseEnglish: Bool {
        notesLanguage == "en" || (notesLanguage == "auto" && AppLanguage.interfaceIsEnglish)
    }

    /// The configured notes model, or the automatic choice: the first model Codex lists
    /// that reasons at "high" or above (Codex lists its flagship first), else the
    /// answer model.
    var notesEngine: NotesEngine {
        if answerBackend == .openAICompatible {
            let model = notesModelID.isEmpty ? apiModel : notesModelID
            return NotesEngine(backend: .openAICompatible, model: model.trimmingCharacters(in: .whitespacesAndNewlines),
                               reasoning: apiReasoningEffort, serviceTier: nil)
        }
        let automatic = codexModels.first { $0.efforts.contains { ["high", "xhigh", "max", "ultra"].contains($0) } }?.id ?? codexModelID
        let model = notesModelID.isEmpty ? automatic : notesModelID
        let option = codexModels.first { $0.id == model }
        let efforts = option?.efforts ?? ["low", "medium", "high"]
        let reasoning = efforts.contains(notesReasoning) ? notesReasoning
            : (Self.notesEffortOrder.first { efforts.contains($0) } ?? notesReasoning)
        return NotesEngine(backend: answerBackend, model: model, reasoning: reasoning,
                           serviceTier: option?.defaultsToFast == true ? "default" : nil)
    }

    /// Built-in templates in the notes language, then the user's own.
    func allNotesTemplates(english: Bool? = nil) -> [NotesTemplate] {
        let english = english ?? notesUseEnglish
        return NotesTemplateKind.allCases.map { NotesTemplate.builtIn($0, english: english) } + customNotesTemplates
    }

    func notesTemplate(id: String, english: Bool? = nil) -> NotesTemplate {
        let english = english ?? notesUseEnglish
        if let kind = NotesTemplateKind(rawValue: id) { return NotesTemplate.builtIn(kind, english: english) }
        return customNotesTemplates.first { $0.id == id } ?? NotesTemplate.builtIn(.general, english: english)
    }

    /// The template a meeting type uses unless changed in Settings → Notes.
    func defaultNotesTemplateID(for mode: SessionMode) -> String {
        let id = notesTemplateByType[mode.rawValue] ?? MeetingTypeDefaults.for(mode).template.rawValue
        if NotesTemplateKind(rawValue: id) != nil || customNotesTemplates.contains(where: { $0.id == id }) { return id }
        return MeetingTypeDefaults.for(mode).template.rawValue
    }

    func notesTemplate(for workspace: Workspace) -> NotesTemplate {
        notesTemplate(id: defaultNotesTemplateID(for: workspace.effectiveMode))
    }

    func setDefaultNotesTemplate(_ id: String, for mode: SessionMode) {
        notesTemplateByType[mode.rawValue] = id
        saveNotesSettings()
    }

    func saveNotesSettings() {
        let defaults = UserDefaults.standard
        defaults.set(notesModelID, forKey: "notesModelID")
        defaults.set(notesReasoning, forKey: "notesReasoning")
        defaults.set(notesLanguage, forKey: "notesLanguage")
        defaults.set(notesTemplateByType, forKey: "notesTemplateByType")
        defaults.set(autoSummarize, forKey: "autoSummarize")
        if let data = try? JSONEncoder().encode(customNotesTemplates) {
            defaults.set(data, forKey: "customNotesTemplates")
        }
    }

    nonisolated static func loadCustomNotesTemplates() -> [NotesTemplate] {
        guard let data = UserDefaults.standard.data(forKey: "customNotesTemplates"),
              let templates = try? JSONDecoder().decode([NotesTemplate].self, from: data) else { return [] }
        return templates
    }

    /// A copy of a template that the user can rename and edit.
    @discardableResult
    func duplicateNotesTemplate(_ template: NotesTemplate) -> NotesTemplate {
        let copy = template.customCopy(named: String(localized: "\(template.name) 副本"))
        customNotesTemplates.append(copy)
        saveNotesSettings()
        return copy
    }

    func updateNotesTemplate(_ template: NotesTemplate) {
        guard let index = customNotesTemplates.firstIndex(where: { $0.id == template.id }) else { return }
        customNotesTemplates[index] = template
        saveNotesSettings()
    }

    func deleteNotesTemplate(_ id: String) {
        customNotesTemplates.removeAll { $0.id == id }
        for (mode, templateID) in notesTemplateByType where templateID == id { notesTemplateByType.removeValue(forKey: mode) }
        saveNotesSettings()
    }

    // MARK: Writing the notes

    /// Writes the notes of a session in the background (never interrupts a live
    /// answer). `templateID` / `english` re-run it with another template or language.
    func generateSummary(for id: UUID, templateID: String? = nil, english: Bool? = nil) {
        guard !archive.summarizing.contains(id), var meta = archive.sessions.first(where: { $0.id == id }) else { return }
        let engine = notesEngine
        guard !engine.model.isEmpty else {
            meta.summaryState = .failed
            meta.summaryError = String(localized: "没有可用的纪要模型。请在 设置 → 纪要 里选择。")
            archive.update(meta)
            return
        }
        archive.summarizing.insert(id)
        meta.summaryState = .pending
        meta.summaryError = nil
        archive.update(meta)
        archive.notesSteps[id] = .proofreading
        let useEnglish = english ?? notesUseEnglish
        Task {
            defer {
                archive.summarizing.remove(id)
                archive.notesSteps[id] = nil
            }
            var loaded: SessionDetail?
            do {
                await archive.flush()
                guard let detail = await archive.detail(id), !detail.lines.isEmpty else { throw SummaryError.emptyTranscript }
                loaded = detail
                let workspace = detail.meta.workspaceID.flatMap { workspaces.workspace($0) }
                let mode = workspace?.effectiveMode ?? detail.meta.mode
                let template = notesTemplate(id: templateID ?? detail.notes?.templateID ?? defaultNotesTemplateID(for: mode), english: useEnglish)
                var notes = try await writeNotes(detail: detail, workspace: workspace, template: template, english: useEnglish, engine: engine)
                notes.model = engine.model
                notes.generatedAt = Date()
                archive.saveNotes(notes, for: id)
                let summary = notesSummary(from: notes, template: template, detail: detail)
                archive.saveSummary(summary, for: id)
                if var latest = archive.sessions.first(where: { $0.id == id }) {
                    latest.actionItemCount = notes.actionItemCount
                    latest.improveCount = notes.improveCount
                    archive.update(latest)
                }
                // The series keeps what the next meeting needs (question bank, lessons).
                if autoApplyLessons { applyReview(summary, sessionID: id, workspaceID: detail.meta.workspaceID) }
                Diagnostics.record("notes.completed", ["template": template.id, "items": notes.sections.reduce(0) { $0 + $1.items.count },
                                                       "corrections": notes.corrections.count, "dropped": notes.droppedCount])
            } catch NotesError.unreadable {
                // The model did not answer in JSON: keep the session useful with the
                // former Markdown summary.
                Diagnostics.record("notes.unreadable")
                do {
                    guard let detail = loaded else { throw NotesError.unreadable }
                    let summary = try await markdownSummary(detail: detail, engine: engine)
                    archive.saveSummary(summary, for: id)
                    if autoApplyLessons { applyReview(summary, sessionID: id, workspaceID: detail.meta.workspaceID) }
                } catch {
                    markSummaryFailed(id, error)
                }
            } catch {
                markSummaryFailed(id, error)
            }
        }
    }

    private func markSummaryFailed(_ id: UUID, _ error: Error) {
        if var failed = archive.sessions.first(where: { $0.id == id }) {
            failed.summaryState = .failed
            failed.summaryError = error.localizedDescription
            archive.update(failed)
        }
        Diagnostics.record("summary.failed")
    }

    /// Proofread → organize → check.
    private func writeNotes(detail: SessionDetail, workspace: Workspace?, template: NotesTemplate, english: Bool,
                            engine: NotesEngine) async throws -> StructuredNotes {
        let start = detail.meta.startedAt
        // 1. Proofread. A failure here only means no corrections.
        archive.notesSteps[detail.meta.id] = .proofreading
        var corrections: [TranscriptCorrection] = []
        do {
            let numbered = NotesPrompt.transcript(detail.lines, start: start, numbered: true, english: english)
            let vocabulary = properNouns(detail: detail, workspace: workspace)
            for chunk in SessionSummaryPrompt.chunks(numbered) {
                let reply = try await notesRequest(system: NotesPrompt.proofreadSystem(english: english),
                                                   user: NotesPrompt.proofreadUser(transcript: chunk, vocabulary: vocabulary), engine: engine)
                corrections += NotesJSON.corrections(reply)
            }
            corrections = NotesCorrections.valid(corrections, lines: detail.lines)
        } catch is CancellationError {
            throw CancellationError()
        } catch {
            Diagnostics.record("notes.proofread_failed")
        }
        let lines = NotesCorrections.apply(corrections, to: detail.lines)

        // 2. Organize with the template; long meetings in parts, then merged.
        archive.notesSteps[detail.meta.id] = .organizing
        let material = SessionSummaryPrompt.Material(meta: detail.meta, lines: lines, answers: detail.answers,
                                                     detected: detail.questions, context: detail.context)
        let header = SessionSummaryPrompt.header(material)
        let transcript = NotesPrompt.transcript(lines, start: start, english: english)
        let chunks = SessionSummaryPrompt.chunks(transcript)
        let system = NotesPrompt.organizeSystem(template: template, english: english, meetingDate: start,
                                                inPerson: detail.meta.inPerson == true)
        let answers = detail.answers.suffix(40).map { "Q：\($0.question.prefix(300))\nA：\($0.answer.prefix(600))" }.joined(separator: "\n\n")
        let reply: String
        if chunks.count == 1 {
            reply = try await notesRequest(system: system, user: NotesPrompt.organizeUser(header: header, transcript: transcript, answers: answers), engine: engine)
        } else {
            var partials: [String] = []
            for (index, chunk) in chunks.enumerated() {
                partials.append(try await notesRequest(system: system, user: NotesPrompt.partialUser(header: header, chunk: chunk, index: index, count: chunks.count), engine: engine))
            }
            reply = try await notesRequest(system: system, user: NotesPrompt.mergeUser(header: header, partials: partials), engine: engine)
        }
        guard var notes = NotesJSON.parse(reply, template: template, language: english ? "en" : "zh") else { throw NotesError.unreadable }

        // 3. Check: support in the transcript, concrete dates, "待确认".
        archive.notesSteps[detail.meta.id] = .checking
        let offsets = lines.map { Int(max(0, $0.date.timeIntervalSince(start))) }
        NotesVerifier.verify(&notes, lineOffsets: offsets, duration: detail.meta.duration, meetingDate: start)
        notes.corrections = corrections
        return notes
    }

    /// Names, companies and terms the proofreading step may rely on.
    private func properNouns(detail: SessionDetail, workspace: Workspace?) -> [String] {
        var words = recognitionVocabulary.split(whereSeparator: { ",，、;；\n".contains($0) })
            .map { $0.trimmingCharacters(in: .whitespaces) }
        if let workspace {
            words.append(workspace.name)
            words += workspace.fields.values.filter { $0.count <= 40 }
            words += workspace.documents.map { ($0.name as NSString).deletingPathExtension }
        }
        if let context = detail.context { words += context.documents.map { ($0.name as NSString).deletingPathExtension } }
        var seen = Set<String>()
        return words.filter { !$0.isEmpty && seen.insert($0).inserted }
    }

    /// One request to the notes model: a separate one-shot process (Codex) or the
    /// long-timeout API client, so live answers are never blocked.
    func notesRequest(system: String, user: String, engine: NotesEngine) async throws -> String {
        let request = AnswerRequest(system: system, user: user, model: engine.model, effort: engine.reasoning, imageURL: nil, serviceTier: engine.serviceTier)
        switch engine.backend {
        case .openAICompatible:
            return try await summaryAPIClient.answer(request, baseURL: apiBaseURL, apiKey: apiKey, reasoningEffort: apiEffortParameter) { _ in }
        case .codexExec, .codexAppServer:
            return try await summaryCodex.run(prompt: request.fullPrompt, model: engine.model, reasoning: engine.reasoning,
                                              serviceTier: engine.serviceTier, timeout: 600) { _ in }
        }
    }

    /// The SessionSummary every other part reads (export, search, stage review,
    /// question bank), written from the structured notes.
    private func notesSummary(from notes: StructuredNotes, template: NotesTemplate, detail: SessionDetail) -> SessionSummary {
        let given = detail.meta.title?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let title = !given.isEmpty ? given : (notes.title.isEmpty ? detail.meta.displayTitle : notes.title)
        let questions = notes.sections.filter { $0.kind == .questions }.flatMap(\.items)
            .map { SummaryQuestion(text: $0.text, answerGist: $0.answer) }
        return SessionSummary(title: title, markdown: NotesMarkdown.render(notes, title: title, facts: notesFacts(detail.meta, notes: notes)),
                              questions: questions, generatedAt: Date(), model: notes.model,
                              lessons: notes.lessons(template: template), bank: notes.bankEntries, appliedToWorkspace: false)
    }

    /// "10 月 27 日（周一）10:00 · 38 分钟 · 线上 · 已校对 4 处识别错误".
    func notesFacts(_ meta: SessionMeta, notes: StructuredNotes?) -> String {
        let english = notes?.language == "en" || (notes == nil && AppLanguage.interfaceIsEnglish)
        var parts = [NotesDates.format(meta.startedAt, english: english) + " " + meta.startedAt.formatted(date: .omitted, time: .shortened)]
        let minutes = Int(meta.duration / 60)
        if minutes > 0 { parts.append(english ? "\(minutes) min" : "\(minutes) 分钟") }
        parts.append(meta.inPerson == true ? (english ? "In person" : "线下") : (english ? "Online" : "线上"))
        let fixed = notes?.activeCorrections.count ?? 0
        if fixed > 0 { parts.append(english ? "\(fixed) recognition errors fixed" : "已校对 \(fixed) 处识别错误") }
        return parts.joined(separator: " · ")
    }

    /// The former Markdown summary (used when the notes model does not return JSON).
    private func markdownSummary(detail: SessionDetail, engine: NotesEngine) async throws -> SessionSummary {
        let material = SessionSummaryPrompt.Material(meta: detail.meta, lines: detail.lines, answers: detail.answers,
                                                     detected: detail.questions, context: detail.context)
        let chunks = SessionSummaryPrompt.chunks(SessionSummaryPrompt.transcriptText(detail.lines, start: detail.meta.startedAt))
        let system = SessionSummaryPrompt.system(for: detail.meta.mode)
        let markdown: String
        if chunks.count == 1 {
            markdown = try await notesRequest(system: system, user: SessionSummaryPrompt.full(material), engine: engine)
        } else {
            var partials: [String] = []
            for (index, chunk) in chunks.enumerated() {
                partials.append(try await notesRequest(system: system, user: SessionSummaryPrompt.partial(chunk, index: index, count: chunks.count, material: material), engine: engine))
            }
            markdown = try await notesRequest(system: system, user: SessionSummaryPrompt.merge(partials, material: material), engine: engine)
        }
        let parsed = SessionSummaryParser.parseAll(markdown)
        return SessionSummary(title: parsed.title ?? detail.meta.displayTitle, markdown: markdown,
                              questions: parsed.questions, generatedAt: Date(), model: engine.model,
                              lessons: parsed.lessons, bank: parsed.bank, appliedToWorkspace: false)
    }

    // MARK: On the notes page

    /// Undo or redo one proofreading correction (the transcript file never changes).
    func toggleCorrection(_ correctionID: UUID, in sessionID: UUID) {
        archive.updateNotes(sessionID) { notes in
            guard let index = notes.corrections.firstIndex(where: { $0.id == correctionID }) else { return }
            notes.corrections[index].reverted.toggle()
        }
    }

    /// "复制纪要": Markdown of the notes (or of the older summary).
    func copyNotes(_ detail: SessionDetail) {
        let text: String
        if let notes = detail.notes {
            let title = detail.summary?.title ?? detail.meta.displayTitle
            text = NotesMarkdown.render(notes, title: title, facts: notesFacts(detail.meta, notes: notes))
        } else {
            text = SessionMarkdownExporter.export(meta: detail.meta, summary: detail.summary, lines: detail.lines,
                                                  answers: detail.answers, detected: detail.questions)
        }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }
}
