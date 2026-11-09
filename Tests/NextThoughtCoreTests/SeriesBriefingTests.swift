import Foundation
import Testing
@testable import NextThoughtCore

struct SeriesBriefingTests {
    @Test func briefingUsesOnlyThisSeriesAndReturnsTheNewestThreeMeetings() {
        let workspace = Workspace(name: "研究组会", mode: .labMeeting,
                                  lessons: [WorkspaceLesson(text: "保留已保存的经验", pinned: true)],
                                  questionBank: [QuestionBankItem(question: "为什么延期？", answer: "等待数据")])
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let seriesSessions = (0..<4).map { index in
            SessionMeta(startedAt: start.addingTimeInterval(Double(index * 100)), mode: .labMeeting,
                        title: "第\(index + 1)场", workspaceID: workspace.id)
        }
        let unrelated = SessionMeta(startedAt: start.addingTimeInterval(900), mode: .meeting, title: "外部", workspaceID: UUID())

        let briefing = SeriesBriefingBuilder.build(workspace: workspace, sessions: seriesSessions + [unrelated])

        #expect(briefing.sessionCount == 4)
        #expect(briefing.latestSessions.map(\.title) == ["第4场", "第3场", "第2场"])
        #expect(briefing.lessons.map(\.text) == ["保留已保存的经验"])
        #expect(briefing.questionBank.map(\.answer) == ["等待数据"])
        #expect(!briefing.latestSessions.contains(where: { $0.id == unrelated.id }))
    }

    @Test func disabledSeriesKnowledgeIsNotIncludedInTheBriefingOrMeetingCarry() {
        let workspace = Workspace(name: "面试", mode: .interview,
                                  lessons: [WorkspaceLesson(text: "只在本系列复用")],
                                  questionBank: [QuestionBankItem(question: "如何选择？", answer: "比较依据")],
                                  useKnowledge: false)
        let briefing = SeriesBriefingBuilder.build(workspace: workspace, sessions: [])
        let snapshot = MeetingCarry.standard.workspaceSnapshot(from: workspace, baselineDocumentIDs: [])

        #expect(!briefing.knowledgeEnabled)
        #expect(briefing.lessons.isEmpty)
        #expect(briefing.questionBank.isEmpty)
        #expect(snapshot.lessons.isEmpty)
        #expect(snapshot.questionBank.isEmpty)
    }

