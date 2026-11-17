import Foundation
import NextThoughtCore

/// Which table the glossary page edits: the one for every meeting, or the active series' own.
enum GlossaryScope: Hashable {
    case global, series
}

/// The glossary of names and terms (“术语和人名”) wired into recognition, answers and notes.
/// Original transcripts are never rewritten: the glossary only helps the recognizer (hotwords),
/// steers the model's wording, and widens searches to the wrong spellings.
@MainActor
extension AppState {
    /// The global table plus the series' own table, and after them the terms found in the
    /// series' materials: hotwords and spellings without anyone typing them in.
    func effectiveGlossary(for workspace: Workspace? = nil) -> Glossary {
        let workspace = workspace ?? activeWorkspace
        return Self.adding(materialTerms(of: workspace), to: Glossary.layered(global: GlossaryStore.shared.glossary, series: workspace.glossary))
    }

    /// Terms of a series' enabled materials, worked out once per set of materials.
    func materialTerms(of workspace: Workspace) -> [String] {
        let documents = workspace.documents.filter(\.enabled)
        guard !documents.isEmpty else { return [] }
        let signature = documents.map { $0.id.uuidString + String($0.characterCount) }.joined(separator: "|")
        if let cached = MaterialTermsCache.entries[workspace.id], cached.signature == signature { return cached.terms }
        let terms = MaterialTerms.extract(from: documents.flatMap(\.pages))
        MaterialTermsCache.entries[workspace.id] = (signature, terms)
        return terms
    }

    private static func adding(_ terms: [String], to glossary: Glossary) -> Glossary {
        guard !terms.isEmpty else { return glossary }
        let known = Set(glossary.entries.map { Glossary.strictKey($0.canonical) })
        var result = glossary
        result.entries += terms.filter { !known.contains(Glossary.strictKey($0)) }.map { GlossaryEntry(canonical: $0) }
        return result
    }

    /// Hotwords for the local recognizer: the user's “专业词” followed by the glossary's canonical spellings.
    var recognitionHotwords: String {
        effectiveGlossary().hotwordString(merging: recognitionVocabulary)
    }

    /// The “术语与人名对照” block for a prompt; empty when the glossary is empty.
    func glossaryPromptSection(for workspace: Workspace? = nil, english: Bool = false, maxCharacters: Int = 1_500) -> String {
        effectiveGlossary(for: workspace).promptSection(maxCharacters: maxCharacters, english: english)
    }

    // MARK: Notes

    /// The table for a finished meeting: the global one plus the meeting's own series (never the
    /// series that happens to be active now).
    func glossaryForNotes(workspace: Workspace?) -> Glossary {
        let table = Glossary.layered(global: GlossaryStore.shared.glossary, series: workspace?.glossary)
        return workspace.map { Self.adding(materialTerms(of: $0), to: table) } ?? table
    }

    /// Appended to the notes prompts: the glossary and how to use it. Empty without a glossary.
    /// The proofreading step may correct misheard spellings (an overlay on the transcript, which can
    /// be undone); the organizing step writes the canonical spellings but copies `quote` verbatim.
    func glossaryNotesBlock(workspace: Workspace?, english: Bool, proofreading: Bool = false) -> String {
        let section = glossaryForNotes(workspace: workspace).promptSection(maxCharacters: 2_000, english: english)
        guard !section.isEmpty else { return "" }
        let rule: String
        switch (english, proofreading) {
        case (false, true): rule = "转写里出现“听错的写法”时，纠正为规范写法（from 必须仍是该行原文里一字不差的片段）。"
        case (false, false): rule = "纪要的标题、各条的文字、回答要点和“更好的说法”一律写规范写法；quote 字段照抄转写原话，不要改。"
        case (true, true): rule = "When the transcript has a misheard spelling, correct it to the canonical one (\"from\" must still be an exact fragment of that line)."
        case (true, false): rule = "In the title, items, answers and \"better\" use the canonical spelling; copy \"quote\" from the transcript exactly."
        }
        return "\n\n" + section + "\n" + rule
    }

    // MARK: Editing from the glossary page

    func glossaryEntries(in scope: GlossaryScope) -> [GlossaryEntry] {
        switch scope {
        case .global: return GlossaryStore.shared.glossary.entries
        case .series: return activeWorkspace.glossary?.entries ?? []
        }
    }

    func setGlossaryEntries(_ entries: [GlossaryEntry], in scope: GlossaryScope) {
        switch scope {
        case .global:
            GlossaryStore.shared.replaceEntries(entries)
        case .series:
            var workspace = activeWorkspace
            workspace.glossary = entries.isEmpty ? nil : Glossary(entries: entries)
            activeWorkspace = workspace
        }
    }

