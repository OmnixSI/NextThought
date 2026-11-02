import Foundation
import InterviewHackerCore
import Observation

/// Scenario workspaces (“字节后端二面”, “ACL 口头报告”, “周一客户例会”), one JSON file
/// each under `Application Support/InterviewHacker/Workspaces/`. Edits are saved
/// shortly after they happen with atomic writes, so a crash never leaves a half file.
@MainActor
@Observable
final class WorkspaceStore {
    static let shared = WorkspaceStore()

    private(set) var workspaces: [Workspace] = []
    private(set) var activeID: UUID?

    @ObservationIgnored private var dirty: Set<UUID> = []
    @ObservationIgnored private var saveTask: Task<Void, Never>?
    private let io = DispatchQueue(label: "app.interviewhacker.workspaces", qos: .utility)

    private init() {}

    var active: Workspace {
        get { workspaces.first { $0.id == activeID } ?? workspaces.first ?? Workspace(name: "通用", mode: .general) }
        set { update(newValue) }
    }

    var visible: [Workspace] { workspaces.filter { !$0.archived }.sorted { $0.updatedAt > $1.updatedAt } }

    func workspace(_ id: UUID?) -> Workspace? { id.flatMap { id in workspaces.first { $0.id == id } } }

    func canDelete(_ id: UUID) -> Bool {
        guard !DemoMode.isEnabled, let workspace = workspace(id), workspaces.count > 1 else { return false }
        return workspace.archived || visible.count > 1
    }

    // MARK: Loading

    /// Loads all workspaces; on first run converts the old single context (mode,
    /// answer requirements, background) and saved presets into workspaces.
    func load() {
        let directory = AppPaths.workspaces
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let decoder = JSONLines.decoder()
        let files = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []
        var loaded: [Workspace] = []
        for file in files where file.pathExtension == "json" {
            guard let data = try? Data(contentsOf: file) else { continue }
            if let workspace = try? decoder.decode(Workspace.self, from: data) {
                loaded.append(workspace)
            } else {
                // Never delete what we cannot read: keep it aside for a newer version.
                try? FileManager.default.moveItem(at: file, to: file.appendingPathExtension("unreadable"))
            }
        }
        if loaded.isEmpty { loaded = migrateLegacyContext() }
        workspaces = loaded
        let saved = UserDefaults.standard.string(forKey: "activeWorkspaceID").flatMap(UUID.init(uuidString:))
        activeID = loaded.contains { $0.id == saved } ? saved : visible.first?.id
    }

    private func migrateLegacyContext() -> [Workspace] {
        let defaults = UserDefaults.standard
        var result: [Workspace] = []
        let mode = SessionMode(rawValue: defaults.string(forKey: "sessionMode") ?? "") ?? .general
        let preferences = defaults.string(forKey: "answerPreferences") ?? ""
        let profile = defaults.string(forKey: "profile") ?? ""
        let style = AnswerStyle(rawValue: defaults.string(forKey: "answerStyle") ?? "") ?? .concise
        var current = Workspace(name: "我的\(mode.title)", mode: mode, style: style,
                                preferences: preferences.isEmpty ? nil : preferences, notes: profile)
        current.updatedAt = Date()
        result.append(current)
        if let data = try? Data(contentsOf: AppPaths.legacyPresets),
           let presets = try? JSONDecoder().decode([SessionPreset].self, from: data) {
            for preset in presets {
                result.append(Workspace(name: preset.name, mode: preset.mode, style: preset.style,
                                        preferences: preset.preferences, notes: preset.profile,
                                        documents: preset.documents, updatedAt: preset.updatedAt))
            }
        }
        for workspace in result { write(workspace) }
        Diagnostics.record("workspaces.migrated", ["count": result.count])
        return result
    }

    /// Screenshot mode: in-memory workspaces only.
    func installDemo(_ items: [Workspace]) {
        workspaces = items
        activeID = items.first?.id
    }

    // MARK: Editing

    func activate(_ id: UUID) {
        guard workspaces.contains(where: { $0.id == id }) else { return }
        activeID = id
        UserDefaults.standard.set(id.uuidString, forKey: "activeWorkspaceID")
    }

    func update(_ workspace: Workspace) {
        guard let index = workspaces.firstIndex(where: { $0.id == workspace.id }) else { return }
        guard workspaces[index] != workspace else { return }
        var next = workspace
        next.updatedAt = Date()
        workspaces[index] = next
        scheduleSave(next.id)
    }

