import Foundation
import Observation

/// Asks before a regeneration (another template or language) when the user has edited the notes.
///
/// The notes page shows the user's edited version, so a regenerated AI draft would appear to
/// change nothing. Everything that regenerates calls `request`; the page of that meeting shows
/// the confirmation (`MeetingPage`).
@MainActor
@Observable
final class NotesRegeneration {
    static let shared = NotesRegeneration()

    struct Request: Equatable {
        var sessionID: UUID
        var templateID: String?
        var english: Bool?
    }

    /// Waiting for the user's answer.
    var pending: Request?

    private init() {}

    /// `hasEdits`: pass what the caller already knows; nil loads the meeting to find out.
    func request(sessionID: UUID, templateID: String? = nil, english: Bool? = nil, hasEdits: Bool? = nil) {
        let request = Request(sessionID: sessionID, templateID: templateID, english: english)
        Task { @MainActor in
            let edited: Bool
            if let hasEdits { edited = hasEdits } else {
                let detail = await SessionArchive.shared.detail(sessionID)
                edited = detail.map { $0.userContent.editedNotes != nil || $0.userContent.editedSummary != nil || $0.userContent.editedMarkdown != nil } ?? false
            }
            if edited { pending = request } else { run(request) }
        }
    }

    func confirm() {
        guard let request = pending else { return }
        pending = nil
        run(request)
    }

    func cancel() { pending = nil }

    private func run(_ request: Request) {
        AppState.shared.generateSummary(for: request.sessionID, templateID: request.templateID, english: request.english)
    }
}
