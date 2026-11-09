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
        var sessionsReplaced = 0
        var sessionsKept = 0
        var sessionsIdentical = 0
        var templatesAdded = 0
        var templatesReplaced = 0
        var templatesKept = 0
        var settingsApplied = 0
        var secretsApplied = 0
        var skillsAdded = 0
        var glossaryAdded = 0
        var description: String {
            var parts: [String] = []
            if skillsAdded > 0 { parts.append("技能 \(skillsAdded) 个") }
            if glossaryAdded > 0 { parts.append("术语和人名 \(glossaryAdded) 项") }
            if workspacesAdded + workspacesUpdated > 0 { parts.append("场景 新增 \(workspacesAdded)、更新 \(workspacesUpdated)") }
            if sessionsAdded + sessionsReplaced + sessionsKept + sessionsIdentical > 0 {
                parts.append("记录 新增 \(sessionsAdded)、替换 \(sessionsReplaced)、保留本机 \(sessionsKept)、相同跳过 \(sessionsIdentical)")
            }
            if templatesAdded + templatesReplaced + templatesKept > 0 { parts.append("模板 新增 \(templatesAdded)、替换 \(templatesReplaced)、保留 \(templatesKept)") }
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
    static func exportAll(to destination: URL, includeSecrets: Bool, workspaces: [Workspace], skills: [Skill] = [], glossary: Glossary = Glossary()) throws {
        let staging = try makeStaging()
        defer { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()) }
        try writeWorkspaces(workspaces, into: staging)
        try writeSkills(skills, into: staging)
        try writeGlossary(glossary, into: staging)
        try writeTemplates(into: staging)
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

    struct SessionImport: Identifiable {
        var id: UUID
        var title: String
        var status: BackupImportStatus
        var localFingerprint: String?
        var incomingFingerprint: String
    }
    struct TemplateImport: Identifiable {
        var id: String
        var title: String
        var status: BackupImportStatus
    }
    final class PreparedImport: Identifiable {
        let id = UUID()
        let staging: URL
        let root: URL
        let manifest: BackupManifest
        let sessions: [SessionImport]
        let templates: [TemplateImport]
        let incomingTemplates: [NotesTemplate]
        init(staging: URL, root: URL, manifest: BackupManifest, sessions: [SessionImport], templates: [TemplateImport], incomingTemplates: [NotesTemplate]) {
            self.staging = staging; self.root = root; self.manifest = manifest; self.sessions = sessions
            self.templates = templates; self.incomingTemplates = incomingTemplates
        }
        func discard() { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()) }
        deinit { discard() }
    }

    /// Preflight writes only to its owned temporary directory; review precedes any merge.
    @MainActor
    static func prepareImport(_ source: URL) async throws -> PreparedImport {
        let staging = try makeStaging()
        do {
            if source.pathExtension == "ihrestore" { try await recoveryStore.restore(source, into: staging) }
            else {
                try await Task.detached(priority: .utility) {
                    let names = try Shell.check("/usr/bin/zipinfo", ["-1", source.path], "读取备份目录").output
                    let listing = try Shell.check("/usr/bin/zipinfo", ["-l", source.path], "检查备份容量").output
                    let free = (try FileManager.default.attributesOfFileSystem(forPath: staging.path)[.systemFreeSize] as? NSNumber)?.int64Value ?? 0
                    try BackupArchivePolicy.validate(names: names, listing: listing, freeBytes: free)
                    try Shell.check("/usr/bin/ditto", ["-x", "-k", source.path, staging.path], "解压备份")
                }.value
            }
            let root = try locateRoot(in: staging)
            let manifest = try JSONLines.decoder().decode(BackupManifest.self, from: Data(contentsOf: root.appendingPathComponent("manifest.json")))
            try manifest.validate()
            let localRoot = AppPaths.sessions
            let existingTemplates = currentTemplates()
            let prepared = try await Task.detached(priority: .utility) { () -> ([SessionImport], [TemplateImport], [NotesTemplate]) in
                try validateContents(root, manifest: manifest)
                let directory = root.appendingPathComponent("Sessions")
                let folders = ((try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []).filter { UUID(uuidString: $0.lastPathComponent) != nil }
                let sessions = try folders.map { folder -> SessionImport in
                    let id = UUID(uuidString: folder.lastPathComponent)!
                    let local = localRoot.appendingPathComponent(id.uuidString)
                    let existing = FileManager.default.fileExists(atPath: local.path) ? try BackupContent.fingerprint(local) : nil
                    let incoming = try BackupContent.fingerprint(folder)
                    let meta = try? JSONLines.decoder().decode(SessionMeta.self, from: Data(contentsOf: folder.appendingPathComponent("meta.json")))
                    return SessionImport(id: id, title: meta?.displayTitle ?? id.uuidString, status: BackupContent.status(local: existing, incoming: incoming), localFingerprint: existing, incomingFingerprint: incoming)
                }.sorted { $0.title.localizedStandardCompare($1.title) == .orderedAscending }
                let templates = try readTemplates(in: root)
                let templatePlan = templates.map { template in
                    let current = existingTemplates.first { $0.id == template.id }
                    return TemplateImport(id: template.id, title: template.name, status: current == nil ? .new : current == template ? .identical : .conflict)
                }
                return (sessions, templatePlan, templates)
            }.value
            try Task.checkCancellation()
            return PreparedImport(staging: staging, root: root, manifest: manifest, sessions: prepared.0, templates: prepared.1, incomingTemplates: prepared.2)
        } catch { try? FileManager.default.removeItem(at: staging.deletingLastPathComponent()); throw error }
    }

    /// Programmatic callers keep conflicts on this Mac. UI callers review the same prepared plan.
    @MainActor
    static func importArchive(_ source: URL, applySettings: Bool) async throws -> ImportSummary {
        let prepared = try await prepareImport(source)
        return try await applyImport(prepared, useBackupSessions: [], useBackupTemplates: [], applySettings: applySettings)
    }

    @MainActor
    static func applyImport(_ prepared: PreparedImport, useBackupSessions: Set<UUID>, useBackupTemplates: Set<String>, applySettings: Bool) async throws -> ImportSummary {
        guard !AppState.shared.isRunning, SessionArchive.shared.summarizing.isEmpty else { throw AppFailure.capture("请等记录和纪要整理结束后再恢复。") }
        WorkspaceStore.shared.flush(); await SessionArchive.shared.flush()
        let localRoot = AppPaths.sessions
        try await Task.detached(priority: .utility) {
            for item in prepared.sessions {
                let folder = localRoot.appendingPathComponent(item.id.uuidString)
                let fingerprint = FileManager.default.fileExists(atPath: folder.path) ? try BackupContent.fingerprint(folder) : nil
                guard fingerprint == item.localFingerprint else { throw AppFailure.capture("预览后本机记录有变化，请重新导入以查看最新差异。") }
            }
        }.value
        _ = try await automaticBackup(reason: "import")
        var summary = ImportSummary()
        let root = prepared.root
        let incoming = try await Task.detached(priority: .utility) { try readWorkspaces(in: root, materialize: true) }.value
        let merged = BackupMerge.workspaces(existing: WorkspaceStore.shared.workspaces, incoming: incoming)
        WorkspaceStore.shared.replaceAll(merged.merged)
        summary.workspacesAdded = merged.added; summary.workspacesUpdated = merged.updated
        if prepared.manifest.kind == .workspace, let first = incoming.first { WorkspaceStore.shared.activate(first.id) }
        if let data = try? Data(contentsOf: root.appendingPathComponent("skills.json")) {
            summary.skillsAdded = SkillLibrary.shared.merge(try JSONLines.decoder().decode([Skill].self, from: data))
        }
        if let data = try? Data(contentsOf: root.appendingPathComponent("glossary.json")) {
            summary.glossaryAdded = GlossaryStore.shared.merge(try JSONLines.decoder().decode(Glossary.self, from: data))
        }
        let counts = try await Task.detached(priority: .utility) { () -> (Int, Int, Int, Int) in
            try FileManager.default.createDirectory(at: localRoot, withIntermediateDirectories: true)
            var added = 0, replaced = 0, kept = 0, identical = 0
            for item in prepared.sessions {
                if item.status == .identical { identical += 1; continue }
                if item.status == .conflict && !useBackupSessions.contains(item.id) { kept += 1; continue }
                let target = localRoot.appendingPathComponent(item.id.uuidString)
                let temporary = localRoot.appendingPathComponent(".restore-" + UUID().uuidString)
                defer { try? FileManager.default.removeItem(at: temporary) }
                try FileManager.default.copyItem(at: root.appendingPathComponent("Sessions").appendingPathComponent(item.id.uuidString), to: temporary)
                if item.status == .conflict {
                    _ = try FileManager.default.replaceItemAt(target, withItemAt: temporary)
                    replaced += 1
                } else { try FileManager.default.moveItem(at: temporary, to: target); added += 1 }
            }
            let originals = root.appendingPathComponent("Originals")
            let target = AppPaths.root.appendingPathComponent("Originals")
            let files = (try? FileManager.default.contentsOfDirectory(at: originals, includingPropertiesForKeys: nil)) ?? []
            if !files.isEmpty { try FileManager.default.createDirectory(at: target, withIntermediateDirectories: true) }
            for file in files where !file.lastPathComponent.hasPrefix("._") {
                let destination = target.appendingPathComponent(file.lastPathComponent)
                if !FileManager.default.fileExists(atPath: destination.path) { try FileManager.default.copyItem(at: file, to: destination) }
            }
            return (added, replaced, kept, identical)
        }.value
        summary.sessionsAdded = counts.0; summary.sessionsReplaced = counts.1; summary.sessionsKept = counts.2; summary.sessionsIdentical = counts.3
        let templateMerge = BackupMerge.notesTemplates(existing: currentTemplates(), incoming: prepared.incomingTemplates, useIncoming: useBackupTemplates)
        if applySettings {
            summary.settingsApplied = applySettingsFile(root.appendingPathComponent("settings.plist"))
            summary.secretsApplied = applySecretsFile(root.appendingPathComponent("secrets.plist"))
        }
        // Templates are user data, independent of the preference-import switch.
        UserDefaults.standard.set(try JSONEncoder().encode(templateMerge.templates), forKey: "customNotesTemplates")
        AppState.shared.customNotesTemplates = templateMerge.templates
        if applySettings {
            let defaults = UserDefaults.standard
            AppState.shared.notesModelID = defaults.string(forKey: "notesModelID") ?? AppState.shared.notesModelID
            AppState.shared.notesReasoning = defaults.string(forKey: "notesReasoning") ?? AppState.shared.notesReasoning
            AppState.shared.notesLanguage = defaults.string(forKey: "notesLanguage") ?? AppState.shared.notesLanguage
            AppState.shared.notesTemplateByType = defaults.dictionary(forKey: "notesTemplateByType") as? [String: String] ?? AppState.shared.notesTemplateByType
        }
        summary.templatesAdded = templateMerge.added; summary.templatesReplaced = templateMerge.replaced; summary.templatesKept = templateMerge.kept
        SessionArchive.shared.contentDidChange()
        SessionArchive.shared.load()
        Diagnostics.record("backup.imported", ["workspaces": merged.added + merged.updated, "sessions": summary.sessionsAdded, "replaced": summary.sessionsReplaced])
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
        let workspaces = WorkspaceStore.shared.workspaces, skills = SkillLibrary.shared.skills, glossary = GlossaryStore.shared.glossary
        let settings = SettingsSnapshot.filter(UserDefaults.standard.dictionaryRepresentation())
        let settingsData = try PropertyListSerialization.data(fromPropertyList: settings, format: .xml, options: 0)
        let version = appVersion, build = appBuild
        let files = try await Task.detached(priority: .utility) { () -> [String: Data] in
            let encoder = JSONLines.encoder(); encoder.outputFormatting.insert(.sortedKeys)
            var files: [String: Data] = ["settings.plist": settingsData, "skills.json": try encoder.encode(skills)]
            if !glossary.isEmpty { files["glossary.json"] = try encoder.encode(glossary) }
            files["note-templates.json"] = try encoder.encode(Self.currentTemplates())
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
        if try await recoveryStore.storageBytes() > 2 * 1024 * 1024 * 1024 {
            AppState.shared.dataStatus = "最新恢复点超出 2 GiB 软预算，已保留完整恢复依据；可手动导出后管理空间。"
            Diagnostics.record("backup.soft_budget_exceeded")
        }
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

    /// All semantic checks precede materialization/merge: a corrupt original
    /// cannot leave half-imported workspaces or preferences behind.
    private static func validateContents(_ root: URL, manifest: BackupManifest) throws {
        let fm = FileManager.default
        let directory = root.appendingPathComponent("Workspaces")
        let files = ((try? fm.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? [])
            .filter { $0.pathExtension == "json" && !$0.lastPathComponent.hasPrefix("._") }
        guard files.count == manifest.workspaceCount else { throw AppFailure.capture("备份场景数量不完整。") }
        for file in files { _ = try JSONLines.decoder().decode(Workspace.self, from: Data(contentsOf: file)) }
        let sessions = ((try? fm.contentsOfDirectory(at: root.appendingPathComponent("Sessions"), includingPropertiesForKeys: nil)) ?? []).filter { UUID(uuidString: $0.lastPathComponent) != nil }
        guard sessions.count == manifest.sessionCount else { throw AppFailure.capture("备份会议数量不完整。") }
        for name in ["settings.plist", "secrets.plist"] {
            let file = root.appendingPathComponent(name)
            if fm.fileExists(atPath: file.path) {
                guard try PropertyListSerialization.propertyList(from: Data(contentsOf: file), format: nil) is [String: Any] else { throw AppFailure.capture("备份设置格式无效。") }
            }
        }
        _ = try readTemplates(in: root)
        let invalidMetadata = sessions.filter { folder in
            guard let data = try? Data(contentsOf: folder.appendingPathComponent("meta.json")),
                  let meta = try? JSONLines.decoder().decode(SessionMeta.self, from: data) else { return true }
            return meta.id != UUID(uuidString: folder.lastPathComponent)
        }
        guard invalidMetadata.isEmpty else { throw AppFailure.capture(String(localized: "备份中有 \(invalidMetadata.count) 场记录的元数据损坏或标识不匹配；尚未导入任何内容。")) }
        for session in sessions {
            let user = session.appendingPathComponent(SessionUserContent.filename)
            if fm.fileExists(atPath: user.path) { _ = try JSONLines.decoder().decode(SessionUserContent.self, from: Data(contentsOf: user)) }
        }
        let skills = root.appendingPathComponent("skills.json")
        if fm.fileExists(atPath: skills.path) { _ = try JSONLines.decoder().decode([Skill].self, from: Data(contentsOf: skills)) }
        let glossary = root.appendingPathComponent("glossary.json")
        if fm.fileExists(atPath: glossary.path) { _ = try JSONLines.decoder().decode(Glossary.self, from: Data(contentsOf: glossary)) }
        let originals = ((try? fm.contentsOfDirectory(at: root.appendingPathComponent("Originals"), includingPropertiesForKeys: [.isRegularFileKey])) ?? [])
        for file in originals where !file.lastPathComponent.hasPrefix("._") {
            let hash = file.deletingPathExtension().lastPathComponent
            guard hash.count == 64, hash.allSatisfy({ $0.isHexDigit }),
                  ManagedModelStore.sha256(of: file) == hash else { throw AppFailure.capture("备份原文件校验失败，尚未导入任何资料。") }
        }
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

    private static func writeGlossary(_ glossary: Glossary, into staging: URL) throws {
        guard !glossary.isEmpty else { return }
        try JSONLines.encoder().encode(glossary).write(to: staging.appendingPathComponent("glossary.json"))
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
                if let hash = document.content?.sourceHash, let format = document.sourceFormat,
                   hash.count == 64, hash.allSatisfy({ $0.isHexDigit }), !format.contains("/"), !format.contains("..") {
                    let original = AppPaths.root.appendingPathComponent("Originals").appendingPathComponent(hash + "." + format)
                    if FileManager.default.fileExists(atPath: original.path) {
                        let copy = staging.appendingPathComponent("Originals").appendingPathComponent(original.lastPathComponent)
                        try FileManager.default.createDirectory(at: copy.deletingLastPathComponent(), withIntermediateDirectories: true)
                        if !FileManager.default.fileExists(atPath: copy.path) { try FileManager.default.copyItem(at: original, to: copy) }
                    }
                }
                workspace.documents[index].markdownPath = relative
            }
            try write(workspace, to: directory.appendingPathComponent("\(workspace.id.uuidString).json"))
        }
    }

    private static func readWorkspaces(in root: URL, materialize: Bool = false) throws -> [Workspace] {
        let directory = root.appendingPathComponent("Workspaces", isDirectory: true)
        let files = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)) ?? []
        return try files.filter { $0.pathExtension == "json" && !$0.lastPathComponent.hasPrefix("._") }.map { file in
            var workspace = try JSONLines.decoder().decode(Workspace.self, from: Data(contentsOf: file))
            if materialize {
                for index in workspace.documents.indices {
                    let document = workspace.documents[index]
                    let url = try MaterialArchive.archive(document.content?.markdown ?? document.pages.joined(separator: "\n\n"), name: document.name, format: document.sourceFormat ?? "legacy", workspaceID: workspace.id)
                    workspace.documents[index].markdownPath = MaterialArchive.relative(url)
                    if let content = document.content { try JSONEncoder().encode(content).write(to: url.deletingPathExtension().appendingPathExtension("json"), options: .atomic) }
                }
            }
            return workspace
        }
    }

    private static func currentTemplates() -> [NotesTemplate] {
        guard let data = UserDefaults.standard.data(forKey: "customNotesTemplates") else { return [] }
        return (try? JSONDecoder().decode([NotesTemplate].self, from: data)) ?? []
    }
    private static func writeTemplates(into root: URL) throws {
        try write(currentTemplates(), to: root.appendingPathComponent("note-templates.json"))
    }
    private static func readTemplates(in root: URL) throws -> [NotesTemplate] {
        let file = root.appendingPathComponent("note-templates.json")
        if FileManager.default.fileExists(atPath: file.path) { return try JSONLines.decoder().decode([NotesTemplate].self, from: Data(contentsOf: file)) }
        if let data = try? Data(contentsOf: root.appendingPathComponent("settings.plist")),
           let values = try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any],
           let templates = values["customNotesTemplates"] as? Data { return try JSONDecoder().decode([NotesTemplate].self, from: templates) }
        return []
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
