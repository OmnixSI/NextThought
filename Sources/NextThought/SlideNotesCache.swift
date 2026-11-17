import Foundation
import NextThoughtCore

/// Talking points the answer engine wrote for a slide, kept between meetings.
///
/// A talk is rehearsed before it is given: the points written during the rehearsal are
/// there the moment the slide comes up in the real one, instead of six seconds later.
/// One small file per deck; an entry is used only while the slide's text is unchanged.
enum SlideNotesCache {
    private struct Entry: Codable {
        var fingerprint: String
        var notes: SlideNotes
    }

    private static var directory: URL { AppPaths.root.appendingPathComponent("SlideNotes", isDirectory: true) }
    private static func file(_ documentID: UUID) -> URL { directory.appendingPathComponent(documentID.uuidString + ".json") }

    static func fingerprint(_ page: SlidePage, mode: SessionMode) -> String {
        ContextFingerprint.hash("1\n" + mode.rawValue + "\n" + page.text)
    }

    static func notes(for page: SlidePage, fingerprint: String) -> SlideNotes? {
        guard let entry = load(page.documentID)[String(page.page)], entry.fingerprint == fingerprint, !entry.notes.points.isEmpty else { return nil }
        return entry.notes
    }

    static func save(_ notes: SlideNotes, for page: SlidePage, fingerprint: String) {
        var entries = load(page.documentID)
        entries[String(page.page)] = Entry(fingerprint: fingerprint, notes: notes)
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            try JSONEncoder().encode(entries).write(to: file(page.documentID), options: .atomic)
        } catch {
            Diagnostics.record("insight.slide_notes_cache_failed")
        }
    }

    private static func load(_ documentID: UUID) -> [String: Entry] {
        guard let data = try? Data(contentsOf: file(documentID)) else { return [:] }
        return (try? JSONDecoder().decode([String: Entry].self, from: data)) ?? [:]
    }
}
