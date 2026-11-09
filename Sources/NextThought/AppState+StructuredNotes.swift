import AppKit
import Foundation
import Observation
import SwiftUI
import NextThoughtCore

/// One structured view of one meeting (a meeting can have a fishbone, an outline and a
/// viewpoint matrix at the same time).
struct StructuredJobKey: Hashable {
    let id: UUID
    let format: NotesFormat
}

/// The fishbone, outline or viewpoint matrix passes that are running or have just failed.
/// The notes page reads this to show progress, "取消" and "重试". Nothing here is saved: the
/// finished views live in `SessionSummary.structuredAll`.
/// (The old keys `notesFormatByType` and `notesStructuredEnabled` are no longer read: the
/// user picks the view on the notes page, nothing is written automatically.)
@MainActor
@Observable
final class StructuredNotesJobs {
    static let shared = StructuredNotesJobs()

    /// Views being written right now (the value tells a job from an older one of the same view).
    private(set) var running: [StructuredJobKey: UUID] = [:]
    /// Views whose last attempt did not produce anything (the page says so quietly).
    private(set) var failed: Set<StructuredJobKey> = []
    private(set) var completed: [StructuredJobKey: StructuredNotesPayload] = [:]
    @ObservationIgnored private var tasks: [StructuredJobKey: Task<Void, Never>] = [:]
    @ObservationIgnored private var committing = false

    private init() {}

    func isRunning(_ key: StructuredJobKey) -> Bool { running[key] != nil }

    /// Any view of this meeting is being written.
    func isRunning(_ id: UUID) -> Bool { running.keys.contains { $0.id == id } }

    func hasFailed(_ key: StructuredJobKey) -> Bool { failed.contains(key) }

    func start(_ key: StructuredJobKey) -> UUID {
        let token = UUID()
        running[key] = token
        failed.remove(key)
        return token
    }

    func attach(_ task: Task<Void, Never>, to key: StructuredJobKey, token: UUID) {
        guard running[key] == token else { task.cancel(); return }
        tasks[key] = task
    }

    func complete(_ payload: StructuredNotesPayload, for key: StructuredJobKey, token: UUID) {
        guard running[key] == token else { return }
        completed[key] = payload
    }

    /// Release the display cache only after the page has loaded the same persisted result.
    func acknowledge(_ summary: SessionSummary?, for id: UUID) {
        for key in completed.keys.filter({ $0.id == id }) {
            if summary?.payload(for: key.format) == completed[key] { completed[key] = nil }
        }
    }

    func markFailed(_ key: StructuredJobKey) { failed.insert(key) }

    /// The job is over (finished, failed or cancelled); a newer job of the same view is left alone.
    func finish(_ key: StructuredJobKey, token: UUID) {
        guard running[key] == token else { return }
        running[key] = nil
        tasks[key] = nil
    }

    /// "取消": stops the request and clears the progress right away.
    func cancel(_ key: StructuredJobKey) {
        tasks[key]?.cancel()
        tasks[key] = nil
        running[key] = nil
    }

    func cancelAll() {
        for key in Array(tasks.keys) { cancel(key) }
    }

    /// Runs `body` alone: two views finishing together must not write the summary over each other.
    func exclusive(_ body: () async -> Void) async {
        while committing {
            if Task.isCancelled { return }
            try? await Task.sleep(for: .milliseconds(40))
        }
        committing = true
        await body()
        committing = false
    }
}

extension NotesFormat {
    /// The short name on the format bar (follows the app language; `title(english:)` follows the notes).
    var barTitle: String {
        switch self {
        case .standard: String(localized: "纪要")
        case .outline: String(localized: "提纲")
        case .fishbone: String(localized: "鱼骨图")
        case .viewpoints: String(localized: "观点对照")
        }
    }

