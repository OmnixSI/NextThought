import Foundation
import InterviewHackerCore

/// One-click export / import of settings, workspaces and session records, plus
/// event-triggered, deduplicated recovery points.
/// Archives are ZIP files with the `.ihbackup` extension; see `BackupManifest`.
enum DataBackup {
    struct ImportSummary {
        var workspacesAdded = 0
        var workspacesUpdated = 0
        var sessionsAdded = 0
        var settingsApplied = 0
        var secretsApplied = 0
        var skillsAdded = 0
        var description: String {
            var parts: [String] = []
            if skillsAdded > 0 { parts.append("技能 \(skillsAdded) 个") }
            if workspacesAdded + workspacesUpdated > 0 { parts.append("场景 新增 \(workspacesAdded)、更新 \(workspacesUpdated)") }
            if sessionsAdded > 0 { parts.append("会话记录 \(sessionsAdded) 条") }
            if settingsApplied > 0 { parts.append("设置 \(settingsApplied) 项") }
            if secretsApplied > 0 { parts.append("密钥 \(secretsApplied) 个") }
            return parts.isEmpty ? "没有需要导入的新内容（已有的数据都保持不变）。" : "已导入：" + parts.joined(separator: "，") + "。"
        }
    }

    struct BackupFile: Identifiable, Hashable {
        var url: URL
        var date: Date
        var size: Int
        var reason: String
        var id: URL { url }
    }

