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

    // MARK: Presets

    private var presetsURL: URL? {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first?
            .appendingPathComponent("InterviewHacker/presets.json")
    }

    func loadPresets() {
        guard let url = presetsURL, let data = try? Data(contentsOf: url),
              let values = try? JSONDecoder().decode([SessionPreset].self, from: data) else { return }
        presets = values
    }

    private func persistPresets() {
        guard let url = presetsURL else { return }
        do {
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try JSONEncoder().encode(presets).write(to: url, options: .atomic)
        } catch { errorMessage = "保存预设失败：\(error.localizedDescription)" }
    }

    /// Saves the current prompt/mode/background and, if chosen, the imported files.
    func savePreset(named rawName: String, includeDocuments: Bool) {
        let name = rawName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else { return }
        let preset = SessionPreset(id: presets.first(where: { $0.name == name })?.id ?? UUID(), name: name, mode: sessionMode, style: answerStyle,
                                   preferences: answerPreferences, profile: profile, documents: includeDocuments ? documents : [])
        presets.removeAll { $0.name == name }
        presets.insert(preset, at: 0)
        persistPresets()
    }

    func applyPreset(_ preset: SessionPreset) {
        sessionMode = preset.mode
        answerStyle = preset.style
        answerPreferences = preset.preferences
        setProfile(preset.profile)
        if !preset.documents.isEmpty {
            documents = preset.documents
            rebuildContextIndex()
        }
        updateSettings()
    }

    func deletePreset(_ id: UUID) {
        presets.removeAll { $0.id == id }
        persistPresets()
    }

    func applyTemplatePreferences() {
        answerPreferences = SessionPreset.templatePreferences(for: sessionMode)
        updateSettings()
    }

    /// Pre-session preparation: likely questions with short talking points from the files.
    func predictQuestions() {
        guard !documents.filter(\.enabled).isEmpty || !profile.isEmpty else {
            errorMessage = "请先添加资料（简历、JD、议程或课件）或填写背景说明。"; return
        }
        section = .assistant
        let topic = documents.filter(\.enabled).map(\.name).joined(separator: "、")
        ask("请根据我提供的资料（\(topic.isEmpty ? "背景说明" : topic)），预测本次\(sessionMode.title)中最可能被问到的 8 个问题，并为每个问题给出 1 句可直接说的回答要点；引用资料时标注来源页码。", screenshot: nil)
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
