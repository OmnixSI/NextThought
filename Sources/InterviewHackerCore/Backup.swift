import Foundation

/// Describes an export archive (`.ihbackup`, a ZIP of a folder):
///
///     manifest.json          this struct
///     settings.plist         app preferences (no API keys unless includesSecrets)
///     secrets.plist          only when the user chose to include API keys
///     Workspaces/<id>.json   scenarios with materials, lessons and question bank
///     Sessions/<id>/…        recorded sessions (same layout as on disk)
public struct BackupManifest: Codable, Sendable, Equatable {
    public enum Kind: String, Codable, Sendable { case full, workspace }

    public static let formatIdentifier = "interviewhacker-backup"
    public static let currentVersion = 1

    public var format: String
    public var version: Int
    public var kind: Kind
    public var createdAt: Date
    public var appVersion: String
    public var appBuild: String
    public var includesSecrets: Bool
    public var workspaceCount: Int
    public var sessionCount: Int

    public init(kind: Kind, createdAt: Date = Date(), appVersion: String, appBuild: String, includesSecrets: Bool, workspaceCount: Int, sessionCount: Int) {
        format = Self.formatIdentifier; version = Self.currentVersion
        self.kind = kind; self.createdAt = createdAt; self.appVersion = appVersion; self.appBuild = appBuild
        self.includesSecrets = includesSecrets; self.workspaceCount = workspaceCount; self.sessionCount = sessionCount
    }

    public enum ValidationError: LocalizedError, Equatable {
        case notABackup, newerFormat(Int)
        public var errorDescription: String? {
            switch self {
            case .notABackup: "这不是 InterviewHacker 导出的备份文件。"
            case let .newerFormat(version): "备份格式版本 \(version) 比当前应用新，请先更新应用再导入。"
            }
        }
    }

    public func validate() throws {
        guard format == Self.formatIdentifier else { throw ValidationError.notABackup }
        guard version <= Self.currentVersion else { throw ValidationError.newerFormat(version) }
    }
}

public enum BackupMerge {
    public struct WorkspaceResult: Sendable, Equatable {
        public var merged: [Workspace]
        public var added: Int
        public var updated: Int
        public var skipped: Int
    }

    /// Workspaces are matched by id; the more recently edited copy wins, so importing
    /// an old backup never overwrites newer work. Knowledge is unioned either way.
    public static func workspaces(existing: [Workspace], incoming: [Workspace]) -> WorkspaceResult {
        var result = existing
        var added = 0, updated = 0, skipped = 0
        for var item in incoming {
            if let index = result.firstIndex(where: { $0.id == item.id }) {
                let current = result[index]
                if item.updatedAt > current.updatedAt {
                    item.lessons = union(item.lessons, current.lessons)
                    item.questionBank = union(item.questionBank, current.questionBank)
                    result[index] = item
                    updated += 1
                } else {
                    var kept = current
                    let lessons = union(current.lessons, item.lessons)
                    let bank = union(current.questionBank, item.questionBank)
                    if lessons.count != current.lessons.count || bank.count != current.questionBank.count {
                        kept.lessons = lessons; kept.questionBank = bank
                        result[index] = kept
                        updated += 1
                    } else {
                        skipped += 1
                    }
                }
            } else {
                // Same name from another machine: keep both, mark the import.
                if result.contains(where: { $0.name == item.name }) { item.name += "（导入）" }
                result.append(item)
                added += 1
            }
        }
        return WorkspaceResult(merged: result, added: added, updated: updated, skipped: skipped)
    }

    private static func union<T: Identifiable>(_ primary: [T], _ secondary: [T]) -> [T] where T.ID == UUID {
        let ids = Set(primary.map(\.id))
        return primary + secondary.filter { !ids.contains($0.id) }
    }

    /// Session folders to copy: those not present yet (sessions are immutable once ended).
    public static func sessionsToImport(existing: Set<UUID>, incoming: [UUID]) -> [UUID] {
        incoming.filter { !existing.contains($0) }
    }
}

/// Which preferences travel in a backup. Machine-specific values (selected window,
/// warm-up state, signing) stay on the machine.
public enum SettingsSnapshot {
    public static let exportedKeys: [String] = [
        "answerBackend", "apiBaseURL", "apiModel", "codexModelID", "codexReasoning", "verifyUncertainWithModel",
        "automaticAnswers", "recordSessions", "autoSummarize", "autoApplyLessons",
        "transcriptionBackend", "localStreamingModel", "secondPassEnabled", "recognitionLanguage", "recognitionVocabulary",
        "cloudProvider", "cloudModel", "cloudConsent",
        "echoSuppression", "microphoneVoiceProcessing", "microphoneEnabled",
        "teleprompterOpacity", "teleprompterFontSize", "capturePrivacy", "keepOnTop", "appearance", "reducedEffects",
        "customShortcuts", "companionPort", "keepRecognizerWarm",
        "updateChannel", "automaticUpdateChecks", "updateRepository",
        "activeWorkspaceID", "autoBackup",
    ]

    /// Keychain accounts exported only on request.
    public static var secretAccounts: [String] { ["answer.api", "github.token"] + CloudSTTProvider.allCases.map(\.keychainAccount) }

    public static func filter(_ values: [String: Any]) -> [String: Any] {
        values.filter { exportedKeys.contains($0.key) }
    }
}
