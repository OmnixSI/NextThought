import Foundation
import InterviewHackerCore
import Observation

/// The global glossary of names and terms (“术语和人名”), saved as one JSON file at
/// `Application Support/InterviewHacker/Glossary/glossary.json`. A series can add its own
/// entries (`Workspace.glossary`); `AppState.effectiveGlossary(for:)` combines both.
/// Edits are written shortly after they happen with atomic writes.
@MainActor
@Observable
final class GlossaryStore {
    static let shared = GlossaryStore()

    private(set) var glossary = Glossary()
    /// Bumped on every change; lets caches (search terms) notice that the table changed.
    private(set) var revision = 0

    @ObservationIgnored private var saveTask: Task<Void, Never>?

    static var fileURL: URL {
        AppPaths.root.appendingPathComponent("Glossary", isDirectory: true).appendingPathComponent("glossary.json")
    }

    private init() { load() }

    // MARK: Loading and saving

    func load() {
        guard !DemoMode.isEnabled, let data = try? Data(contentsOf: Self.fileURL) else { return }
        if let saved = try? JSONLines.decoder().decode(Glossary.self, from: data) {
            glossary = saved.normalized()
        } else {
            // Never delete what we cannot read: keep it aside for a newer version.
            try? FileManager.default.moveItem(at: Self.fileURL, to: Self.fileURL.appendingPathExtension("unreadable"))
        }
    }

    private func scheduleSave() {
        revision += 1
        saveTask?.cancel()
        saveTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 500_000_000)
            guard !Task.isCancelled else { return }
            self?.flush()
        }
    }

    /// Writes pending edits now.
    func flush() {
        saveTask?.cancel()
        saveTask = nil
        guard !DemoMode.isEnabled else { return }
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        guard let data = try? encoder.encode(glossary) else { return }
        try? FileManager.default.createDirectory(at: Self.fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try? data.write(to: Self.fileURL, options: .atomic)
    }

    // MARK: Editing

    /// Replaces the entry with the same id as typed (no merging while a name is being edited),
    /// or appends it.
    func update(_ entry: GlossaryEntry) {
        if let index = glossary.entries.firstIndex(where: { $0.id == entry.id }) {
            guard glossary.entries[index] != entry else { return }
            glossary.entries[index] = entry
        } else {
            glossary.entries.append(entry)
        }
        scheduleSave()
    }

    func remove(_ id: UUID) {
        glossary.entries.removeAll { $0.id == id }
        scheduleSave()
    }

    /// Replaces the whole table (used while editing from the glossary page).
    func replaceEntries(_ entries: [GlossaryEntry]) {
        guard glossary.entries != entries else { return }
        glossary.entries = entries
        scheduleSave()
    }

    /// Cleans the table: empty entries dropped, duplicates merged, variants tidied.
    func normalize() {
        let cleaned = glossary.normalized()
        guard cleaned != glossary else { return }
        glossary = cleaned
        scheduleSave()
    }

    /// Adds `incoming` to the table (backup import, pasted text, “加入术语表”). Nothing existing is
    /// removed. Returns how many entries and variants are new.
    @discardableResult
    func merge(_ incoming: Glossary) -> Int {
        let before = Self.size(glossary)
        let merged = glossary.merging(incoming)
        guard merged != glossary else { return 0 }
        glossary = merged
        scheduleSave()
        return max(0, Self.size(merged) - before)
    }

    /// Entries plus variants: the measure of “how much is in the table”.
    static func size(_ glossary: Glossary) -> Int {
        glossary.entries.reduce(0) { $0 + 1 + $1.variants.count }
    }
}
