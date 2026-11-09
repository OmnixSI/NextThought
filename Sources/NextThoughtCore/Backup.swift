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
                let deletedSources = Set(primary.sourceDeletionTombstones ?? [])
                    .union(secondary.sourceDeletionTombstones ?? [])
                merged.sourceDeletionTombstones = deletedSources.isEmpty
                    && primary.sourceDeletionTombstones == nil && secondary.sourceDeletionTombstones == nil
                    ? nil : deletedSources.sorted { $0.uuidString.lowercased() < $1.uuidString.lowercased() }
                merged.lessons = mergeLessons(primary.lessons, secondary.lessons)
                merged.questionBank = mergeQuestionBank(primary.questionBank, secondary.questionBank)
                merged.seriesItemSourceTombstones = mergeSeriesItemSourceTombstones(
                    primary.seriesItemSourceTombstones, secondary.seriesItemSourceTombstones)
                let seriesCandidates = mergeSeriesCandidates(primary.seriesChangeCandidates ?? [],
                                                              secondary.seriesChangeCandidates ?? [])
                let seriesTombstones = seriesItemSourceTombstones(
                    candidates: seriesCandidates,
                    workspaces: [primary, secondary])
                let seriesItems = mergeSeriesItems(primary.seriesItems ?? [], secondary.seriesItems ?? [],
                                                    tombstones: seriesTombstones)
                merged.seriesItems = seriesItems.isEmpty && primary.seriesItems == nil && secondary.seriesItems == nil
                    ? nil : seriesItems
                merged.seriesChangeCandidates = seriesCandidates.isEmpty && primary.seriesChangeCandidates == nil && secondary.seriesChangeCandidates == nil
                    ? nil : seriesCandidates
                let candidates = mergeCandidates(primary.knowledgeCandidates ?? [], secondary.knowledgeCandidates ?? [])
                merged.knowledgeCandidates = candidates.isEmpty && primary.knowledgeCandidates == nil && secondary.knowledgeCandidates == nil
                    ? nil : candidates
                if !deletedSources.isEmpty {
                    _ = WorkspaceSourceIntegrity.removeMeetings(deletedSources, from: &merged,
                                                                retainAcceptedContent: false, now: merged.updatedAt)
                }
                if merged != current { result[index] = merged; updated += 1 }
                else { skipped += 1 }
            } else {
                // Same name from another machine: keep both, mark the import.
                let deletedSources = Set(item.sourceDeletionTombstones ?? [])
                if !deletedSources.isEmpty {
                    _ = WorkspaceSourceIntegrity.removeMeetings(deletedSources, from: &item,
                                                                retainAcceptedContent: false, now: item.updatedAt)
                }
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
            let existing = result[index]
            let current = incoming.revision > existing.revision
                || (incoming.revision == existing.revision && incoming.updatedAt > existing.updatedAt) ? incoming : existing
            let fallback = current.id == existing.id && current.revision == existing.revision
                && current.updatedAt == existing.updatedAt ? incoming : existing
            var item = current
            var history = current.history
            let deletedSourceIDs = Set(current.sourceMissingSessionIDs ?? [])
                .union(fallback.sourceMissingSessionIDs ?? [])
                .union(current.history.flatMap { $0.sourceMissingSessionIDs ?? [] })
                .union(fallback.history.flatMap { $0.sourceMissingSessionIDs ?? [] })
            func contains(_ revision: WorkspaceLessonRevision, in history: [WorkspaceLessonRevision]) -> Bool {
                history.contains {
                    $0.revision == revision.revision && $0.text == revision.text
                        && $0.sourceSessionID == revision.sourceSessionID
                        && $0.sourceSessionIDs == revision.sourceSessionIDs
                        && $0.sourceMissingSessionIDs == revision.sourceMissingSessionIDs
                }
            }
            for revision in fallback.history where !contains(revision, in: history) {
                history.append(revision)
            }
            if fallback.currentRevision != current.currentRevision,
               !contains(fallback.currentRevision, in: history) {
                history.append(fallback.currentRevision)
            }
            // A deletion tombstone is authoritative across backup imports. The older
            // copy may still list that meeting as a live source, so scrub it from both
            // the merged current version and every historical snapshot before merging.
            func removingDeletedSources(from revision: WorkspaceLessonRevision) -> WorkspaceLessonRevision {
                let originalSources = WorkspaceLessonHistory.allSourceIDs(revision)
                let missingSources = originalSources.intersection(deletedSourceIDs)
                guard !missingSources.isEmpty else { return revision }
                let remainingSources = originalSources.subtracting(deletedSourceIDs)
                var cleaned = revision
                cleaned.sourceSessionIDs = remainingSources.isEmpty ? nil : WorkspaceLessonHistory.sorted(remainingSources)
                cleaned.sourceSessionID = revision.sourceSessionID.flatMap {
                    remainingSources.contains($0) ? $0 : nil
                } ?? WorkspaceLessonHistory.sorted(remainingSources).first
                cleaned.sourceMissingSessionIDs = WorkspaceLessonHistory.sorted(
                    Set(revision.sourceMissingSessionIDs ?? []).union(missingSources))
                return cleaned
            }
            history = history.map(removingDeletedSources)
            var uniqueHistory: [WorkspaceLessonRevision] = []
            for revision in history {
                if let index = uniqueHistory.firstIndex(where: {
                    $0.revision == revision.revision && $0.text == revision.text
                        && $0.sourceSessionID == revision.sourceSessionID
                        && $0.sourceSessionIDs == revision.sourceSessionIDs
                        && $0.sourceMissingSessionIDs == revision.sourceMissingSessionIDs
                }) {
                    if revision.recordedAt < uniqueHistory[index].recordedAt {
                        uniqueHistory[index].recordedAt = revision.recordedAt
                    }
                } else {
                    uniqueHistory.append(revision)
                }
            }
            if current.text == fallback.text {
                let allSources = WorkspaceLessonHistory.allSourceIDs(current)
                    .union(WorkspaceLessonHistory.allSourceIDs(fallback))
                let removedSources = allSources.intersection(deletedSourceIDs)
                let sources = allSources.subtracting(deletedSourceIDs)
                item.sourceSessionIDs = sources.isEmpty ? nil : WorkspaceLessonHistory.sorted(sources)
                item.sourceSessionID = current.sourceSessionID.flatMap { sources.contains($0) ? $0 : nil }
                    ?? WorkspaceLessonHistory.sorted(sources).first
                let missing = Set(current.sourceMissingSessionIDs ?? [])
                    .union(fallback.sourceMissingSessionIDs ?? [])
                    .union(removedSources)
                item.sourceMissingSessionIDs = missing.isEmpty ? nil : WorkspaceLessonHistory.sorted(missing)
                item.independentlyRetained = sources.isEmpty
                    && (current.independentlyRetained || fallback.independentlyRetained)
            } else {
                let currentSources = WorkspaceLessonHistory.allSourceIDs(current)
                let removedSources = currentSources.intersection(deletedSourceIDs)
                let sources = currentSources.subtracting(deletedSourceIDs)
                item.sourceSessionIDs = sources.isEmpty ? nil : WorkspaceLessonHistory.sorted(sources)
                item.sourceSessionID = current.sourceSessionID.flatMap { sources.contains($0) ? $0 : nil }
                    ?? WorkspaceLessonHistory.sorted(sources).first
                let missing = Set(current.sourceMissingSessionIDs ?? []).union(removedSources)
                item.sourceMissingSessionIDs = missing.isEmpty ? nil : WorkspaceLessonHistory.sorted(missing)
                if !sources.isEmpty { item.independentlyRetained = false }
            }
            uniqueHistory.removeAll { contains($0, in: [item.currentRevision]) }
            item.history = uniqueHistory.sorted {
                $0.revision == $1.revision ? $0.recordedAt < $1.recordedAt : $0.revision < $1.revision
            }
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
                // Preserve the winning answer's legacy source before the revision below
                // makes `knownAnswerSourceSessionID` stop consulting sourceSessionID.
                item.answerSourceSessionID = current.knownAnswerSourceSessionID
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
                // A missing source on a revised answer is meaningful: it can be a
                // manual edit. Do not resurrect provenance from an older duplicate
                // just because its answer text happens to be identical.
                item.answerSourceSessionID = (current.answerRevision ?? 0) > 0
                    ? current.answerSourceSessionID
                    : current.knownAnswerSourceSessionID ?? fallback.knownAnswerSourceSessionID
            }

            let contentDiverged = current.question != fallback.question || current.answer != fallback.answer
            let revision = max(existingRevision, incomingRevision)
            item.answerRevision = contentDiverged ? revision + 1 : (existing.answerRevision == nil && incoming.answerRevision == nil ? nil : revision)
            result[index] = item
        }
        return result
    }

    private static func mergeSeriesItems(_ preferred: [WorkspaceSeriesItem],
                                         _ other: [WorkspaceSeriesItem],
                                         tombstones: [SeriesItemSourceKey: Bool]) -> [WorkspaceSeriesItem] {
        var result = preferred
        for incoming in other {
            let incomingKey = SeriesItemSourceKey(itemID: incoming.id, sessionID: incoming.sourceSessionID)
            guard let index = result.firstIndex(where: { $0.id == incoming.id }) else {
                result.append(incoming)
                continue
            }
            let existing = result[index]
            let existingKey = SeriesItemSourceKey(itemID: existing.id, sessionID: existing.sourceSessionID)
            let existingIsDeleted = tombstones[existingKey] != nil
            let incomingIsDeleted = tombstones[incomingKey] != nil
            let existingWasRetained = tombstones[existingKey] == true
                && existing.sourceMissing == true && existing.independentlyRetained == true
            let incomingWasRetained = tombstones[incomingKey] == true
                && incoming.sourceMissing == true && incoming.independentlyRetained == true
            let currentIsExisting: Bool
            if existingWasRetained != incomingWasRetained {
                let retainedIsExisting = existingWasRetained
                let retained = retainedIsExisting ? existing : incoming
                let otherItem = retainedIsExisting ? incoming : existing
                let otherKey = retainedIsExisting ? incomingKey : existingKey
                let otherIsLiveOnDifferentSource = tombstones[otherKey] == nil
                    && otherItem.sourceMissing != true
                    && otherItem.sourceSessionID != retained.sourceSessionID
                let otherIsNewer = otherItem.revision > retained.revision
                    || (otherItem.revision == retained.revision && otherItem.updatedAt > retained.updatedAt)
                currentIsExisting = otherIsLiveOnDifferentSource && otherIsNewer
                    ? !retainedIsExisting : retainedIsExisting
            } else if existingIsDeleted != incomingIsDeleted {
                currentIsExisting = !existingIsDeleted
            } else {
                currentIsExisting = !(incoming.revision > existing.revision
                    || (incoming.revision == existing.revision && incoming.updatedAt > existing.updatedAt))
            }
            let current = currentIsExisting ? existing : incoming
            let fallback = currentIsExisting ? incoming : existing
            var item = current
            var history = current.history + fallback.history
            if fallback.revision < current.revision
                || (fallback.revision == current.revision && fallback.currentRevision != current.currentRevision) {
                // Preserve every losing snapshot, including concurrent edits that share a revision number.
                history.append(fallback.currentRevision)
            }
            let cleanedHistory = history.compactMap { revision -> WorkspaceSeriesItemRevision? in
                let key = SeriesItemSourceKey(itemID: item.id, sessionID: revision.sourceSessionID)
                guard let retain = tombstones[key] else { return revision }
                guard retain else { return nil }
                return scrubbedDeletedSource(revision, itemID: item.id)
            }
            var historyByID: [UUID: WorkspaceSeriesItemRevision] = [:]
            for var revision in cleanedHistory {
                revision.id = revision.stableID(itemID: item.id)
                if let existing = historyByID[revision.id] {
                    if revision.recordedAt < existing.recordedAt { historyByID[revision.id] = revision }
                } else {
                    historyByID[revision.id] = revision
                }
            }
            item.history = historyByID.values.sorted {
                if $0.revision != $1.revision { return $0.revision < $1.revision }
                if $0.recordedAt != $1.recordedAt { return $0.recordedAt < $1.recordedAt }
                return $0.id.uuidString.lowercased() < $1.id.uuidString.lowercased()
            }
            result[index] = item
        }
        return result.compactMap { item in
            let key = SeriesItemSourceKey(itemID: item.id, sessionID: item.sourceSessionID)
            guard let retain = tombstones[key] else {
                var updated = item
                updated.history = updated.history.compactMap { revision in
                    let revisionKey = SeriesItemSourceKey(itemID: item.id, sessionID: revision.sourceSessionID)
                    guard let retain = tombstones[revisionKey] else { return revision }
                    return retain ? scrubbedDeletedSource(revision, itemID: item.id) : nil
                }
                updated.history = uniqueSeriesHistory(updated.history, itemID: updated.id)
                    .filter { $0.id != updated.currentRevision.id }
                return updated
            }
            guard retain else { return nil }
            var updated = scrubbedDeletedSource(item)
            updated.independentlyRetained = true
            updated.history = updated.history.compactMap { revision in
                let revisionKey = SeriesItemSourceKey(itemID: item.id, sessionID: revision.sourceSessionID)
                guard let retain = tombstones[revisionKey] else { return revision }
                return retain ? scrubbedDeletedSource(revision, itemID: item.id) : nil
            }
            updated.history = uniqueSeriesHistory(updated.history, itemID: updated.id)
                .filter { $0.id != updated.currentRevision.id }
            return updated
        }
    }

    private static func uniqueSeriesHistory(_ history: [WorkspaceSeriesItemRevision], itemID: UUID)
        -> [WorkspaceSeriesItemRevision] {
        var result: [UUID: WorkspaceSeriesItemRevision] = [:]
        for var revision in history {
            revision.id = revision.stableID(itemID: itemID)
            if let existing = result[revision.id] {
                if revision.recordedAt < existing.recordedAt { result[revision.id] = revision }
            } else {
                result[revision.id] = revision
            }
        }
        return result.values.sorted {
            if $0.revision != $1.revision { return $0.revision < $1.revision }
            if $0.recordedAt != $1.recordedAt { return $0.recordedAt < $1.recordedAt }
            return $0.id.uuidString.lowercased() < $1.id.uuidString.lowercased()
        }
    }

    private struct SeriesItemSourceKey: Hashable {
        var itemID: UUID
        var sessionID: UUID
    }

    private static func mergeSeriesItemSourceTombstones(
        _ preferred: [WorkspaceSeriesItemSourceTombstone]?,
        _ other: [WorkspaceSeriesItemSourceTombstone]?
    ) -> [WorkspaceSeriesItemSourceTombstone]? {
        let tombstones = Set((preferred ?? []) + (other ?? [])).sorted {
            let leftItem = $0.itemID.uuidString.lowercased()
            let rightItem = $1.itemID.uuidString.lowercased()
            if leftItem != rightItem { return leftItem < rightItem }
            return $0.sourceSessionID.uuidString.lowercased() < $1.sourceSessionID.uuidString.lowercased()
        }
        return tombstones.isEmpty && preferred == nil && other == nil ? nil : tombstones
    }

    /// A redacted accepted/rejected receipt is the durable tombstone for its source.
    /// Only preserve the corresponding item when either backup contains the explicit
    /// independently-retained marker (or a redacted retained history revision).
    private static func seriesItemSourceTombstones(candidates: [WorkspaceSeriesChangeCandidate],
                                                   workspaces: [Workspace]) -> [SeriesItemSourceKey: Bool] {
        var deleted = Set<SeriesItemSourceKey>()
        var retained = Set<SeriesItemSourceKey>()
        for workspace in workspaces {
            for tombstone in workspace.seriesItemSourceTombstones ?? [] {
                deleted.insert(SeriesItemSourceKey(itemID: tombstone.itemID, sessionID: tombstone.sourceSessionID))
            }
            let deletedSessionIDs = Set(workspace.sourceDeletionTombstones ?? [])
            for item in workspace.seriesItems ?? [] {
                let currentKey = SeriesItemSourceKey(itemID: item.id, sessionID: item.sourceSessionID)
                if deletedSessionIDs.contains(item.sourceSessionID) || item.sourceMissing == true {
                    deleted.insert(currentKey)
                }
                if item.sourceMissing == true && item.independentlyRetained == true {
                    retained.insert(currentKey)
                }
                for revision in item.history {
                    let key = SeriesItemSourceKey(itemID: item.id, sessionID: revision.sourceSessionID)
                    if deletedSessionIDs.contains(revision.sourceSessionID) || revision.sourceMissing == true {
                        deleted.insert(key)
                    }
                    if revision.sourceMissing == true { retained.insert(key) }
                }
            }
        }
        for candidate in candidates where candidate.sourceMissing == true {
            guard let itemID = candidate.receipt?.resultItemID else { continue }
            deleted.insert(SeriesItemSourceKey(itemID: itemID, sessionID: candidate.sourceSessionID))
        }
        return Dictionary(uniqueKeysWithValues: deleted.map { ($0, retained.contains($0)) })
    }

    private static func scrubbedDeletedSource(_ item: WorkspaceSeriesItem) -> WorkspaceSeriesItem {
        var updated = item
        updated.evidenceID = ""
        updated.claimRevision = ""
        updated.sourceRevision = ""
        updated.sourceQuote = nil
        updated.location = nil
        updated.transcriptSourceRevision = nil
        updated.sourceMissing = true
        return updated
    }

    private static func scrubbedDeletedSource(_ revision: WorkspaceSeriesItemRevision,
                                             itemID: UUID) -> WorkspaceSeriesItemRevision {
        var updated = revision
        updated.evidenceID = ""
        updated.claimRevision = ""
        updated.sourceRevision = ""
        updated.sourceQuote = nil
        updated.location = nil
        updated.transcriptSourceRevision = nil
        updated.sourceMissing = true
        updated.id = updated.stableID(itemID: itemID)
        return updated
    }

    private static func mergeSeriesCandidates(_ preferred: [WorkspaceSeriesChangeCandidate],
                                              _ other: [WorkspaceSeriesChangeCandidate]) -> [WorkspaceSeriesChangeCandidate] {
        var result = preferred
        for incoming in other {
            guard let index = result.firstIndex(where: { $0.id == incoming.id }) else {
                if incoming.sourceMissing == true {
                    var tombstone = scrubbedDeletedSource(incoming)
                    let receipts = mergedSeriesReceipts([incoming], candidateID: incoming.id)
                    tombstone.receipt = receipts.current
                    tombstone.receiptHistory = receipts.history
                    tombstone.status = receipts.current.map { $0.decision == .reject ? .rejected : .accepted } ?? .superseded
                    result.append(tombstone)
                } else {
                    result.append(incoming)
                }
                continue
            }
            let existing = result[index]
            if existing.sourceMissing == true || incoming.sourceMissing == true {
                let bothTombstoned = existing.sourceMissing == true && incoming.sourceMissing == true
                let deletionCopy = existing.sourceMissing == true ? existing : incoming
                var tombstone = scrubbedDeletedSource(deletionCopy)
                // Merge receipts only when both copies already recorded deletion.
                // A live stale copy cannot replace a deletion with a later review.
                let receipts = mergedSeriesReceipts(bothTombstoned ? [existing, incoming] : [deletionCopy],
                                                    candidateID: existing.id)
                tombstone.receipt = receipts.current
                tombstone.receiptHistory = receipts.history
                tombstone.status = receipts.current.map { $0.decision == .reject ? .rejected : .accepted } ?? .superseded
                result[index] = tombstone
                continue
            }
            let receipts = mergedSeriesReceipts([existing, incoming], candidateID: existing.id)
            if let current = receipts.current {
                var merged = existing
                if incoming.receipt?.operationID == current.operationID { merged = incoming }
                merged.receipt = current
                merged.receiptHistory = receipts.history
                merged.status = current.decision == .reject ? .rejected : .accepted
                result[index] = merged
            } else if incoming.status == .superseded && existing.status == .pending {
                result[index] = incoming
            }
        }
        return result
    }

    private static func mergedSeriesReceipts(
        _ candidates: [WorkspaceSeriesChangeCandidate],
        candidateID: String
    ) -> (current: WorkspaceSeriesChangeReceipt?, history: [WorkspaceSeriesChangeReceipt]?) {
        var byOperationID: [UUID: WorkspaceSeriesChangeReceipt] = [:]
        for candidate in candidates {
            for receipt in (candidate.receiptHistory ?? []) + (candidate.receipt.map { [$0] } ?? [])
                where receipt.candidateID == candidateID {
                if let previous = byOperationID[receipt.operationID] {
                    if seriesReceiptPrecedes(previous, receipt) { byOperationID[receipt.operationID] = receipt }
                } else {
                    byOperationID[receipt.operationID] = receipt
                }
            }
        }
        let ordered = byOperationID.values.sorted(by: seriesReceiptPrecedes)
        guard let current = ordered.last else { return (nil, nil) }
        let history = Array(ordered.dropLast())
        return (current, history.isEmpty ? nil : history)
    }

    /// Orders competing receipts by review time, then stable fields so backup
    /// import order cannot change which decision is current.
    private static func seriesReceiptPrecedes(_ lhs: WorkspaceSeriesChangeReceipt,
                                              _ rhs: WorkspaceSeriesChangeReceipt) -> Bool {
        if lhs.reviewedAt != rhs.reviewedAt { return lhs.reviewedAt < rhs.reviewedAt }
        let leftOperation = lhs.operationID.uuidString.lowercased()
        let rightOperation = rhs.operationID.uuidString.lowercased()
        if leftOperation != rightOperation { return leftOperation < rightOperation }
        if lhs.decision.rawValue != rhs.decision.rawValue { return lhs.decision.rawValue < rhs.decision.rawValue }
        let leftResult = lhs.resultItemID?.uuidString.lowercased() ?? ""
        let rightResult = rhs.resultItemID?.uuidString.lowercased() ?? ""
        if leftResult != rightResult { return leftResult < rightResult }
        return lhs.candidateID < rhs.candidateID
    }

    private static func scrubbedDeletedSource(_ candidate: WorkspaceSeriesChangeCandidate) -> WorkspaceSeriesChangeCandidate {
        var updated = candidate
        updated.text = ""
        updated.owner = nil
        updated.due = nil
        updated.dueDate = nil
        updated.evidenceID = ""
        updated.claimRevision = ""
        updated.sourceRevision = ""
        updated.sourceQuote = nil
        updated.location = nil
        updated.transcriptSourceRevision = nil
        updated.sourceMissing = true
        return updated
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