    /// Cleans both tables (duplicates merged, variants tidied) and writes them out.
    func tidyGlossaries() {
        GlossaryStore.shared.normalize()
        GlossaryStore.shared.flush()
        if let series = activeWorkspace.glossary {
            let cleaned = series.normalized()
            if cleaned != series {
                var workspace = activeWorkspace
                workspace.glossary = cleaned.isEmpty ? nil : cleaned
                activeWorkspace = workspace
            }
        }
    }

    /// Adds one name or term (merged with an existing entry of the same spelling).
    /// Returns false when there is nothing to add.
    @discardableResult
    func addToGlossary(canonical: String, variants: [String], kind: GlossaryKind, in scope: GlossaryScope = .global) -> Bool {
        let entry = GlossaryEntry(canonical: canonical, kind: kind, variants: variants)
        let incoming = Glossary(entries: [entry]).normalized()
        guard !incoming.isEmpty else { return false }
        switch scope {
        case .global:
            GlossaryStore.shared.merge(incoming)
        case .series:
            var workspace = activeWorkspace
            workspace.glossary = (workspace.glossary ?? Glossary()).merging(incoming)
            activeWorkspace = workspace
        }
        return true
    }

    // MARK: Text import and export

    /// Pasted lines (`规范写法 | 变体1, 变体2 | 类型 | 备注`) added to a table. Returns how many entries
    /// and variants are new.
    func importGlossaryText(_ text: String, defaultKind: GlossaryKind, in scope: GlossaryScope) -> Int {
        let incoming = Glossary(entries: Glossary.parse(lines: text, defaultKind: defaultKind)).normalized()
        guard !incoming.isEmpty else { return 0 }
        switch scope {
        case .global:
            return GlossaryStore.shared.merge(incoming)
        case .series:
            let current = activeWorkspace.glossary ?? Glossary()
            let merged = current.merging(incoming)
            guard merged != current else { return 0 }
            var workspace = activeWorkspace
            workspace.glossary = merged
            activeWorkspace = workspace
            return max(0, GlossaryStore.size(merged) - GlossaryStore.size(current))
        }
    }

    func exportGlossaryText(in scope: GlossaryScope) -> String {
        Glossary(entries: glossaryEntries(in: scope)).exportText()
    }
}

// MARK: - Search

@MainActor
extension AppState {
    /// Every table there is: the global one and all series' own. Searches across meetings use it,
    /// because a meeting's series is not necessarily the active one.
    func searchGlossary(workspaceID: UUID? = nil, includeAllWorkspaces: Bool = true) -> Glossary {
        var entries = GlossaryStore.shared.glossary.entries
        if let workspaceID {
            entries += workspaces.workspaces.first(where: { $0.id == workspaceID })?.glossary?.entries ?? []
        } else if includeAllWorkspaces {
            for workspace in workspaces.workspaces { entries += workspace.glossary?.entries ?? [] }
        }
        return Glossary(entries: entries).normalized()
    }

    /// Changes whenever any table may have changed (cache key for search terms).
    var glossaryRevision: String {
        let stamps = workspaces.workspaces.reduce(0.0) { $0 + $1.updatedAt.timeIntervalSince1970 }
        return "\(GlossaryStore.shared.revision)|\(workspaces.workspaces.count)|\(stamps)"
    }
}

/// Remembers the expansion of the last search text, so the many recomputations of one render
/// (and of one search over hundreds of meetings) share it.
final class GlossarySearchCache {
    private var stamp = ""
    private var values: [String: GlossarySearch] = [:]

    @MainActor
    func terms(for query: String, state: AppState, workspaceID: UUID? = nil,
               includeAllWorkspaces: Bool = true) -> GlossarySearch {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        let key = trimmed + "\u{1}" + state.glossaryRevision
        if key != stamp {
            stamp = key
            values = [:]
        }
        let scope = workspaceID?.uuidString ?? (includeAllWorkspaces ? "all" : "none")
        if let value = values[scope] { return value }
        let result = state.searchGlossary(workspaceID: workspaceID, includeAllWorkspaces: includeAllWorkspaces).search(for: trimmed)
        values[scope] = result
        return result
    }
}

extension GlossarySearch {
    /// The existing locale-aware match for what was typed, then every other spelling.
    func appMatch(in text: String) -> GlossarySearchHit? {
        guard !query.isEmpty else { return nil }
        if text.localizedCaseInsensitiveContains(query) { return GlossarySearchHit(form: query, isMisheard: false) }
        return firstMatch(in: text)
    }
}

/// Material terms per series, keyed by which materials they came from.
@MainActor
enum MaterialTermsCache {
    static var entries: [UUID: (signature: String, terms: [String])] = [:]
}