    /// "正在整理成鱼骨图…" while the view is being written.
    var progressTitle: String {
        switch self {
        case .standard: ""
        case .outline: String(localized: "正在整理成提纲…")
        case .fishbone: String(localized: "正在整理成鱼骨图…")
        case .viewpoints: String(localized: "正在整理成观点对照…")
        }
    }

    /// "复制鱼骨图" on the page's copy button while that view is shown.
    var copyTitle: String {
        switch self {
        case .standard: String(localized: "复制纪要")
        case .outline: String(localized: "复制提纲")
        case .fishbone: String(localized: "复制鱼骨图")
        case .viewpoints: String(localized: "复制观点对照")
        }
    }
}

/// On demand, never by itself: one pass over the transcript that writes the meeting as a
/// fishbone, an outline or a viewpoint matrix, when the user picks that view on the notes
/// page. Every node carries a time; nodes without transcript support are dropped (same check
/// as the template notes). Each finished view is saved beside the others and beside the
/// text notes, which are never touched. When the pass fails the page says so quietly and
/// offers 重试.
@MainActor
extension AppState {
    // MARK: Small pieces other features add to

    /// The transcript the structured pass reads (speaker labels are added where the lines are joined).
    func structuredTranscript(lines: [SessionTranscriptLine], start: Date, english: Bool) -> String {
        NotesPrompt.transcript(lines, start: start, english: english)
    }

    /// The instructions of the structured pass (a glossary of terms would be appended here).
    func structuredSystemPrompt(format: NotesFormat, english: Bool) -> String {
        StructuredNotesPrompt.system(format: format, english: english)
    }

    // MARK: Entry points

    /// Writes one view of a meeting in the background (the page shows progress and 取消). One
    /// view of a meeting is written at most once at a time; a view that is already saved is
    /// written again only when the user asks for 重新生成.
    func generateStructuredNotes(for id: UUID, format: NotesFormat, regenerate: Bool = false) {
        guard format.isStructured else { return }
        let jobs = StructuredNotesJobs.shared
        let key = StructuredJobKey(id: id, format: format)
        guard !jobs.isRunning(key), !archive.summarizing.contains(id), id != archive.activeID else { return }
        let engine = notesEngine
        guard !engine.model.isEmpty else {
            jobs.markFailed(key)
            return
        }
        let token = jobs.start(key)
        let task = Task {
            await runStructuredNotes(key, token: token, engine: engine, regenerate: regenerate)
        }
        jobs.attach(task, to: key, token: token)
    }

    /// "取消" on the progress line.
    func cancelStructuredNotes(for id: UUID, format: NotesFormat) {
        StructuredNotesJobs.shared.cancel(StructuredJobKey(id: id, format: format))
    }

    // MARK: Writing

