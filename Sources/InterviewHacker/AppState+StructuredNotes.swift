import AppKit
import Foundation
import Observation
import SwiftUI
import InterviewHackerCore

/// The notes format of each meeting type ("纪要格式") and the sessions whose fishbone,
/// outline or viewpoint matrix is being written. The text notes are always written; a
/// format only adds one more view, at the cost of one more request to the notes model,
/// so Settings → 纪要 can turn it off (`enabled`) or pick the plain text format per type.
@MainActor
@Observable
final class NotesFormatPrefs {
    static let shared = NotesFormatPrefs()

    /// SessionMode raw value → NotesFormat raw value, only for the types the user changed.
    private(set) var byType: [String: String]
    /// Off: no extra model call, the notes stay text only.
    private(set) var enabled: Bool
    /// Sessions whose structured view is being written right now.
    var generating: Set<UUID> = []
    /// Sessions whose last attempt did not produce a view (the page says so quietly).
    var failed: Set<UUID> = []

    private init() {
        byType = UserDefaults.standard.dictionary(forKey: "notesFormatByType") as? [String: String] ?? [:]
        enabled = UserDefaults.standard.object(forKey: "notesStructuredEnabled") as? Bool ?? true
    }

    func format(for mode: SessionMode) -> NotesFormat {
        NotesFormat.resolve(for: mode, overrides: byType)
    }

    func setFormat(_ format: NotesFormat, for mode: SessionMode) {
        byType[mode.rawValue] = format.rawValue
        UserDefaults.standard.set(byType, forKey: "notesFormatByType")
    }

    func setEnabled(_ value: Bool) {
        enabled = value
        UserDefaults.standard.set(value, forKey: "notesStructuredEnabled")
    }
}

extension NotesFormat {
    /// Name for the interface (follows the app language; `title(english:)` follows the notes).
    var displayName: String {
        switch self {
        case .standard: String(localized: "文字纪要（原有）")
        case .fishbone: String(localized: "因果鱼骨图")
        case .outline: String(localized: "要点大纲")
        case .viewpoints: String(localized: "观点对照")
        }
    }
}

/// After the text notes: one more pass over the transcript that writes the meeting as a
/// fishbone, an outline or a viewpoint matrix. Every node carries a time; nodes without
/// transcript support are dropped (same check as the template notes). When the pass fails
/// the text notes stay as they are and the page only says "结构图未能生成".
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

    /// Called right after the text notes are saved: writes the format of the meeting type in the
    /// background (the meeting's earlier choice wins when it is written again).
    func scheduleStructuredNotes(detail: SessionDetail, notes: StructuredNotes, summary: SessionSummary,
                                 mode: SessionMode, english: Bool, engine: NotesEngine) {
        let prefs = NotesFormatPrefs.shared
        let previous = detail.summary?.structured
        let format = previous?.format ?? prefs.format(for: mode)
        guard prefs.enabled, format.isStructured || previous != nil else { return }
        Task {
            await runStructuredNotes(detail: detail, notes: notes, baseSummary: summary, format: format,
                                     english: english, engine: engine)
        }
    }

    /// "换一种格式重新整理": rewrites only the structured view; the text notes stay.
    func regenerateStructuredNotes(for id: UUID, format: NotesFormat) {
        let prefs = NotesFormatPrefs.shared
        guard !prefs.generating.contains(id), !archive.summarizing.contains(id), id != archive.activeID else { return }
        let engine = notesEngine
        guard format == .standard || !engine.model.isEmpty else { return }
        Task {
            await archive.flush()
            guard let detail = await archive.detail(id), !detail.lines.isEmpty else { return }
            let english = detail.notes.map { $0.language == "en" } ?? notesUseEnglish
            let base = detail.summary ?? SessionSummary(title: detail.meta.displayTitle, markdown: "", questions: [],
                                                        generatedAt: Date(), model: engine.model)
            await runStructuredNotes(detail: detail, notes: detail.notes, baseSummary: base, format: format,
                                     english: english, engine: engine)
        }
    }

    // MARK: Writing

    private func runStructuredNotes(detail: SessionDetail, notes: StructuredNotes?, baseSummary: SessionSummary,
                                    format: NotesFormat, english: Bool, engine: NotesEngine) async {
        let id = detail.meta.id
        let prefs = NotesFormatPrefs.shared
        guard !prefs.generating.contains(id) else { return }
        prefs.generating.insert(id)
        prefs.failed.remove(id)
        defer { prefs.generating.remove(id) }

        let payload: StructuredNotesPayload
        if format == .standard {
            payload = .plain(english: english)
        } else {
            do {
                payload = try await writeStructuredNotes(detail: detail, notes: notes, format: format, english: english, engine: engine,
                                                         fallbackTitle: baseSummary.title)
                Diagnostics.record("notes.structured", ["session_id": id.uuidString, "format": format.rawValue,
                                                        "dropped": payload.droppedCount])
            } catch is CancellationError {
                return
            } catch {
                Diagnostics.record("notes.structured_failed", ["session_id": id.uuidString, "format": format.rawValue])
                payload = .failure(format: format, english: english, model: engine.model)
            }
        }

        // The text notes may have been rewritten while this ran: write onto what is saved now,
        // and leave it to that run to start its own structured pass.
        guard !archive.summarizing.contains(id) else { return }
        await archive.flush()
        var summary = await archive.detail(id)?.summary ?? baseSummary
        if payload.failed == true, summary.structured?.hasContent == true {
            // A failed second try never replaces a view that works.
            prefs.failed.insert(id)
            return
        }
        summary.structured = payload
        archive.saveSummary(summary, for: id)
        archive.notesRevision += 1
    }

    /// Proofread transcript → one request (or parts, then a merge) → tolerant parse → check.
    private func writeStructuredNotes(detail: SessionDetail, notes: StructuredNotes?, format: NotesFormat, english: Bool,
                                      engine: NotesEngine, fallbackTitle: String) async throws -> StructuredNotesPayload {
        let start = detail.meta.startedAt
        let lines = NotesCorrections.apply(notes?.corrections ?? [], to: detail.lines)
        let material = SessionSummaryPrompt.Material(meta: detail.meta, lines: lines, answers: [], detected: [], context: detail.context)
        let header = SessionSummaryPrompt.header(material)
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
        return cleaned
    }
}