    @Test func f07ThreeMeetingsKeepTheCurrentDecisionAndNeverResurrectItsOldRevision() throws {
        var workspace = Workspace(name: "项目交付", mode: .meeting)
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let friday = SessionMeta(startedAt: start, mode: .meeting, title: "第一次讨论", workspaceID: workspace.id)
        let monday = SessionMeta(startedAt: start.addingTimeInterval(86_400), mode: .meeting,
                                 title: "数据到齐", workspaceID: workspace.id)
        let next = SessionMeta(startedAt: start.addingTimeInterval(172_800), mode: .meeting,
                               title: "下一次", workspaceID: workspace.id)
        let firstEvidence = evidence(id: "notes:decisions:friday", claim: "暂定周五提交，等待外部数据确认。",
                                     quote: "先按周五安排，等数据确认后再定。", lineID: UUID(),
                                     sourceRevision: "line-friday-v1", claimRevision: "claim-friday-v1")

        let firstIDs = WorkspaceSeriesChangeMerger.collectCandidates(from: [firstEvidence], sessionID: friday.id,
                                                                     into: &workspace, now: start)
        #expect(firstIDs.count == 1)
        #expect(WorkspaceSeriesChangeMerger.collectCandidates(from: [firstEvidence], sessionID: friday.id,
                                                              into: &workspace, now: start).isEmpty)
        let firstID = try #require(firstIDs.first)
        let firstCandidate = try #require(workspace.seriesChangeCandidates?.first(where: { $0.id == firstID }))
        let firstReceipt = try #require(WorkspaceSeriesChangeMerger.applyCandidate(
            firstCandidate.id, decision: .add, operationID: UUID(), into: &workspace, now: start))
        let replay = WorkspaceSeriesChangeMerger.applyCandidate(
            firstCandidate.id, decision: .add, operationID: UUID(), into: &workspace,
            now: start.addingTimeInterval(1))
        #expect(replay == firstReceipt)
        #expect(workspace.seriesItems?.count == 1)

        let secondEvidence = evidence(id: "notes:decisions:monday", claim: "正式改为下周一提交，外部数据已到齐。",
                                      quote: "数据今天到齐了，正式改到下周一。", lineID: UUID(),
                                      sourceRevision: "line-monday-v1", claimRevision: "claim-monday-v1")
        let secondIDs = WorkspaceSeriesChangeMerger.collectCandidates(from: [secondEvidence], sessionID: monday.id,
                                                                      into: &workspace, now: start.addingTimeInterval(86_400))
        let secondID = try #require(secondIDs.first)
        let secondCandidate = try #require(workspace.seriesChangeCandidates?.first(where: { $0.id == secondID }))
        let itemID = try #require(workspace.seriesItems?.first?.id)
        let changed = WorkspaceSeriesChangeMerger.applyCandidate(
            secondCandidate.id, decision: .replace, operationID: UUID(), targetItemID: itemID,
            expectedRevision: 1, into: &workspace, now: start.addingTimeInterval(86_400))
        #expect(changed?.decision == .replace)
        #expect(workspace.seriesItems?.first?.text == "正式改为下周一提交，外部数据已到齐。")
        #expect(workspace.seriesItems?.first?.revision == 2)
        #expect(workspace.seriesItems?.first?.history.map(\.text) == ["暂定周五提交，等待外部数据确认。"])

        let briefing = SeriesBriefingBuilder.build(workspace: workspace, sessions: [friday, monday, next],
                                                   evidenceReferences: [firstEvidence, secondEvidence])
        #expect(briefing.sessionCount == 3)
        #expect(briefing.latestSessions.map(\.id) == [next.id, monday.id, friday.id])
        #expect(briefing.decisions.map(\.text) == ["正式改为下周一提交，外部数据已到齐。"])
        #expect(briefing.decisions.first?.history.map(\.text) == ["暂定周五提交，等待外部数据确认。"])

        // Removing the meeting that supplied the current version hides the item;
        // it never falls back to the older Friday revision in its history.
        let afterSourceDeletion = SeriesBriefingBuilder.build(workspace: workspace, sessions: [friday, next],
                                                              evidenceReferences: [firstEvidence, secondEvidence])
        #expect(afterSourceDeletion.decisions.isEmpty)
        #expect(!afterSourceDeletion.decisions.contains(where: { $0.text.contains("周五") }))

        workspace.useKnowledge = false
        let disabled = SeriesBriefingBuilder.build(workspace: workspace, sessions: [friday, monday, next],
                                                   evidenceReferences: [firstEvidence, secondEvidence])
        #expect(disabled.decisions.isEmpty)
    }

    @Test func reviewingOnTheLatestWorkspacePreservesConcurrentActionProgress() throws {
        var workspace = Workspace(name: "项目交付", mode: .meeting)
        let sessionID = UUID()
        let existingAction = WorkspaceSeriesItem(kind: .action, text: "发送数据请求", owner: "A", due: "周五",
                                                  sourceSessionID: sessionID, evidenceID: "action-existing",
                                                  claimRevision: "claim-action", sourceRevision: "line-action")
        workspace.seriesItems = [existingAction]
        let decision = evidence(id: "notes:decisions:delivery", claim: "正式改为下周一提交。",
                                sourceRevision: "line-decision", claimRevision: "claim-decision")
        let candidateID = try #require(WorkspaceSeriesChangeMerger.collectCandidates(
            from: [decision], sessionID: sessionID, into: &workspace).first)
        let candidate = try #require(workspace.seriesChangeCandidates?.first(where: { $0.id == candidateID }))

        // The review started with an older snapshot; while it awaited source reads,
        // a separate action update committed to the workspace.
        var latestWorkspace = workspace
        #expect(WorkspaceSeriesChangeMerger.setActionState(.done, itemID: existingAction.id,
                                                           expectedRevision: 1, into: &latestWorkspace))
        let receipt = WorkspaceSeriesChangeMerger.applyCandidate(
            candidate.id, decision: .add, operationID: UUID(), into: &latestWorkspace)

        #expect(receipt?.decision == .add)
        #expect(latestWorkspace.seriesItems?.first(where: { $0.id == existingAction.id })?.actionState == .done)
        #expect(latestWorkspace.seriesItems?.contains(where: { $0.text == "正式改为下周一提交。" }) == true)
    }

    @Test func staleConcurrentDecisionReplacementCannotOverwriteTheNewerRevision() throws {
        var workspace = Workspace(name: "项目交付", mode: .meeting)
        let firstSession = UUID(), secondSession = UUID(), thirdSession = UUID()
        var item = WorkspaceSeriesItem(kind: .decision, text: "暂定周五提交", sourceSessionID: firstSession,
                                       evidenceID: "decision-old", claimRevision: "claim-old", sourceRevision: "line-old")
        workspace.seriesItems = [item]
        let monday = evidence(id: "notes:decisions:monday", claim: "改到下周一。",
                              sourceRevision: "line-monday", claimRevision: "claim-monday")
        let tuesday = evidence(id: "notes:decisions:tuesday", claim: "改到下周二。",
                               sourceRevision: "line-tuesday", claimRevision: "claim-tuesday")
        let mondayID = try #require(WorkspaceSeriesChangeMerger.collectCandidates(
            from: [monday], sessionID: secondSession, into: &workspace).first)
        let tuesdayID = try #require(WorkspaceSeriesChangeMerger.collectCandidates(
            from: [tuesday], sessionID: thirdSession, into: &workspace).first)

        _ = WorkspaceSeriesChangeMerger.applyCandidate(mondayID, decision: .replace, operationID: UUID(),
                                                        targetItemID: item.id, expectedRevision: 1,
                                                        into: &workspace)
        let staleAttempt = WorkspaceSeriesChangeMerger.applyCandidate(tuesdayID, decision: .replace, operationID: UUID(),
                                                                      targetItemID: item.id, expectedRevision: 1,
                                                                      into: &workspace)
        item = try #require(workspace.seriesItems?.first)
        #expect(staleAttempt == nil)
        #expect(item.text == "改到下周一。")
        #expect(item.revision == 2)
        #expect(workspace.seriesChangeCandidates?.first(where: { $0.id == tuesdayID })?.status == .pending)
        #expect(workspace.seriesChangeCandidates?.first(where: { $0.id == tuesdayID })?.receipt == nil)
    }

    @Test func actionOwnerAndRelativeDeadlineAreConfirmedAndCompletionCanBeUndone() throws {
        var workspace = Workspace(name: "项目交付", mode: .meeting)
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let session = SessionMeta(startedAt: start, mode: .meeting, title: "项目会", workspaceID: workspace.id)
        let action = evidence(id: "notes:actions:data-request", title: "待办", claim: "向外部团队发送数据请求。",
                              quote: "我今天会把数据请求发过去。", lineID: UUID(),
                              sourceRevision: "line-action-v1", claimRevision: "claim-action-v1",
                              kind: .actions, owner: "未确认", due: "下周一 15:00（悉尼时间）")
        let ids = WorkspaceSeriesChangeMerger.collectCandidates(from: [action], sessionID: session.id,
                                                                into: &workspace, now: start)
        let candidateID = try #require(ids.first)
        let candidate = try #require(workspace.seriesChangeCandidates?.first(where: { $0.id == candidateID }))
        let reviewDetails = WorkspaceSeriesChangeReviewDetails(owner: "Shiyin", due: "周一 16:00（悉尼时间）")
        let receipt = WorkspaceSeriesChangeMerger.applyCandidate(
            candidate.id, decision: .add, operationID: UUID(), reviewedDetails: reviewDetails,
            into: &workspace, now: start)
        #expect(receipt?.decision == .add)
        var item = try #require(workspace.seriesItems?.first)
        #expect(item.owner == "Shiyin")
        #expect(item.due == "周一 16:00（悉尼时间）")
        #expect(item.dueDate == nil) // Edited wording does not keep a mismatched parsed date.

        #expect(WorkspaceSeriesChangeMerger.setActionState(.done, itemID: item.id, expectedRevision: 1,
                                                           into: &workspace, now: start.addingTimeInterval(10)))
        item = try #require(workspace.seriesItems?.first)
        #expect(item.actionState == .done)
        #expect(item.history.map(\.actionState) == [.open])
        var briefing = SeriesBriefingBuilder.build(workspace: workspace, sessions: [session],
                                                   evidenceReferences: [action])
        #expect(briefing.openActions.isEmpty)
        #expect(briefing.resolvedActions.map(\.id) == [item.id])

        #expect(WorkspaceSeriesChangeMerger.setActionState(.open, itemID: item.id, expectedRevision: 2,
                                                           into: &workspace, now: start.addingTimeInterval(20)))
        item = try #require(workspace.seriesItems?.first)
        #expect(item.actionState == .open)
        #expect(item.due == "周一 16:00（悉尼时间）")
        #expect(item.history.map(\.actionState) == [.open, .done])
        #expect(!WorkspaceSeriesChangeMerger.setActionState(.open, itemID: item.id, expectedRevision: 3,
                                                            into: &workspace, now: start.addingTimeInterval(30)))
        briefing = SeriesBriefingBuilder.build(workspace: workspace, sessions: [session],
                                               evidenceReferences: [action])
        #expect(briefing.openActions.map(\.id) == [item.id])
        #expect(briefing.resolvedActions.isEmpty)
    }

    @Test func onlyExplicitlySupportedCurrentEvidenceCanSeedSeriesChanges() {
        var workspace = Workspace(name: "项目交付", mode: .meeting)
        let session = SessionMeta(startedAt: Date(), mode: .meeting, workspaceID: workspace.id)
        let sessionID = session.id
        let unsupported = evidence(id: "unsupported", claim: "周五提交", lineID: UUID(), sourceRevision: "line-1",
                                   claimRevision: "claim-1", reviewDecision: nil)
        let rejected = evidence(id: "rejected", claim: "周五提交", lineID: UUID(), sourceRevision: "line-2",
                                claimRevision: "claim-2", reviewDecision: .unsupported)
        let stale = evidence(id: "stale", claim: "周五提交", lineID: UUID(), sourceRevision: "line-3",
                             claimRevision: "claim-3", reviewIsStale: true)
        let unlocated = evidence(id: "unlocated", claim: "周五提交", lineID: nil, sourceRevision: nil,
                                 claimRevision: "claim-4")
        let notDecision = evidence(id: "not-a-decision", title: "议题", claim: "周五提交", lineID: UUID(),
                                   sourceRevision: "line-5", claimRevision: "claim-5")
        let structured = evidence(id: "structured", claim: "周五提交", lineID: UUID(), sourceRevision: "line-6",
                                 claimRevision: "claim-6", origin: .structuredNotes)
        let changedWorkspace = WorkspaceSeriesChangeMerger.collectCandidates(
            from: [unsupported, rejected, stale, unlocated, notDecision, structured], sessionID: sessionID,
            into: &workspace)
        #expect(changedWorkspace.isEmpty)
        #expect(workspace.seriesChangeCandidates == nil)
    }

    @Test func changedOwnerOrDeadlineSupersedesTheOldPendingProposal() throws {
        var workspace = Workspace(name: "项目交付", mode: .meeting)
        let session = SessionMeta(startedAt: Date(), mode: .meeting, workspaceID: workspace.id)
        let sessionID = session.id
        let old = evidence(id: "notes:actions:task", title: "待办", claim: "提交报告。", lineID: UUID(),
                           sourceRevision: "line-1", claimRevision: "claim-1", kind: .actions,
                           owner: "A", due: "周五")
        let oldID = try #require(WorkspaceSeriesChangeMerger.collectCandidates(from: [old], sessionID: sessionID,
                                                                              into: &workspace).first)
        let current = evidence(id: old.id, title: old.title, claim: old.claim, lineID: old.lineID,
                               sourceRevision: old.sourceRevision, claimRevision: old.claimRevision,
                               kind: .actions, owner: "B", due: "周一")
        let currentID = try #require(WorkspaceSeriesChangeMerger.collectCandidates(from: [current], sessionID: sessionID,
                                                                                  into: &workspace).first)
        #expect(currentID != oldID)
        #expect(workspace.seriesChangeCandidates?.first(where: { $0.id == oldID })?.status == .superseded)
        #expect(workspace.seriesChangeCandidates?.first(where: { $0.id == currentID })?.status == .pending)
        let briefing = SeriesBriefingBuilder.build(
            workspace: workspace,
            sessions: [session],
            evidenceReferences: [current])
        #expect(briefing.pendingSeriesChanges.map(\.id) == [currentID])
    }

    @Test func seriesHistoryAndSupersededProposalsSurviveIdempotentBackupMerge() throws {
        let workspaceID = UUID()
        let itemID = UUID()
        let firstSession = UUID()
        let secondSession = UUID()
        let start = Date(timeIntervalSince1970: 1_800_000_000)
        let first = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "周五", sourceSessionID: firstSession,
                                        evidenceID: "e1", claimRevision: "c1", sourceRevision: "s1",
                                        confirmedAt: start, updatedAt: start)
        var second = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "周一", sourceSessionID: secondSession,
                                         evidenceID: "e2", claimRevision: "c2", sourceRevision: "s2",
                                         revision: 2, confirmedAt: start.addingTimeInterval(10),
                                         updatedAt: start.addingTimeInterval(10))
        second.history = [first.currentRevision]
        let oldCandidate = WorkspaceSeriesChangeCandidate(id: "same", kind: .action, text: "提交",
                                                          sourceSessionID: firstSession, evidenceID: "a1",
                                                          claimRevision: "ac1", sourceRevision: "as1", status: .pending)
        var supersededCandidate = oldCandidate
        supersededCandidate.status = .superseded
        let newer = Workspace(id: workspaceID, name: "项目交付", mode: .meeting,
                              seriesItems: [second], seriesChangeCandidates: [supersededCandidate],
                              updatedAt: start.addingTimeInterval(20))
        let older = Workspace(id: workspaceID, name: "项目交付", mode: .meeting,
                              seriesItems: [first], seriesChangeCandidates: [oldCandidate], updatedAt: start)

        let merged = try #require(BackupMerge.workspaces(existing: [newer], incoming: [older]).merged.first)
        #expect(merged.seriesItems?.first?.text == "周一")
        #expect(merged.seriesItems?.first?.history.map(\.text) == ["周五"])
        #expect(merged.seriesChangeCandidates?.first?.status == .superseded)
        let replayed = try #require(BackupMerge.workspaces(existing: [merged], incoming: [older]).merged.first)
        #expect(replayed.seriesItems?.first?.history.filter { $0.text == "周五" }.count == 1)
        #expect(replayed.seriesChangeCandidates?.first?.status == .superseded)
    }

    @Test func oldWorkspaceWithoutSeriesFieldsStillDecodesWithItsStableKnowledgeIDs() throws {
        let lesson = WorkspaceLesson(text: "经验")
        let question = QuestionBankItem(question: "如何选？", answer: "看依据")
        let workspace = Workspace(name: "旧系列", mode: .meeting, lessons: [lesson], questionBank: [question])
        let encoded = try JSONEncoder().encode(workspace)
        var object = try #require(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        object.removeValue(forKey: "seriesItems")
        object.removeValue(forKey: "seriesChangeCandidates")
        let legacyData = try JSONSerialization.data(withJSONObject: object)

        let decoded = try JSONDecoder().decode(Workspace.self, from: legacyData)
        #expect(decoded.id == workspace.id)
        #expect(decoded.lessons.first?.id == lesson.id)
        #expect(decoded.questionBank.first?.id == question.id)
        #expect(decoded.seriesItems == nil)
        #expect(decoded.seriesChangeCandidates == nil)
    }

    private func evidence(id: String, title: String = "决定", claim: String,
                          quote: String? = "这句原话能定位。", lineID: UUID? = UUID(),
                          sourceRevision: String? = "line-revision", claimRevision: String? = "claim-revision",
                          kind: NotesSectionKind? = .items, owner: String? = nil, due: String? = nil,
                          dueDate: Date? = nil, reviewDecision: SessionEvidenceDecision? = .supported,
                          reviewIsStale: Bool = false,
                          origin: SessionEvidenceOrigin = .textNotes) -> SessionEvidenceReference {
        SessionEvidenceReference(id: id, title: title, claim: claim, quote: quote, location: "00:12",
                                 lineID: lineID, sourceRevision: sourceRevision, sourceText: quote,
                                 support: .quoted, origin: origin, kind: kind, owner: owner, due: due,
                                 dueDate: dueDate, claimRevision: claimRevision,
                                 transcriptSourceRevision: "transcript-revision", sourceStatus: .current,
                                 reviewDecision: reviewDecision, reviewIsStale: reviewIsStale)
    }
}
