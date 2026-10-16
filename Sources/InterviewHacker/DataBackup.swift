import Foundation
import InterviewHackerCore

/// One-click export / import of settings, workspaces and session records, plus
/// automatic backups (daily, before every update install and before every import).
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
    static func importArchive(_ source: URL, applySettings: Bool) throws -> ImportSummary {
        let staging = try makeStaging()
        defer { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()) }
        try Shell.check("/usr/bin/ditto", ["-x", "-k", source.path, staging.path], "解压备份")
        // ditto keeps the top-level folder name of the archive.
        let root = try locateRoot(in: staging)
        let manifest = try JSONLines.decoder().decode(BackupManifest.self, from: Data(contentsOf: root.appendingPathComponent("manifest.json")))
        try manifest.validate()

        WorkspaceStore.shared.flush()
        _ = try? automaticBackup(reason: "import")

        var summary = ImportSummary()
        let incoming = readWorkspaces(in: root)
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

    /// Snapshot into `Backups/` (keeps the newest 10). Secrets are never included.
    @MainActor
    @discardableResult
    static func automaticBackup(reason: String) throws -> URL {
        WorkspaceStore.shared.flush()
        try FileManager.default.createDirectory(at: AppPaths.backups, withIntermediateDirectories: true)
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMdd-HHmmss"
        let url = AppPaths.backups.appendingPathComponent("auto-\(formatter.string(from: Date()))-\(reason).ihbackup")
        try exportAll(to: url, includeSecrets: false, workspaces: WorkspaceStore.shared.workspaces, skills: SkillLibrary.shared.skills)
        UserDefaults.standard.set(Date(), forKey: "lastAutomaticBackup")
        prune(keep: 10)
        Diagnostics.record("backup.automatic", ["reason": reason])
        return url
    }

    /// Daily backup at launch (skipped when one was made in the last 24 hours).
    @MainActor
    static func dailyBackupIfNeeded() {
        guard UserDefaults.standard.object(forKey: "autoBackup") as? Bool ?? true else { return }
        if let last = UserDefaults.standard.object(forKey: "lastAutomaticBackup") as? Date, Date().timeIntervalSince(last) < 86_400 { return }
        _ = try? automaticBackup(reason: "daily")
    }

    static func listBackups() -> [BackupFile] {
        let files = (try? FileManager.default.contentsOfDirectory(at: AppPaths.backups, includingPropertiesForKeys: [.contentModificationDateKey, .fileSizeKey])) ?? []
        return files.filter { $0.pathExtension == "ihbackup" }.compactMap { url in
            let values = try? url.resourceValues(forKeys: [.contentModificationDateKey, .fileSizeKey])
            let reason = url.deletingPathExtension().lastPathComponent.components(separatedBy: "-").last ?? ""
            return BackupFile(url: url, date: values?.contentModificationDate ?? .distantPast, size: values?.fileSize ?? 0, reason: reason)
        }.sorted { $0.date > $1.date }
    }

    private static func prune(keep: Int) {
        for file in listBackups().dropFirst(keep) { try? FileManager.default.removeItem(at: file.url) }
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
        for workspace in workspaces { try write(workspace, to: directory.appendingPathComponent("\(workspace.id.uuidString).json")) }
    }

    private static func readWorkspaces(in root: URL) -> [Workspace] {
        let directory = root.appendingPathComponent("Workspaces", isDirectory: true)
        let files = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []
        return files.filter { $0.pathExtension == "json" }.compactMap { try? JSONLines.decoder().decode(Workspace.self, from: Data(contentsOf: $0)) }
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
