import CryptoKit
import Foundation

/// A synchronous, local snapshot for continuing a series. It contains only meetings in this
/// workspace and accepted knowledge or decisions/actions whose current source is still valid.
/// Unreviewed and stale proposals stay outside the active briefing.
public struct SeriesBriefing: Sendable, Equatable {
    public let workspaceID: UUID
    public let workspaceName: String
    public let sessionCount: Int
    public let knowledgeEnabled: Bool
    public let latestSessions: [SessionMeta]
    public let lessons: [WorkspaceLesson]
    public let questionBank: [QuestionBankItem]
    public let pendingCandidates: [WorkspaceKnowledgeCandidate]
    public let decisions: [WorkspaceSeriesItem]
    public let openActions: [WorkspaceSeriesItem]
    public let resolvedActions: [WorkspaceSeriesItem]
    public let pendingSeriesChanges: [WorkspaceSeriesChangeCandidate]

    public init(workspaceID: UUID, workspaceName: String, sessionCount: Int, knowledgeEnabled: Bool,
                latestSessions: [SessionMeta], lessons: [WorkspaceLesson], questionBank: [QuestionBankItem],
                pendingCandidates: [WorkspaceKnowledgeCandidate], decisions: [WorkspaceSeriesItem] = [],
                openActions: [WorkspaceSeriesItem] = [], resolvedActions: [WorkspaceSeriesItem] = [],
                pendingSeriesChanges: [WorkspaceSeriesChangeCandidate] = []) {
        self.workspaceID = workspaceID
        self.workspaceName = workspaceName
        self.sessionCount = sessionCount
        self.knowledgeEnabled = knowledgeEnabled
        self.latestSessions = latestSessions
        self.lessons = lessons
        self.questionBank = questionBank
        self.pendingCandidates = pendingCandidates
        self.decisions = decisions
        self.openActions = openActions
        self.resolvedActions = resolvedActions
        self.pendingSeriesChanges = pendingSeriesChanges
    }
}

public enum SeriesBriefingBuilder {
    public static func build(workspace: Workspace, sessions: [SessionMeta], meetingLimit: Int = 3,
                             knowledgeLimit: Int = 3,
                             evidenceReferences: [SessionEvidenceReference] = []) -> SeriesBriefing {
        let seriesSessions = sessions
            .filter { $0.workspaceID == workspace.id && $0.workspaceDeleted != true }
            .sorted { $0.startedAt > $1.startedAt }
        let recent = Array(seriesSessions.prefix(max(0, meetingLimit)))
        let lessons: [WorkspaceLesson]
        let questionBank: [QuestionBankItem]
        if workspace.useKnowledge {
            lessons = Array((workspace.lessons.filter(\.pinned) + workspace.lessons.filter { !$0.pinned }
                .sorted { $0.createdAt > $1.createdAt }).prefix(max(0, knowledgeLimit)))
            questionBank = Array(workspace.questionBank.sorted {
                ($0.timesAsked, $0.updatedAt) > ($1.timesAsked, $1.updatedAt)
            }.prefix(max(0, knowledgeLimit)))
        } else {
            lessons = []
            questionBank = []
        }
        let validSessionIDs = Set(seriesSessions.map(\.id))
        let reviewed = Dictionary(evidenceReferences.compactMap { reference -> (String, SessionEvidenceReference)? in
            guard reference.reviewDecision == .supported, !reference.reviewIsStale,
                  let claimRevision = reference.claimRevision,
                  reference.id.count > 0 else { return nil }
            return (reference.id + "|" + claimRevision + "|" + (reference.sourceRevision ?? ""), reference)
        }, uniquingKeysWith: { _, latest in latest })
        let activeItems = workspace.useKnowledge ? (workspace.seriesItems ?? []).filter { item in
            guard validSessionIDs.contains(item.sourceSessionID) else { return false }
            let key = item.evidenceID + "|" + item.claimRevision + "|" + item.sourceRevision
            guard let reference = reviewed[key], reference.lineID != nil else { return false }
            return true
        } : []
        let decisions = activeItems.filter { $0.kind == .decision }
            .sorted { $0.updatedAt > $1.updatedAt }
        let openActions = activeItems.filter {
            $0.kind == .action && ($0.actionState == nil || $0.actionState == .open || $0.actionState == .inProgress)
        }.sorted(by: actionOrder)
        let resolvedActions = activeItems.filter {
            $0.kind == .action && ($0.actionState == .done || $0.actionState == .cancelled || $0.actionState == .superseded)
        }.sorted { $0.updatedAt > $1.updatedAt }
        let pending = workspace.useKnowledge ? (workspace.knowledgeCandidates ?? [])
            .filter { $0.status == .pending && $0.sourceSessionID.map(validSessionIDs.contains) ?? true }
            .sorted { $0.createdAt > $1.createdAt } : []
        let pendingSeries = workspace.useKnowledge ? (workspace.seriesChangeCandidates ?? [])
            .filter { candidate in
                guard candidate.status == .pending, validSessionIDs.contains(candidate.sourceSessionID) else { return false }
                guard let reference = reviewed[candidate.evidenceID + "|" + candidate.claimRevision + "|" + candidate.sourceRevision]
                else { return false }
                return WorkspaceSeriesChangeMerger.matchesCurrentEvidence(candidate, reference: reference)
            }
            .sorted { $0.createdAt > $1.createdAt } : []
        return SeriesBriefing(workspaceID: workspace.id, workspaceName: workspace.name,
                              sessionCount: seriesSessions.count, knowledgeEnabled: workspace.useKnowledge,
                              latestSessions: recent, lessons: lessons, questionBank: questionBank,
                              pendingCandidates: pending, decisions: decisions, openActions: openActions,
                              resolvedActions: resolvedActions,
                              pendingSeriesChanges: pendingSeries)
    }

