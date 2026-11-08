import Foundation

/// Describes an export archive (`.ntbackup`, a ZIP of a folder):
///
///     manifest.json          this struct
///     settings.plist         app preferences (no API keys unless includesSecrets)
///     secrets.plist          only when the user chose to include API keys
///     Workspaces/<id>.json   scenarios with materials, lessons and question bank
///     Sessions/<id>/…        recorded sessions (same layout as on disk)
public struct BackupManifest: Codable, Sendable, Equatable {
    public enum Kind: String, Codable, Sendable { case full, workspace }

    public static let formatIdentifier = "nextthought-backup"
    private static let legacyFormatIdentifier = "interviewhacker-backup"
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
            case .notABackup: "这不是 NextThought 导出的备份文件。"
            case let .newerFormat(version): "备份格式版本 \(version) 比当前应用新，请先更新应用再导入。"
            }
        }
    }

    public func validate() throws {
        guard format == Self.formatIdentifier || format == Self.legacyFormatIdentifier else { throw ValidationError.notABackup }
        guard version >= 1, workspaceCount >= 0, sessionCount >= 0 else { throw ValidationError.notABackup }
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
                let primary = item.updatedAt > current.updatedAt ? item : current
                let secondary = item.updatedAt > current.updatedAt ? current : item
                var merged = primary
                merged.lessons = mergeLessons(primary.lessons, secondary.lessons)
                merged.questionBank = mergeQuestionBank(primary.questionBank, secondary.questionBank)
                let candidates = mergeCandidates(primary.knowledgeCandidates ?? [], secondary.knowledgeCandidates ?? [])
                merged.knowledgeCandidates = candidates.isEmpty && primary.knowledgeCandidates == nil && secondary.knowledgeCandidates == nil
                    ? nil : candidates
                if merged != current { result[index] = merged; updated += 1 }
                else { skipped += 1 }
            } else {
                // Same name from another machine: keep both, mark the import.
                if result.contains(where: { $0.name == item.name }) { item.name += "（导入）" }
                result.append(item)
                added += 1
            }
        }
        return WorkspaceResult(merged: result, added: added, updated: updated, skipped: skipped)
    }

    private static func mergeLessons(_ preferred: [WorkspaceLesson], _ other: [WorkspaceLesson]) -> [WorkspaceLesson] {
        var result = preferred
        for incoming in other {
            guard let index = result.firstIndex(where: { $0.id == incoming.id }) else { result.append(incoming); continue }
            var item = result[index]
            var sources = Set(item.sourceSessionIDs ?? [])
            var fallbackSources = Set(incoming.sourceSessionIDs ?? [])
            if let source = item.sourceSessionID { sources.insert(source) }
            if let source = incoming.sourceSessionID { fallbackSources.insert(source) }
            sources.formUnion(fallbackSources)
            item.sourceSessionID = item.sourceSessionID ?? incoming.sourceSessionID
            item.sourceSessionIDs = sources.isEmpty ? nil : sources.sorted { $0.uuidString < $1.uuidString }
            item.createdAt = min(item.createdAt, incoming.createdAt)
            result[index] = item
        }
        return result
    }

    private static func mergeQuestionBank(_ preferred: [QuestionBankItem], _ other: [QuestionBankItem]) -> [QuestionBankItem] {
        var result = preferred
        for incoming in other {
            guard let index = result.firstIndex(where: { $0.id == incoming.id }) else { result.append(incoming); continue }
            let existing = result[index]
            let existingRevision = existing.answerRevision ?? 0
            let incomingRevision = incoming.answerRevision ?? 0
            let current: QuestionBankItem
            let fallback: QuestionBankItem
            if existing.updatedAt > incoming.updatedAt ||
                (existing.updatedAt == incoming.updatedAt && existingRevision >= incomingRevision) {
                current = existing; fallback = incoming
            } else {
                current = incoming; fallback = existing
            }
            var item = current
            var sources = Set(current.sourceSessionIDs ?? [])
            var fallbackSources = Set(fallback.sourceSessionIDs ?? [])
            if let source = current.sourceSessionID { sources.insert(source) }
            if let source = fallback.sourceSessionID { fallbackSources.insert(source) }
            sources.formUnion(fallbackSources)
            item.sourceSessionID = current.sourceSessionID ?? fallback.sourceSessionID
            item.sourceSessionIDs = sources.isEmpty ? nil : sources.sorted { $0.uuidString < $1.uuidString }
            item.timesAsked = max(max(current.timesAsked, fallback.timesAsked), sources.count)

            var history = current.answerHistory ?? []
            var historyIDs = Set(history.map(\.id))
            history.append(contentsOf: (fallback.answerHistory ?? []).filter { historyIDs.insert($0.id).inserted })
            if current.answer != fallback.answer {
                // The losing current answer may not yet be in its history. Preserve it, but
                // compare its content, timestamp, and attribution so re-import is idempotent.
                let losingAnswer = WorkspaceAnswerRevision(answer: fallback.answer, recordedAt: fallback.updatedAt,
                                                           sourceSessionID: fallback.knownAnswerSourceSessionID)
                let alreadyPreserved = history.contains {
                    $0.answer == losingAnswer.answer && $0.recordedAt == losingAnswer.recordedAt
                        && $0.sourceSessionID == losingAnswer.sourceSessionID
                }
                if !alreadyPreserved { history.append(losingAnswer) }
            }
            history.sort { $0.recordedAt == $1.recordedAt ? $0.id.uuidString < $1.id.uuidString : $0.recordedAt < $1.recordedAt }
            item.answerHistory = history.isEmpty ? nil : history
            if current.answer == fallback.answer {
                item.answerSourceSessionID = current.knownAnswerSourceSessionID ?? fallback.knownAnswerSourceSessionID
            }

            let contentDiverged = current.question != fallback.question || current.answer != fallback.answer
            let revision = max(existingRevision, incomingRevision)
            item.answerRevision = contentDiverged ? revision + 1 : (existing.answerRevision == nil && incoming.answerRevision == nil ? nil : revision)
            result[index] = item
        }
        return result
    }

    private static func mergeCandidates(_ preferred: [WorkspaceKnowledgeCandidate],
                                        _ other: [WorkspaceKnowledgeCandidate]) -> [WorkspaceKnowledgeCandidate] {
        var result = preferred
        for incoming in other {
            guard let index = result.firstIndex(where: { $0.id == incoming.id }) else { result.append(incoming); continue }
            let existing = result[index]
            let hasAcceptance = [existing, incoming].contains {
                $0.status == .accepted || ($0.receipt?.decision != nil && $0.receipt?.decision != .reject)
            }
            if hasAcceptance {
                let accepted = [existing, incoming].filter { $0.status == .accepted || ($0.receipt?.decision != nil && $0.receipt?.decision != .reject) }
                result[index] = accepted.max { ($0.receipt?.reviewedAt ?? $0.createdAt) < ($1.receipt?.reviewedAt ?? $1.createdAt) } ?? existing
            } else if existing.status == .rejected || incoming.status == .rejected {
                let rejected = [existing, incoming].filter { $0.status == .rejected }
                result[index] = rejected.max { ($0.receipt?.reviewedAt ?? $0.createdAt) < ($1.receipt?.reviewedAt ?? $1.createdAt) } ?? existing
            } else {
                var pending = existing
                if existing.baseQuestionID != incoming.baseQuestionID || existing.baseAnswerRevision != incoming.baseAnswerRevision {
                    pending.baseQuestionID = nil
                    pending.baseAnswerRevision = nil
                }
                result[index] = pending
            }
        }
        return result
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
        "apiVisionEnabled", "answerBackend", "apiBaseURL", "apiModel", "codexModelID", "codexReasoning", "verifyUncertainWithModel",
        "automaticAnswers", "recordSessions", "autoSummarize", "autoCollectKnowledgeCandidates", "autoApplyLessons",
        "transcriptionBackend", "localStreamingModel", "secondPassEnabled", "recognitionLanguage", "recognitionVocabulary",
        "cloudProvider", "cloudModel", "cloudConsent",
        "echoSuppression", "microphoneVoiceProcessing", "microphoneEnabled",
        "teleprompterOpacity", "teleprompterFontSize", "capturePrivacy", "keepOnTop", "appearance", "reducedEffects",
        "customShortcuts", "companionPort", "companionRelayURL", "keepRecognizerWarm",
        "updateChannel", "automaticUpdateChecks", "updateRepository",
        "activeWorkspaceID", "autoBackup", "advancedSettingsEnabled",
        "apiPresets", "activeAPIPresetID",
        "webSearchMode", "webSearchProvider",
        "insightScreenWatch", "insightScreenInterval", "insightFollowSlides", "insightAnswerProgress",
        "globalSkillIDs", "assistantLayout",
        "documentOCRProvider", "documentProviderWasSelected", "ocrFallbackToVision", "semanticRetrievalEnabled",
        "localOCRModelID", "codexOCRModel", "codexOCREffort", "codexOCRTimeout",
    ]

    /// Keychain accounts exported only on request.
    public static var secretAccounts: [String] { ["answer.api", "github.token", "websearch.tavily"] + CloudSTTProvider.allCases.map(\.keychainAccount) }

    /// Fixed accounts plus one per saved API preset (`answer.api.<id>`).
    public static func isSecretAccount(_ account: String) -> Bool {
        secretAccounts.contains(account) || account.hasPrefix("answer.api.")
    }

    public static func filter(_ values: [String: Any]) -> [String: Any] {
        values.filter { exportedKeys.contains($0.key) }
    }

    /// Map the renamed preference when restoring a backup made before per-item review.
    /// A value from the backup takes precedence over the installed app's newer key.
    public static func valuesForRestore(_ values: [String: Any]) -> [String: Any] {
        var restored = filter(values)
        if restored["autoCollectKnowledgeCandidates"] == nil,
           let legacy = restored["autoApplyLessons"] {
            restored["autoCollectKnowledgeCandidates"] = legacy
        }
        restored.removeValue(forKey: "autoApplyLessons")
        return restored
    }
}
