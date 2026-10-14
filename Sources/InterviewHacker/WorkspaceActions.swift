import AppKit
import Foundation
import InterviewHackerCore

@MainActor
extension AppState {
    func importDocuments(_ urls: [URL]) {
        guard !isImportingDocuments else { return }
        guard documents.count + urls.count <= 12 else { errorMessage = "每个工作区最多 12 份资料，请先移除不需要的文件。"; return }
        isImportingDocuments = true
        importTask = Task {
            defer { isImportingDocuments = false; importTask = nil }
            do {
                let imported = try await DocumentImporter.read(urls)
                try Task.checkCancellation()
                guard (documents + imported).reduce(0, { $0 + $1.characterCount }) <= 600_000 else {
                    throw AppFailure.capture("工作区资料总量超过 60 万字，请只保留本次会话相关章节。")
                }
                var names = Set(documents.map(\.name))
                for var document in imported {
                    let original = document.name
                    var suffix = 2
                    while names.contains(document.name) { document.name = "\(original) (\(suffix))"; suffix += 1 }
                    names.insert(document.name)
                    documents.append(document)
                }
                rebuildContextIndex()
            } catch is CancellationError {} catch { errorMessage = "资料导入失败：\(error.localizedDescription)" }
        }
    }

    func rebuildContextIndex() {
        let revision = UUID(); documentRevision = revision
        let snapshot = documents
        // Never reuse a disabled/removed document while a replacement index builds.
        contextIndex = SessionContextIndex(); selectedExcerpts = []
        isIndexingDocuments = true
        Task {
            let index = await Task.detached(priority: .userInitiated) { SessionContextIndex(documents: snapshot) }.value
            guard documentRevision == revision else { return }
            contextIndex = index
            isIndexingDocuments = false
        }
    }

    func removeDocument(_ id: UUID) { documents.removeAll { $0.id == id }; rebuildContextIndex() }
    func setDocumentEnabled(_ id: UUID, enabled: Bool) {
        guard let index = documents.firstIndex(where: { $0.id == id }) else { return }
        documents[index].enabled = enabled; rebuildContextIndex()
    }
    func shortcutLabel(_ action: HotkeyAction) -> String {
        (customShortcuts[String(action.rawValue)] ?? action.defaultBinding).label
    }
    func setShortcut(_ action: HotkeyAction, binding: ShortcutBinding) {
        var next = customShortcuts
        next[String(action.rawValue)] = binding
        var effective = Dictionary(uniqueKeysWithValues: HotkeyAction.allCases.map { (String($0.rawValue), $0.defaultBinding) })
        effective.merge(next) { _, value in value }
        if let error = ShortcutBinding.validate(effective) { errorMessage = error; return }
        customShortcuts = next
        UserDefaults.standard.set(try? JSONEncoder().encode(next), forKey: "customShortcuts")
        registerHotkeys()
    }
    func resetShortcuts() {
        customShortcuts = [:]; UserDefaults.standard.removeObject(forKey: "customShortcuts"); registerHotkeys()
    }
}