    private static func actionOrder(_ lhs: WorkspaceSeriesItem, _ rhs: WorkspaceSeriesItem) -> Bool {
        switch (lhs.dueDate, rhs.dueDate) {
        case let (left?, right?) where left != right: return left < right
        case (_?, nil): return true
        case (nil, _?): return false
        default: return lhs.updatedAt > rhs.updatedAt
        }
    }
}

/// Turns only explicitly supported, current note evidence into inert series proposals.
/// The next-meeting brief reads accepted items, never the proposal queue.
public enum WorkspaceSeriesChangeMerger {
    /// Confirms that a queued proposal still matches the live reviewed claim and
    /// its owner/deadline details. A note can change those fields without changing
    /// the transcript line revision, so compare them separately.
    public static func matchesCurrentEvidence(_ candidate: WorkspaceSeriesChangeCandidate,
                                              reference: SessionEvidenceReference) -> Bool {
        guard reference.origin == .textNotes,
              reference.reviewDecision == .supported, !reference.reviewIsStale,
              reference.lineID != nil,
              reference.claimRevision == candidate.claimRevision,
              reference.sourceRevision == candidate.sourceRevision,
              let kind = seriesKind(for: reference) else { return false }
        return reference.id == candidate.evidenceID && reference.claim == candidate.text
            && reference.owner == candidate.owner && reference.due == candidate.due
            && reference.dueDate == candidate.dueDate && reference.quote == candidate.sourceQuote
            && reference.location == candidate.location && kind == candidate.kind
    }

    @discardableResult
    public static func collectCandidates(from evidence: [SessionEvidenceReference], sessionID: UUID,
                                         into workspace: inout Workspace, now: Date = Date()) -> [String] {
        guard workspace.useKnowledge else { return [] }
        var candidates = workspace.seriesChangeCandidates ?? []
        var added: [String] = []
        for reference in evidence {
            guard reference.origin == .textNotes,
                  reference.reviewDecision == .supported, !reference.reviewIsStale,
                  reference.lineID != nil, let sourceRevision = reference.sourceRevision,
                  let claimRevision = reference.claimRevision,
                  let kind = seriesKind(for: reference) else { continue }
            let id = candidateID(sessionID: sessionID, evidenceID: reference.id,
                                 claimRevision: claimRevision, sourceRevision: sourceRevision,
                                 text: reference.claim, owner: reference.owner, due: reference.due,
                                 dueDate: reference.dueDate, quote: reference.quote, location: reference.location)
            guard !candidates.contains(where: { $0.id == id }) else { continue }
            // A regenerated proposal for the same note can carry a revised owner or
            // deadline even when the claim text is unchanged. Keep the prior draft in
            // history, but make only the latest proposal actionable.
            for index in candidates.indices where candidates[index].status == .pending
                && candidates[index].sourceSessionID == sessionID
                && candidates[index].evidenceID == reference.id {
                candidates[index].status = .superseded
            }
            candidates.append(WorkspaceSeriesChangeCandidate(
                id: id, kind: kind, text: reference.claim, owner: reference.owner, due: reference.due,
                dueDate: reference.dueDate, sourceSessionID: sessionID, evidenceID: reference.id,
                claimRevision: claimRevision, sourceRevision: sourceRevision, sourceQuote: reference.quote,
                location: reference.location, createdAt: now))
            added.append(id)
        }
        if !added.isEmpty {
            workspace.seriesChangeCandidates = candidates
            workspace.updatedAt = now
        }
        return added
    }