    private func runStructuredNotes(_ key: StructuredJobKey, token: UUID, engine: NotesEngine, regenerate: Bool) async {
        let jobs = StructuredNotesJobs.shared
        defer { jobs.finish(key, token: token) }
        let id = key.id
        guard await archive.flush(sessionID: id) else {
            if !Task.isCancelled { jobs.markFailed(key) }
            return
        }
        guard !Task.isCancelled, !isPreparingForTermination else { return }
        guard let detail = await archive.detail(id), !detail.lines.isEmpty, let base = detail.summary else {
            if !Task.isCancelled { jobs.markFailed(key) }
            return
        }
        // A stale page must not start another paid request after the previous result was saved.
        if !regenerate, let existing = base.payload(for: key.format) {
            jobs.complete(existing, for: key, token: token)
            return
        }
        let english = detail.effectiveNotes.map { $0.language == "en" } ?? notesUseEnglish

        var payload: StructuredNotesPayload
        do {
            payload = try await writeStructuredNotes(detail: detail, notes: detail.effectiveNotes, format: key.format, english: english,
                                                     engine: engine, fallbackTitle: base.title)
            payload.sourceRevision = detail.userContent.revision
            Diagnostics.record("notes.structured", ["session_id": id.uuidString, "format": key.format.rawValue,
                                                    "dropped": payload.droppedCount])
        } catch {
            // Cancelled by the user (the page already cleared its progress): nothing to report.
            if Task.isCancelled || error is CancellationError { return }
            Diagnostics.record("notes.structured_failed", ["session_id": id.uuidString, "format": key.format.rawValue])
            jobs.markFailed(key)
            return
        }
        if Task.isCancelled { return }

        // The text notes may be rewritten while this ran: write onto what is saved after that.
        while archive.summarizing.contains(id) {
            try? await Task.sleep(for: .milliseconds(500))
            if Task.isCancelled { return }
        }
        var saved = false
        await jobs.exclusive {
            guard await archive.flush(sessionID: id), !Task.isCancelled, !isPreparingForTermination else { return }
            guard var summary = await archive.detail(id)?.summary else { return }
            guard !Task.isCancelled, jobs.running[key] == token else { return }
            summary.store(payload)
            do { try await archive.saveDerivedSummary(summary, for: id) }
            catch { jobs.markFailed(key); return }
            guard await archive.flush(sessionID: id), !Task.isCancelled, !isPreparingForTermination else { return }
            jobs.complete(payload, for: key, token: token)
            archive.notesRevision += 1
            saved = true
        }
        if !saved && !Task.isCancelled { jobs.markFailed(key) }
    }

    /// Proofread transcript → one request (or parts, then a merge) → tolerant parse → check.
    private func writeStructuredNotes(detail: SessionDetail, notes: StructuredNotes?, format: NotesFormat, english: Bool,
                                      engine: NotesEngine, fallbackTitle: String) async throws -> StructuredNotesPayload {
        let start = detail.meta.startedAt
        let lines = detail.effectiveLines
        let material = SessionSummaryPrompt.Material(meta: detail.meta, lines: lines, answers: [], detected: [], context: detail.context)
        var header = SessionSummaryPrompt.header(material)
        if let edited = detail.userContent.editedNotes {
            header += "\n用户修订的纪要（作为整理重点，原话与时间引用仍以转写为准）：\n" + NotesMarkdown.render(edited, title: detail.meta.displayTitle, facts: "").prefix(8_000)
        } else if let edited = detail.userContent.editedMarkdown {
            header += "\n用户修订的纪要（原话与时间仍以转写为准）：\n" + edited.prefix(8_000)
        }
        let transcript = structuredTranscript(lines: lines, start: start, english: english)
        let chunks = SessionSummaryPrompt.chunks(transcript)
        let system = structuredSystemPrompt(format: format, english: english)
        let reply: String
        if chunks.count == 1 {
            reply = try await notesRequest(system: system, user: StructuredNotesPrompt.user(header: header, transcript: transcript), engine: engine)
        } else {
            var partials: [String] = []
            for (index, chunk) in chunks.enumerated() {
                partials.append(try await notesRequest(system: system, user: StructuredNotesPrompt.partialUser(header: header, chunk: chunk, index: index, count: chunks.count), engine: engine))
            }
            reply = try await notesRequest(system: system, user: StructuredNotesPrompt.mergeUser(header: header, partials: partials, format: format), engine: engine)
        }
        guard let parsed = StructuredNotesParser.parse(reply, format: format, english: english, fallbackTitle: fallbackTitle, model: engine.model) else {
            throw NotesError.unreadable
        }
        let offsets = lines.map { Int(max(0, $0.date.timeIntervalSince(start))) }
        guard var cleaned = StructuredNotesCleaner.clean(parsed, lineOffsets: offsets, duration: detail.meta.duration) else {
            throw NotesError.unreadable
        }
        cleaned.generatedAt = Date()
        cleaned.model = engine.model
        cleaned.transcriptSourceRevision = SessionProjectionBuilder.transcriptSourceRevision(
            meta: detail.meta, lines: detail.lines, notes: notes)
        return cleaned
    }
}