    func commit(_ workspace: Workspace) throws {
        guard let index = workspaces.firstIndex(where: { $0.id == workspace.id }) else {
            throw CocoaError(.fileNoSuchFile)
        }
        guard !DemoMode.isEnabled else { throw CocoaError(.fileWriteNoPermission) }

        var next = workspace
        next.updatedAt = Date()
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        let data = try encoder.encode(next)
        let url = fileURL(next.id)

        saveTask?.cancel()
        saveTask = nil
        for dirtyID in dirty {
            if let pending = self.workspace(dirtyID) { write(pending, synchronously: true) }
        }
        io.sync { }
        do {
            try io.sync {
                try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
                try data.write(to: url, options: .atomic)
            }
        } catch {
            if let dirtyID = dirty.first { scheduleSave(dirtyID) }
            throw error
        }
        dirty.removeAll()
        workspaces[index] = next
    }

    /// Changes the mode; answer requirements that were still the old template follow.
    func setMode(_ mode: SessionMode, for id: UUID) {
        guard var workspace = self.workspace(id), workspace.mode != mode else { return }
        if workspace.preferences.isEmpty || workspace.preferences == ModeProfile.for(workspace.mode).templatePreferences {
            workspace.preferences = ModeProfile.for(mode).templatePreferences
        }
        workspace.mode = mode
        update(workspace)
    }

    @discardableResult
    func create(mode: SessionMode, name: String? = nil) -> Workspace {
        let base = name ?? "新的\(mode.title)"
        var unique = base, counter = 2
        while workspaces.contains(where: { $0.name == unique }) { unique = "\(base) \(counter)"; counter += 1 }
        let workspace = Workspace(name: unique, mode: mode)
        workspaces.append(workspace)
        write(workspace)
        activate(workspace.id)
        return workspace
    }

    @discardableResult
    func duplicate(_ id: UUID) -> Workspace? {
        guard let source = workspace(id) else { return nil }
        var copy = source
        copy.id = UUID()
        copy.name = source.name + " 副本"
        copy.createdAt = Date(); copy.updatedAt = Date()
        copy.stageReview = nil; copy.stageReviewAt = nil
        workspaces.append(copy)
        write(copy)
        activate(copy.id)
        return copy
    }

    /// Deletion keeps a copy in the backups folder (`Backups/deleted-workspaces`).
    @discardableResult
    func delete(_ id: UUID) -> Bool {
        guard canDelete(id), let workspace = workspace(id) else { return false }
        flush()
        let file = fileURL(id)
        let trash = AppPaths.backups.appendingPathComponent("deleted-workspaces", isDirectory: true)
        let safeName = workspace.name.replacingOccurrences(of: "/", with: "-")
        let target = trash.appendingPathComponent("\(safeName)-\(Int(Date().timeIntervalSince1970))-\(UUID().uuidString.prefix(8)).json")
        let moved = io.sync { () -> Bool in
            do {
                try FileManager.default.createDirectory(at: trash, withIntermediateDirectories: true)
                try FileManager.default.moveItem(at: file, to: target)
                return true
            } catch {
                return false
            }
        }
        guard moved else {
            Diagnostics.record("workspace.delete_backup_failed")
            return false
        }
        workspaces.removeAll { $0.id == id }
        if activeID == id, let next = visible.first?.id ?? workspaces.first?.id { activate(next) }
        return true
    }

    /// Replaces the whole list (backup import); everything is written immediately.
    func replaceAll(_ next: [Workspace]) {
        workspaces = next
        for workspace in next { write(workspace) }
        if !next.contains(where: { $0.id == activeID }) { activeID = visible.first?.id }
    }

    /// Writes pending edits now (before export, backup, quit or update).
    func flush() {
        saveTask?.cancel(); saveTask = nil
        for id in dirty { if let workspace = workspace(id) { write(workspace, synchronously: true) } }
        dirty.removeAll()
        io.sync { }
    }

    // MARK: Files

    func fileURL(_ id: UUID) -> URL { AppPaths.workspaces.appendingPathComponent("\(id.uuidString).json") }

    private func scheduleSave(_ id: UUID) {
        dirty.insert(id)
        saveTask?.cancel()
        saveTask = Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(600))
            guard !Task.isCancelled, let self else { return }
            for id in self.dirty { if let workspace = self.workspace(id) { self.write(workspace) } }
            self.dirty.removeAll()
        }
    }

    private func write(_ workspace: Workspace, synchronously: Bool = false) {
        guard !DemoMode.isEnabled else { return }
        let url = fileURL(workspace.id)
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        guard let data = try? encoder.encode(workspace) else { return }
        let work: @Sendable () -> Void = {
            try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try? data.write(to: url, options: .atomic)
        }
        if synchronously { io.sync(execute: work) } else { io.async(execute: work) }
    }
}