    /// Apply an explicit add, replace, or reject. A replace checks the revision seen by
    /// the reviewer and retains the prior decision/action as an immutable history entry.
    @discardableResult
    public static func applyCandidate(_ candidateID: String, decision: WorkspaceSeriesChangeDecision,
                                      operationID: UUID, targetItemID: UUID? = nil, expectedRevision: Int? = nil,
                                      reviewedDetails: WorkspaceSeriesChangeReviewDetails? = nil,
                                      into workspace: inout Workspace, now: Date = Date()) -> WorkspaceSeriesChangeReceipt? {
        guard var candidates = workspace.seriesChangeCandidates,
              let candidateIndex = candidates.firstIndex(where: { $0.id == candidateID }) else { return nil }
        let candidate = candidates[candidateIndex]
        if let receipt = candidate.receipt { return receipt }
        guard candidate.status == .pending else { return nil }
        guard decision == .reject || workspace.useKnowledge else { return nil }

        var resultItemID: UUID?
        let owner = reviewedDetails.map(\.owner) ?? candidate.owner
        let due = reviewedDetails.map(\.due) ?? candidate.due
        // If the person changes the original deadline wording, discard a derived date
        // rather than keep a potentially contradictory interpretation.
        let dueDate = reviewedDetails == nil || reviewedDetails?.due == candidate.due ? candidate.dueDate : nil
        switch decision {
        case .reject:
            candidates[candidateIndex].status = .rejected
        case .add:
            var items = workspace.seriesItems ?? []
            let item = WorkspaceSeriesItem(kind: candidate.kind, text: candidate.text, owner: owner,
                                            due: due, dueDate: dueDate,
                                            actionState: candidate.kind == .action ? .open : nil,
                                            sourceSessionID: candidate.sourceSessionID, evidenceID: candidate.evidenceID,
                                            claimRevision: candidate.claimRevision, sourceRevision: candidate.sourceRevision,
                                            sourceQuote: candidate.sourceQuote, location: candidate.location,
                                            confirmedAt: now, updatedAt: now)
            items.append(item)
            workspace.seriesItems = items
            resultItemID = item.id
        case .replace:
            guard let targetItemID, let expectedRevision,
                  var items = workspace.seriesItems,
                  let index = items.firstIndex(where: { $0.id == targetItemID }),
                  items[index].kind == candidate.kind,
                  items[index].revision == expectedRevision else { return nil }
            var item = items[index]
            item.history.append(item.currentRevision)
            item.text = candidate.text
            item.owner = owner
            item.due = due
            item.dueDate = dueDate
            item.sourceSessionID = candidate.sourceSessionID
            item.evidenceID = candidate.evidenceID
            item.claimRevision = candidate.claimRevision
            item.sourceRevision = candidate.sourceRevision
            item.sourceQuote = candidate.sourceQuote
            item.location = candidate.location
            item.revision += 1
            // A changed commitment is open again until a user advances its state.
            if item.kind == .action { item.actionState = .open }
            item.confirmedAt = now
            item.updatedAt = now
            items[index] = item
            workspace.seriesItems = items
            resultItemID = item.id
        }

        let receipt = WorkspaceSeriesChangeReceipt(operationID: operationID, candidateID: candidate.id,
                                                   decision: decision, reviewedAt: now, resultItemID: resultItemID)
        candidates[candidateIndex].receipt = receipt
        if decision != .reject { candidates[candidateIndex].status = .accepted }
        workspace.seriesChangeCandidates = candidates
        workspace.updatedAt = now
        return receipt
    }

    @discardableResult
    public static func setActionState(_ newState: WorkspaceSeriesItem.ActionState, itemID: UUID,
                                      expectedRevision: Int, into workspace: inout Workspace,
                                      now: Date = Date()) -> Bool {
        guard var items = workspace.seriesItems,
              let index = items.firstIndex(where: { $0.id == itemID }),
              items[index].kind == .action,
              items[index].revision == expectedRevision,
              items[index].actionState != newState else { return false }
        var item = items[index]
        item.history.append(item.currentRevision)
        item.actionState = newState
        item.revision += 1
        item.updatedAt = now
        items[index] = item
        workspace.seriesItems = items
        workspace.updatedAt = now
        return true
    }

    private static func seriesKind(for reference: SessionEvidenceReference) -> WorkspaceSeriesItem.Kind? {
        if reference.kind == .actions { return .action }
        guard reference.kind == .items else { return nil }
        let components = reference.id.split(separator: ":", omittingEmptySubsequences: false)
        let sectionID = components.count > 1 ? String(components[1]).lowercased() : ""
        let title = reference.title.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return sectionID == "decisions" || sectionID == "decision" || title == "决定" || title == "decisions"
            ? .decision : nil
    }

    private static func candidateID(sessionID: UUID, evidenceID: String,
                                    claimRevision: String, sourceRevision: String, text: String,
                                    owner: String?, due: String?, dueDate: Date?, quote: String?, location: String?) -> String {
        func component(_ value: String?) -> String {
            let value = value ?? ""
            return "\(value.utf8.count):\(value)"
        }
        let dueDateBits = dueDate.map { String($0.timeIntervalSince1970.bitPattern, radix: 16) }
        let source = [sessionID.uuidString.lowercased(), evidenceID, claimRevision, sourceRevision, text,
                      owner, due, dueDateBits, quote, location].map(component).joined(separator: "|")
        return SHA256.hash(data: Data(source.utf8)).map { String(format: "%02x", $0) }.joined()
    }
}
