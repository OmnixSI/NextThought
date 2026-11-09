import Foundation
import Testing
@testable import NextThoughtCore

struct WorkspaceTests {
    let now = Date(timeIntervalSince1970: 1_900_000_000)

    @Test func modesHaveProfilesAndOldValuesStillDecode() throws {
        for mode in SessionMode.allCases {
            let profile = ModeProfile.for(mode)
            #expect(!profile.fields.isEmpty)
            #expect(!profile.documentCategories.isEmpty)
            #expect(!profile.templatePreferences.isEmpty)
        }
        let decoded = try JSONDecoder().decode([SessionMode].self, from: Data(#"["meeting","interview","lesson","futuremode"]"#.utf8))
        #expect(decoded == [.meeting, .interview, .lesson, .general])
        #expect(SessionMode.meeting.title == "工作会议")
    }

    @Test func legacyDefaultAnswerRequirementsFollowModeChanges() {
        let oldDefaults: [(SessionMode, String)] = [
            (.meeting, "我是会议参与者。回答聚焦决策、风险和下一步行动，给出可以直接说出口的简短发言；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。"),
            (.client, "我是我方代表，正在和客户沟通。回答聚焦客户的需求和顾虑，给出可以直接说出口的简短回应；涉及价格、时间和承诺时以资料为准，不确定的说“会后确认”，不替公司做资料里没有的承诺。"),
            (.general, "直接回答当前问题：先给一句可以说出口的结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。"),
        ]
        for (mode, oldValue) in oldDefaults {
            let profile = ModeProfile.for(mode)
            #expect(profile.matchesDefaultTemplatePreferences(oldValue))
            #expect(profile.matchesDefaultTemplatePreferences(profile.templatePreferences))
            #expect(!profile.matchesDefaultTemplatePreferences("我自己的回答要求"))
        }
    }

    @Test func workspaceDecodesOlderFilesAndBuildsMaterial() throws {
        let id = UUID()
        let json = #"{"id":"\#(id.uuidString)","name":"字节二面","mode":"interview"}"#
        var workspace = try JSONDecoder().decode(Workspace.self, from: Data(json.utf8))
        #expect(workspace.name == "字节二面")
        #expect(workspace.useKnowledge)
        workspace.fields = ["company": "字节跳动", "position": "后端工程师", "legacy": "旧字段"]
        workspace.notes = "重点准备缓存"
        let text = workspace.materialText
        #expect(text.hasPrefix("应聘公司：字节跳动\n岗位：后端工程师"))
        #expect(text.contains("legacy：旧字段"))
        #expect(text.hasSuffix("重点准备缓存"))
    }

    @Test func questionBankIsSearchableAndCategoriesLabelSources() {
        var workspace = Workspace(name: "面试", mode: .interview)
        workspace.documents = [ContextDocument(name: "resume.pdf", pages: ["负责订单服务 Redis 缓存层"], category: "简历")]
        workspace.questionBank = [QuestionBankItem(question: "缓存和数据库怎么保证一致", answer: "先更新数据库再删除缓存，binlog 兜底")]
        let index = SessionContextIndex(documents: workspace.retrievalDocuments)
        let hits = index.retrieve(question: "你们缓存一致性怎么保证")
        #expect(hits.contains { $0.source == "[题库] 题库（历次复盘）" })
        #expect(index.retrieve(question: "Redis 缓存层").first?.source == "[简历] resume.pdf")
        workspace.useKnowledge = false
        #expect(workspace.retrievalDocuments.count == 1)
        #expect(workspace.experienceNotes().isEmpty)
    }

    @Test func deletingMeetingSourcesRemovesDependentKnowledgeAndKeepsOtherSources() throws {
        let deletedSession = UUID(), remainingSession = UUID(), unrelatedSession = UUID()
        let deletedRevision = WorkspaceAnswerRevision(answer: "已删除来源旧答法", recordedAt: now,
                                                      sourceSessionID: deletedSession)
        let remainingRevision = WorkspaceAnswerRevision(answer: "仍有效的历史答法", recordedAt: now,
                                                         sourceSessionID: remainingSession)
        let friday = WorkspaceSeriesItemRevision(revision: 1, kind: .decision, text: "暂定周五",
                                                 sourceSessionID: deletedSession, evidenceID: "friday",
                                                 claimRevision: "claim-friday", sourceRevision: "line-friday",
                                                 sourceQuote: "周五原话", location: "00:12")
        var workspace = Workspace(name: "项目交付", mode: .meeting,
                                  lessons: [
                                    WorkspaceLesson(text: "只在被删会议中总结的经验", sourceSessionID: deletedSession),
                                    WorkspaceLesson(text: "多场会议共同支持的经验", sourceSessionID: deletedSession,
                                                    sourceSessionIDs: [deletedSession, remainingSession])
                                  ],
                                  questionBank: [
                                    QuestionBankItem(question: "被删会议独有的问题？", answer: "被删会议独有答法",
                                                     sourceSessionID: deletedSession,
                                                     sourceSessionIDs: [deletedSession],
                                                     answerSourceSessionID: deletedSession),
                                    QuestionBankItem(question: "多场会议重复提问？", answer: "由被删会议提供的答法",
                                                     timesAsked: 2, sourceSessionID: deletedSession,
                                                     sourceSessionIDs: [deletedSession, remainingSession],
                                                     answerSourceSessionID: deletedSession,
                                                     answerHistory: [deletedRevision, remainingRevision]),
                                    QuestionBankItem(question: "仍有效的问题？", answer: "有效来源的答法",
                                                     timesAsked: 2, sourceSessionID: deletedSession,
                                                     sourceSessionIDs: [deletedSession, remainingSession],
                                                     answerSourceSessionID: remainingSession)
                                  ],
                                  knowledgeCandidates: [
                                    WorkspaceKnowledgeCandidate(id: "deleted", logicalItemID: "deleted", kind: .lesson,
                                                                text: "待审阅经验", sourceSessionID: deletedSession),
                                    WorkspaceKnowledgeCandidate(id: "other", logicalItemID: "other", kind: .lesson,
                                                                text: "其他会议经验", sourceSessionID: unrelatedSession)
                                  ],
                                  seriesItems: [WorkspaceSeriesItem(kind: .decision, text: "正式改为周一",
                                                                    sourceSessionID: deletedSession, evidenceID: "monday",
                                                                    claimRevision: "claim-monday", sourceRevision: "line-monday",
                                                                    sourceQuote: "周一原话", location: "01:20",
                                                                    transcriptSourceRevision: "transcript-monday",
                                                                    revision: 2, history: [friday])],
                                  seriesChangeCandidates: [WorkspaceSeriesChangeCandidate(
                                    id: "deleted-series", kind: .action, text: "已失效的行动",
                                    sourceSessionID: deletedSession, evidenceID: "action", claimRevision: "claim",
                                    sourceRevision: "line", transcriptSourceRevision: "transcript")])

        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &workspace, now: now.addingTimeInterval(1)))
        #expect(workspace.lessons.map(\.text) == ["多场会议共同支持的经验"])
        #expect(workspace.lessons[0].sourceSessionID == remainingSession)
        #expect(workspace.lessons[0].sourceSessionIDs == [remainingSession])
        #expect(workspace.experienceNotes().contains("多场会议共同支持的经验"))
        #expect(!workspace.experienceNotes().contains("只在被删会议中总结的经验"))

        #expect(workspace.questionBank.map(\.question) == ["多场会议重复提问？", "仍有效的问题？"])
        #expect(workspace.questionBank[0].sourceSessionIDs == [remainingSession])
        #expect(workspace.questionBank[0].timesAsked == 1)
        #expect(workspace.questionBank[0].answer.isEmpty)
        #expect(workspace.questionBank[0].answerHistory?.map(\.answer) == ["仍有效的历史答法"])
        #expect(workspace.questionBank[1].answer == "有效来源的答法")
        let retrievalText = workspace.retrievalDocuments.flatMap(\.pages).joined(separator: "\n")
        #expect(!retrievalText.contains("被删会议独有答法"))
        #expect(!retrievalText.contains("由被删会议提供的答法"))
        #expect(retrievalText.contains("有效来源的答法"))

        #expect(workspace.knowledgeCandidates?.map(\.id) == ["other"])
        let deletedSeriesCandidate = try #require(workspace.seriesChangeCandidates?.first)
        #expect(workspace.seriesChangeCandidates?.count == 1)
        #expect(deletedSeriesCandidate.status == .superseded)
        #expect(deletedSeriesCandidate.sourceMissing == true && deletedSeriesCandidate.text.isEmpty)
        #expect(deletedSeriesCandidate.sourceQuote == nil && deletedSeriesCandidate.transcriptSourceRevision == nil)
        #expect(workspace.seriesItems?.isEmpty == true)
        #expect(!WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &workspace, now: now.addingTimeInterval(2)))
    }