    static var appVersion: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev" }
    static var appBuild: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "0" }

    // MARK: Export

    /// Full export: settings, all workspaces, all sessions (and API keys if asked).
    static func exportAll(to destination: URL, includeSecrets: Bool, workspaces: [Workspace], skills: [Skill] = []) throws {
        let staging = try makeStaging()
        defer { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()) }
        try writeWorkspaces(workspaces, into: staging)
        try writeSkills(skills, into: staging)
        let sessionIDs = try copySessions(into: staging, only: nil)
        try writeSettings(into: staging)
        if includeSecrets { try writeSecrets(into: staging) }
        let manifest = BackupManifest(kind: .full, appVersion: appVersion, appBuild: appBuild, includesSecrets: includeSecrets,
                                      workspaceCount: workspaces.count, sessionCount: sessionIDs.count)
        try write(manifest, to: staging.appendingPathComponent("manifest.json"))
        try zip(staging, to: destination)
    }

    /// One workspace with its materials and knowledge, optionally with its sessions —
    /// for moving “this interview setup” to another Mac or sharing a template.
    static func exportWorkspace(_ workspace: Workspace, sessions: [UUID], to destination: URL, skills: [Skill] = []) throws {
        let staging = try makeStaging()
        defer { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()) }
        try writeWorkspaces([workspace], into: staging)
        // Its attached skills travel with it (global ones are the user's own setup).
        try writeSkills(skills.filter { workspace.skillIDs.contains($0.id) }, into: staging)
        let copied = try copySessions(into: staging, only: Set(sessions))
        let manifest = BackupManifest(kind: .workspace, appVersion: appVersion, appBuild: appBuild, includesSecrets: false,
                                      workspaceCount: 1, sessionCount: copied.count)
        try write(manifest, to: staging.appendingPathComponent("manifest.json"))
        try zip(staging, to: destination)
    }

    // MARK: Import

    /// Merges an archive into the current data. Nothing existing is deleted; newer
    /// local edits win over older copies in the archive. A backup of the current state
    /// is taken first.
    @MainActor
    static func importArchive(_ source: URL, applySettings: Bool) async throws -> ImportSummary {
        let staging = try makeStaging()
        defer { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()) }
        if source.pathExtension == "ihrestore" {
            try await recoveryStore.restore(source, into: staging)
        } else {
            try await Task.detached(priority: .utility) {
                try Shell.check("/usr/bin/ditto", ["-x", "-k", source.path, staging.path], "解压备份")
            }.value
        }
        // ditto keeps the top-level folder name of the archive.
        let root = try locateRoot(in: staging)
        let manifest = try JSONLines.decoder().decode(BackupManifest.self, from: Data(contentsOf: root.appendingPathComponent("manifest.json")))
        try manifest.validate()

        WorkspaceStore.shared.flush()
        _ = try await automaticBackup(reason: "import")

        var summary = ImportSummary()
        let incoming = await Task.detached(priority: .utility) { readWorkspaces(in: root) }.value
        let merged = BackupMerge.workspaces(existing: WorkspaceStore.shared.workspaces, incoming: incoming)
        WorkspaceStore.shared.replaceAll(merged.merged)
        summary.workspacesAdded = merged.added
        summary.workspacesUpdated = merged.updated
        if manifest.kind == .workspace, let first = incoming.first,
           let imported = merged.merged.first(where: { $0.id == first.id }) ?? merged.merged.last {
            WorkspaceStore.shared.activate(imported.id)
        }

        if let data = try? Data(contentsOf: root.appendingPathComponent("skills.json")),
           let skills = try? JSONLines.decoder().decode([Skill].self, from: data) {
            summary.skillsAdded = SkillLibrary.shared.merge(skills)
        }

        let sessionsDirectory = root.appendingPathComponent("Sessions", isDirectory: true)
        let incomingSessions = ((try? FileManager.default.contentsOfDirectory(at: sessionsDirectory, includingPropertiesForKeys: nil)) ?? [])
            .compactMap { UUID(uuidString: $0.lastPathComponent) }
        let existing = Set(((try? FileManager.default.contentsOfDirectory(at: AppPaths.sessions, includingPropertiesForKeys: nil)) ?? [])
            .compactMap { UUID(uuidString: $0.lastPathComponent) })
        try FileManager.default.createDirectory(at: AppPaths.sessions, withIntermediateDirectories: true)
        for id in BackupMerge.sessionsToImport(existing: existing, incoming: incomingSessions) {
            try FileManager.default.copyItem(at: sessionsDirectory.appendingPathComponent(id.uuidString),
                                             to: AppPaths.sessions.appendingPathComponent(id.uuidString))
            summary.sessionsAdded += 1
        }

        if applySettings {
            summary.settingsApplied = applySettingsFile(root.appendingPathComponent("settings.plist"))
            summary.secretsApplied = applySecretsFile(root.appendingPathComponent("secrets.plist"))
        }
        Diagnostics.record("backup.imported", ["workspaces": merged.added + merged.updated, "sessions": summary.sessionsAdded, "settings": summary.settingsApplied])
        return summary
    }

    // MARK: Automatic backups

    private static let recoveryStore = IncrementalBackupStore(root: AppPaths.backups)

    /// Capture mutable UI state first; all serialization, hashing, copying and
    /// recovery-point maintenance runs away from the main actor.
    @MainActor
    @discardableResult
    static func automaticBackup(reason: String) async throws -> URL {
        WorkspaceStore.shared.flush()
        await SessionArchive.shared.flush()
        let workspaces = WorkspaceStore.shared.workspaces, skills = SkillLibrary.shared.skills
        let settings = SettingsSnapshot.filter(UserDefaults.standard.dictionaryRepresentation())
        let settingsData = try PropertyListSerialization.data(fromPropertyList: settings, format: .xml, options: 0)
        let version = appVersion, build = appBuild
        let files = try await Task.detached(priority: .utility) { () -> [String: Data] in
            let encoder = JSONLines.encoder(); encoder.outputFormatting.insert(.sortedKeys)
            var files: [String: Data] = ["settings.plist": settingsData, "skills.json": try encoder.encode(skills)]
            for workspace in workspaces { files["Workspaces/\(workspace.id.uuidString).json"] = try encoder.encode(workspace) }
            let sessions = ((try? FileManager.default.contentsOfDirectory(at: AppPaths.sessions, includingPropertiesForKeys: nil)) ?? []).filter { UUID(uuidString: $0.lastPathComponent) != nil }
            var manifest = BackupManifest(kind: .full, appVersion: version, appBuild: build, includesSecrets: false, workspaceCount: workspaces.count, sessionCount: sessions.count)
            // The point has its own date. A volatile archive date must not defeat deduplication.
            manifest.createdAt = Date(timeIntervalSince1970: 0)
            files["manifest.json"] = try encoder.encode(manifest)
            return files
        }.value
        let url = try await recoveryStore.create(files: files, directories: ["Sessions": AppPaths.sessions,
            "Originals": AppPaths.root.appendingPathComponent("Originals", isDirectory: true)], reason: reason)
        Diagnostics.record("backup.automatic", ["reason": reason])
        return url
    }

    @MainActor
    static func afterSession() {
        guard UserDefaults.standard.object(forKey: "autoBackup") as? Bool ?? true else { return }
        Task {
            do { _ = try await automaticBackup(reason: "session") }
            catch { AppState.shared.dataStatus = "会议已保存，恢复点创建失败：" + error.localizedDescription }
        }
    }

    static func listBackups() -> [BackupFile] {
        let fm = FileManager.default
        let legacy = (try? fm.contentsOfDirectory(at: AppPaths.backups, includingPropertiesForKeys: [.contentModificationDateKey, .fileSizeKey])) ?? []
        let points = (try? fm.contentsOfDirectory(at: AppPaths.backups.appendingPathComponent("RestorePoints"), includingPropertiesForKeys: [.contentModificationDateKey, .fileSizeKey])) ?? []
        return (legacy + points).filter { ["ihbackup", "ihrestore"].contains($0.pathExtension) }.compactMap { url in
            let values = try? url.resourceValues(forKeys: [.contentModificationDateKey, .fileSizeKey])
            if url.pathExtension == "ihrestore", let data = try? Data(contentsOf: url), let point = try? JSONDecoder().decode(RestorePoint.self, from: data) {
                return BackupFile(url: url, date: point.createdAt, size: values?.fileSize ?? 0, reason: point.reason)
            }
            let reason = url.deletingPathExtension().lastPathComponent.components(separatedBy: "-").last ?? ""
            return BackupFile(url: url, date: values?.contentModificationDate ?? .distantPast, size: values?.fileSize ?? 0, reason: reason)
        }.sorted { $0.date > $1.date }
    }

    // MARK: Helpers

    private static func makeStaging() throws -> URL {
        let base = FileManager.default.temporaryDirectory.appendingPathComponent("ih-backup-\(UUID().uuidString)", isDirectory: true)
        let staging = base.appendingPathComponent("InterviewHacker-Backup", isDirectory: true)
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        return staging
    }

    private static func locateRoot(in directory: URL) throws -> URL {
        if FileManager.default.fileExists(atPath: directory.appendingPathComponent("manifest.json").path) { return directory }
        let children = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []
        if let root = children.first(where: { FileManager.default.fileExists(atPath: $0.appendingPathComponent("manifest.json").path) }) { return root }
        throw BackupManifest.ValidationError.notABackup
    }

    private static func zip(_ folder: URL, to destination: URL) throws {
        try? FileManager.default.removeItem(at: destination)
        try Shell.check("/usr/bin/ditto", ["-c", "-k", "--sequesterRsrc", "--keepParent", folder.path, destination.path], "压缩备份")
    }

    private static func write<T: Encodable>(_ value: T, to url: URL) throws {
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        try encoder.encode(value).write(to: url, options: .atomic)
    }

    private static func writeSkills(_ skills: [Skill], into staging: URL) throws {
        guard !skills.isEmpty else { return }
        try JSONLines.encoder().encode(skills).write(to: staging.appendingPathComponent("skills.json"))
    }

    private static func writeWorkspaces(_ workspaces: [Workspace], into staging: URL) throws {
        let directory = staging.appendingPathComponent("Workspaces", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        for var workspace in workspaces {
            for index in workspace.documents.indices {
                let document = workspace.documents[index]
                let relative = "Materials/\(workspace.id.uuidString)/\(document.id.uuidString).md"
                let target = staging.appendingPathComponent(relative)
                try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
                let markdown = document.content?.markdown ?? document.pages.joined(separator: "\n\n")
                try markdown.write(to: target, atomically: true, encoding: .utf8)
                if let content = document.content { try write(content, to: target.deletingPathExtension().appendingPathExtension("json")) }
                workspace.documents[index].markdownPath = relative
            }
            try write(workspace, to: directory.appendingPathComponent("\(workspace.id.uuidString).json"))
        }
    }

    private static func readWorkspaces(in root: URL) -> [Workspace] {
        let directory = root.appendingPathComponent("Workspaces", isDirectory: true)
        let files = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []
        return files.filter { $0.pathExtension == "json" }.compactMap { file in
            guard var workspace = try? JSONLines.decoder().decode(Workspace.self, from: Data(contentsOf: file)) else { return nil }
            for index in workspace.documents.indices {
                let document = workspace.documents[index]
                // Recreate archives from the canonical inline content; never trust paths from an imported backup.
                if let url = try? MaterialArchive.archive(document.content?.markdown ?? document.pages.joined(separator: "\n\n"), name: document.name, format: document.sourceFormat ?? "legacy", workspaceID: workspace.id) {
                    workspace.documents[index].markdownPath = MaterialArchive.relative(url)
                    if let content = document.content { try? JSONEncoder().encode(content).write(to: url.deletingPathExtension().appendingPathExtension("json"), options: .atomic) }
                } else { workspace.documents[index].markdownPath = nil }
            }
            return workspace
        }
    }

    /// Copies session folders; `only` limits to the given ids.
    private static func copySessions(into staging: URL, only: Set<UUID>?) throws -> [UUID] {
        let target = staging.appendingPathComponent("Sessions", isDirectory: true)
        try FileManager.default.createDirectory(at: target, withIntermediateDirectories: true)
        let folders = (try? FileManager.default.contentsOfDirectory(at: AppPaths.sessions, includingPropertiesForKeys: nil)) ?? []
        var copied: [UUID] = []
        for folder in folders {
            guard let id = UUID(uuidString: folder.lastPathComponent), only?.contains(id) ?? true else { continue }
            try FileManager.default.copyItem(at: folder, to: target.appendingPathComponent(folder.lastPathComponent))
            copied.append(id)
        }
        return copied
    }

    private static func writeSettings(into staging: URL) throws {
        let values = SettingsSnapshot.filter(UserDefaults.standard.dictionaryRepresentation())
        let data = try PropertyListSerialization.data(fromPropertyList: values, format: .xml, options: 0)
        try data.write(to: staging.appendingPathComponent("settings.plist"))
    }

    private static func writeSecrets(into staging: URL) throws {
        var secrets: [String: String] = [:]
        for account in SettingsSnapshot.secretAccounts + APIPreset.loadAll().map(\.keyAccount) {
            let value = KeychainStore.read(account)
            if !value.isEmpty { secrets[account] = value }
        }
        let data = try PropertyListSerialization.data(fromPropertyList: secrets, format: .xml, options: 0)
        try data.write(to: staging.appendingPathComponent("secrets.plist"))
    }

    private static func applySettingsFile(_ url: URL) -> Int {
        guard let data = try? Data(contentsOf: url),
              let values = try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any] else { return 0 }
        let filtered = SettingsSnapshot.filter(values)
        for (key, value) in filtered { UserDefaults.standard.set(value, forKey: key) }
        return filtered.count
    }

    private static func applySecretsFile(_ url: URL) -> Int {
        guard let data = try? Data(contentsOf: url),
              let values = try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: String] else { return 0 }
        var applied = 0
        for (account, value) in values where SettingsSnapshot.isSecretAccount(account) {
            if KeychainStore.write(value, account: account) { applied += 1 }
        }
        return applied
    }
}