    @Test func staleBackupCannotRestoreDefaultDeletedLessonsOrTheirSourceIDs() throws {
        let workspaceID = UUID(), deletedSession = UUID(), remainingSession = UUID()
        let deletedLessonID = UUID(), sharedLessonID = UUID()
        let oldBackup = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                  lessons: [
                                    WorkspaceLesson(id: deletedLessonID, text: "只由已删除会议支持的经验",
                                                    sourceSessionID: deletedSession, sourceSessionIDs: [deletedSession],
                                                    createdAt: now, updatedAt: now),
                                    WorkspaceLesson(id: sharedLessonID, text: "多场会议共同支持的经验",
                                                    sourceSessionID: deletedSession,
                                                    sourceSessionIDs: [deletedSession, remainingSession],
                                                    createdAt: now, updatedAt: now)
                                  ], updatedAt: now)
        var deleted = oldBackup
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &deleted,
                                                        retainAcceptedContent: false,
                                                        now: now.addingTimeInterval(10)))
        #expect(deleted.lessons.map(\.id) == [sharedLessonID])
        #expect(deleted.lessons[0].sourceSessionIDs == [remainingSession])
        #expect(deleted.sourceDeletionTombstones == [deletedSession])

        // An offline device later exports both the deleted lesson and a stale
        // source list for the shared lesson. Neither deletion nor provenance may
        // be undone when that backup is imported more than once.
        var staleBackup = oldBackup
        staleBackup.lessons[0].revision = 5
        staleBackup.lessons[0].text = "已删除会议中的更新经验"
        staleBackup.lessons[0].updatedAt = now.addingTimeInterval(100)
        staleBackup.lessons[1].revision = 7
        staleBackup.lessons[1].updatedAt = now.addingTimeInterval(100)
        staleBackup.updatedAt = now.addingTimeInterval(200)

        let merged = try #require(BackupMerge.workspaces(existing: [deleted], incoming: [staleBackup]).merged.first)
        #expect(merged.lessons.map(\.id) == [sharedLessonID])
        #expect(merged.lessons[0].sourceSessionIDs == [remainingSession])
        #expect(merged.lessons[0].sourceSessionID == remainingSession)
        #expect(merged.sourceDeletionTombstones == [deletedSession])
        let encoded = String(decoding: try JSONEncoder().encode(merged), as: UTF8.self)
        #expect(!encoded.contains("已删除会议中的更新经验"))

        let replayed = try #require(BackupMerge.workspaces(existing: [merged], incoming: [staleBackup]).merged.first)
        #expect(replayed == merged)
    }

    @Test func knowledgeCandidateAcceptanceRequiresItsArchivedSummaryAndTranscriptRevision() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let sessionID = UUID()
        let revision = "transcript-v1"
        let summary = SessionSummary(title: "复盘", markdown: "", questions: [], generatedAt: now,
                                     model: "test", lessons: ["先说结论，再展开证据"],
                                     bank: [BankEntry(question: "如何处理缓存击穿？", answer: "互斥重建")],
                                     transcriptSourceRevision: revision)
        let candidateID = try #require(KnowledgeMerger.collectCandidates(
            lessons: summary.lessons ?? [], bank: summary.bank ?? [], into: &workspace,
            session: sessionID, originID: sessionID, transcriptSourceRevision: revision).first)
        let candidate = try #require(workspace.knowledgeCandidates?.first(where: { $0.id == candidateID }))

        #expect(KnowledgeMerger.matchesCurrentSummary(candidate, summary: summary, sessionID: sessionID,
                                                      liveTranscriptSourceRevision: revision))
        #expect(!KnowledgeMerger.matchesCurrentSummary(candidate, summary: summary, sessionID: sessionID,
                                                       liveTranscriptSourceRevision: "transcript-v2"))
        var changedClaim = summary
        changedClaim.lessons = ["新转写对应的新结论"]
        #expect(!KnowledgeMerger.matchesCurrentSummary(candidate, summary: changedClaim, sessionID: sessionID,
                                                       liveTranscriptSourceRevision: revision))
    }

    @Test func acceptedLessonEditsPersistSourceVersionsAndRestoreWithoutTranscriptData() throws {
        let sourceA = UUID(), sourceB = UUID()
        let lessonID = UUID()
        let initialTime = now.addingTimeInterval(-100)
        var lesson = WorkspaceLesson(id: lessonID, text: "先讲结论", sourceSessionID: sourceA,
                                     sourceSessionIDs: [sourceA, sourceB], createdAt: initialTime,
                                     updatedAt: initialTime)

        #expect(WorkspaceLessonHistory.editText("先讲结论，再给证据", on: &lesson, now: now))
        #expect(lesson.revision == 2)
        let oldRevision = try #require(lesson.history.first)
        #expect(oldRevision.revision == 1)
        #expect(oldRevision.text == "先讲结论")
        #expect(oldRevision.sourceSessionID == sourceA)
        #expect(oldRevision.sourceSessionIDs == [sourceA, sourceB])

        let encoded = try JSONEncoder().encode(lesson)
        var decoded = try JSONDecoder().decode(WorkspaceLesson.self, from: encoded)
        #expect(decoded.history == lesson.history)
        #expect(WorkspaceLessonHistory.restore(oldRevision.id, on: &decoded, now: now.addingTimeInterval(10)))
        #expect(decoded.text == "先讲结论")
        #expect(decoded.revision == 3)
        #expect(decoded.history.contains { $0.text == "先讲结论，再给证据" && $0.revision == 2 })

        var workspace = Workspace(name: "项目复盘", mode: .meeting, lessons: [decoded])
        #expect(WorkspaceSourceIntegrity.removeMeetings([sourceA, sourceB], from: &workspace,
                                                        retainAcceptedContent: true, now: now.addingTimeInterval(20)))
        let retained = try #require(workspace.lessons.first)
        #expect(retained.independentlyRetained)
        #expect(Set(retained.sourceMissingSessionIDs ?? []) == [sourceA, sourceB])
        #expect(retained.sourceSessionID == nil && retained.sourceSessionIDs == nil)

        let latestHistory = try #require(retained.history.first { $0.text == "先讲结论，再给证据" })
        let persisted = try JSONEncoder().encode(workspace)
        var reopened = try JSONDecoder().decode(Workspace.self, from: persisted)
        #expect(WorkspaceLessonHistory.restore(latestHistory.id, on: &reopened.lessons[0],
                                               now: now.addingTimeInterval(30)))
        #expect(reopened.lessons[0].text == "先讲结论，再给证据")
        #expect(reopened.lessons[0].sourceSessionID == nil)
        #expect(Set(reopened.lessons[0].sourceMissingSessionIDs ?? []) == [sourceA, sourceB])
        #expect(reopened.lessons[0].independentlyRetained)
    }

    @Test func acceptingAnotherSourceCreatesAnAuditableLessonRevision() throws {
        let sourceA = UUID(), sourceB = UUID()
        let start = now.addingTimeInterval(-10)
        var lesson = WorkspaceLesson(text: "先讲结论", sourceSessionID: sourceA,
                                     sourceSessionIDs: [sourceA], createdAt: start, updatedAt: start)

        #expect(WorkspaceLessonHistory.addSources([sourceB], to: &lesson, now: now))
        #expect(lesson.revision == 2)
        #expect(lesson.sourceSessionIDs == [sourceA, sourceB].sorted { $0.uuidString < $1.uuidString })
        let old = try #require(lesson.history.first)
        #expect(old.revision == 1)
        #expect(old.text == "先讲结论")
        #expect(old.sourceSessionID == sourceA)
        #expect(old.sourceSessionIDs == [sourceA])
        #expect(!WorkspaceLessonHistory.addSources([sourceB], to: &lesson, now: now.addingTimeInterval(1)))
        #expect(lesson.revision == 2)

        let decoded = try JSONDecoder().decode(WorkspaceLesson.self, from: JSONEncoder().encode(lesson))
        #expect(decoded.history == lesson.history)
        #expect(decoded.currentRevision.revision == 2)
        #expect(decoded.currentRevision.sourceSessionIDs == lesson.sourceSessionIDs)
        #expect(decoded.currentRevision.id == lesson.currentRevision.id)
    }

    @Test func historicalLessonSourceKeepsTheDeletionRetentionChoiceAvailable() throws {
        let deletedSession = UUID(), remainingSession = UUID()
        let oldRevision = WorkspaceLessonRevision(revision: 1, text: "旧版经验来自被删除的会议",
                                                  sourceSessionID: deletedSession,
                                                  sourceSessionIDs: [deletedSession],
                                                  recordedAt: now.addingTimeInterval(-20))
        let lesson = WorkspaceLesson(text: "当前经验由另一场会议支持",
                                     sourceSessionID: remainingSession,
                                     sourceSessionIDs: [remainingSession],
                                     createdAt: now.addingTimeInterval(-10), updatedAt: now,
                                     revision: 2, history: [oldRevision])
        let original = Workspace(name: "系列", mode: .meeting, lessons: [lesson])

        #expect(WorkspaceSourceIntegrity.hasRetainableAcceptedContent(in: original,
                                                                      from: [deletedSession]))

        var removed = original
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &removed,
                                                        retainAcceptedContent: false,
                                                        now: now.addingTimeInterval(1)))
        #expect(removed.lessons.first?.text == lesson.text)
        #expect(removed.lessons.first?.history.isEmpty == true)

        var retained = original
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &retained,
                                                        retainAcceptedContent: true,
                                                        now: now.addingTimeInterval(1)))
        let retainedRevision = try #require(retained.lessons.first?.history.first)
        #expect(retainedRevision.text == oldRevision.text)
        #expect(retainedRevision.sourceSessionID == nil)
        #expect(retainedRevision.sourceSessionIDs == nil)
        #expect(retainedRevision.sourceMissingSessionIDs == [deletedSession])
    }

    @Test func backupMergeKeepsLessonDeletionTombstonesAuthoritativeAndIdempotent() throws {
        let workspaceID = UUID(), lessonID = UUID(), deletedSession = UUID()
        let createdAt = now.addingTimeInterval(-100)
        var retainedWorkspace = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                          lessons: [WorkspaceLesson(id: lessonID, text: "保留的经验",
                                                                    sourceSessionID: deletedSession,
                                                                    sourceSessionIDs: [deletedSession],
                                                                    createdAt: createdAt, updatedAt: now,
                                                                    revision: 1)],
                                          updatedAt: now)
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &retainedWorkspace,
                                                        retainAcceptedContent: true,
                                                        now: now.addingTimeInterval(10)))
        let retainedLesson = try #require(retainedWorkspace.lessons.first)
        #expect(retainedLesson.independentlyRetained)
        #expect(retainedLesson.sourceSessionID == nil && retainedLesson.sourceSessionIDs == nil)
        #expect(retainedLesson.sourceMissingSessionIDs == [deletedSession])

        let oldBackup = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                  lessons: [WorkspaceLesson(id: lessonID, text: "保留的经验",
                                                            sourceSessionID: deletedSession,
                                                            sourceSessionIDs: [deletedSession],
                                                            createdAt: createdAt, updatedAt: now,
                                                            revision: 1)],
                                  updatedAt: now)
        let merged = try #require(BackupMerge.workspaces(existing: [retainedWorkspace],
                                                         incoming: [oldBackup]).merged.first)
        let mergedLesson = try #require(merged.lessons.first)
        #expect(mergedLesson.text == "保留的经验")
        #expect(mergedLesson.sourceSessionID == nil && mergedLesson.sourceSessionIDs == nil)
        #expect(mergedLesson.sourceMissingSessionIDs == [deletedSession])
        #expect(mergedLesson.independentlyRetained)
        #expect(mergedLesson.history.count == 1)
        #expect(mergedLesson.history.first?.sourceSessionID == nil)
        #expect(mergedLesson.history.first?.sourceSessionIDs == nil)
        #expect(mergedLesson.history.first?.sourceMissingSessionIDs == [deletedSession])

        let replayed = try #require(BackupMerge.workspaces(existing: [merged],
                                                          incoming: [oldBackup]).merged.first)
        #expect(replayed == merged)
    }

    @Test func backupMergeDoesNotResurrectDefaultDeletedSeriesContent() throws {
        let workspaceID = UUID(), itemID = UUID(), deletedSession = UUID()
        let acceptedAt = now.addingTimeInterval(-30)
        let receipt = WorkspaceSeriesChangeReceipt(operationID: UUID(), candidateID: "decision-candidate",
                                                   decision: .add, reviewedAt: acceptedAt, resultItemID: itemID)
        let accepted = WorkspaceSeriesChangeCandidate(id: "decision-candidate", kind: .decision,
                                                      text: "改为周一提交", sourceSessionID: deletedSession,
                                                      evidenceID: "decision-1", claimRevision: "claim-1",
                                                      sourceRevision: "line-1", sourceQuote: "周一之前提交",
                                                      location: "01:25", transcriptSourceRevision: "transcript-1",
                                                      status: .accepted, receipt: receipt, createdAt: acceptedAt)
        let oldItem = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "改为周一提交",
                                          sourceSessionID: deletedSession, evidenceID: "decision-1",
                                          claimRevision: "claim-1", sourceRevision: "line-1",
                                          sourceQuote: "周一之前提交", location: "01:25",
                                          transcriptSourceRevision: "transcript-1",
                                          confirmedAt: acceptedAt, updatedAt: acceptedAt)
        let oldBackup = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                  seriesItems: [oldItem], seriesChangeCandidates: [accepted],
                                  updatedAt: acceptedAt)
        var deleted = oldBackup
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &deleted,
                                                        retainAcceptedContent: false,
                                                        now: now.addingTimeInterval(10)))
        #expect(deleted.seriesItems?.isEmpty == true)
        let receiptTombstone = try #require(deleted.seriesChangeCandidates?.first)
        #expect(receiptTombstone.status == .accepted)
        #expect(receiptTombstone.receipt == receipt)
        #expect(receiptTombstone.sourceMissing == true)
        #expect(receiptTombstone.text.isEmpty && receiptTombstone.sourceQuote == nil)

        // Model a later backup from a device that never saw the deletion and has a
        // higher revision/time; the deletion receipt must still win.
        var concurrentItem = oldItem
        concurrentItem.text = "仍按周五提交"
        concurrentItem.revision = 4
        concurrentItem.updatedAt = now.addingTimeInterval(100)
        concurrentItem.sourceQuote = "周五之前提交"
        var concurrentCandidate = accepted
        concurrentCandidate.text = "仍按周五提交"
        concurrentCandidate.sourceQuote = "周五之前提交"
        concurrentCandidate.receipt = WorkspaceSeriesChangeReceipt(operationID: UUID(), candidateID: accepted.id,
                                                                   decision: .replace,
                                                                   reviewedAt: now.addingTimeInterval(100),
                                                                   resultItemID: itemID)
        let concurrentBackup = Workspace(id: workspaceID, name: "系列备份", mode: .meeting,
                                         seriesItems: [concurrentItem],
                                         seriesChangeCandidates: [concurrentCandidate],
                                         updatedAt: now.addingTimeInterval(100))
        let merged = try #require(BackupMerge.workspaces(existing: [deleted], incoming: [concurrentBackup]).merged.first)
        #expect(merged.seriesItems?.isEmpty == true)
        let mergedTombstone = try #require(merged.seriesChangeCandidates?.first)
        #expect(mergedTombstone.sourceMissing == true)
        #expect(mergedTombstone.text.isEmpty && mergedTombstone.sourceQuote == nil)
        #expect(mergedTombstone.transcriptSourceRevision == nil)
        #expect(mergedTombstone.receipt?.resultItemID == itemID)

        let replayed = try #require(BackupMerge.workspaces(existing: [merged], incoming: [concurrentBackup]).merged.first)
        #expect(replayed == merged)
    }

    @Test func backupMergePreservesExplicitSeriesRetentionWithoutRevivingItsSource() throws {
        let workspaceID = UUID(), itemID = UUID(), deletedSession = UUID()
        let acceptedAt = now.addingTimeInterval(-30)
        let receipt = WorkspaceSeriesChangeReceipt(operationID: UUID(), candidateID: "retained-decision",
                                                   decision: .add, reviewedAt: acceptedAt, resultItemID: itemID)
        let candidate = WorkspaceSeriesChangeCandidate(id: "retained-decision", kind: .decision,
                                                       text: "改为周一提交", sourceSessionID: deletedSession,
                                                       evidenceID: "decision-2", claimRevision: "claim-2",
                                                       sourceRevision: "line-2", sourceQuote: "周一之前提交",
                                                       location: "02:10", transcriptSourceRevision: "transcript-2",
                                                       status: .accepted, receipt: receipt, createdAt: acceptedAt)
        let item = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "改为周一提交",
                                       sourceSessionID: deletedSession, evidenceID: "decision-2",
                                       claimRevision: "claim-2", sourceRevision: "line-2",
                                       sourceQuote: "周一之前提交", location: "02:10",
                                       transcriptSourceRevision: "transcript-2",
                                       confirmedAt: acceptedAt, updatedAt: acceptedAt)
        var retained = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                 seriesItems: [item], seriesChangeCandidates: [candidate], updatedAt: acceptedAt)
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &retained,
                                                        retainAcceptedContent: true,
                                                        now: now.addingTimeInterval(10)))
        let retainedBackup = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                       seriesItems: [item], seriesChangeCandidates: [candidate],
                                       updatedAt: now.addingTimeInterval(100))
        let merged = try #require(BackupMerge.workspaces(existing: [retained], incoming: [retainedBackup]).merged.first)
        let mergedItem = try #require(merged.seriesItems?.first)
        #expect(mergedItem.text == "改为周一提交")
        #expect(mergedItem.sourceMissing == true && mergedItem.independentlyRetained == true)
        #expect(mergedItem.sourceQuote == nil && mergedItem.location == nil)
        #expect(mergedItem.transcriptSourceRevision == nil)
        let mergedCandidate = try #require(merged.seriesChangeCandidates?.first)
        #expect(mergedCandidate.sourceMissing == true && mergedCandidate.text.isEmpty)
        #expect(mergedCandidate.sourceQuote == nil && mergedCandidate.transcriptSourceRevision == nil)
    }

    @Test func explicitSeriesRetentionWinsAgainstDefaultDeleteInEitherBackupOrder() throws {
        let workspaceID = UUID(), itemID = UUID(), deletedSession = UUID()
        let item = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "正式改为周一提交",
                                       sourceSessionID: deletedSession, evidenceID: "decision",
                                       claimRevision: "claim", sourceRevision: "line",
                                       sourceQuote: "周一原话", location: "01:10",
                                       transcriptSourceRevision: "transcript", revision: 3,
                                       updatedAt: now)
        let original = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                 seriesItems: [item], updatedAt: now)
        var retained = original
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &retained,
                                                        retainAcceptedContent: true,
                                                        now: now.addingTimeInterval(1)))
        var defaultDeleted = original
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &defaultDeleted,
                                                        retainAcceptedContent: false,
                                                        now: now.addingTimeInterval(1)))
        // Equal workspace timestamps make the existing/incoming order determine
        // which copy is preferred; the explicit keep must still win both ways.
        retained.updatedAt = now.addingTimeInterval(2)
        defaultDeleted.updatedAt = now.addingTimeInterval(2)

        let keptFirst = try #require(BackupMerge.workspaces(existing: [retained],
                                                             incoming: [defaultDeleted]).merged.first)
        let deletedFirst = try #require(BackupMerge.workspaces(existing: [defaultDeleted],
                                                               incoming: [retained]).merged.first)
        let keptFirstItem = try #require(keptFirst.seriesItems?.first)
        let deletedFirstItem = try #require(deletedFirst.seriesItems?.first)
        #expect(keptFirstItem == deletedFirstItem)
        #expect(keptFirstItem.text == "正式改为周一提交")
        #expect(keptFirstItem.sourceMissing == true && keptFirstItem.independentlyRetained == true)
        #expect(keptFirstItem.sourceQuote == nil && keptFirstItem.location == nil)
        #expect(keptFirstItem.transcriptSourceRevision == nil)
    }

    @Test func higherLiveSeriesRevisionWinsOverRetainedStaleTextAndKeepsItInHistory() throws {
        let workspaceID = UUID(), itemID = UUID(), deletedSession = UUID(), liveSession = UUID()
        let retainedItem = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "保留的旧结论",
                                               sourceSessionID: deletedSession, evidenceID: "old-evidence",
                                               claimRevision: "old-claim", sourceRevision: "old-line",
                                               sourceQuote: "旧会议原话", location: "00:40",
                                               transcriptSourceRevision: "old-transcript", revision: 2,
                                               updatedAt: now)
        var retainedWorkspace = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                          seriesItems: [retainedItem], updatedAt: now)
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &retainedWorkspace,
                                                        retainAcceptedContent: true,
                                                        now: now.addingTimeInterval(10)))

        let liveItem = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "新会议确认的结论",
                                           sourceSessionID: liveSession, evidenceID: "live-evidence",
                                           claimRevision: "live-claim", sourceRevision: "live-line",
                                           sourceQuote: "新会议原话", location: "02:00",
                                           transcriptSourceRevision: "live-transcript", revision: 3,
                                           updatedAt: now.addingTimeInterval(20))
        let liveWorkspace = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                      seriesItems: [liveItem], updatedAt: now.addingTimeInterval(20))

        let forward = try #require(BackupMerge.workspaces(existing: [retainedWorkspace],
                                                          incoming: [liveWorkspace]).merged.first)
        let reverse = try #require(BackupMerge.workspaces(existing: [liveWorkspace],
                                                          incoming: [retainedWorkspace]).merged.first)
        let forwardItem = try #require(forward.seriesItems?.first)
        let reverseItem = try #require(reverse.seriesItems?.first)
        #expect(forwardItem == reverseItem)
        #expect(forwardItem.text == "新会议确认的结论")
        #expect(forwardItem.sourceSessionID == liveSession)
        #expect(forwardItem.revision == 3)
        #expect(forwardItem.sourceMissing != true && forwardItem.independentlyRetained != true)
        let retainedHistory = try #require(forwardItem.history.first { $0.text == "保留的旧结论" })
        #expect(retainedHistory.sourceSessionID == deletedSession)
        #expect(retainedHistory.sourceMissing == true)
        #expect(retainedHistory.sourceQuote == nil && retainedHistory.location == nil)
        #expect(retainedHistory.transcriptSourceRevision == nil)
    }

    @Test func divergentSameNumberSeriesSnapshotsHaveStableUniqueReplayableHistoryIDs() throws {
        let workspaceID = UUID(), itemID = UUID(), sourceSessionID = UUID()
        let snapshots = ["阿青", "小林", "Morgan"].enumerated().map { index, owner in
            WorkspaceSeriesItem(id: itemID, kind: .action, text: "周五前提交接口评审",
                                owner: owner, due: "周五前", actionState: .open,
                                sourceSessionID: sourceSessionID, evidenceID: "action-evidence",
                                claimRevision: "same-claim", sourceRevision: "same-line",
                                sourceQuote: "周五前提交", location: "01:20",
                                transcriptSourceRevision: "same-transcript", revision: 5,
                                confirmedAt: now, updatedAt: now.addingTimeInterval(Double(index)))
        }
        let snapshotIDs = snapshots.map { $0.currentRevision.id }
        #expect(Set(snapshotIDs).count == 3)
        for item in snapshots {
            let decoded = try JSONDecoder().decode(WorkspaceSeriesItem.self,
                                                  from: JSONEncoder().encode(item))
            #expect(decoded.currentRevision.id == item.currentRevision.id)
        }

        let workspaces = snapshots.map {
            Workspace(id: workspaceID, name: "系列", mode: .meeting,
                      seriesItems: [$0], updatedAt: now.addingTimeInterval(100))
        }
        let mergedForward = try #require(BackupMerge.workspaces(existing: [workspaces[0]],
                                                                 incoming: [workspaces[1], workspaces[2]]).merged.first)
        let mergedReverse = try #require(BackupMerge.workspaces(existing: [workspaces[2]],
                                                                 incoming: [workspaces[1], workspaces[0]]).merged.first)
        let forwardItem = try #require(mergedForward.seriesItems?.first)
        let reverseItem = try #require(mergedReverse.seriesItems?.first)
        #expect(forwardItem.owner == "Morgan")
        #expect(reverseItem.owner == "Morgan")
        #expect(forwardItem.history == reverseItem.history)
        #expect(Set(forwardItem.history.map(\.id)).count == forwardItem.history.count)
        #expect(Set(forwardItem.history.map(\.id)) == Set(snapshotIDs.prefix(2)))
        #expect(Set(forwardItem.history.map(\.owner)) == Set(["阿青", "小林"]))

        let replayed = try #require(BackupMerge.workspaces(existing: [mergedForward],
                                                            incoming: [workspaces[1], workspaces[2]]).merged.first)
        let replayedItem = try #require(replayed.seriesItems?.first)
        #expect(replayedItem.history == forwardItem.history)
        #expect(Set(replayedItem.history.map(\.id)).count == replayedItem.history.count)
    }

    @Test func backupReimportCannotResurrectDefaultDeletedSeriesItem() throws {
        let workspaceID = try #require(UUID(uuidString: "00000000-0000-0000-0000-000000000101"))
        let itemID = try #require(UUID(uuidString: "00000000-0000-0000-0000-000000000102"))
        let deletedSession = try #require(UUID(uuidString: "00000000-0000-0000-0000-000000000103"))
        let history = WorkspaceSeriesItemRevision(revision: 1, kind: .decision,
                                                  text: "此前按周五提交", sourceSessionID: deletedSession,
                                                  evidenceID: "old-evidence", claimRevision: "old-claim",
                                                  sourceRevision: "old-line", sourceQuote: "旧原话周五",
                                                  location: "00:15", transcriptSourceRevision: "old-transcript",
                                                  recordedAt: now)
        let staleItem = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "改为周一提交",
                                            sourceSessionID: deletedSession, evidenceID: "current-evidence",
                                            claimRevision: "current-claim", sourceRevision: "current-line",
                                            sourceQuote: "新原话周一", location: "01:25",
                                            transcriptSourceRevision: "current-transcript", revision: 2,
                                            history: [history], confirmedAt: now, updatedAt: now)
        let oldBackup = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                  seriesItems: [staleItem], updatedAt: now)
        var deleted = oldBackup
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &deleted,
                                                        retainAcceptedContent: false,
                                                        now: now.addingTimeInterval(10)))
        #expect(deleted.seriesItems?.isEmpty == true)
        #expect(deleted.seriesItemSourceTombstones == [
            WorkspaceSeriesItemSourceTombstone(itemID: itemID, sourceSessionID: deletedSession)
        ])

        // Simulate an older device exporting the pre-delete record later, with a
        // higher local revision and backup timestamp. Reimporting it repeatedly
        // must not restore the item, its quote, or its deleted history.
        var staleBackup = oldBackup
        var newerStaleItem = staleItem
        newerStaleItem.text = "仍按周五提交"
        newerStaleItem.revision = 8
        newerStaleItem.updatedAt = now.addingTimeInterval(200)
        newerStaleItem.sourceQuote = "已删除来源中的新原话"
        var staleHistory = history
        staleHistory.text = "来源中记载的旧决定"
        staleHistory.sourceQuote = "已删除来源中的历史原话"
        newerStaleItem.history = [staleHistory]
        staleBackup.seriesItems = [newerStaleItem]
        staleBackup.updatedAt = now.addingTimeInterval(300)

        let imported = try #require(BackupMerge.workspaces(existing: [deleted], incoming: [staleBackup]).merged.first)
        #expect(imported.seriesItems?.isEmpty == true)
        #expect(imported.seriesItemSourceTombstones == deleted.seriesItemSourceTombstones)
        let importedJSON = String(decoding: try JSONEncoder().encode(imported), as: UTF8.self)
        #expect(!importedJSON.contains("仍按周五提交"))
        #expect(!importedJSON.contains("已删除来源中的新原话"))
        #expect(!importedJSON.contains("来源中记载的旧决定"))
        #expect(!importedJSON.contains("已删除来源中的历史原话"))

        let replayed = try #require(BackupMerge.workspaces(existing: [imported], incoming: [staleBackup]).merged.first)
        #expect(replayed.seriesItems?.isEmpty == true)
        #expect(replayed.seriesItemSourceTombstones == deleted.seriesItemSourceTombstones)
    }

    @Test func backupMergeKeepsAndDeterministicallyResolvesReceiptsAcrossDeletedCandidates() throws {
        let workspaceID = UUID(), sourceSessionID = UUID(), itemID = UUID()
        let candidateID = "deleted-candidate"
        let firstReceipt = WorkspaceSeriesChangeReceipt(operationID: UUID(), candidateID: candidateID,
                                                        decision: .add, reviewedAt: now,
                                                        resultItemID: itemID)
        let laterReceipt = WorkspaceSeriesChangeReceipt(operationID: UUID(), candidateID: candidateID,
                                                        decision: .reject, reviewedAt: now.addingTimeInterval(10),
                                                        resultItemID: nil)
        let receiptless = WorkspaceSeriesChangeCandidate(id: candidateID, kind: .decision, text: "",
                                                         sourceSessionID: sourceSessionID, evidenceID: "",
                                                         claimRevision: "", sourceRevision: "",
                                                         sourceMissing: true, status: .superseded)
        let acceptedTombstone = WorkspaceSeriesChangeCandidate(id: candidateID, kind: .decision, text: "",
                                                               sourceSessionID: sourceSessionID, evidenceID: "",
                                                               claimRevision: "", sourceRevision: "",
                                                               sourceMissing: true, status: .accepted,
                                                               receipt: firstReceipt)
        let deletedWorkspace = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                         seriesChangeCandidates: [receiptless], updatedAt: now.addingTimeInterval(20))
        let acceptedBackup = Workspace(id: workspaceID, name: "系列备份", mode: .meeting,
                                       seriesChangeCandidates: [acceptedTombstone], updatedAt: now)

        let merged = try #require(BackupMerge.workspaces(existing: [deletedWorkspace],
                                                         incoming: [acceptedBackup]).merged.first)
        var candidate = try #require(merged.seriesChangeCandidates?.first)
        #expect(candidate.sourceMissing == true)
        #expect(candidate.text.isEmpty && candidate.sourceQuote == nil)
        #expect(candidate.receipt == firstReceipt)
        var firstReplayWorkspace = merged
        #expect(WorkspaceSeriesChangeMerger.applyCandidate(candidateID, decision: .add,
                                                           operationID: firstReceipt.operationID,
                                                           into: &firstReplayWorkspace) == firstReceipt)

        var rejectedTombstone = acceptedTombstone
        rejectedTombstone.receipt = laterReceipt
        rejectedTombstone.status = .rejected
        let rejectedBackup = Workspace(id: workspaceID, name: "系列备份", mode: .meeting,
                                       seriesChangeCandidates: [rejectedTombstone], updatedAt: now.addingTimeInterval(30))
        let conflicted = try #require(BackupMerge.workspaces(existing: [merged],
                                                             incoming: [rejectedBackup]).merged.first)
        candidate = try #require(conflicted.seriesChangeCandidates?.first)
        #expect(candidate.sourceMissing == true)
        #expect(candidate.receipt == laterReceipt)
        #expect(candidate.status == .rejected)
        #expect(candidate.receiptHistory == [firstReceipt])

        var replayWorkspace = conflicted
        #expect(WorkspaceSeriesChangeMerger.applyCandidate(candidateID, decision: .add,
                                                           operationID: firstReceipt.operationID,
                                                           into: &replayWorkspace) == firstReceipt)
        #expect(WorkspaceSeriesChangeMerger.applyCandidate(candidateID, decision: .reject,
                                                           operationID: laterReceipt.operationID,
                                                           into: &replayWorkspace) == laterReceipt)

        let reversed = try #require(BackupMerge.workspaces(existing: [rejectedBackup],
                                                           incoming: [merged]).merged.first)
        let reversedCandidate = try #require(reversed.seriesChangeCandidates?.first)
        #expect(reversedCandidate.receipt == laterReceipt)
        #expect(reversedCandidate.receiptHistory == [firstReceipt])
        #expect(reversedCandidate.sourceMissing == true && reversedCandidate.text.isEmpty)
    }

    @Test func explicitDeletionRetentionKeepsAcceptedTextWithAnAccurateSourceTombstone() throws {
        let deletedSession = UUID()
        let itemID = UUID()
        let oldRevision = WorkspaceSeriesItemRevision(revision: 1, kind: .decision, text: "先按周五提交",
                                                      sourceSessionID: deletedSession, evidenceID: "old-evidence",
                                                      claimRevision: "old-claim", sourceRevision: "old-line",
                                                      sourceQuote: "周五之前提交", location: "00:15",
                                                      transcriptSourceRevision: "old-transcript")
        let receipt = WorkspaceSeriesChangeReceipt(operationID: UUID(), candidateID: "accepted-candidate",
                                                   decision: .add, reviewedAt: now, resultItemID: itemID)
        let candidate = WorkspaceSeriesChangeCandidate(id: "accepted-candidate", kind: .decision,
                                                       text: "正式改为周一提交", sourceSessionID: deletedSession,
                                                       evidenceID: "current-evidence", claimRevision: "current-claim",
                                                       sourceRevision: "current-line", sourceQuote: "周一之前提交",
                                                       location: "01:25", transcriptSourceRevision: "current-transcript",
                                                       status: .accepted, receipt: receipt)
        var workspace = Workspace(name: "项目交付", mode: .meeting,
                                  lessons: [WorkspaceLesson(text: "已确认的独立经验", sourceSessionID: deletedSession,
                                                            createdAt: now, updatedAt: now)],
                                  seriesItems: [WorkspaceSeriesItem(id: itemID, kind: .decision,
                                                                    text: "正式改为周一提交",
                                                                    sourceSessionID: deletedSession,
                                                                    evidenceID: "current-evidence",
                                                                    claimRevision: "current-claim",
                                                                    sourceRevision: "current-line",
                                                                    sourceQuote: "周一之前提交", location: "01:25",
                                                                    transcriptSourceRevision: "current-transcript",
                                                                    revision: 2, history: [oldRevision])],
                                  seriesChangeCandidates: [candidate])

        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &workspace,
                                                        retainAcceptedContent: true, now: now.addingTimeInterval(1)))
        let retainedLesson = try #require(workspace.lessons.first)
        #expect(retainedLesson.text == "已确认的独立经验")
        #expect(retainedLesson.independentlyRetained)
        #expect(retainedLesson.sourceSessionID == nil && retainedLesson.sourceSessionIDs == nil)
        #expect(retainedLesson.sourceMissingSessionIDs == [deletedSession])
        let retainedItem = try #require(workspace.seriesItems?.first)
        #expect(retainedItem.text == "正式改为周一提交")
        #expect(retainedItem.sourceMissing == true)
        #expect(retainedItem.independentlyRetained == true)
        #expect(retainedItem.evidenceID.isEmpty && retainedItem.claimRevision.isEmpty && retainedItem.sourceRevision.isEmpty)
        #expect(retainedItem.transcriptSourceRevision == nil)
        #expect(retainedItem.sourceQuote == nil && retainedItem.location == nil)
        #expect(retainedItem.history.first?.text == "先按周五提交")
        #expect(retainedItem.history.first?.sourceMissing == true)
        #expect(retainedItem.history.first?.transcriptSourceRevision == nil)
        #expect(retainedItem.history.first?.sourceQuote == nil && retainedItem.history.first?.location == nil)

        let retainedCandidate = try #require(workspace.seriesChangeCandidates?.first)
        #expect(retainedCandidate.status == .accepted)
        #expect(retainedCandidate.receipt == receipt)
        #expect(retainedCandidate.sourceMissing == true)
        #expect(retainedCandidate.text.isEmpty)
        #expect(retainedCandidate.sourceQuote == nil && retainedCandidate.location == nil)
        #expect(retainedCandidate.transcriptSourceRevision == nil)
        #expect(WorkspaceSeriesChangeMerger.applyCandidate("accepted-candidate", decision: .replace,
                                                           operationID: UUID(), into: &workspace) == receipt)
        #expect(!WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &workspace,
                                                         retainAcceptedContent: true, now: now.addingTimeInterval(2)))
    }

    @Test func stageReviewKnowledgeTracksEveryContributingMeeting() throws {
        let deletedSession = try #require(UUID(uuidString: "00000000-0000-0000-0000-000000000001"))
        let remainingSession = try #require(UUID(uuidString: "00000000-0000-0000-0000-000000000002"))
        let sources = [
            WorkspaceKnowledgeCandidateSource(sessionID: deletedSession, startedAt: now,
                                              transcriptSourceRevision: "transcript-1", projectionRevision: "projection-1"),
            WorkspaceKnowledgeCandidateSource(sessionID: remainingSession, startedAt: now.addingTimeInterval(1),
                                              transcriptSourceRevision: "transcript-2", projectionRevision: "projection-2")
        ]
        var workspace = Workspace(name: "阶段复盘", mode: .meeting)
        let candidateIDs = KnowledgeMerger.collectCandidates(
            lessons: ["跨会复盘经验能帮助下一步准备"],
            bank: [BankEntry(question: "阶段复盘候选问题？", answer: "由多场会议共同总结的回答")],
            into: &workspace, session: nil, originID: workspace.id,
            contributingSources: sources, now: now)
        #expect(candidateIDs.count == 2)

        let lessonCandidate = try #require(workspace.knowledgeCandidates?.first { $0.kind == .lesson })
        let questionCandidate = try #require(workspace.knowledgeCandidates?.first { $0.kind == .question })
        #expect(lessonCandidate.sourceSessionID == nil)
        #expect(lessonCandidate.contributingSessionIDs == [deletedSession, remainingSession])

        var pending = workspace
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &pending, now: now.addingTimeInterval(1)))
        #expect(pending.knowledgeCandidates?.isEmpty == true)
        #expect(KnowledgeMerger.applyCandidate(lessonCandidate.id, decision: .add, operationID: UUID(), into: &pending) == nil)

        #expect(KnowledgeMerger.applyCandidate(lessonCandidate.id, decision: .add, operationID: UUID(), into: &workspace) != nil)
        #expect(KnowledgeMerger.applyCandidate(questionCandidate.id, decision: .add, operationID: UUID(), into: &workspace) != nil)
        #expect(WorkspaceSourceIntegrity.removeMeetings([deletedSession], from: &workspace, now: now.addingTimeInterval(1)))
        #expect(workspace.lessons.first?.sourceSessionIDs == [remainingSession])
        #expect(workspace.questionBank.first?.sourceSessionIDs == [remainingSession])
        #expect(workspace.questionBank.first?.answer.isEmpty == true)
        #expect(workspace.questionBank.first?.timesAsked == 1)
        let retainedCandidates = try #require(workspace.knowledgeCandidates)
        #expect(retainedCandidates.count == 2)
        #expect(retainedCandidates.allSatisfy { $0.receipt != nil && $0.status == .accepted })
        #expect(retainedCandidates.allSatisfy { $0.text.isEmpty && $0.proposedAnswer == nil })
        #expect(retainedCandidates.allSatisfy { $0.contributingSessionIDs == [remainingSession] })
        #expect(!workspace.retrievalDocuments.flatMap(\.pages).joined().contains("由多场会议共同总结的回答"))
    }

    @Test func knowledgeCandidatesStayPendingUntilAcceptedAndRepeatedReviewsDoNotCountTwice() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let earlierSession = UUID(), session = UUID()
        workspace.questionBank = [QuestionBankItem(question: "缓存一致性怎么保证？", answer: "旧答法保留", timesAsked: 2,
                                                    sourceSessionID: earlierSession, updatedAt: now)]
        let first = KnowledgeMerger.collectCandidates(lessons: ["先说结论再展开", "先说结论再展开。"],
                                                       bank: [BankEntry(question: "缓存一致性怎么保证？", answer: "先更新库再删缓存")],
                                                       into: &workspace, session: session, now: now)
        #expect(first.count == 2)
        #expect(workspace.questionBank.count == 1)
        #expect(workspace.questionBank[0].timesAsked == 2)
        #expect(workspace.questionBank[0].answer == "旧答法保留")
        let again = KnowledgeMerger.collectCandidates(lessons: ["先说结论再展开"],
                                                       bank: [BankEntry(question: "缓存一致性怎么保证？", answer: "先更新库再删缓存")],
                                                       into: &workspace, session: session, now: now)
        #expect(again.isEmpty)

        let candidate = try #require(workspace.knowledgeCandidates?.first { $0.kind == .question })
        let receipt = try #require(KnowledgeMerger.applyCandidate(candidate.id, decision: .keepExisting,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(workspace.questionBank[0].answer == "旧答法保留")
        #expect(workspace.questionBank[0].timesAsked == 3)
        #expect(workspace.questionBank[0].sourceSessionIDs == [earlierSession, session].sorted { $0.uuidString < $1.uuidString })
        let retry = KnowledgeMerger.applyCandidate(candidate.id, decision: .replaceAnswer,
                                                   operationID: UUID(), into: &workspace, now: now.addingTimeInterval(1))
        #expect(retry == receipt)
        #expect(workspace.questionBank[0].timesAsked == 3)
        let data = try JSONEncoder().encode(workspace)
        var restored = try JSONDecoder().decode(Workspace.self, from: data)
        let afterRestart = KnowledgeMerger.applyCandidate(candidate.id, decision: .replaceAnswer,
                                                           operationID: UUID(), into: &restored, now: now.addingTimeInterval(2))
        #expect(afterRestart == receipt)
        #expect(restored.questionBank[0].timesAsked == 3)
    }

    @Test func replacingAnAnswerRequiresFreshRevisionAndKeepsTheOldAnswer() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "人工维护的答法", timesAsked: 4)]
        let session = UUID()
        let ids = KnowledgeMerger.collectCandidates(lessons: [],
                                                     bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "分批过期并加随机值")],
                                                     into: &workspace, session: session, now: now)
        let candidateID = try #require(ids.first)
        KnowledgeMerger.editQuestionBankItem(&workspace.questionBank[0],
                                             question: "Redis 缓存雪崩如何处理", answer: "用户后来手动调整的答法",
                                             now: now.addingTimeInterval(1))
        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace))
        let receipt = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(receipt.decision == .replaceAnswer)
        #expect(workspace.questionBank[0].answer == "分批过期并加随机值")
        #expect(workspace.questionBank[0].answerSourceSessionID == session)
        #expect(workspace.questionBank[0].answerHistory?.map(\.answer) == ["人工维护的答法", "用户后来手动调整的答法"])
        #expect(workspace.questionBank[0].timesAsked == 5)
    }

    @Test func replacingAnAnswerPreservesItsSourceAttribution() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let originalQuestionSession = UUID(), answerSession = UUID(), candidateSession = UUID()
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "原答法",
                                                    timesAsked: 2, sourceSessionID: originalQuestionSession,
                                                    sourceSessionIDs: [originalQuestionSession, answerSession],
                                                    answerSourceSessionID: answerSession, answerRevision: 2,
                                                    updatedAt: now)]
        let ids = KnowledgeMerger.collectCandidates(lessons: [],
                                                     bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "新答法")],
                                                     into: &workspace, session: candidateSession, now: now)
        let candidateID = try #require(ids.first)

        _ = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                                         operationID: UUID(), into: &workspace,
                                                         now: now.addingTimeInterval(1)))

        #expect(workspace.questionBank[0].answer == "新答法")
        #expect(workspace.questionBank[0].answerSourceSessionID == candidateSession)
        #expect(workspace.questionBank[0].answerHistory?.count == 1)
        #expect(workspace.questionBank[0].answerHistory?.first?.answer == "原答法")
        #expect(workspace.questionBank[0].answerHistory?.first?.sourceSessionID == answerSession)
    }

    @Test func staleKeepExistingRequiresReviewOfTheCurrentQuestionRevision() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let existingSession = UUID(), candidateSession = UUID()
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "原答法", timesAsked: 2,
                                                    sourceSessionID: existingSession, sourceSessionIDs: [existingSession])]
        let candidateIDs = KnowledgeMerger.collectCandidates(lessons: [],
                                                              bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "候选答法")],
                                                              into: &workspace, session: candidateSession, now: now)
        let candidateID = try #require(candidateIDs.first)

        KnowledgeMerger.editQuestionBankItem(&workspace.questionBank[0],
                                             question: "Redis 缓存雪崩怎么处理", answer: "刚刚手动更新的答法",
                                             now: now.addingTimeInterval(1))
        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .keepExisting,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(workspace.questionBank[0].answer == "刚刚手动更新的答法")
        #expect(workspace.questionBank[0].timesAsked == 2)
        #expect(workspace.questionBank[0].sourceSessionIDs == [existingSession])

        #expect(KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace))
        let receipt = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .keepExisting,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(receipt.sourceCounted)
        #expect(workspace.questionBank[0].answer == "刚刚手动更新的答法")
        #expect(workspace.questionBank[0].timesAsked == 3)
        #expect(Set(workspace.questionBank[0].sourceSessionIDs ?? []) == [existingSession, candidateSession])
    }

    @Test func keepingAnExistingLessonIsRejectedWhenNoSimilarLessonRemains() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let ids = KnowledgeMerger.collectCandidates(lessons: ["面试回答先说结论，再按证据展开"], bank: [],
                                                     into: &workspace, session: UUID(), now: now)
        let candidateID = try #require(ids.first)

        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .keepExisting,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(workspace.lessons.isEmpty)
        #expect(workspace.knowledgeCandidates?.first?.status == .pending)
    }

    @Test func editingQuestionWordingInvalidatesOpenReplacementWithoutErasingProvenance() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let sourceSession = UUID(), currentSession = UUID()
        let oldRevision = WorkspaceAnswerRevision(answer: "更早的答法", recordedAt: now.addingTimeInterval(-10))
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "人工维护的答法",
                                                    timesAsked: 4, sourceSessionID: sourceSession,
                                                    sourceSessionIDs: [sourceSession], answerHistory: [oldRevision],
                                                    answerRevision: 3, updatedAt: now)]
        let ids = KnowledgeMerger.collectCandidates(lessons: [],
                                                     bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "分批过期并加随机值")],
                                                     into: &workspace, session: currentSession, now: now)
        let candidateID = try #require(ids.first)

        KnowledgeMerger.editQuestionBankItem(&workspace.questionBank[0],
                                             question: "Redis 缓存雪崩如何处理", answer: "人工维护的答法",
                                             now: now.addingTimeInterval(1))
        #expect(workspace.questionBank[0].answerRevision == 4)
        #expect(workspace.questionBank[0].answerHistory == [oldRevision])
        #expect(workspace.questionBank[0].sourceSessionIDs == [sourceSession])
        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace))

        let receipt = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(receipt.sourceCounted)
        #expect(workspace.questionBank[0].answer == "分批过期并加随机值")
        #expect(workspace.questionBank[0].answerHistory?.map(\.answer) == ["更早的答法", "人工维护的答法"])
        #expect(workspace.questionBank[0].sourceSessionIDs?.contains(currentSession) == true)
    }

    @Test func whyAndHowQuestionsRemainSeparateCandidates() {
        var workspace = Workspace(name: "面试", mode: .interview)
        let ids = KnowledgeMerger.collectCandidates(lessons: [], bank: [
            BankEntry(question: "为什么选择 Redis？", answer: "需要低延迟缓存"),
            BankEntry(question: "如何使用 Redis？", answer: "设置过期时间并处理并发")
        ], into: &workspace, session: UUID(), now: now)
        #expect(ids.count == 2)
        #expect(Set(ids).count == 2)
    }

    @Test func legacyKnowledgeFieldsRemainReadable() throws {
        let session = UUID()
        let workspaceID = UUID(), lessonID = UUID(), questionID = UUID()
        let json = """
        {"id":"\(workspaceID.uuidString)","name":"旧场景","mode":"interview","lessons":[{"id":"\(lessonID.uuidString)","text":"旧经验","sourceSessionID":"\(session.uuidString)","createdAt":0,"pinned":false}],"questionBank":[{"id":"\(questionID.uuidString)","question":"旧问题","answer":"旧答法","timesAsked":3,"sourceSessionID":"\(session.uuidString)","updatedAt":0}]}
        """
        let workspace = try JSONDecoder().decode(Workspace.self, from: Data(json.utf8))
        #expect(workspace.questionBank.first?.answer == "旧答法")
        #expect(workspace.questionBank.first?.answerHistory == nil)
        #expect(workspace.questionBank.first?.answerRevision == nil)
        #expect(workspace.knowledgeCandidates == nil)
    }

    @Test func legacyReaderCanReadNewWorkspaceFields() throws {
        let item = QuestionBankItem(question: "缓存问题", answer: "新答法", timesAsked: 2,
                                    answerHistory: [WorkspaceAnswerRevision(answer: "旧答法", recordedAt: now)],
                                    answerRevision: 1, updatedAt: now)
        var workspace = Workspace(name: "新场景", mode: .interview, questionBank: [item])
        workspace.knowledgeCandidates = [WorkspaceKnowledgeCandidate(id: "candidate", logicalItemID: "question",
                                                                      kind: .question, text: "缓存问题",
                                                                      proposedAnswer: "待审阅答法")]
        let data = try JSONEncoder().encode(workspace)
        let oldView = try JSONDecoder().decode(LegacyWorkspaceView.self, from: data)

        #expect(oldView.name == "新场景")
        #expect(oldView.questionBank.first?.answer == "新答法")
        #expect(oldView.questionBank.first?.timesAsked == 2)
    }

    @Test func parsesLessonsAndBankFromReview() {
        let markdown = """
        # 标题
        二面
        ## 问题清单
        1. 介绍一下项目 —— 我的回答要点：订单服务
        ## 经验沉淀
        - 先画时序再讲方案
        - 报出 QPS 等具体数字
        ## 题库更新
        - 问：缓存和数据库怎么保证一致？
          答：先更新数据库再删除缓存。
          删除失败用重试队列兜底。
        - 问：为什么选 Kafka
          答：吞吐高，生态成熟。
        ## 复盘建议
        - 多练
        """
        let parsed = SessionSummaryParser.parseAll(markdown)
        #expect(parsed.title == "二面")
        #expect(parsed.questions.count == 1)
        #expect(parsed.lessons == ["先画时序再讲方案", "报出 QPS 等具体数字"])
        #expect(parsed.bank.count == 2)
        #expect(parsed.bank[0].answer == "先更新数据库再删除缓存。\n删除失败用重试队列兜底。")
        #expect(parsed.bank[1].question == "为什么选 Kafka")
        #expect(SessionSummaryPrompt.system(for: .academic).contains("报告人"))
    }

    @Test func backupMergeNeverOverwritesNewerWork() {
        let id = UUID()
        var mine = Workspace(id: id, name: "A", mode: .interview, updatedAt: now)
        mine.lessons = [WorkspaceLesson(text: "我的经验")]
        var old = Workspace(id: id, name: "A-old", mode: .interview, updatedAt: now.addingTimeInterval(-100))
        old.lessons = [WorkspaceLesson(text: "备份里的经验")]
        let other = Workspace(name: "A", mode: .academic)
        let result = BackupMerge.workspaces(existing: [mine], incoming: [old, other])
        #expect(result.merged.count == 2)
        #expect(result.merged[0].name == "A")
        #expect(result.merged[0].lessons.count == 2)
        #expect(result.merged[1].name == "A（导入）")
        #expect(result.added == 1 && result.updated == 1)
        let newer = Workspace(id: id, name: "A-new", mode: .interview, updatedAt: now.addingTimeInterval(100))
        let second = BackupMerge.workspaces(existing: result.merged, incoming: [newer])
        #expect(second.merged[0].name == "A-new")
        #expect(second.merged[0].lessons.count == 2)
    }

    @Test func backupMergePreservesKnowledgeSourcesHistoryAndReviewReceipt() throws {
        let workspaceID = UUID(), lessonID = UUID(), questionID = UUID()
        let oldSession = UUID(), sharedSession = UUID(), currentSession = UUID()
        let sharedRevision = WorkspaceAnswerRevision(answer: "共同历史", recordedAt: now.addingTimeInterval(-300))
        let olderRevision = WorkspaceAnswerRevision(answer: "备份中的旧答法", recordedAt: now.addingTimeInterval(-200),
                                                    sourceSessionID: oldSession)
        let currentRevision = WorkspaceAnswerRevision(answer: "本机历史", recordedAt: now.addingTimeInterval(-100),
                                                      sourceSessionID: sharedSession)
        var current = Workspace(id: workspaceID, name: "A", mode: .interview, updatedAt: now)
        current.lessons = [WorkspaceLesson(id: lessonID, text: "当前经验", sourceSessionID: currentSession,
                                           sourceSessionIDs: [sharedSession, currentSession],
                                           createdAt: now.addingTimeInterval(-50), pinned: true)]
        current.questionBank = [QuestionBankItem(id: questionID, question: "缓存雪崩如何处理", answer: "当前答法",
                                                 timesAsked: 5, sourceSessionID: currentSession,
                                                 sourceSessionIDs: [sharedSession, currentSession],
                                                 answerSourceSessionID: currentSession,
                                                 answerHistory: [sharedRevision, currentRevision], answerRevision: 4,
                                                 updatedAt: now)]
        let acceptedReceipt = WorkspaceKnowledgeReceipt(operationID: UUID(), candidateID: "same-candidate",
                                                        decision: .keepExisting, reviewedAt: now.addingTimeInterval(-25),
                                                        resultItemID: questionID, sourceCounted: true)
        current.knowledgeCandidates = [WorkspaceKnowledgeCandidate(id: "same-candidate", logicalItemID: "question",
                                                                   kind: .question, text: "缓存雪崩如何处理",
                                                                   proposedAnswer: "备选答法")]

        var backup = Workspace(id: workspaceID, name: "A backup", mode: .interview,
                               updatedAt: now.addingTimeInterval(-20))
        backup.lessons = [WorkspaceLesson(id: lessonID, text: "较旧经验", sourceSessionID: oldSession,
                                          sourceSessionIDs: [oldSession, sharedSession],
                                          createdAt: now.addingTimeInterval(-150))]
        backup.questionBank = [QuestionBankItem(id: questionID, question: "缓存雪崩如何处理", answer: "旧答法",
                                                 timesAsked: 4, sourceSessionID: oldSession,
                                                 sourceSessionIDs: [oldSession, sharedSession],
                                                 answerSourceSessionID: oldSession,
                                                 answerHistory: [sharedRevision, olderRevision], answerRevision: 2,
                                                 updatedAt: now.addingTimeInterval(-10))]
        let acceptedCandidate = WorkspaceKnowledgeCandidate(id: "same-candidate", logicalItemID: "question",
                                                             kind: .question, text: "缓存雪崩如何处理",
                                                             proposedAnswer: "备选答法", status: .accepted,
                                                             receipt: acceptedReceipt, createdAt: now.addingTimeInterval(-80))
        backup.knowledgeCandidates = [acceptedCandidate]

        let merged = BackupMerge.workspaces(existing: [current], incoming: [backup])
        let workspace = try #require(merged.merged.first)
        let lesson = try #require(workspace.lessons.first)
        let question = try #require(workspace.questionBank.first)
        let candidate = try #require(workspace.knowledgeCandidates?.first)

        #expect(workspace.name == "A")
        #expect(lesson.text == "当前经验")
        #expect(lesson.pinned)
        #expect(lesson.createdAt == now.addingTimeInterval(-150))
        #expect(Set(lesson.sourceSessionIDs ?? []) == [sharedSession, currentSession])
        #expect(lesson.history.contains { $0.text == "较旧经验" && $0.sourceSessionIDs == [oldSession, sharedSession] })
        #expect(question.answer == "当前答法")
        #expect(question.answerSourceSessionID == currentSession)
        #expect(question.answerRevision == 5)
        #expect(question.timesAsked == 5)
        #expect(Set(question.sourceSessionIDs ?? []) == [oldSession, sharedSession, currentSession])
        let preservedBackupAnswer = try #require(question.answerHistory?.first { $0.answer == "旧答法" })
        #expect(preservedBackupAnswer.recordedAt == backup.questionBank[0].updatedAt)
        #expect(preservedBackupAnswer.sourceSessionID == oldSession)
        #expect(Set(question.answerHistory?.map(\.id) ?? []) == Set([sharedRevision.id, olderRevision.id,
                                                                      currentRevision.id, preservedBackupAnswer.id]))
        #expect(candidate.status == .accepted)
        #expect(candidate.receipt == acceptedReceipt)
        #expect(candidate.receipt?.resultItemID == questionID)
        #expect(merged.updated == 1)

        let replayed = try #require(BackupMerge.workspaces(existing: [workspace], incoming: [backup]).merged.first)
        #expect(replayed.lessons.first?.text == "当前经验")
        #expect(replayed.lessons.first?.history.filter { $0.text == "较旧经验" }.count == 1)
    }

    @Test func backupMergePreservesLosingManualAnswerOnceWithItsSourceAndTimestamp() throws {
        let workspaceID = UUID(), questionID = UUID(), sourceSessionID = UUID()
        let manualEditAt = now.addingTimeInterval(-60)
        let acceptedAt = now
        var manualItem = QuestionBankItem(id: questionID, question: "缓存雪崩怎么处理", answer: "初始答法",
                                           sourceSessionID: sourceSessionID, answerSourceSessionID: sourceSessionID,
                                           updatedAt: now.addingTimeInterval(-120))
        KnowledgeMerger.editQuestionBankItem(&manualItem, question: "缓存雪崩怎么处理",
                                             answer: "手动维护的答法", now: manualEditAt)
        #expect(manualItem.answerHistory?.map(\.answer) == ["初始答法"])
        #expect(manualItem.answerSourceSessionID == nil)
        #expect(manualItem.knownAnswerSourceSessionID == nil)

        var current = Workspace(id: workspaceID, name: "面试", mode: .interview, updatedAt: acceptedAt)
        current.questionBank = [QuestionBankItem(id: questionID, question: manualItem.question, answer: "已接受的建议",
                                                  sourceSessionID: sourceSessionID, answerSourceSessionID: UUID(),
                                                  answerRevision: 2, updatedAt: acceptedAt)]
        var backup = Workspace(id: workspaceID, name: "面试备份", mode: .interview,
                               updatedAt: acceptedAt.addingTimeInterval(-1))
        backup.questionBank = [manualItem]

        let firstMerge = try #require(BackupMerge.workspaces(existing: [current], incoming: [backup]).merged.first)
        let firstQuestion = try #require(firstMerge.questionBank.first)
        let preserved = try #require(firstQuestion.answerHistory?.first(where: { $0.answer == "手动维护的答法" }))
        #expect(firstQuestion.answer == "已接受的建议")
        #expect(preserved.recordedAt == manualEditAt)
        #expect(preserved.sourceSessionID == nil)

        let secondMerge = try #require(BackupMerge.workspaces(existing: [firstMerge], incoming: [backup]).merged.first)
        let secondQuestion = try #require(secondMerge.questionBank.first)
        let preservedRevisions = secondQuestion.answerHistory?.filter { $0.answer == "手动维护的答法" } ?? []
        #expect(preservedRevisions.count == 1)
        #expect(preservedRevisions.first?.recordedAt == manualEditAt)
        #expect(preservedRevisions.first?.sourceSessionID == nil)
    }

    @Test func backupMergeDoesNotRestoreSourceForSameTextManualAnswer() throws {
        let workspaceID = UUID(), questionID = UUID(), oldSourceSessionID = UUID()
        var current = Workspace(id: workspaceID, name: "面试", mode: .interview, updatedAt: now)
        current.questionBank = [QuestionBankItem(id: questionID, question: "如何处理缓存击穿", answer: "手动维护的答法",
                                                  sourceSessionID: oldSourceSessionID, answerRevision: 1,
                                                  updatedAt: now)]
        var backup = Workspace(id: workspaceID, name: "面试备份", mode: .interview,
                               updatedAt: now.addingTimeInterval(-10))
        backup.questionBank = [QuestionBankItem(id: questionID, question: "如何处理缓存击穿", answer: "手动维护的答法",
                                                 sourceSessionID: oldSourceSessionID,
                                                 answerSourceSessionID: oldSourceSessionID,
                                                 updatedAt: now.addingTimeInterval(-20))]

        let merged = BackupMerge.workspaces(existing: [current], incoming: [backup])
        let question = try #require(merged.merged.first?.questionBank.first)

        #expect(question.answer == "手动维护的答法")
        #expect(question.answerRevision == 1)
        #expect(question.answerSourceSessionID == nil)
        #expect(question.knownAnswerSourceSessionID == nil)
    }

    @Test func editingOnlyLegacyQuestionWordingKeepsAnswerAttribution() {
        let sourceSession = UUID()
        var item = QuestionBankItem(question: "如何处理缓存击穿", answer: "先加互斥锁",
                                    sourceSessionID: sourceSession, updatedAt: now)
        let answer = item.answer

        KnowledgeMerger.editQuestionBankItem(&item, question: "缓存击穿如何处理", answer: answer,
                                             now: now.addingTimeInterval(1))

        #expect(item.answerRevision == 1)
        #expect(item.answerHistory == nil)
        #expect(item.answerSourceSessionID == sourceSession)
        #expect(item.knownAnswerSourceSessionID == sourceSession)
    }

    @Test func backupMergePreservesWinningLegacyAnswerAttributionWhenAnswersDiverge() throws {
        let workspaceID = UUID(), questionID = UUID()
        let currentSource = UUID(), backupSource = UUID()
        let current = Workspace(id: workspaceID, name: "面试", mode: .interview,
                                questionBank: [QuestionBankItem(id: questionID, question: "如何处理缓存击穿",
                                                                answer: "当前答法", sourceSessionID: currentSource,
                                                                updatedAt: now)],
                                updatedAt: now)
        let backup = Workspace(id: workspaceID, name: "面试备份", mode: .interview,
                               questionBank: [QuestionBankItem(id: questionID, question: "如何处理缓存击穿",
                                                               answer: "旧答法", sourceSessionID: backupSource,
                                                               updatedAt: now.addingTimeInterval(-20))],
                               updatedAt: now.addingTimeInterval(-10))

        let merged = try #require(BackupMerge.workspaces(existing: [current], incoming: [backup]).merged.first)
        let question = try #require(merged.questionBank.first)
        let preserved = try #require(question.answerHistory?.first)

        #expect(question.answer == "当前答法")
        #expect(question.answerRevision == 1)
        #expect(question.answerSourceSessionID == currentSource)
        #expect(question.knownAnswerSourceSessionID == currentSource)
        #expect(preserved.answer == "旧答法")
        #expect(preserved.sourceSessionID == backupSource)
    }

    @Test func currentSeriesRevisionHasStableIdentityAndDuplicateBackupDoesNotAddHistory() throws {
        let workspaceID = UUID(), itemID = UUID(), sessionID = UUID()
        let item = WorkspaceSeriesItem(id: itemID, kind: .decision, text: "按周发布",
                                       sourceSessionID: sessionID, evidenceID: "decision-1",
                                       claimRevision: "claim-1", sourceRevision: "line-1",
                                       confirmedAt: now, updatedAt: now)

        #expect(item.currentRevision == item.currentRevision)

        let current = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                                seriesItems: [item], updatedAt: now)
        let backup = Workspace(id: workspaceID, name: "系列", mode: .meeting,
                               seriesItems: [item], updatedAt: now.addingTimeInterval(-1))
        let merged = try #require(BackupMerge.workspaces(existing: [current], incoming: [backup]).merged.first)
        let mergedItem = try #require(merged.seriesItems?.first)

        #expect(mergedItem == item)
        #expect(mergedItem.history.isEmpty)
    }

    @Test func manifestValidationAndSettingsFilter() throws {
        let manifest = BackupManifest(kind: .full, appVersion: "1.3.0", appBuild: "20", includesSecrets: false, workspaceCount: 1, sessionCount: 2)
        try manifest.validate()
        #expect(manifest.format == "nextthought-backup")
        var legacy = manifest; legacy.format = "interviewhacker-backup"
        try legacy.validate()
        var future = manifest; future.version = 99
        #expect(throws: BackupManifest.ValidationError.newerFormat(99)) { try future.validate() }
        let filtered = SettingsSnapshot.filter(["apiModel": "x", "selectedTargetID": "window:1", "AppleLanguages": ["zh"]])
        #expect(filtered.keys.sorted() == ["apiModel"])
        #expect(SettingsSnapshot.secretAccounts.contains("stt.soniox"))
    }

    @Test func restoreMapsLegacyAutoApplyLessonsPreferenceToCandidateCollection() {
        let legacy = SettingsSnapshot.valuesForRestore(["autoApplyLessons": false, "apiModel": "legacy-model",
                                                        "selectedTargetID": "window:1"])
        #expect(legacy["autoCollectKnowledgeCandidates"] as? Bool == false)
        #expect(legacy["autoApplyLessons"] == nil)
        #expect(legacy["apiModel"] as? String == "legacy-model")
        #expect(legacy["selectedTargetID"] == nil)

        let current = SettingsSnapshot.valuesForRestore(["autoApplyLessons": true,
                                                         "autoCollectKnowledgeCandidates": false])
        #expect(current["autoCollectKnowledgeCandidates"] as? Bool == false)
        #expect(current["autoApplyLessons"] == nil)

        let currentOnly = SettingsSnapshot.valuesForRestore(["autoCollectKnowledgeCandidates": true])
        #expect(currentOnly["autoCollectKnowledgeCandidates"] as? Bool == true)
    }
}

private struct LegacyWorkspaceView: Decodable {
    let name: String
    let questionBank: [LegacyQuestion]
}

private struct LegacyQuestion: Decodable {
    let answer: String
    let timesAsked: Int
}
